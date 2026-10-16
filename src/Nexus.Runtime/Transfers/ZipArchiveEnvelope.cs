using System.Buffers.Binary;

namespace Nexus.Runtime.Transfers;

// Bound the central directory before ZipArchive materializes its entry objects.
// ZIP64 is supported; split disks, encrypted/unsupported methods and extra
// central-directory records are refused. File content is decoded by .NET.
internal static class ZipArchiveEnvelope
{
    private static ushort U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
    private static ulong U64(byte[] data, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset));
    private static InvalidDataException Invalid() => new("This ZIP has an invalid, unsupported or oversized directory. Use the archive's original tool.");
    internal static async Task<int> ValidateAsync(Stream stream, FileTransferFlow flow, CancellationToken cancellation)
    {
        long length = stream.Length;
        if (length < 22 || length > ArchiveJobRules.MaximumBytes) throw Invalid();
        byte[] tail = new byte[(int)Math.Min(length, 65557)];
        stream.Position = length - tail.Length;
        await stream.ReadExactlyAsync(tail, cancellation).ConfigureAwait(false);
        int end = -1;
        for (int i = tail.Length - 22; i >= 0; i--)
            if (U32(tail, i) == 0x06054B50 && i + 22 + U16(tail, i + 20) == tail.Length) { end = i; break; }
        if (end < 0 || U16(tail, end + 4) != 0 || U16(tail, end + 6) != 0 || U16(tail, end + 8) != U16(tail, end + 10)) throw Invalid();
        ulong entries = U16(tail, end + 10), bytes = U32(tail, end + 12), offset = U32(tail, end + 16);
        long boundary = length - tail.Length + end;
        bool zip64Required = entries == ushort.MaxValue || bytes == uint.MaxValue || offset == uint.MaxValue;
        byte[] locator = new byte[20];
        if (boundary >= 20)
        { stream.Position = boundary - 20; await stream.ReadExactlyAsync(locator, cancellation).ConfigureAwait(false); }
        bool zip64Present = U32(locator, 0) == 0x07064B50;
        if (zip64Required && !zip64Present) throw Invalid();
        if (zip64Present)
        {
            if (boundary < 76) throw Invalid();
            if (U32(locator, 4) != 0 || U32(locator, 16) != 1 || U64(locator, 8) > (ulong)(boundary - 20 - 56)) throw Invalid();
            long recordPosition = (long)U64(locator, 8);
            byte[] record = new byte[56]; stream.Position = recordPosition;
            await stream.ReadExactlyAsync(record, cancellation).ConfigureAwait(false);
            if (U32(record, 0) != 0x06064B50 || U64(record, 4) < 44 || U64(record, 4) > ArchiveJobRules.MaximumMetadataBytes || U64(record, 4) != (ulong)(boundary - 20 - recordPosition - 12)
                || U32(record, 16) != 0 || U32(record, 20) != 0 || U64(record, 24) != U64(record, 32)) throw Invalid();
            if (entries != ushort.MaxValue && entries != U64(record, 32) || bytes != uint.MaxValue && bytes != U64(record, 40)
                || offset != uint.MaxValue && offset != U64(record, 48)) throw Invalid();
            entries = U64(record, 32); bytes = U64(record, 40); offset = U64(record, 48); boundary = recordPosition;
        }
        if (entries > FileJobRules.MaximumEntries || bytes > ArchiveJobRules.MaximumMetadataBytes || offset > (ulong)boundary
            || bytes != (ulong)boundary - offset) throw Invalid();
        stream.Position = (long)offset;
        byte[] header = new byte[46];
        for (ulong i = 0; i < entries; i++)
        {
            await flow.WaitAsync(cancellation).ConfigureAwait(false);
            if (boundary - stream.Position < header.Length) throw Invalid();
            await stream.ReadExactlyAsync(header, cancellation).ConfigureAwait(false);
            if (U32(header, 0) != 0x02014B50 || (U16(header, 8) & 0x0041) != 0 || U16(header, 10) is not (0 or 8)
                || U16(header, 34) != 0 || U16(header, 28) == 0) throw Invalid();
            int remainder = U16(header, 28) + U16(header, 30) + U16(header, 32);
            if (remainder > boundary - stream.Position) throw Invalid();
            stream.Position += remainder;
        }
        if (stream.Position != boundary) throw Invalid();
        stream.Position = 0;
        return (int)entries;
    }
}
