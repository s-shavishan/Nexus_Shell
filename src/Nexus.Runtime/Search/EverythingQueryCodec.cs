// නමෝ බුද්ධාය 🤍
using System.Buffers.Binary;
using System.Text;

namespace Nexus.Runtime;

// Everything QUERY2W / LIST2, documented by voidtools. No SDK DLL is loaded.
// https://www.voidtools.com/forum/viewtopic.php?t=5724
public static class EverythingQueryCodec
{
    public const uint QueryMessage = 18, FullPath = 4, NameAscending = 1;
    public const int MaximumReplyBytes = 512 * 1024;
    private static readonly UnicodeEncoding Unicode = new(false, false, true);
    public static byte[] Encode(SearchPlan plan, uint replyWindow, uint replyId)
    {
        byte[] text = Unicode.GetBytes(plan.Expression + '\0');
        byte[] packet = new byte[28 + text.Length];
        uint[] header = [replyWindow, replyId, 0, (uint)plan.Request.Offset, SearchPolicy.PageSize, FullPath, NameAscending];
        for (int i = 0; i < header.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(i * 4), header[i]);
        text.CopyTo(packet, 28); return packet;
    }
    public static byte[] Cancel(uint replyWindow)
    {
        // A new QUERY2 for the same HWND cancels its previous query. Request
        // zero rows with a never-matching filename expression; ignore reply ID 0.
        byte[] packet = Encode(new(new(""), "regex:\"^$\"", ""), replyWindow, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), 0); return packet;
    }
    public static SearchReply Decode(ReadOnlySpan<byte> bytes, SearchPlan plan, string version)
    {
        if (bytes.Length < 20 || bytes.Length > MaximumReplyBytes) throw new InvalidDataException("Invalid search reply size.");
        uint total = Read(bytes, 0), count = Read(bytes, 4), offset = Read(bytes, 8), flags = Read(bytes, 12);
        if (count > SearchPolicy.PageSize || total < count || offset != (uint)plan.Request.Offset || flags != FullPath
            || (count > 0 && (ulong)offset + count > total) || bytes.Length < 20 + count * 8)
            throw new InvalidDataException("Invalid search reply header.");
        int dataStart = 20 + (int)count * 8;
        List<SearchHit> items = [];
        for (int i = 0; i < count; i++)
        {
            uint attributes = Read(bytes, 20 + i * 8), data = Read(bytes, 24 + i * 8);
            if (data < dataStart || data > bytes.Length - 4) throw new InvalidDataException("Invalid search result offset.");
            uint length = Read(bytes, (int)data);
            if (length > SearchPolicy.MaximumPathLength || (ulong)data + 4 + ((ulong)length + 1) * 2 > (ulong)bytes.Length)
                throw new InvalidDataException("Invalid search result length.");
            int start = (int)data + 4, size = (int)length * 2;
            if (bytes[start + size] != 0 || bytes[start + size + 1] != 0) throw new InvalidDataException("Unterminated search result.");
            string path = Unicode.GetString(bytes.Slice(start, size));
            if (!SearchPolicy.InScope(path, plan.Root)) throw new InvalidDataException("Unexpected search result scope.");
            items.Add(new(path, SearchPolicy.Name(path), (attributes & 3) != 0));
        }
        return new(SearchStatus.Ready, items.ToArray(), total, (int)offset, version);
    }
    private static uint Read(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
}
