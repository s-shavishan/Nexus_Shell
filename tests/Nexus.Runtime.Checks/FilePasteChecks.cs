using Nexus.Runtime;
using Nexus.Runtime.Transfers;
using System.Text.Json;

internal static class FilePasteChecks
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool pass, string message) { checks++; if (!pass) throw new InvalidOperationException(message); }
        void Reject(Action action) { try { action(); } catch (RuntimeFailure) { checks++; return; } throw new Exception("Expected paste rejection."); }
        string root = Path.Combine(Path.GetTempPath(), "Nexus-paste-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source"), destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
        string file = Path.Combine(source, "資料 with spaces.txt");
        await File.WriteAllTextAsync(file, "Preserve my files 🤍");
        string cutFolder = Path.Combine(source, "Cut folder"); Directory.CreateDirectory(cutFolder);
        await File.WriteAllTextAsync(Path.Combine(cutFolder, "note.txt"), "Keep this folder note");
        Directory.CreateDirectory(Path.Combine(destination, "Cut folder"));
        try
        {
            await using var jobs = new FileJobCoordinator(new FileTransferEngine(), new Journal());
            jobs.Control(new(jobs.Epoch, Guid.Empty, "pause"));
            var paste = new FilePasteSession(); var clip = new FileClipboardContent(41, FileJobKind.Copy, [file]);
            var first = paste.Prepare(jobs.Epoch, clip, destination);
            var admitted = jobs.Submit(new(jobs.Epoch, first)); // Lose this reply.
            first.Sources[0] = Path.Combine(source, "caller changed.txt");
            clip.Paths[0] = file + ".changed";
            var retry = paste.Prepare(jobs.Epoch, new(41, FileJobKind.Copy, [file]), destination);
            var reconciled = jobs.Submit(new(jobs.Epoch, retry));
            Check(admitted.Jobs.Single().Job.Id == retry.Id && reconciled.Jobs.Length == 1
                && reconciled.Jobs.Single().Job.Sources.Single() == file, "A lost paste reply must retain an immutable ID and selection and admit one transfer.");
            Reject(() => paste.Prepare(jobs.Epoch, new(42, FileJobKind.Copy, [file]), destination));
            Reject(() => paste.Prepare(jobs.Epoch, new(41, FileJobKind.Copy, [file]), root));
            Reject(() => paste.Prepare(Guid.NewGuid(), new(41, FileJobKind.Copy, [file]), destination));
            paste.Accepted(Guid.NewGuid());
            Check(paste.Prepare(jobs.Epoch, new(41, FileJobKind.Copy, [file]), destination).Id == retry.Id, "A stale acknowledgement cannot discard a pending paste.");
            paste.Accepted(retry.Id);
            var repeatedCopy = paste.Prepare(jobs.Epoch, new(41, FileJobKind.Copy, [file]), destination);
            Check(repeatedCopy.Id != retry.Id, "An intentional second Copy/Paste is a separate copy.");
            jobs.Submit(new(jobs.Epoch, repeatedCopy)); paste.Accepted(repeatedCopy.Id);
            jobs.Control(new(jobs.Epoch, Guid.Empty, "resume"));
            await Finished(jobs);
            Check(await File.ReadAllTextAsync(Path.Combine(destination, Path.GetFileName(file))) == "Preserve my files 🤍"
                && await File.ReadAllTextAsync(Path.Combine(destination, "資料 with spaces (2).txt")) == "Preserve my files 🤍"
                && File.Exists(file), "Normal clipboard paste must perform real verified copies and keep both colliding names.");

            var cut = new FileClipboardContent(50, FileJobKind.Move, [cutFolder]);
            var moveSession = new FilePasteSession(); var move = moveSession.Prepare(jobs.Epoch, cut, destination);
            var restarted = new FilePasteSession().Prepare(jobs.Epoch, JsonSerializer.Deserialize<FileClipboardContent>(JsonSerializer.Serialize(cut))!, root);
            Check(restarted.Id == move.Id, "The same cut after a Files restart or destination change must retain its Core-session ID.");
            jobs.Submit(new(jobs.Epoch, move));
            Check(jobs.Submit(new(jobs.Epoch, new FilePasteSession().Prepare(jobs.Epoch, cut, destination))).Jobs.Count(job => job.Job.Id == move.Id) == 1,
                "Reopening Files and retrying the same Cut in the same folder must return one original receipt.");
            Reject(() => jobs.Submit(new(jobs.Epoch, restarted)));
            moveSession.Accepted(move.Id);
            Reject(() => moveSession.Prepare(jobs.Epoch, cut, destination));
            await Finished(jobs);
            Check(!Directory.Exists(cutFolder) && await File.ReadAllTextAsync(Path.Combine(destination, "Cut folder (2)", "note.txt")) == "Keep this folder note"
                && Directory.Exists(Path.Combine(destination, "Cut folder")),
                "A folder Cut must atomically commit to a free destination name without replacing an existing folder.");
            Check(new FilePasteSession().Prepare(Guid.NewGuid(), cut, destination).Id != move.Id,
                "New Core epochs cannot inherit an old receipt identity.");
            Check(new FilePasteSession().Prepare(jobs.Epoch, cut with { Sequence = 51 }, destination).Id != move.Id,
                "An explicit new Cut creates a new transfer identity.");

            var refused = new FilePasteSession(); var refusedSpec = refused.Prepare(jobs.Epoch, new(61, FileJobKind.Copy, [file]), destination);
            refused.Rejected(Guid.NewGuid());
            Check(refused.Prepare(jobs.Epoch, new(61, FileJobKind.Copy, [file]), destination).Id == refusedSpec.Id,
                "An unrelated rejection cannot unlock an uncertain paste.");
            refused.Rejected(refusedSpec.Id);
            Check(refused.Prepare(jobs.Epoch, new(62, FileJobKind.Copy, [file]), root).Id != refusedSpec.Id,
                "A definitive refusal permits a different explicit paste.");
            Reject(() => new FilePasteSession().Prepare(Guid.Empty, cut, destination));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut with { Sequence = 0 }, destination));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut with { Kind = (FileJobKind)17 }, destination));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut with { Paths = [] }, destination));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut with { Paths = Enumerable.Range(0, 25).Select(i => file + i).ToArray() }, destination));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut with { Paths = ["relative.txt"] }, destination));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut with { Paths = [source] }, Path.Combine(source, "child")));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut with { Paths = [source, file] }, destination));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut, source));
            Reject(() => new FilePasteSession().Prepare(jobs.Epoch, cut with { Paths = [file, Path.Combine(destination, "already-here.txt")] }, destination));
            var normalized = new FilePasteSession().Prepare(jobs.Epoch, new(71, FileJobKind.Copy, [file, file]), destination);
            Check(normalized.Sources.Length == 1 && normalized.Verify && normalized.Collision == FileCollision.KeepBoth,
                "Clipboard jobs retain deduplication, verification and non-overwriting collision defaults.");
            Console.WriteLine($"PASS: {checks} file-paste checks; real verified Copy and atomic folder Cut, keep-both collisions, immutable lost-reply reconciliation, restart IDs, cross-destination cut deduplication and input limits. Explorer/native clipboard and file-move handles need Windows acceptance.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static async Task Finished(FileJobCoordinator jobs)
    {
        for (int i = 0; i < 400; i++)
        {
            var entries = jobs.Snapshot().Jobs;
            if (entries.All(job => FileJobRules.Terminal(job.State)))
            {
                if (entries.Any(job => job.State != FileJobState.Complete)) throw new Exception("A real paste failed: " + string.Join("; ", entries.Select(job => job.Detail)));
                return;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("The paste did not finish.");
    }
    private sealed class Journal : IFileJobsJournal
    {
        public string Notice => "";
        public FileJobRecord[] Read() => [];
        public void Write(IReadOnlyList<FileJobRecord> jobs) { }
    }
}
