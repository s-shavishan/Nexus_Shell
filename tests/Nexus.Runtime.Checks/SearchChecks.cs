using System.Buffers.Binary;
using System.Text;
using Nexus.Runtime;
using Nexus.Shell.Services;

internal static class SearchChecks
{
    private static int _checks;
    private static void Check(bool valid, string name) { if (!valid) throw new Exception(name); _checks++; }
    private static void Reject(Action action)
    { try { action(); } catch (Exception error) when (error is InvalidDataException or RuntimeFailure or DecoderFallbackException) { _checks++; return; } throw new Exception("Malformed search data accepted."); }
    internal static byte[] Fixture(string[] paths, int offset = 0, uint? total = null)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output, Encoding.Unicode, leaveOpen: true);
        writer.Write(total ?? (uint)paths.Length); writer.Write((uint)paths.Length); writer.Write((uint)offset); writer.Write(4u); writer.Write(1u);
        int position = 20 + paths.Length * 8;
        foreach (string path in paths) { writer.Write(path.EndsWith('\\') ? 1u : 0u); writer.Write((uint)position); position += 4 + (path.Length + 1) * 2; }
        foreach (string path in paths) { writer.Write((uint)path.Length); writer.Write(Encoding.Unicode.GetBytes(path + '\0')); }
        return output.ToArray();
    }
    private sealed class Engine : IFileSearchBackend
    {
        internal int Calls;
        internal Func<SearchPlan, CancellationToken, Task<SearchReply>> Work = (plan, _) => Task.FromResult(new SearchReply(SearchStatus.Ready, [], Offset: plan.Request.Offset));
        public Task<SearchReply> QueryAsync(SearchPlan plan, CancellationToken cancellation) { Calls++; return Work(plan, cancellation); }
    }
    internal static async Task RunAsync()
    {
        const string profile = @"C:\Users\Shan";
        var plan = SearchPolicy.Create(new("report"), profile);
        foreach (var category in Enum.GetValues<SearchCategory>().Where(c => c <= SearchCategory.Code))
            Check(SearchPolicy.Create(new("test", category), profile).Expression.Contains("nopath:regex:\"test\"", StringComparison.Ordinal), "Each file category must use literal name matching.");
        var literal = SearchPolicy.Create(new("report.pdf | content:secret \"x\" * [a]"), profile);
        Check(literal.Expression.Contains("report\\.pdf", StringComparison.Ordinal) && literal.Expression.Contains("regex:\"\\|\"", StringComparison.Ordinal)
            && literal.Expression.Contains("regex:\"content:secret\"", StringComparison.Ordinal) && literal.Expression.Contains("\\x22x\\x22", StringComparison.Ordinal)
            && literal.Expression.Contains("\\*", StringComparison.Ordinal) && literal.Expression.Contains("\\[a]", StringComparison.Ordinal), "Engine syntax in a name must stay literal.");
        Check(plan.Root == profile + "\\" && plan.Expression.StartsWith("path:regex:\"^C:\\\\Users\\\\Shan\\\\\"", StringComparison.Ordinal), "Profile scope must be anchored with a directory boundary.");
        Check(SearchPolicy.Create(new("name", Scope: SearchScope.IndexedDrives), "").Root == "", "Indexed-drive search works when a profile isn't available.");
        foreach (var request in new[] { new SearchRequest(null!), new SearchRequest(new string('x', 257)), new SearchRequest("x\0y"), new SearchRequest("x\ny"), new SearchRequest("x", (SearchCategory)99), new SearchRequest("x", SearchCategory.Commands), new SearchRequest("x", Scope: (SearchScope)77), new SearchRequest("x", Offset: -50), new SearchRequest("x", Offset: 1), new SearchRequest("x", Offset: 10050) }) Reject(() => SearchPolicy.Create(request, profile));
        Reject(() => SearchPolicy.Create(new("x"), ""));
        foreach (string path in new[] { @"C:\Users\Shan\notes.txt", @"D:\Work\demo.cs", @"C:\Users\Shan\සිංහල.txt", "C:\\" }) Check(SearchPolicy.IsLocalPath(path), "A normal local path was rejected.");
        foreach (string path in new[] { "cmd.exe", "ms-settings:", "https://example.org", @"\\server\share\file", @"\\?\C:\file", "//?/C:/file", @"C:\Users\Shan\..\Other\file", @"C:\a\file:stream", @"C:\a\NUL", @"C:\a\com1.txt", @"C:\a\LPT¹.log", @"C:\a\CONIN$", @"C:\a\file.", @"C:\a\ file ", "C:\\a\\bad\0path" }) Check(!SearchPolicy.IsLocalPath(path), "Unsafe result path accepted: " + path);
        Check(!SearchPolicy.InScope(@"C:\Users\ShanElse\file", plan.Root) && SearchPolicy.InScope(@"c:\users\SHAN\file", plan.Root), "The scope must preserve a case-insensitive directory boundary.");
        Check(SearchPolicy.Parent(@"C:\readme.txt") == "C:\\" && SearchPolicy.Parent(@"C:\Users\Shan\notes.txt") == profile, "Root and nested parent paths.");
        byte[] query = EverythingQueryCodec.Encode(plan, 0xF1234567, 47);
        Check(BinaryPrimitives.ReadUInt32LittleEndian(query) == 0xF1234567 && BinaryPrimitives.ReadUInt32LittleEndian(query.AsSpan(4)) == 47
            && BinaryPrimitives.ReadUInt32LittleEndian(query.AsSpan(16)) == 50 && BinaryPrimitives.ReadUInt32LittleEndian(query.AsSpan(20)) == 4
            && Encoding.Unicode.GetString(query[28..]) == plan.Expression + '\0', "QUERY2W must use 32-bit HWND fields on x64 and a terminated Unicode expression.");
        byte[] cancellationPacket = EverythingQueryCodec.Cancel(0xF1234567);
        Check(BinaryPrimitives.ReadUInt32LittleEndian(cancellationPacket) == 0xF1234567 && BinaryPrimitives.ReadUInt32LittleEndian(cancellationPacket.AsSpan(4)) == 0
            && BinaryPrimitives.ReadUInt32LittleEndian(cancellationPacket.AsSpan(16)) == 0 && Encoding.Unicode.GetString(cancellationPacket[28..]) == "regex:\"^$\"\0", "Cancellation must replace the same receiver's query with zero requested rows.");
        byte[] fixture = Fixture([profile + @"\report.txt", profile + @"\සිංහල😀.txt", profile + @"\Folder\"]);
        var decoded = EverythingQueryCodec.Decode(fixture, plan, "1.4.1");
        Check(decoded.Items.Length == 3 && decoded.Items[1].Name == "සිංහල😀.txt" && decoded.Items[2].IsFolder && decoded.EngineVersion == "1.4.1", "Unicode and folder results must decode without touching the filesystem.");
        for (int length = 0; length < fixture.Length; length++) Reject(() => EverythingQueryCodec.Decode(fixture.AsSpan(0, length), plan, "1.4.1"));
        void Corrupt(int offset, uint value) { var malformed = fixture.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(offset), value); Reject(() => EverythingQueryCodec.Decode(malformed, plan, "")); }
        Corrupt(4, 51); Corrupt(0, 1); Corrupt(8, 50); Corrupt(12, 20); Corrupt(24, 0); Corrupt(24, uint.MaxValue); Corrupt(44, uint.MaxValue);
        var unterminated = fixture.ToArray(); unterminated[^1] = 3; Reject(() => EverythingQueryCodec.Decode(unterminated, plan, ""));
        Reject(() => EverythingQueryCodec.Decode(new byte[EverythingQueryCodec.MaximumReplyBytes + 1], plan, ""));
        Reject(() => EverythingQueryCodec.Decode(Fixture([@"C:\Users\Other\private.txt"]), plan, ""));
        Check(EverythingQueryCodec.Decode(Fixture([], 50, 0), SearchPolicy.Create(new("x", Offset: 50), profile), "").Items.Length == 0, "A changing index may make a later page empty.");
        var engine = new Engine(); var coordinator = new SearchCoordinator(engine, profile);
        Check((await coordinator.QueryAsync(new("  "), default)).Items.Length == 0 && engine.Calls == 0, "An empty query must not enumerate the index.");
        using (var canceled = new CancellationTokenSource())
        { canceled.Cancel(); try { await coordinator.QueryAsync(new("x"), canceled.Token); throw new Exception("Cancellation ignored."); } catch (OperationCanceledException) { _checks++; } }
        var pending = new TaskCompletionSource<SearchReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.Work = (_, _) => pending.Task;
        using (var cancellation = new CancellationTokenSource())
        {
            var first = coordinator.QueryAsync(new("old"), cancellation.Token); cancellation.Cancel();
            var busy = await coordinator.QueryAsync(new("new"), default);
            Check(busy.Status == SearchStatus.Busy && engine.Calls == 1, "Canceled but unfinished engine work must retain its slot.");
            pending.SetResult(new(SearchStatus.Ready, []));
            try { await first; throw new Exception("Canceled result escaped."); } catch (OperationCanceledException) { _checks++; }
        }
        engine.Work = (_, _) => throw new TimeoutException();
        Check((await coordinator.QueryAsync(new("x"), default)).Status == SearchStatus.TimedOut, "Engine timeout must be a status, not a desktop failure.");
        engine.Work = (_, _) => throw new IOException("engine stopped");
        Check((await coordinator.QueryAsync(new("x"), default)).Status == SearchStatus.Failed, "Engine failure must release the slot.");
        foreach (SearchStatus status in new[] { SearchStatus.Unavailable, SearchStatus.Indexing, SearchStatus.Unsupported })
        { engine.Work = (_, _) => Task.FromResult(SearchReply.Empty(status)); Check((await coordinator.QueryAsync(new("x"), default)).Status == status, "Dependency status lost."); }
        engine.Work = (_, _) => Task.FromResult(new SearchReply(SearchStatus.Ready, [new("ms-settings:", "Fake", false)], 1));
        Check((await coordinator.QueryAsync(new("x"), default)).Status == SearchStatus.Failed, "Structured backend results also need validation.");
        engine.Work = (p, _) => Task.FromResult(new SearchReply(SearchStatus.Ready, [], Offset: p.Request.Offset));
        Check((await coordinator.QueryAsync(new("recovered"), default)).Status == SearchStatus.Ready, "Search must recover after engine failure.");
        Check(RuntimeOperations.Allows("desktop", RuntimeOperations.Search) && !RuntimeOperations.Allows("files", RuntimeOperations.Search) && !RuntimeOperations.Allows("controlcenter", RuntimeOperations.Search), "Only the authenticated desktop can ask Core for searches.");
        string temp = Path.Combine(Path.GetTempPath(), "Nexus-search-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var state = new CoreStateRepository(new StateStore(temp)); using var files = new FilesCoordinator(_ => throw new Exception("No worker should be started."), (_, _) => false);
            var router = new CoreRouter(state, files, search: coordinator);
            var response = await router.DispatchAsync(new(RuntimeProtocol.Version, Guid.NewGuid(), "desktop", Environment.ProcessId, RuntimeOperations.Search, RuntimeProtocol.Body(new SearchRequest("query"))), default);
            Check(response.Ok && RuntimeProtocol.Payload<SearchReply>(response.Payload).Status == SearchStatus.Ready, "Production Core routing and serialization.");
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
        Console.WriteLine($"PASS: {_checks} Phase 02 search assertions; scope, literal queries, bounded Unicode IPC parsing, cancellation, engine failure and Core routing.");
    }
}
