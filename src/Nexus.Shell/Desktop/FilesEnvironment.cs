using Microsoft.UI.Xaml;
using Nexus.Runtime;
using Nexus.Runtime.Transfers;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Nexus.Shell.Interop;
using System.Diagnostics;

namespace Nexus.Shell.Desktop;

// An isolated Files process owns one browser/picker and reads preferences from
// Core. It never constructs a desktop, registers a taskbar, or writes settings.
internal sealed class FilesEnvironment
{
    internal ShellState State { get; } = new();
    internal ShellTheme Theme { get; } = new();
    internal event Action? Stopped;
    private readonly Process _core;
    private readonly RuntimeClient _client;
    private readonly Guid _toolId;
    private readonly CancellationTokenSource _cancel = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private FilesWindow? _window;
    private FilesWork? _work;
    private bool _stopping, _pulsing;
    internal bool Validation { get; }
    private int _failures;
    private FilesEnvironment()
    { Validation = true; _stopping = true; _core = null!; _client = null!; Theme.Apply("Midnight"); }
    internal static FilesEnvironment ForValidation() => new();
    internal FilesEnvironment(string[] args)
    {
        if (args.Length != 7 || args[0] != "--files-worker" || args[1] != "--core-pipe" || args[3] != "--core-pid"
            || !int.TryParse(args[4], out int id) || id <= 0 || args[5] != "--tool-id" || !Guid.TryParseExact(args[6], "N", out _toolId))
            throw new ArgumentException("Start Files through the Nexus desktop.");
        _core = Process.GetProcessById(id);
        try
        {
            using var own = Process.GetCurrentProcess();
            string prefix = "WhiteDreams.Nexus.Core." + own.SessionId + ".";
            if (_core.HasExited || _core.SessionId != own.SessionId || !string.Equals(_core.MainModule?.FileName,
                Path.Combine(AppContext.BaseDirectory, "Nexus.Core.exe"), StringComparison.OrdinalIgnoreCase)
                || !args[2].StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(args[2][prefix.Length..], "N", out _))
                throw new InvalidOperationException("The Files owner does not belong to this Nexus folder and session.");
            _ = _core.Handle;
            _client = new(args[2], "files", id);
        }
        catch { _core.Dispose(); throw; }
    }
    internal async Task StartAsync()
    {
        _work = await _client.CallAsync<FilesWork>(RuntimeOperations.Work, new { }, TimeSpan.FromSeconds(5), _cancel.Token);
        if (_work.ToolId != _toolId) throw new InvalidDataException("The Files launch does not belong to this process.");
        Apply(await _client.CallAsync<AppearanceSettings>(RuntimeOperations.Pulse, new { }, TimeSpan.FromSeconds(3), _cancel.Token));
        var launch = _work.Launch;
        string folder = launch.Folder ?? Environment.GetFolderPath(launch.Request.Kind == FileSelectionKind.Browse ? Environment.SpecialFolder.UserProfile : Environment.SpecialFolder.MyDocuments);
        _window = new(this, launch.Request, folder);
        _window.Closed += async (_, _) => await WindowClosedAsync();
        _timer.Tick += Pulse; _timer.Start();
        await _client.CallAsync<object>(RuntimeOperations.Ready, new FilesReady(_work.OperationId, _window.Handle.ToInt64()), TimeSpan.FromSeconds(3), _cancel.Token);
        if (launch.Request.Kind == FileSelectionKind.Browse) _ = CommandsAsync();
    }
    private void Apply(AppearanceSettings settings)
    {
        bool changed = State.Wallpaper != settings.Wallpaper || State.NativeGlass != settings.NativeGlass || State.ReducedEffects != settings.ReducedEffects || State.SurfaceAnimations != settings.Animations || State.GlassStyle != settings.GlassStyle || State.GlassQuality != settings.GlassQuality;
        State.Wallpaper = settings.Wallpaper; State.NativeGlass = settings.NativeGlass; State.ReducedEffects = settings.ReducedEffects; State.SurfaceAnimations = settings.Animations; State.GlassStyle = GlassStyle.Normalize(settings.GlassStyle);
        State.GlassQuality = GlassQuality.Normalize(settings.GlassQuality);
        changed |= Theme.Apply(settings.Wallpaper, settings.ReducedEffects, State.GlassStyle, State.GlassQuality);
        if (changed) _window?.ApplyAppearance();
    }
    private async void Pulse(object? sender, object args)
    {
        if (_stopping || _pulsing) return; _pulsing = true;
        try
        {
            if (_core.HasExited) { Stop(); return; }
            var settings = await _client.CallAsync<AppearanceSettings>(RuntimeOperations.Pulse, new { }, TimeSpan.FromSeconds(3), _cancel.Token);
            if (!_stopping) { _failures = 0; Apply(settings); }
        }
        catch (OperationCanceledException) when (_stopping) { }
        catch (Exception ex)
        { if (!_stopping && ++_failures >= 3) { Log.Write("Files lost its Core connection", ex); Stop(); } }
        finally { _pulsing = false; }
    }
    private async Task CommandsAsync()
    {
        var dispatcher = _window!.DispatcherQueue;
        try
        {
            while (!_cancel.IsCancellationRequested)
            {
                var next = await _client.CallAsync<FilesNext>(RuntimeOperations.Next, new { }, TimeSpan.FromSeconds(25), _cancel.Token).ConfigureAwait(false);
                if (next.Command is not { } command) continue;
                if (!dispatcher.TryEnqueue(async () =>
                {
                    if (_stopping || _window is null) return;
                    try
                    {
                        if (command.Folder is null) _window.ReturnToWindow(); else _window.ShowFolder(command.Folder);
                        await _client.CallAsync<object>(RuntimeOperations.Ready, new FilesReady(command.OperationId, _window.Handle.ToInt64()), TimeSpan.FromSeconds(3), _cancel.Token);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    { if (!_stopping) { Log.Write("Files navigation handoff failed", ex); Stop(); } }
                })) break;
            }
        }
        catch (OperationCanceledException) when (_stopping) { }
        catch (Exception ex)
        { Log.Write("Files command connection ended", ex); dispatcher.TryEnqueue(Stop); }
    }
    private async Task WindowClosedAsync()
    {
        if (_stopping) return; _stopping = true; _timer.Stop(); _cancel.Cancel();
        try
        {
            string[] paths = _window is null ? [] : (await _window.Selection).ToArray();
            if (_work is not null && !_core.HasExited)
                await _client.CallAsync<object>(RuntimeOperations.Complete, new FilesCompletion(_work.OperationId, paths), TimeSpan.FromSeconds(3));
        }
        catch (Exception ex) { Log.Write("Files completion was not acknowledged", ex); }
        finally { _core.Dispose(); Stopped?.Invoke(); }
    }
    internal void Stop()
    {
        if (_stopping) return;
        if (_window is not null) _window.Close();
        else { _stopping = true; _timer.Stop(); _cancel.Cancel(); _core.Dispose(); Stopped?.Invoke(); }
    }
    internal void OpenTarget(string path)
    {
        if (_stopping) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Files could not open the selected item", ex); _window?.View.ShowMessage("Could not open this item: " + ex.Message); }
    }
    internal async Task QueueTransferAsync(string[] sources, FileJobKind kind)
    {
        var destination = await _client.CallAsync<FilesResult>(RuntimeOperations.OpenFiles, new FilesLaunch(new(FileSelectionKind.Folder, kind == FileJobKind.Copy ? "Copy selection to…" : "Move selection to…")), null, _cancel.Token);
        if (_stopping || destination.Paths.Length == 0) return;
        var snapshot = await _client.CallAsync<FileJobsSnapshot>(RuntimeOperations.FileJobs, new { }, TimeSpan.FromSeconds(5), _cancel.Token);
        var spec = FileJobRules.Validate(new(Guid.NewGuid(), kind, sources, destination.Paths[0]));
        try
        {
            await _client.CallAsync<FileJobsSnapshot>(RuntimeOperations.SubmitFileJob, new FileJobSubmit(snapshot.Epoch, spec), TimeSpan.FromSeconds(5), _cancel.Token);
            if (!_stopping) _window?.View.ShowMessage("Transfer accepted by Core. Open Nexus menu → File jobs for progress and cancellation.");
        }
        catch (Exception error) when (error is IOException or TimeoutException)
        {
            // Submission is not retried with a new ID after a lost reply.
            if (!_stopping) _window?.View.ShowMessage("Transfer acknowledgement was lost. Review File Jobs before queuing it again.");
        }
    }
    internal async Task WindowsFileAsync(string path, string kind)
    {
        if (_stopping || _window is null) return;
        var (x, y) = WindowsShellBridge.Cursor(); var request = new WindowsFileAction(Guid.NewGuid(), kind, path, _window.Handle.ToInt64(), x, y);
        WindowsShellBridge.AllowForeground(_core.Id);
        var status = await _client.CallAsync<WindowsFileActionStatus>(RuntimeOperations.WindowsFileAction, request, TimeSpan.FromSeconds(5), _cancel.Token);
        WindowsShellBridge.AllowForeground(status.ProcessId);
        for (int i = 0; i < 8 && !_stopping; i++)
        {
            await Task.Delay(250, _cancel.Token);
            status = await _client.CallAsync<WindowsFileActionStatus>(RuntimeOperations.WindowsFileStatus, new WindowsFileStatusRequest(request.Id), TimeSpan.FromSeconds(5), _cancel.Token);
            if (status.State == "Failed") throw new InvalidOperationException(status.Detail);
            if (status.State == "Finished") break;
        }
    }
}
