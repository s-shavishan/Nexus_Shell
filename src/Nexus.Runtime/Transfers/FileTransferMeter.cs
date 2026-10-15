namespace Nexus.Runtime.Transfers;

// Monotonic, bounded-memory copy-phase average. Pauses and non-copy phases
// reset the sample; verification/rename time is never advertised as copy ETA.
public sealed class FileTransferMeter
{
    private long? _started;
    private long _baseline, _lastBytes, _lastTick;
    public void Reset() => _started = null;
    public FileJobProgress Sample(FileJobProgress progress, long now)
    {
        var clear = progress with { BytesPerSecond = 0, SecondsRemaining = null };
        if (progress.Phase != "Copying" || progress.BytesCopied < 0)
        { Reset(); return clear; }
        if (_started is null || now < _lastTick || progress.BytesCopied < _lastBytes)
        { _started = now; _baseline = progress.BytesCopied; }
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
