namespace Nexus.Runtime;

public sealed record MixerLevel(string Id, string Name, float Volume, bool Muted);
public sealed record SoundState(bool Available, string DeviceId, string Device, float Volume, bool Muted, MixerLevel[] Sessions, string Message);
public sealed record BrightnessState(bool Available, string DeviceId, string Device, double Percent, string Message);
public sealed record LinkState(string Name, string Kind, string State, string Address, string Gateway, string Dns, long SpeedMbps);
public sealed record WifiNetwork(string Name, string Profile, int Signal, bool Connected, bool Secured,
    string SsidHex = "", int Authentication = 0, int Cipher = 0, bool CanJoin = false, bool CanForget = false)
{ public string Id => SsidHex + ":" + Authentication + ":" + Cipher; }
public sealed record WifiAdapter(string Id, string Name, bool RadioAvailable, bool SoftwareOn, bool HardwareOn, WifiNetwork[] Networks, string Message);
public sealed record NetworkState(LinkState[] Links, WifiAdapter[] Wifi, string Message);
public sealed record BluetoothDeviceState(string Id, string Name, bool Connected, bool Paired);
public sealed record BluetoothRadioState(string Id, string Name, bool Discoverable, BluetoothDeviceState[] Devices);
public sealed record BluetoothState(BluetoothRadioState[] Radios, string Message);
public sealed record PowerPlan(string Id, string Name, bool Active);
public sealed record PowerState(PowerPlan[] Plans, string Status, string Message);
public sealed record DisplayState(string Name, int Width, int Height, int RefreshHz, bool Primary);
public sealed record SettingsChange(string Kind, string DeviceId = "", string ItemId = "", double? Value = null, bool? Enabled = null, Guid Epoch = default, string Secret = "")
{ public override string ToString() => "SettingsChange { Kind = " + Kind + ", Epoch = " + Epoch + ", connection details redacted }"; }
public sealed record SettingsRequest(string Section, long Window = 0, SettingsChange? Change = null, bool Scan = false);
public sealed record SettingsSnapshot(Guid Epoch, string Section, SoundState? Sound = null, BrightnessState? Brightness = null,
    NetworkState? Network = null, BluetoothState? Bluetooth = null, PowerState? Power = null, DisplayState[]? Displays = null, string Message = "");
public interface ISystemSettingsBackend
{ Task<SettingsSnapshot> ExecuteAsync(SettingsRequest request, CancellationToken cancellation); }

public static class SettingsRules
{
    public static IReadOnlyList<string> Sections { get; } = Array.AsReadOnly<string>(["Sound", "Display", "Network", "Bluetooth", "Power"]);
    public static void Validate(SettingsRequest request, Guid epoch)
    {
        if (!Sections.Contains(request.Section, StringComparer.Ordinal)) throw new RuntimeFailure("settings-section", "That settings section is unavailable.");
        if (request.Window < 0) throw new RuntimeFailure("settings-window", "The desktop window is invalid.");
        if (request.Scan && request.Section is not ("Network" or "Bluetooth")) throw new RuntimeFailure("settings-scan", "Scanning is unavailable for that section.");
        if (request.Change is not { } change) return;
        if (request.Scan) throw new RuntimeFailure("settings-command", "Scan and change must be separate requests.");
        if (change.Epoch == Guid.Empty || change.Epoch != epoch) throw new RuntimeFailure("settings-stale", "Nexus services restarted. Refresh these controls before changing a device.");
        if (change.DeviceId is null || change.ItemId is null || change.DeviceId.Length > 512 || change.ItemId.Length > 512
            || change.DeviceId.Contains('\0') || change.ItemId.Contains('\0')) throw new RuntimeFailure("settings-target", "The settings target is invalid.");
        if (change.Secret is null || change.Secret.Length > 64 || change.Secret.Contains('\0') || change.Kind != "wifi-join" && change.Secret.Length != 0)
            throw new RuntimeFailure("settings-secret", "This command contains invalid connection details.");
        bool allowed = request.Section switch
        {
            "Sound" => change.Kind == "volume" && (change.Value is not null || change.Enabled is not null),
            "Display" => change.Kind == "brightness" && change.Value is not null && change.Enabled is null && change.ItemId.Length == 0,
            "Network" => change.Value is null && (change.Kind == "wifi-connect" && change.Enabled is null && !string.IsNullOrWhiteSpace(change.ItemId) && change.ItemId.Length <= 256
                || change.Kind == "wifi-join" && change.Enabled is null && !string.IsNullOrWhiteSpace(change.ItemId)
                || change.Kind == "wifi-forget" && change.Enabled is null && !string.IsNullOrWhiteSpace(change.ItemId) && change.ItemId.Length <= 256
                || change.Kind == "wifi-disconnect" && change.Enabled is null && change.ItemId.Length == 0
                || change.Kind == "wifi-radio" && change.Enabled is not null && change.ItemId.Length == 0),
            "Bluetooth" => change.Kind == "bluetooth-discovery" && change.Enabled is not null && change.Value is null && change.ItemId.Length == 0,
            "Power" => change.Kind == "power-plan" && change.Value is null && change.Enabled is null && change.ItemId.Length == 0,
            _ => false
        };
        if (!allowed) throw new RuntimeFailure("settings-command", "That device command is unavailable in this section.");
        if (change.Value is double number && (!double.IsFinite(number) || number < 0 || number > 100))
            throw new RuntimeFailure("settings-value", "The setting must be between 0 and 100.");
        if (string.IsNullOrWhiteSpace(change.DeviceId)) throw new RuntimeFailure("settings-target", "Refresh and select a device first.");
    }
    public static string? SectionForUri(string uri)
    {
        if (uri.Equals("ms-settings:", StringComparison.OrdinalIgnoreCase)) return "Sound";
        if (!uri.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)) return null;
        string page = uri[12..].Split('?', '#')[0].ToLowerInvariant();
        return page switch
        {
            "sound" or "apps-volume" or "easeofaccess-audio" => "Sound",
            "display" or "screenrotation" => "Display",
            "network" or "network-status" or "network-wifi" or "network-wifisettings" or "network-ethernet" => "Network",
            "bluetooth" or "connecteddevices" => "Bluetooth",
            "powersleep" or "batterysaver" or "batterysaver-settings" => "Power",
            "personalization" or "personalization-background" or "personalization-colors" or "themes" or "taskbar" => "Desktop",
            _ => null
        };
    }
}
