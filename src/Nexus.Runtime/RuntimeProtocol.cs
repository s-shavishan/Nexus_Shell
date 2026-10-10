using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Runtime;

public static class RuntimeOperations
{
    public const string Health = "runtime.health", Stop = "runtime.stop", ReadState = "state.read", CommitState = "state.commit";
    public const string OpenFiles = "files.open", Work = "files.work", Next = "files.next", Ready = "files.ready", Complete = "files.complete", Pulse = "files.pulse";
    public const string Settings = "settings.execute";
    public const string PanelSync = "panel.sync", PanelPulse = "panel.pulse", PanelReady = "panel.ready", PanelHide = "panel.hide", PanelAction = "panel.action";
    public static bool Allows(string role, string operation) => role switch
    {
        "desktop" => operation is Health or Stop or ReadState or CommitState or OpenFiles or Settings or PanelSync,
        "files" => operation is Work or Next or Ready or Complete or Pulse,
        "controlcenter" => operation is PanelPulse or PanelReady or PanelHide or PanelAction or Settings,
        _ => false
    };
}

public sealed record RuntimeRequest(int Version, Guid Id, string Role, int ProcessId, string Operation, JsonElement Payload);
public sealed record RuntimeResponse(int Version, Guid Id, bool Ok, string Code, string Message, JsonElement Payload);
public sealed record StateReadResult(ShellState State, string RecoveryMessage);
public sealed record StateCommit(ShellState State, long ExpectedRevision, Guid CommitId);
public sealed record StateCommitResult(long Revision, Guid CommitId);
public sealed record FilesLaunch(FileSelectionRequest Request, string? Folder = null);
public sealed record FilesWork(Guid ToolId, Guid OperationId, FilesLaunch Launch);
public sealed record FilesCommand(Guid OperationId, string? Folder);
public sealed record FilesReady(Guid OperationId, long Window);
public sealed record FilesCompletion(Guid OperationId, string[] Paths);
public sealed record FilesResult(int ProcessId, long Window, string[] Paths);
public sealed record AppearanceSettings(string Wallpaper, bool NativeGlass, bool ReducedEffects);
public sealed record RuntimeHealth(int CoreProcessId, long Revision, int FilesProcesses, Guid IssueId, string Issue);

public sealed class RuntimeFailure(string code, string message) : Exception(message)
{ public string Code { get; } = code; }

public static class RuntimeProtocol
{
    public const int Version = 1, MaximumFrameBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 32 };
    public static JsonElement Body<T>(T value) => JsonSerializer.SerializeToElement(value, Json);
    public static T Payload<T>(JsonElement value) => value.Deserialize<T>(Json) ?? throw new InvalidDataException("The request body is empty.");
    public static RuntimeResponse Success<T>(Guid id, T value) => new(Version, id, true, "", "", Body(value));
    public static RuntimeResponse Failure(Guid id, string code, string message) => new(Version, id, false, code, message[..Math.Min(message.Length, 400)], Body(new { }));
    public static void Validate(RuntimeRequest request)
    {
        if (request.Version != Version) throw new RuntimeFailure("version", "These Nexus components use different protocol versions. Keep the complete build together.");
        if (request.Id == Guid.Empty || request.ProcessId <= 0 || !RuntimeOperations.Allows(request.Role, request.Operation))
            throw new RuntimeFailure("forbidden", "This component cannot perform that operation.");
        if (request.Payload.ValueKind != JsonValueKind.Object) throw new InvalidDataException("A request must contain an object body.");
    }
    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellation)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length is <= 0 or > MaximumFrameBytes) throw new InvalidDataException("The local message exceeds its size limit.");
        byte[] prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, bytes.Length);
        await stream.WriteAsync(prefix, cancellation).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellation).ConfigureAwait(false);
        await stream.FlushAsync(cancellation).ConfigureAwait(false);
    }
    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellation) where T : class
    {
        return await ReadFrameAsync<T>(stream, false, cancellation).ConfigureAwait(false)
            ?? throw new EndOfStreamException("The connection closed before sending a message.");
    }
    public static Task<RuntimeRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellation)
        => ReadFrameAsync<RuntimeRequest>(stream, true, cancellation);
    private static async Task<T?> ReadFrameAsync<T>(Stream stream, bool allowEmpty, CancellationToken cancellation) where T : class
    {
        byte[] prefix = new byte[4];
        int first = await stream.ReadAsync(prefix.AsMemory(0, 1), cancellation).ConfigureAwait(false);
        if (first == 0 && allowEmpty) return null;
        if (first == 0) throw new EndOfStreamException("The connection closed before sending a message.");
        await stream.ReadExactlyAsync(prefix.AsMemory(1), cancellation).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is <= 0 or > MaximumFrameBytes) throw new InvalidDataException("The local message has an invalid size.");
        byte[] bytes = new byte[length]; await stream.ReadExactlyAsync(bytes, cancellation).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException("The local message is empty.");
    }
}

public static class PipePeer
{
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(IntPtr pipe, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out uint processId);
    public static bool IsClient(NamedPipeServerStream pipe, int processId) => OperatingSystem.IsWindows()
        && GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out uint actual) && actual == processId;
    public static void VerifyServer(NamedPipeClientStream pipe, int expectedProcessId)
    {
        if (OperatingSystem.IsWindows() && (!GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out uint actual) || actual != expectedProcessId))
            throw new RuntimeFailure("peer", "The local endpoint does not belong to this Nexus Core process.");
    }
}

public sealed class RuntimeClient(string pipeName, string role, int serverProcessId)
{
    public async Task<T> CallAsync<T>(string operation, object payload, TimeSpan? timeout = null, CancellationToken cancellation = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        if (timeout is { } limit) deadline.CancelAfter(limit);
        Guid id = Guid.NewGuid();
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(3000, deadline.Token).ConfigureAwait(false);
            PipePeer.VerifyServer(pipe, serverProcessId);
            await RuntimeProtocol.WriteAsync(pipe, new RuntimeRequest(RuntimeProtocol.Version, id, role, Environment.ProcessId, operation, RuntimeProtocol.Body(payload)), deadline.Token).ConfigureAwait(false);
            var response = await RuntimeProtocol.ReadAsync<RuntimeResponse>(pipe, deadline.Token).ConfigureAwait(false);
            if (response.Version != RuntimeProtocol.Version || response.Id != id) throw new InvalidDataException("The response does not match this request.");
            if (!response.Ok) throw new RuntimeFailure(response.Code, response.Message);
            return RuntimeProtocol.Payload<T>(response.Payload);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new TimeoutException("Nexus Core did not finish this request within its deadline."); }
    }
}
