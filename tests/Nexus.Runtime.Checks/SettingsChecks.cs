using Nexus.Core.Settings;
using Nexus.Runtime;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;

internal static class SettingsChecks
{
    private static int _checks;
    private static void Check(bool condition, string message) { _checks++; if (!condition) throw new Exception(message); }
    internal sealed class Backend(Func<SettingsRequest, CancellationToken, Task<SettingsSnapshot>> execute) : ISystemSettingsBackend
    {
        internal int Calls;
        public Task<SettingsSnapshot> ExecuteAsync(SettingsRequest request, CancellationToken cancellation)
        { Interlocked.Increment(ref Calls); return execute(request, cancellation); }
    }
    private static async Task Reject(string code, Func<Task> work)
    { try { await work(); } catch (RuntimeFailure error) { Check(error.Code == code, "Expected " + code + ", got " + error.Code); return; } throw new Exception("Expected " + code); }
    internal static async Task RunAsync()
    {
        var backend = new Backend((request, _) => Task.FromResult(new SettingsSnapshot(Guid.NewGuid(), request.Section)));
        var service = new SystemSettingsCoordinator(backend);
        foreach (string section in SettingsRules.Sections)
        {
            var state = await service.ExecuteAsync(new(section), CancellationToken.None);
            Check(state.Section == section && state.Epoch == service.Epoch && state.Epoch != Guid.Empty, "The service must own the snapshot epoch.");
        }
        int reads = backend.Calls;
        await Reject("settings-section", () => service.ExecuteAsync(new("Registry"), CancellationToken.None));
        await Reject("settings-window", () => service.ExecuteAsync(new("Sound", Window: -1), CancellationToken.None));
        await Reject("settings-scan", () => service.ExecuteAsync(new("Sound", Scan: true), CancellationToken.None));
        await Reject("settings-stale", () => service.ExecuteAsync(new("Sound", Change: new("volume", "output", Value: 30)), CancellationToken.None));
        await Reject("settings-stale", () => service.ExecuteAsync(new("Sound", Change: new("volume", "output", Value: 30, Epoch: Guid.NewGuid())), CancellationToken.None));
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1, 101 })
            await Reject("settings-value", () => service.ExecuteAsync(new("Sound", Change: new("volume", "output", Value: invalid, Epoch: service.Epoch)), CancellationToken.None));
        foreach (string target in new[] { "", " ", new string('x', 513), "bad\0device" })
            await Reject("settings-target", () => service.ExecuteAsync(new("Sound", Change: new("volume", target, Value: 10, Epoch: service.Epoch)), CancellationToken.None));
        await Reject("settings-command", () => service.ExecuteAsync(new("Display", Change: new("volume", "output", Value: 30, Epoch: service.Epoch)), CancellationToken.None));
        await Reject("settings-command", () => service.ExecuteAsync(new("Power", Change: new("power-plan", "plan", Value: 30, Epoch: service.Epoch)), CancellationToken.None));
        await Reject("settings-command", () => service.ExecuteAsync(new("Network", Change: new("wifi-connect", "adapter", Epoch: service.Epoch)), CancellationToken.None));
        await Reject("settings-command", () => service.ExecuteAsync(new("Network", Change: new("wifi-radio", "adapter", Enabled: true, Epoch: service.Epoch), Scan: true), CancellationToken.None));
        await Reject("settings-command", () => service.ExecuteAsync(new("Bluetooth", Change: new("bluetooth-power", "radio", Enabled: true, Epoch: service.Epoch)), CancellationToken.None));
        await Reject("settings-secret", () => service.ExecuteAsync(new("Sound", Change: new("volume", "output", Value: 20, Epoch: service.Epoch, Secret: "unexpected")), CancellationToken.None));
        await Reject("settings-secret", () => service.ExecuteAsync(new("Network", Change: new("wifi-join", "adapter", "network", Epoch: service.Epoch, Secret: new string('x', 65))), CancellationToken.None));
        Check(backend.Calls == reads, "Rejected commands must never reach hardware.");
        foreach (var request in new SettingsRequest[]
        {
            new("Sound", Change: new("volume", "output", Value: 0, Epoch: service.Epoch)),
            new("Sound", Change: new("volume", "output", "session", Value: 100, Enabled: true, Epoch: service.Epoch)),
            new("Sound", Change: new("volume", "output", Enabled: false, Epoch: service.Epoch)),
            new("Display", Change: new("brightness", "display", Value: 65, Epoch: service.Epoch)),
            new("Network", Change: new("wifi-connect", "adapter", "Saved profile", Epoch: service.Epoch)),
            new("Network", Change: new("wifi-disconnect", "adapter", Epoch: service.Epoch)),
            new("Network", Change: new("wifi-radio", "adapter", Enabled: true, Epoch: service.Epoch)),
            new("Network", Change: new("wifi-join", "adapter", "network", Epoch: service.Epoch, Secret: "Example key")),
            new("Network", Change: new("wifi-forget", "adapter", "Nexus-profile", Epoch: service.Epoch)),
            new("Bluetooth", Change: new("bluetooth-discovery", "radio", Enabled: false, Epoch: service.Epoch)),
            new("Power", Change: new("power-plan", Guid.NewGuid().ToString(), Epoch: service.Epoch)),
            new("Bluetooth", Scan: true), new("Network", Scan: true)
        }) Check((await service.ExecuteAsync(request, CancellationToken.None)).Section == request.Section, "An allowed command should reach its section.");
        var restarted = new SystemSettingsCoordinator(backend);
        Check(restarted.Epoch != service.Epoch, "Every Core restart must change the hardware command epoch.");
        await Reject("settings-stale", () => restarted.ExecuteAsync(new("Sound", Change: new("volume", "output", Value: 5, Epoch: service.Epoch)), CancellationToken.None));
        var wrong = new SystemSettingsCoordinator(new Backend((_, _) => Task.FromResult(new SettingsSnapshot(default, "Power"))));
        await Reject("settings-state", () => wrong.ExecuteAsync(new("Sound"), CancellationToken.None));

        await TimeoutAndCancellationAsync(); IntentChecks(service.Epoch); NativeLayouts(); WifiProfiles();
        foreach (var (uri, section) in new[] { ("ms-settings:", "Sound"), ("MS-SETTINGS:Sound", "Sound"), ("ms-settings:apps-volume", "Sound"), ("ms-settings:display?source=test", "Display"),
            ("ms-settings:network-status", "Network"), ("ms-settings:network-wifi", "Network"), ("ms-settings:network-ethernet", "Network"), ("ms-settings:bluetooth", "Bluetooth"), ("ms-settings:powersleep", "Power"),
            ("ms-settings:personalization-background", "Desktop"), ("ms-settings:taskbar", "Desktop") })
            Check(SettingsRules.SectionForUri(uri) == section, "The desktop must route " + uri + " into Nexus.");
        Check(SettingsRules.SectionForUri("ms-settings:windowsupdate") is null && SettingsRules.SectionForUri("https://example.com") is null, "Unimplemented pages must remain explicit external controls.");
        var hardware = new SettingsSnapshot(service.Epoch, "Sound", Sound: new(true, "output", "Speakers", .75f, false, [new("session", "Player", .5f, true)], ""));
        var roundTrip = RuntimeProtocol.Payload<SettingsSnapshot>(RuntimeProtocol.Body(hardware));
        Check(roundTrip.Epoch == hardware.Epoch && roundTrip.Sound!.Sessions[0].Muted && roundTrip.Sound.Volume == .75f, "Hardware snapshots must survive the framed JSON contract.");
        var preferences = new ShellState { QuietNotifications = true, CompactDock = true, NativeGlass = false };
        Check(preferences.Snapshot().QuietNotifications && JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(preferences))!.QuietNotifications, "Quiet Nexus alerts must survive snapshot and restart.");
        Check(!JsonSerializer.Deserialize<ShellState>("{}")!.QuietNotifications, "Older profiles must keep normal alerts enabled.");
        Console.WriteLine($"PASS: {_checks} settings assertions; allowlist, stale epochs, timeouts, cancellation, bounded final intents, routes, persistence and x64 native layouts. Hardware calls require Windows VM acceptance.");
    }
    private static async Task TimeoutAndCancellationAsync()
    {
        var native = new TaskCompletionSource<SettingsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new Backend((request, _) => request.Section == "Sound" ? native.Task : Task.FromResult(new SettingsSnapshot(default, request.Section)));
        var service = new SystemSettingsCoordinator(backend, TimeSpan.FromMilliseconds(40));
        await Reject("settings-timeout", () => service.ExecuteAsync(new("Sound", Change: new("volume", "output", Value: 80, Epoch: service.Epoch)), CancellationToken.None));
        await Reject("settings-busy", () => service.ExecuteAsync(new("Sound"), CancellationToken.None));
        Check(backend.Calls == 1, "A timed-out driver must retain its lane and must not be replayed.");
        Check((await service.ExecuteAsync(new("Power"), CancellationToken.None)).Section == "Power", "A hung audio driver must not block power or Core health.");
        native.SetResult(new(default, "Sound"));
        // The production continuation releases the lane only on actual completion.
        for (int i = 0; i < 100; i++)
        {
            try { await service.ExecuteAsync(new("Sound"), CancellationToken.None); break; }
            catch (RuntimeFailure error) when (error.Code == "settings-busy") { await Task.Delay(5); }
        }
        Check(backend.Calls == 3, "Late completion should permit one new read, without replaying the old write.");

        var cancelledNative = new TaskCompletionSource<SettingsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelledBackend = new Backend((_, _) => cancelledNative.Task);
        var cancelledService = new SystemSettingsCoordinator(cancelledBackend);
        using var cancel = new CancellationTokenSource();
        var pending = cancelledService.ExecuteAsync(new("Display"), cancel.Token); cancel.Cancel();
        bool cancelled = false; try { await pending; } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Caller cancellation must preserve cancellation rather than turn into a driver failure.");
        await Reject("settings-busy", () => cancelledService.ExecuteAsync(new("Display"), CancellationToken.None));
        cancelledNative.SetResult(new(default, "Display"));
        using var alreadyCancelled = new CancellationTokenSource(); alreadyCancelled.Cancel();
        cancelled = false; try { await cancelledService.ExecuteAsync(new("Power"), alreadyCancelled.Token); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && cancelledBackend.Calls == 1, "Already-cancelled requests must never start hardware work.");
        var failing = new SystemSettingsCoordinator(new Backend((_, _) => throw new IOException("Injected driver failure")));
        for (int i = 0; i < 2; i++)
        { bool threw = false; try { await failing.ExecuteAsync(new("Network"), CancellationToken.None); } catch (IOException) { threw = true; } Check(threw, "A synchronous failure must release its lane for the next request."); }
    }
    private static void IntentChecks(Guid epoch)
    {
        var buffer = new SettingsIntentBuffer();
        for (int i = 0; i < 1000; i++) buffer.Set(new("Sound", Change: new("volume", "output", Value: i % 101, Epoch: epoch)));
        Check(buffer.Count == 1 && buffer.Find("Sound", "volume", "output", "")!.Value == 90, "Slider movement must coalesce to its exact final value.");
        buffer.Set(new("Sound", Change: new("volume", "output", "app", Value: 20, Epoch: epoch)));
        Check(buffer.Count == 2, "Master and application controls must retain separate intents.");
        var final = buffer.Take(); Check(final!.Change!.Value == 90 && final.Change.Epoch == epoch && buffer.Count == 1, "Closing a panel must drain the original final intent and epoch.");
        buffer.Clear(); Check(buffer.Count == 0 && buffer.Take() is null, "An uncertain failure must discard queued writes.");
        buffer.Set(new("Sound", Change: new("volume", "a|b", "c", Value: 1, Epoch: epoch)));
        buffer.Set(new("Sound", Change: new("volume", "a", "b|c", Value: 2, Epoch: epoch)));
        Check(buffer.Count == 2, "Native IDs containing delimiters must not collide.");
        buffer.Clear(); for (int i = 0; i < 64; i++) buffer.Set(new("Sound", Change: new("volume", "output", "app" + i, Value: 1, Epoch: epoch)));
        bool bounded = false; try { buffer.Set(new("Sound", Change: new("volume", "output", "overflow", Value: 1, Epoch: epoch))); } catch (RuntimeFailure) { bounded = true; }
        Check(bounded && buffer.Count == 64, "Device command buffering must be bounded.");
        buffer.Set(new("Sound", Change: new("volume", "output", "app0", Value: 99, Epoch: epoch)));
        Check(buffer.Count == 64 && buffer.Find("Sound", "volume", "output", "app0")!.Value == 99, "A full queue must still accept the latest value for an existing control.");
    }
    private static void NativeLayouts()
    {
        if (IntPtr.Size != 8) { Console.WriteLine("SKIP: x64 native layout checks on a non-x64 process."); return; }
        Check(Marshal.SizeOf<NetworkSettings.Interface>() == 532, "WLAN_INTERFACE_INFO layout");
        Check(Marshal.SizeOf<NetworkSettings.Ssid>() == 36 && Marshal.SizeOf<NetworkSettings.Available>() == 628, "WLAN_AVAILABLE_NETWORK layout");
        Check(Marshal.OffsetOf<NetworkSettings.Available>(nameof(NetworkSettings.Available.Ssid)).ToInt32() == 512, "SSID offset");
        Check(Marshal.SizeOf<NetworkSettings.PhyRadio>() == 12 && Marshal.SizeOf<NetworkSettings.Connection>() == 40, "WLAN radio and connection layouts");
        Check(Marshal.SizeOf<NetworkSettings.ProfileInfo>() == 516, "WLAN_PROFILE_INFO layout");
        Check(Marshal.SizeOf<BluetoothSettings.RadioInfo>() == 520 && Marshal.SizeOf<BluetoothSettings.DeviceInfo>() == 560, "Bluetooth info layouts");
        Check(Marshal.SizeOf<BluetoothSettings.DeviceSearch>() == 40 && Marshal.OffsetOf<BluetoothSettings.DeviceSearch>(nameof(BluetoothSettings.DeviceSearch.Radio)).ToInt32() == 32, "Bluetooth inquiry layout");
        Check(Marshal.SizeOf<DisplaySettings.Device>() == 840 && Marshal.SizeOf<DisplaySettings.Mode>() == 220, "DISPLAY_DEVICEW and DEVMODEW layouts");
        Check(Marshal.SizeOf<PowerSettings.Status>() == 12, "SYSTEM_POWER_STATUS layout");
    }
    private static void WifiProfiles()
    {
        XNamespace ns = "http://www.microsoft.com/networking/WLAN/profile/v1";
        const string example = "<Example&Key>";
        var secured = XElement.Parse(WifiProfile.Build("Nexus-test", "414243", example, 7, 4));
        Check(secured.Name == ns + "WLANProfile" && secured.Descendants(ns + "hex").Single().Value == "414243", "The Wi-Fi profile must preserve SSID bytes.");
        Check(secured.Descendants(ns + "keyMaterial").Single().Value == example && !secured.ToString().Contains(example), "A Wi-Fi password must be XML-escaped without changing its value.");
        Check(secured.Descendants(ns + "authentication").Single().Value == "WPA2PSK" && secured.Descendants(ns + "connectionMode").Single().Value == "manual", "Creating a connection must not enable automatic joining.");
        var open = XElement.Parse(WifiProfile.Build("Nexus-open", "FF00", "", 1, 0));
        Check(!open.Descendants(ns + "sharedKey").Any() && open.Descendants(ns + "authentication").Single().Value == "open", "Open profiles must not contain credentials.");
        var hex = XElement.Parse(WifiProfile.Build("Nexus-hex", "4142", new string('A', 64), 7, 4));
        Check(hex.Descendants(ns + "keyType").Single().Value == "networkKey", "A 64-digit PSK must use the networkKey schema.");
        foreach (var (ssid, key, auth, cipher) in new[] { ("", "Example key", 7, 4), ("ABC", "Example key", 7, 4), ("GG", "Example key", 7, 4),
            ("4142", "short", 7, 4), ("4142", "bad\npassword", 7, 4), ("4142", new string('x', 64), 7, 4), ("4142", "Example key", 1, 0), ("4142", "Example key", 6, 4) })
        { bool rejected = false; try { WifiProfile.Build("Nexus-invalid", ssid, key, auth, cipher); } catch (RuntimeFailure) { rejected = true; } Check(rejected, "Invalid SSIDs, keys and enterprise authentication must be rejected before creating a profile."); }
        Check(!new SettingsChange("wifi-join", Secret: example).ToString().Contains(example), "Diagnostic formatting must redact connection details.");
    }
}
