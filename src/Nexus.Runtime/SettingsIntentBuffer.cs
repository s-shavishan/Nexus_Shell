namespace Nexus.Runtime;

// Dispatcher-owned buffer: keep only the latest intent per device control.
// Requests retain their original epoch; draining never rewrites or retries them.
public sealed class SettingsIntentBuffer
{
    private readonly Dictionary<(string Section, string Kind, string Device, string Item), SettingsRequest> _pending = [];
    public int Count => _pending.Count;
    private static (string, string, string, string) Key(SettingsRequest request)
        => (request.Section, request.Change!.Kind, request.Change.DeviceId, request.Change.ItemId);
    public void Set(SettingsRequest request)
    {
        if (request.Change is not { } change) throw new ArgumentException("Only device changes can be buffered.", nameof(request));
        SettingsRules.Validate(request, change.Epoch);
        var key = Key(request);
        if (_pending.Count >= 64 && !_pending.ContainsKey(key)) throw new RuntimeFailure("settings-busy", "Too many device changes are pending. Wait for the current request.");
        _pending[key] = request;
    }
    public SettingsRequest? Take()
    { if (_pending.Count == 0) return null; var pair = _pending.First(); _pending.Remove(pair.Key); return pair.Value; }
    public SettingsChange? Find(string section, string kind, string device, string item)
        => _pending.TryGetValue((section, kind, device, item), out var request) ? request.Change : null;
    public void Clear() => _pending.Clear();
}
