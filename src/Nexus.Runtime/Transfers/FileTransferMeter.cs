// නමෝ බුද්ධාය 🤍
namespace Nexus.Runtime.Transfers;

// Monotonic, bounded-memory content-phase average. Copy, compression and
// extraction measure their own byte phase; verification/rename time is extra.
public sealed class FileTransferMeter
{
    private long? _started;
    private long _baseline, _lastBytes, _lastTick;
    private string _phase = "";
    public void Reset() { _started = null; _phase = ""; }
    public FileJobProgress Sample(FileJobProgress progress, long now)
    {
        var clear = progress with { BytesPerSecond = 0, SecondsRemaining = null };
        if (!FileJobRules.MeasuredPhase(progress.Phase) || progress.BytesCopied < 0)
        { Reset(); return clear; }
        if (_started is null || _phase != progress.Phase || now < _lastTick || progress.BytesCopied < _lastBytes)
        { _started = now; _baseline = progress.BytesCopied; _phase = progress.Phase; }
        _lastTick = now; _lastBytes = progress.BytesCopied;
        double elapsed = (double)now - _started.Value;
        if (elapsed < 1000) return clear;
        double rate = ((double)progress.BytesCopied - _baseline) * 1000 / elapsed;
        if (!double.IsFinite(rate) || rate <= 0) return clear;
        double? remaining = progress.TotalBytes > progress.BytesCopied
            ? ((double)progress.TotalBytes - progress.BytesCopied) / rate : null;
        return clear with { BytesPerSecond = rate, SecondsRemaining = remaining is { } seconds && double.IsFinite(seconds) ? seconds : null };
    }
}
