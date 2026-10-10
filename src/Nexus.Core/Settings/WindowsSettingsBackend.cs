using Nexus.Runtime;
using Nexus.Shell.Services;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Nexus.Core.Settings;

// Owned by the per-user Core process. The desktop gets only typed snapshots and
// allowlisted commands; no Explorer launch, shell command or privileged broker.
[SupportedOSPlatform("windows")]
internal sealed class WindowsSettingsBackend(int desktopProcessId, Action<Exception>? report = null) : ISystemSettingsBackend, IDisposable
{
    private readonly Lazy<AudioController> _audio = new(() => new());
    public async Task<SettingsSnapshot> ExecuteAsync(SettingsRequest request, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        try
        {
            switch (request.Section)
            {
                case "Sound":
                    var audio = request.Change is { } sound
                        ? await _audio.Value.ChangeAsync(sound.DeviceId, [new(sound.ItemId, sound.Value is double v ? (float)(v / 100) : null, sound.Enabled)], cancellation).ConfigureAwait(false)
                        : await _audio.Value.ReadAsync().ConfigureAwait(false);
                    return new(default, "Sound", Sound: new(audio.Available, audio.DeviceId, audio.Device, audio.Volume, audio.Muted,
                        audio.Apps.Take(32).Select(app => new MixerLevel(app.Id, app.Name, app.Volume, app.Muted)).ToArray(), audio.Message));
                case "Display":
                    // Reject arbitrary HWNDs even from the authenticated desktop.
                    var window = new IntPtr(request.Window);
                    if (window == IntPtr.Zero || GetWindowThreadProcessId(window, out uint owner) == 0 || owner != desktopProcessId)
                        throw new RuntimeFailure("settings-window", "Refresh from the Nexus desktop to select its display.");
                    var brightness = new BrightnessController(window);
                    var level = request.Change is { } display
                        ? await brightness.ChangeAsync(display.DeviceId, display.Value!.Value, cancellation).ConfigureAwait(false)
                        : await brightness.ReadAsync().ConfigureAwait(false);
                    var displays = await Task.Run(DisplaySettings.Read, cancellation).ConfigureAwait(false);
                    return new(default, "Display", Brightness: new(level.Available, level.DeviceId, level.Device, level.Percent, level.Message), Displays: displays);
                case "Network": return await Task.Run(() => new SettingsSnapshot(default, "Network", Network: NetworkSettings.Execute(request, cancellation)), cancellation).ConfigureAwait(false);
                case "Bluetooth": return await Task.Run(() => new SettingsSnapshot(default, "Bluetooth", Bluetooth: BluetoothSettings.Execute(request, cancellation)), cancellation).ConfigureAwait(false);
                case "Power": return await Task.Run(() => new SettingsSnapshot(default, "Power", Power: PowerSettings.Execute(request.Change, cancellation)), cancellation).ConfigureAwait(false);
                default: throw new RuntimeFailure("settings-section", "This settings section is unavailable.");
            }
        }
        catch (Exception error) when (error is Win32Exception or COMException or InvalidOperationException)
        { report?.Invoke(error); throw new RuntimeFailure("settings-device", error.Message); }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        { report?.Invoke(error); throw new RuntimeFailure("settings-device", "Windows did not provide the " + request.Section + " device API. Other Control Center sections remain available."); }
    }
    public void Dispose() { if (_audio.IsValueCreated) _audio.Value.Dispose(); }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
