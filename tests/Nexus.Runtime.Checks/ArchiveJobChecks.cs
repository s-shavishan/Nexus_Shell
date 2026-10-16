using Nexus.Runtime;
using Nexus.Runtime.Transfers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

internal static class ArchiveJobChecks
{
    private static int _checks;
    private static void Check(bool pass, string message) { _checks++; if (!pass) throw new Exception(message); }
    private static void Reject(Action action)
    { try { action(); } catch (RuntimeFailure) { _checks++; return; } throw new Exception("Expected archive request rejection."); }
    private static async Task RejectAsync(Func<Task> action)
    { try { await action(); } catch (Exception error) when (error is IOException or InvalidDataException or RuntimeFailure or NotSupportedException) { _checks++; return; } throw new Exception("Expected archive failure."); }
    private static async Task Eventually(Func<bool> ready)
    { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)); while (!ready()) await Task.Delay(10, timeout.Token); }
    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "Nexus-archives-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "Source"), destination = Path.Combine(root, "Output");
            Directory.CreateDirectory(Path.Combine(source, "empty")); Directory.CreateDirectory(destination);
            byte[] data = new byte[1100000]; new Random(42).NextBytes(data);
            string large = Path.Combine(source, "large.bin"); await File.WriteAllBytesAsync(large, data);
            await File.WriteAllTextAsync(Path.Combine(source, "資料.txt"), "Nexus archive 🤍");
            await File.WriteAllBytesAsync(Path.Combine(source, "zero.bin"), []);
            DateTime timestamp = new(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc); File.SetLastWriteTimeUtc(large, timestamp);
            FileJobSpec Pack(string name = "Bundle.zip") => new(Guid.NewGuid(), FileJobKind.CreateZip, [source], destination, OutputName: name);
            FileJobSpec Extract(string zip, string name) => new(Guid.NewGuid(), FileJobKind.ExtractZip, [zip], destination, OutputName: name);
            var engine = new FileTransferEngine(); var progress = new List<FileJobProgress>();
            await engine.RunAsync(Pack(), progress.Add, CancellationToken.None);
            string zip = Path.Combine(destination, "Bundle.zip");
            using (var reader = ZipFile.OpenRead(zip))
            {
                Check(reader.Entries.Any(entry => entry.FullName == "Source/empty/"), "ZIP creation must preserve empty source folders.");
                using var input = reader.GetEntry("Source/large.bin")!.Open(); using var bytes = new MemoryStream(); input.CopyTo(bytes);
                Check(bytes.ToArray().SequenceEqual(data) && File.ReadAllBytes(large).SequenceEqual(data), "An independent ZIP reader must recover exact bytes without changing sources.");
                Check(reader.GetEntry("Source/資料.txt") is not null && reader.GetEntry("Source/zero.bin")!.Length == 0, "ZIP creation must retain Unicode names and empty files.");
            }
            Check(progress.Last().Completed == 3 && progress.Last().BytesCopied == data.Length + Encoding.UTF8.GetByteCount("Nexus archive 🤍")
                && progress.Any(item => item.Phase == "Verifying archive") && progress.Last().Current == zip, "Archive progress counts source bytes, commits only whole output and exposes the final path.");
            progress.Clear(); await engine.RunAsync(Extract(zip, "Expanded"), progress.Add, CancellationToken.None);
            string expanded = Path.Combine(destination, "Expanded", "Source");
            Check(File.ReadAllBytes(Path.Combine(expanded, "large.bin")).SequenceEqual(data)
                && await File.ReadAllTextAsync(Path.Combine(expanded, "資料.txt")) == "Nexus archive 🤍"
                && Directory.Exists(Path.Combine(expanded, "empty")), "ZIP extraction must reconstruct real files and empty folders.");
            Check(File.GetLastWriteTimeUtc(Path.Combine(expanded, "large.bin")) == timestamp && progress.Last().Completed == 3,
                "Extraction must preserve ZIP modification timestamps and publish committed counts only at completion.");
            await engine.RunAsync(Pack(), _ => { }, CancellationToken.None);
            await engine.RunAsync(Extract(zip, "Expanded"), _ => { }, CancellationToken.None);
            Check(File.Exists(Path.Combine(destination, "Bundle (2).zip")) && Directory.Exists(Path.Combine(destination, "Expanded (2)")), "Archive and extraction collisions keep both whole results.");
            byte[] originalZip = File.ReadAllBytes(zip);
            await engine.RunAsync(Pack() with { Collision = FileCollision.Skip }, _ => { }, CancellationToken.None);
            await engine.RunAsync(Extract(zip, "Expanded") with { Collision = FileCollision.Skip }, _ => { }, CancellationToken.None);
            Check(File.ReadAllBytes(zip).SequenceEqual(originalZip) && !Directory.Exists(Path.Combine(destination, "Expanded (3)"))
                && !Directory.GetFileSystemEntries(destination, ".nexus-job-*").Any(), "Skip must preserve existing results and leave no temporary output.");

            string other = Path.Combine(root, "Other"); Directory.CreateDirectory(other);
            string first = Path.Combine(source, "same.txt"), second = Path.Combine(other, "same.txt"); File.WriteAllText(first, "First"); File.WriteAllText(second, "Second");
            await engine.RunAsync(Pack("Names.zip") with { Sources = [first, second] }, _ => { }, CancellationToken.None);
            using (var names = ZipFile.OpenRead(Path.Combine(destination, "Names.zip")))
                Check(names.GetEntry("same.txt") is not null && names.GetEntry("same (2).txt") is not null, "Same-named sources from different folders must get separate archive roots.");
            string empty = Path.Combine(root, "empty.zip"); File.WriteAllBytes(empty, Zip());
            await engine.RunAsync(Extract(empty, "Empty"), _ => { }, CancellationToken.None);
            Check(Directory.Exists(Path.Combine(destination, "Empty")) && !Directory.EnumerateFileSystemEntries(Path.Combine(destination, "Empty")).Any(), "An empty ZIP must create one empty result folder.");
            string nested = Path.Combine(root, "nested.zip"); File.WriteAllBytes(nested, Zip(("inner.zip", Zip(("inside.txt", "Opaque"u8.ToArray())))));
            await engine.RunAsync(Extract(nested, "Nested"), _ => { }, CancellationToken.None);
            Check(File.Exists(Path.Combine(destination, "Nested", "inner.zip")) && !File.Exists(Path.Combine(destination, "Nested", "inside.txt")), "Nested archives remain opaque files; extraction is not recursive.");

            var malformed = new[] { "../escape.txt", "/outside.txt", "C:/escape.txt", "\\..\\escape.txt", "file:stream", "CON.txt", "aux", "LPT¹.txt",
                "trailing. ", "trailing.", "a//b.txt", "a/../b.txt", "a/./b.txt", "control\n.txt", "/", "dir//", string.Join('/', Enumerable.Repeat("d", 65)) + "/file", new string('x', 241) };
            int number = 0;
            async Task Refuse(byte[] content)
            {
                string bad = Path.Combine(root, "bad-" + ++number + ".zip"), output = "Refused-" + number;
                File.WriteAllBytes(bad, content);
                await RejectAsync(() => engine.RunAsync(Extract(bad, output), _ => { }, CancellationToken.None));
                Check(!Directory.Exists(Path.Combine(destination, output)) && !Directory.GetFileSystemEntries(destination, ".nexus-job-*").Any(), "An invalid archive must publish nothing and clean its own staging.");
            }
            foreach (string name in malformed) await Refuse(Zip((name, "Untrusted"u8.ToArray())));
            foreach (var entries in new[] {
                new[] { ("Photo.png", "A"u8.ToArray()), ("photo.PNG", "B"u8.ToArray()) },
                new[] { ("Parent/One.txt", "A"u8.ToArray()), ("parent/Two.txt", "B"u8.ToArray()) },
                new[] { ("same", "A"u8.ToArray()), ("same", "B"u8.ToArray()) },
                new[] { ("parent", "A"u8.ToArray()), ("parent/child", "B"u8.ToArray()) },
                new[] { ("parent/child", "A"u8.ToArray()), ("parent", "B"u8.ToArray()) },
                new[] { ("dir/", Array.Empty<byte>()), ("dir/", Array.Empty<byte>()) },
                new[] { ("dir/", "Not empty"u8.ToArray()) } }) await Refuse(Zip(entries));
            foreach (int attributes in new[] { unchecked((int)0xA1FF0000), unchecked((int)0x11FF0000), (int)FileAttributes.ReparsePoint })
            {
                byte[] link = Zip(("link", "Target"u8.ToArray())); int central = Find(link, 0x02014B50); Set32(link, central + 38, unchecked((uint)attributes));
                await Refuse(link);
            }
            await Refuse("not a zip"u8.ToArray());
            byte[] normal = Zip(("safe.txt", "123456789"u8.ToArray()));
            int cd = Find(normal, 0x02014B50), eocd = Find(normal, 0x06054B50);
            foreach (Action<byte[]> corrupt in new Action<byte[]>[] {
                bytes => Set16(bytes, cd + 10, 12), bytes => Set16(bytes, cd + 8, 1),
                bytes => Set16(bytes, eocd + 4, 1), bytes => { Set16(bytes, eocd + 8, 0); Set16(bytes, eocd + 10, 0); },
                bytes => Set32(bytes, cd + 16, 123), bytes => bytes[30 + "safe.txt".Length] ^= 1,
                bytes => Set32(bytes, cd + 24, 1), bytes => Set32(bytes, cd + 24, uint.MaxValue - 1) })
            { byte[] altered = normal.ToArray(); corrupt(altered); await Refuse(altered); }
            byte[] corruptCrc = normal.ToArray(); Set32(corruptCrc, cd + 16, 12);
            string noVerify = Path.Combine(root, "noverify.zip"); File.WriteAllBytes(noVerify, corruptCrc);
            await RejectAsync(() => engine.RunAsync(Extract(noVerify, "NoVerify") with { Verify = false }, _ => { }, CancellationToken.None));
            Check(!Directory.Exists(Path.Combine(destination, "NoVerify")), "Disabling SHA readback cannot disable ZIP CRC integrity checks.");
            await Refuse(Zip(Enumerable.Range(0, 10001).Select(i => ("f" + i, Array.Empty<byte>())).ToArray()));
            await Refuse(Zip(Enumerable.Range(0, 180).Select(i => ("d" + i + "/" + string.Join('/', Enumerable.Repeat("x", 60)) + "/f", Array.Empty<byte>())).ToArray()));
            byte[] zip64 = Zip64(normal); string zip64Path = Path.Combine(root, "zip64.zip"); File.WriteAllBytes(zip64Path, zip64);
            await engine.RunAsync(Extract(zip64Path, "Zip64"), _ => { }, CancellationToken.None);
            Check(File.ReadAllText(Path.Combine(destination, "Zip64", "safe.txt")) == "123456789", "A valid ZIP64 envelope must work without requiring a huge fixture.");
            string optional64 = Path.Combine(root, "optional64.zip"); File.WriteAllBytes(optional64, Zip64(normal, false));
            await engine.RunAsync(Extract(optional64, "Optional64"), _ => { }, CancellationToken.None);
            Check(File.ReadAllText(Path.Combine(destination, "Optional64", "safe.txt")) == "123456789", "Optional ZIP64 records must work when legacy footer fields still fit.");
            byte[] invalid64 = zip64.ToArray(); BinaryPrimitives.WriteUInt64LittleEndian(invalid64.AsSpan(eocd + 56 + 8), ulong.MaxValue); await Refuse(invalid64);
            await Refuse(OversizedEntry(normal, ArchiveJobRules.MaximumBytes + 1));
            byte[] hugeMetadata = new byte[ArchiveJobRules.MaximumMetadataBytes + 23]; int hugeEnd = hugeMetadata.Length - 22;
            Set32(hugeMetadata, hugeEnd, 0x06054B50); Set32(hugeMetadata, hugeEnd + 12, (uint)hugeEnd); await Refuse(hugeMetadata);

            foreach (string output in new[] { "../out.zip", "CON.zip", "NUL", "trailing.", "a/b.zip", "file:stream", "", "bad\n.zip" })
                Reject(() => FileJobRules.Validate(Pack(output)));
            Reject(() => FileJobRules.Validate(Pack("wrong.txt")));
            Reject(() => FileJobRules.Validate(Extract(large, "Extracted")));
            Reject(() => FileJobRules.Validate(Extract(zip, "Extracted") with { Sources = [zip, empty] }));
            Reject(() => FileJobRules.Validate(Pack() with { Destination = source }));
            Reject(() => new FilePasteSession().Prepare(Guid.NewGuid(), new(1, FileJobKind.CreateZip, [large]), destination));
            Reject(() => Nexus.Shell.Interop.FileClipboardService.Write(new IntPtr(1), [large], FileJobKind.CreateZip));
            var legacy = JsonSerializer.Deserialize<FileJobSpec>("{\"Id\":\"" + Guid.NewGuid() + "\",\"Kind\":0,\"Sources\":[\"/source\"],\"Destination\":\"/destination\"}")!;
            Check(legacy.OutputName == "" && (int)FileJobKind.Move == 1 && (int)FileJobState.Paused == 8, "Archive fields and enum additions must preserve old job history defaults.");
            var meter = new FileTransferMeter();
            foreach (string phase in new[] { "Compressing", "Extracting" })
            {
                Check(meter.Sample(new(phase, "", 1000, 5000, 0, 0, 1), 0).BytesPerSecond == 0, "Archive rate sampling starts unknown at each phase.");
                Check(meter.Sample(new(phase, "", 2000, 5000, 0, 0, 1), 1000).BytesPerSecond == 1000, "Archive rates measure monotonic content bytes, not compressed file size.");
            }
            Check(meter.Sample(new("Verifying archive", "", 2000, 5000, 0, 0, 1), 2000).BytesPerSecond == 0, "Archive verification must clear active rate/ETA.");

            foreach (var kind in new[] { FileJobKind.CreateZip, FileJobKind.ExtractZip })
                foreach (string phase in new[] { kind == FileJobKind.CreateZip ? "Compressing" : "Extracting", "Verifying archive", "Committing archive" })
                    foreach (bool cancel in new[] { false, true })
                    {
                        string name = "Flow-" + kind + "-" + phase.Replace(' ', '-') + "-" + cancel + (kind == FileJobKind.CreateZip ? ".zip" : "");
                        var spec = kind == FileJobKind.CreateZip ? Pack(name) with { Sources = [large] } : Extract(zip, name);
                        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int pauses = 0;
                        var flow = new FileTransferFlow(state => { if (state) paused.TrySetResult(); }); using var cancellation = new CancellationTokenSource();
                        var run = engine.RunAsync(spec, item => { if (item.Phase == phase && Interlocked.CompareExchange(ref pauses, 1, 0) == 0) flow.Pause(); }, flow, cancellation.Token);
                        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        Check(!run.IsCompleted && !File.Exists(Path.Combine(destination, name)) && !Directory.Exists(Path.Combine(destination, name)), "Paused archive work must keep its complete output unpublished.");
                        if (cancel)
                        {
                            cancellation.Cancel();
                            try { await run; throw new Exception("Expected archive cancellation."); } catch (OperationCanceledException) { _checks++; }
                            Check(!File.Exists(Path.Combine(destination, name)) && !Directory.Exists(Path.Combine(destination, name)), "Cancelling at any archive phase must publish no result.");
                        }
                        else { flow.Resume(); await run; Check(File.Exists(Path.Combine(destination, name)) || Directory.Exists(Path.Combine(destination, name)), "Resuming an archive must finish the original staged job."); }
                        Check(!Directory.GetFileSystemEntries(destination, ".nexus-job-*").Any() && File.ReadAllBytes(large).SequenceEqual(data), "Archive resume/cancel must clean staging and preserve source data.");
                    }
            await using (var jobs = new FileJobCoordinator(engine, new Journal()))
            {
                jobs.Control(new(jobs.Epoch, Guid.Empty, "pause")); var spec = Pack("Queued.zip");
                jobs.Submit(new(jobs.Epoch, spec)); jobs.Submit(new(jobs.Epoch, JsonSerializer.Deserialize<FileJobSpec>(JsonSerializer.Serialize(spec))!));
                Check(jobs.Snapshot().Jobs.Length == 1, "Archive retry IDs must use the same Core admission gate as transfers.");
                Reject(() => jobs.Submit(new(jobs.Epoch, spec with { OutputName = "Changed.zip" })));
                jobs.Control(new(jobs.Epoch, Guid.Empty, "resume")); await Eventually(() => jobs.Snapshot().Jobs.Single().State == FileJobState.Complete);
                Check(File.Exists(Path.Combine(destination, "Queued.zip")), "The actual Core queue must execute archive jobs.");
            }
            if (OperatingSystem.IsWindows())
            {
                if (new DriveInfo(Path.GetPathRoot(root)!).DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(large + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=4\r\n");
                    await engine.RunAsync(Pack("Marked.zip") with { Sources = [large] }, _ => { }, CancellationToken.None);
                    string marked = Path.Combine(destination, "Marked.zip");
                    Check(File.ReadAllText(marked + ":Zone.Identifier").Contains("ZoneId=4"), "Creating an archive must preserve the most restrictive existing Windows source zone.");
                    await engine.RunAsync(Extract(marked, "Marked"), _ => { }, CancellationToken.None);
                    Check(File.ReadAllText(Path.Combine(destination, "Marked", "large.bin") + ":Zone.Identifier").Contains("ZoneId=4"), "Extracted files must retain the archive's Windows origin zone.");
                }
                else Console.WriteLine("SKIP: native origin-zone checks need an NTFS temporary volume.");
            }
            else Console.WriteLine("SKIP: native origin-zone stream checks require Windows.");
            Console.WriteLine($"PASS: {_checks} archive job assertions; real ZIP/ZIP64 round trips, source preservation, empty/Unicode files, collisions, unsafe/damaged metadata and CRC rejection, pause/resume/cancel phases and Core admission. Windows UI and origin-zone acceptance remain required.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static byte[] Zip(params (string Name, byte[] Bytes)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
            foreach (var (name, data) in entries) { var entry = archive.CreateEntry(name, CompressionLevel.NoCompression); using var output = entry.Open(); output.Write(data); }
        return memory.ToArray();
    }
    private static int Find(byte[] data, uint signature)
    { for (int i = data.Length - 4; i >= 0; i--) if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i)) == signature) return i; throw new Exception("Missing fixture signature."); }
    private static void Set16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void Set32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static byte[] Zip64(byte[] original, bool sentinel = true)
    {
        int end = Find(original, 0x06054B50); byte[] result = new byte[original.Length + 76]; original.AsSpan(0, end).CopyTo(result);
        Set32(result, end, 0x06064B50); BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(end + 4), 44);
        Set16(result, end + 12, 45); Set16(result, end + 14, 45);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(end + 24), 1); BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(end + 32), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(end + 40), BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(end + 12)));
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(end + 48), BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(end + 16)));
        Set32(result, end + 56, 0x07064B50); BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(end + 64), (ulong)end); Set32(result, end + 72, 1);
        original.AsSpan(end).CopyTo(result.AsSpan(end + 76));
        if (sentinel) { Set16(result, end + 76 + 8, ushort.MaxValue); Set16(result, end + 76 + 10, ushort.MaxValue); Set32(result, end + 76 + 12, uint.MaxValue); Set32(result, end + 76 + 16, uint.MaxValue); }
        return result;
    }
    private static byte[] OversizedEntry(byte[] original, long size)
    {
        int central = Find(original, 0x02014B50), end = Find(original, 0x06054B50);
        int extra = central + 46 + BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(central + 28));
        byte[] result = new byte[original.Length + 12]; original.AsSpan(0, extra).CopyTo(result); original.AsSpan(extra).CopyTo(result.AsSpan(extra + 12));
        Set16(result, central + 30, 12); Set16(result, extra, 1); Set16(result, extra + 2, 8); BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(extra + 4), (ulong)size);
        Set32(result, central + 24, uint.MaxValue); Set32(result, end + 12 + 12, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(end + 12)) + 12); return result;
    }
    private sealed class Journal : IFileJobsJournal
    { public string Notice => ""; public FileJobRecord[] Read() => []; public void Write(IReadOnlyList<FileJobRecord> jobs) { } }
}
