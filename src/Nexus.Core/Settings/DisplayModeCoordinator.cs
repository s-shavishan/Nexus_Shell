using Nexus.Runtime;

namespace Nexus.Core.Settings;

// Core-owned display-mode transaction. Preview changes are explicitly
// temporary: a timer restores the driver's previous mode unless Keep confirms
// it. Nexus never persists an unconfirmed mode to the Windows registry.
internal sealed class DisplayModeCoordinator(Action<Exception>? report = null) : IDisposable
{
    private const int TimeoutSeconds = 18;
    private sealed class Pending(string device, string id, DisplaySettings.Mode previous, DisplaySettings.Mode selected, DateTimeOffset until)
    {
        internal string Device { get; } = device;
        internal string Id { get; } = id;
        internal DisplaySettings.Mode Previous = previous;
        internal DisplaySettings.Mode Selected = selected;
        internal DateTimeOffset Until { get; } = until;
        internal Timer? Timer;
    }

    private readonly object _gate = new();
    private Pending? _pending;
    private bool _disposed;

    internal DisplayModePreview? Snapshot()
    {
        lock (_gate)
        {
            if (_pending is not { } pending || _disposed) return null;
            return new(pending.Device, pending.Id,
                Math.Max(0, (int)Math.Ceiling((pending.Until - DateTimeOffset.UtcNow).TotalSeconds)));
        }
    }

    internal void Change(SettingsChange change)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisplayModeCoordinator));
            switch (change.Kind)
            {
                case "display-preview":
                {
                    if (_pending is not null)
                        throw new InvalidOperationException("Confirm or revert the previous display preview first.");
                    var previous = DisplaySettings.CurrentMode(change.DeviceId);
                    var selected = DisplaySettings.Choice(change.DeviceId, change.ItemId);
                    if (DisplaySettings.ModeId(previous) == DisplaySettings.ModeId(selected)) return;

                    DisplaySettings.Apply(change.DeviceId, ref selected, persist: false);
                    var pending = new Pending(change.DeviceId, change.ItemId, previous, selected,
                        DateTimeOffset.UtcNow.AddSeconds(TimeoutSeconds));
                    _pending = pending;
                    try
                    {
                        pending.Timer = new Timer(_ => Timeout(pending), null,
                            TimeSpan.FromSeconds(TimeoutSeconds), Timeout.InfiniteTimeSpan);
                    }
                    catch
                    {
                        _pending = null;
                        try { DisplaySettings.Apply(pending.Device, ref pending.Previous, persist: false); }
                        catch (Exception error) { report?.Invoke(error); }
                        throw;
                    }
                    break;
                }
                case "display-keep":
                {
                    if (_pending is not { } pending || pending.Device != change.DeviceId)
                        throw new InvalidOperationException("The display preview already expired. Refresh first.");
                    if (pending.Until <= DateTimeOffset.UtcNow)
                    {
                        bool restored = Restore(pending);
                        throw new InvalidOperationException(restored
                            ? "The preview expired and the previous resolution was restored."
                            : "The preview expired, but Windows could not restore the old resolution yet. Try Revert again.");
                    }
                    // Persist ONLY after explicit confirmation.
                    var selected = pending.Selected;
                    DisplaySettings.Apply(pending.Device, ref selected, persist: true);
                    _pending = null;
                    pending.Timer?.Dispose();
                    break;
                }
                case "display-revert":
                {
                    if (_pending is not { } pending || pending.Device != change.DeviceId)
                        throw new InvalidOperationException("There is no active preview to revert.");
                    if (!Restore(pending))
                        throw new InvalidOperationException("Windows could not restore the old resolution. Revert is still available.");
                    break;
                }
                default:
                    throw new InvalidOperationException("Unknown display-mode operation.");
            }
        }
    }

    private void Timeout(Pending original)
    {
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(_pending, original)) return;
            Restore(original);
        }
    }

    private bool Restore(Pending pending)
    {
        try
        {
            DisplaySettings.Apply(pending.Device, ref pending.Previous, persist: false);
            _pending = null;
            pending.Timer?.Dispose();
            return true;
        }
        catch (Exception error)
        {
            // Do not silently claim rollback when a driver rejects it or the
            // monitor disconnects. Preserve pending state and retry shortly.
            try { report?.Invoke(error); } catch { }
            if (!_disposed) pending.Timer?.Change(TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
            return false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_pending is { } pending)
            {
                // Dispose must not leave an orphan retry timer after Core exits.
                for (int attempt = 0; attempt < 3 && ReferenceEquals(_pending, pending); attempt++)
                    if (Restore(pending)) break;
                pending.Timer?.Dispose();
            }
            _disposed = true;
        }
    }
}
