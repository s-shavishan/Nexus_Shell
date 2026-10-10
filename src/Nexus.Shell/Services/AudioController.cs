using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Nexus.Shell.Services;

public sealed record AudioApp(string Id, string Name, float Volume, bool Muted);
public sealed record AudioSnapshot(bool Available, string DeviceId, string Device, float Volume, bool Muted, AudioApp[] Apps, string Message)
{
    public static AudioSnapshot Unavailable(string message) => new(false, "", "No audio output", 0, false, [], message);
}
public sealed record AudioChange(string Id, float? Volume, bool? Muted);

// Audio reads, writes and retained COM objects stay on one MTA worker. No device opens
// during shell startup, no audio is recorded, and reads never alter a level.
[SupportedOSPlatform("windows")]
public sealed class AudioController : IDisposable
{
    private readonly BlockingCollection<Action> _jobs = new(128);
    private readonly Thread _thread;
    private readonly TaskCompletionSource<bool> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Completion => _finished.Task;
    private readonly Dictionary<string, IAudioSessionControl2> _sessions = new(StringComparer.Ordinal);
    private IMMDeviceEnumerator? _enumerator;
    private IMMDevice? _device;
    private IAudioEndpointVolume? _volume;
    private IAudioSessionManager2? _manager;
    private AudioSessionNotification? _notification;
    private string _deviceId = "", _deviceName = "";
    private long _epoch;
    private volatile bool _disposed;
    private Exception? _startupError;
    private static readonly Guid EventContext = new("54265035-95B1-4B5B-A673-2435F5C10464");
    public AudioController()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Nexus audio" };
        _thread.SetApartmentState(ApartmentState.MTA); _thread.Start();
    }
    public Task<AudioSnapshot> ReadAsync() => Queue(Read);
    public Task<bool> SuspendAsync() => Queue(() => { Disconnect(); return true; });
    public Task<AudioSnapshot> ChangeAsync(string deviceId, IReadOnlyList<AudioChange> changes, CancellationToken cancellation = default) => Queue(() =>
    {
        cancellation.ThrowIfCancellationRequested();
        EnsureEndpoint();
        if (deviceId != _deviceId) throw new InvalidOperationException("The audio output changed. Refresh before changing its volume.");
        foreach (var change in changes)
        {
            cancellation.ThrowIfCancellationRequested();
            if (change.Volume is float level && (!float.IsFinite(level) || level < 0 || level > 1))
                throw new ArgumentOutOfRangeException(nameof(changes));
            var context = EventContext;
            if (change.Id.Length == 0)
            {
                if (change.Volume is float master) Check(_volume!.SetMasterVolumeLevelScalar(master, ref context));
                if (change.Muted is bool muted) Check(_volume!.SetMute(muted, ref context));
            }
            else
            {
                if (!_sessions.TryGetValue(change.Id, out var session)) throw new InvalidOperationException("That audio session ended. Refresh the mixer.");
                var volume = (ISimpleAudioVolume)session;
                if (change.Volume is float appLevel) Check(volume.SetMasterVolume(appLevel, ref context));
                if (change.Muted is bool muted) Check(volume.SetMute(muted, ref context));
            }
        }
        return Read();
    });
    private Task<T> Queue<T>(Func<T> work)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Invoke()
        {
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(AudioController));
                if (_startupError is not null) throw new InvalidOperationException("Windows audio is unavailable.", _startupError);
                result.SetResult(work());
            }
            catch (Exception ex) { result.SetException(ex); }
        }
        try { if (_disposed || !_jobs.TryAdd(Invoke)) result.TrySetException(new InvalidOperationException("Audio controls are busy or closed. Try Refresh.")); }
        catch (InvalidOperationException) { result.TrySetException(new ObjectDisposedException(nameof(AudioController))); }
        return result.Task;
    }
    private void Run()
    {
        int initialized = CoInitializeEx(IntPtr.Zero, 0);
        if (initialized < 0) _startupError = Marshal.GetExceptionForHR(initialized);
        try { foreach (var work in _jobs.GetConsumingEnumerable()) work(); }
        finally
        {
            Disconnect(); Release(_enumerator); _enumerator = null;
            if (initialized >= 0) CoUninitialize();
            _finished.TrySetResult(true);
        }
    }
    private AudioSnapshot Read()
    {
        try
        {
            EnsureEndpoint(); Check(_volume!.GetMasterVolumeLevelScalar(out float level)); Check(_volume.GetMute(out bool muted));
            var apps = new List<AudioApp>();
            foreach (var pair in _sessions.ToArray())
            {
                try
                {
                    Check(pair.Value.GetState(out int state));
                    if (state == 2) { _sessions.Remove(pair.Key); Release(pair.Value); continue; }
                    Check(pair.Value.GetProcessId(out uint pid));
                    var simple = (ISimpleAudioVolume)pair.Value;
                    Check(simple.GetMasterVolume(out float appLevel)); Check(simple.GetMute(out bool appMuted));
                    string name = pid == 0 ? "System sounds" : "App " + pid;
                    if (pid != 0) try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; } catch { }
                    apps.Add(new(pair.Key, name, Scalar(appLevel), appMuted));
                }
                catch { _sessions.Remove(pair.Key); Release(pair.Value); }
            }
            return new(true, _deviceId, _deviceName, Scalar(level), muted,
                apps.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), "");
        }
        catch (Exception ex)
        {
            Disconnect(); Log.Write("Audio read unavailable", ex);
            return AudioSnapshot.Unavailable("Connect an audio output or start Windows Audio, then choose Refresh.");
        }
    }
    private void EnsureEndpoint()
    {
        _enumerator ??= (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), true)!)!;
        Check(_enumerator.GetDefaultAudioEndpoint(0, 0, out var device));
        try
        {
            Check(device.GetId(out string id));
            if (_device is not null && id == _deviceId) return;
            Disconnect(); _device = device; device = null!; _deviceId = id;
            _deviceName = FriendlyName(_device);
            Guid volumeId = typeof(IAudioEndpointVolume).GUID, managerId = typeof(IAudioSessionManager2).GUID;
            Check(_device.Activate(ref volumeId, 23, IntPtr.Zero, out object volume)); _volume = (IAudioEndpointVolume)volume;
            Check(_device.Activate(ref managerId, 23, IntPtr.Zero, out object manager)); _manager = (IAudioSessionManager2)manager;
            long epoch = ++_epoch;
            _notification = new AudioSessionNotification(session => AcceptSession(session, epoch));
            Check(_manager.RegisterSessionNotification(_notification));
            Check(_manager.GetSessionEnumerator(out var sessions));
            try
            {
                Check(sessions.GetCount(out int count));
                for (int i = 0; i < Math.Min(count, 32); i++) if (sessions.GetSession(i, out object session) >= 0) AddSession(session, epoch);
            }
            finally { Release(sessions); }
        }
        catch { Disconnect(); throw; }
        finally { Release(device); }
    }
    private void AcceptSession(object session, long epoch)
    {
        try { if (!_disposed && _jobs.TryAdd(() => AddSession(session, epoch))) return; } catch (InvalidOperationException) { }
        Release(session);
    }
    private void AddSession(object candidate, long epoch)
    {
        bool retained = false;
        try
        {
            if (_disposed || epoch != _epoch || _sessions.Count >= 32) return;
            var session = (IAudioSessionControl2)candidate;
            Check(session.GetSessionInstanceIdentifier(out string id));
            if (_sessions.ContainsKey(id)) return;
            _sessions.Add(id, session); retained = true;
        }
        catch { }
        finally { if (!retained) Release(candidate); }
    }
    private void Disconnect()
    {
        ++_epoch;
        if (_manager is not null && _notification is not null) try { _manager.UnregisterSessionNotification(_notification); } catch { }
        _notification = null;
        foreach (var session in _sessions.Values) Release(session);
        _sessions.Clear(); Release(_manager); Release(_volume); Release(_device);
        _manager = null; _volume = null; _device = null; _deviceId = ""; _deviceName = "";
    }
    private static string FriendlyName(IMMDevice device)
    {
        IPropertyStore? store = null;
        try
        {
            Check(device.OpenPropertyStore(0, out store));
            var key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
            Check(store.GetValue(ref key, out var value));
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? "Default output" : "Default output"; }
            finally { PropVariantClear(ref value); }
        }
        catch { return "Default output"; }
        finally { Release(store); }
    }
    private static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
    private static float Scalar(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : throw new InvalidOperationException("The audio driver returned an invalid level.");
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) try { Marshal.ReleaseComObject(value); } catch { } }
    public void Dispose() { if (_disposed) return; _disposed = true; _jobs.CompleteAdding(); }

    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant
    { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern void CoUninitialize();
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern int PropVariantClear(ref PropVariant value);
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint state, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid id, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float db, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid id, uint flags, out IntPtr session);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid id, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        [PreserveSig] int RegisterSessionNotification(IAudioSessionNotification notification);
        [PreserveSig] int UnregisterSessionNotification(IAudioSessionNotification notification);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}

[ComVisible(true), Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionNotification
{ [PreserveSig] int OnSessionCreated([MarshalAs(UnmanagedType.IUnknown)] object session); }
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class AudioSessionNotification : IAudioSessionNotification
{
    private readonly Action<object> _accept;
    internal AudioSessionNotification(Action<object> accept) => _accept = accept;
    public int OnSessionCreated(object session) { _accept(session); return 0; }
}
