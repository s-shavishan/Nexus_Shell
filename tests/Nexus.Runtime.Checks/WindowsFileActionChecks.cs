using Nexus.Runtime;
using Nexus.Core.Windows;

internal static class WindowsFileActionChecks
{
    internal static void Run()
    {
        int checks = 0; void Check(bool pass, string message) { checks++; if (!pass) throw new InvalidOperationException(message); }
        void Reject(Action action) { try { action(); } catch (RuntimeFailure) { checks++; return; } throw new InvalidOperationException("Expected a file-action rejection."); }
        string file = Path.Combine(Path.GetTempPath(), "Nexus selected file 資料.txt");
        var processes = new List<Worker>(); WindowsFileAction? launched = null; var clock = new Clock();
        using var service = new WindowsFileActionCoordinator((request, caller) => { launched = request; var worker = new Worker(100 + processes.Count); processes.Add(worker); return worker; }, clock);
        WindowsFileAction New(string kind = "menu") => new(Guid.NewGuid(), kind, file, 123, -1900, 400);
        var first = New(); var accepted = service.Submit(first, 44);
        Check(accepted.State == "Requested" && launched!.OwnerProcessId == 44 && service.ActiveCount == 1, "Admission must preserve the target and stamp the authenticated window owner.");
        Check(service.Submit(first, 44).ProcessId == accepted.ProcessId && processes.Count == 1, "A lost reply retry under the same ID must not repeat a native shell command.");
        Reject(() => service.Submit(first with { Kind = "properties" }, 44)); Reject(() => service.Status(first.Id, 55)); Reject(() => service.Submit(first, 55));
        service.Submit(New("open-with"), 44); service.Submit(New("properties"), 44); Reject(() => service.Submit(New(), 44));
        Check(service.ActiveCount == 3, "Windows dialogs and extension helpers must have bounded concurrency.");
        processes[0].HasExited = true; processes[0].ExitCode = 1; service.Monitor();
        Check(service.Status(first.Id, 44).State == "Failed" && service.IssueId != Guid.Empty && service.Issue.Length > 0 && processes[0].Disposed && service.ActiveCount == 2, "Helper failure must free admission, retain status and reach Core health diagnostics.");
        Check(service.Submit(first, 44).State == "Failed" && processes.Count == 3, "A failed native command must not be automatically replayed.");
        var second = New(); service.Submit(second, 44); processes[^1].HasExited = true; service.Monitor();
        Check(service.Status(second.Id, 44).State == "Finished", "Successful completion or dismissal must retain the original receipt.");
        processes[1].Ready = processes[2].Ready = true;
        var slow = New(); service.Submit(slow, 44); clock.Ticks += TimeSpan.FromSeconds(16).Ticks; service.Monitor();
        Check(service.Status(slow.Id, 44).State == "Failed" && processes[^1].Stopped, "A hung pre-command shell handler must reach its monotonic startup deadline and never hold Core indefinitely.");
        processes[^1].Ready = true; service.Monitor(); Check(processes[^1].Admissions == 0, "Late readiness after timeout must never admit a native command, even if process termination was delayed.");
        processes[1].Ready = true; service.Monitor();
        Check(!processes[1].Stopped, "A native handler already admitted for a dialog/command must not be killed by the startup timer.");
        Reject(() => service.Submit(New("arbitrary-verb"), 44)); Reject(() => service.Submit(New() with { Owner = 0 }, 44)); Reject(() => service.Submit(New() with { Path = "relative.txt" }, 44)); Reject(() => service.Submit(New() with { Path = "\\\\?\\C:\\temp\\device" }, 44));
        processes[1].ThrowOnStop = true; service.Dispose();
        Check(processes.Where(p => !p.HasExited).All(p => p.Disposed && p.Stopped), "One failed shutdown must not strand the other owned helpers.");
        Check(RuntimeOperations.Allows("desktop", RuntimeOperations.WindowsFileAction) && RuntimeOperations.Allows("files", RuntimeOperations.WindowsFileStatus) && !RuntimeOperations.Allows("controlcenter", RuntimeOperations.WindowsFileAction), "Windows file actions must retain component permissions.");
        var layout = WindowsFileWorker.Layouts;
        Check(layout.Invoke == 56 && layout.OpenAs == 24 && layout.WindowClass == 80, "Native shell structs must match the complete x64 Windows ABI.");
        var recycle = New("recycle") with { AdditionalPaths = [file + ".second"] };
        var batchWorkers = new List<Worker>(); WindowsFileAction? batchRequest = null;
        using var batches = new WindowsFileActionCoordinator((request, _) => { batchRequest = request; var worker = new Worker(300 + batchWorkers.Count); batchWorkers.Add(worker); return worker; });
        batches.Submit(recycle, 44);
        recycle.AdditionalPaths[0] = file + ".mutated";
        Check(batchRequest!.AdditionalPaths!.Single() == file + ".second", "A recycle batch must detach its targets from the caller's mutable array.");
        var retryRecycle = recycle with { AdditionalPaths = [file + ".second"] };
        Check(batches.Submit(retryRecycle, 44).ProcessId == batchWorkers[0].Id && batchWorkers.Count == 1,
            "A JSON-deserialized recycle retry must not repeat the native batch.");
        Reject(() => batches.Submit(recycle, 44));
        Reject(() => batches.Submit(retryRecycle, 55));
        Reject(() => batches.Submit(retryRecycle with { Id = Guid.NewGuid() }, 44));
        Reject(() => batches.Submit(New("recycle") with { Path = Path.GetDirectoryName(file)! }, 44));
        Reject(() => WindowsFileActionCoordinator.Validate(New("menu") with { AdditionalPaths = [file + ".second"] }));
        Reject(() => WindowsFileActionCoordinator.Validate(New("recycle") with { AdditionalPaths = [file] }));
        Reject(() => WindowsFileActionCoordinator.Validate(New("recycle") with { Path = Path.GetPathRoot(file)! }));
        Reject(() => WindowsFileActionCoordinator.Validate(New("recycle") with { Path = Path.GetDirectoryName(file)!, AdditionalPaths = [file] }));
        Reject(() => WindowsFileActionCoordinator.Validate(New("recycle") with { AdditionalPaths = Enumerable.Range(0, 24).Select(i => file + i).ToArray() }));
        Reject(() => WindowsFileActionCoordinator.Validate(New("recycle") with { AdditionalPaths = ["relative"] }));
        Check(WindowsFileActionCoordinator.Validate(New("recycle") with { AdditionalPaths = Enumerable.Range(0, 23).Select(i => file + i).ToArray() }).AdditionalPaths!.Length == 23,
            "A full 24-item recycle selection must remain supported.");
        batchWorkers[0].HasExited = true; batchWorkers[0].ExitCode = 1; batches.Monitor();
        Check(batches.Submit(retryRecycle, 44).State == "Failed" && batchWorkers.Count == 1,
            "Cancelled or partially failed recycle actions must retain their receipt and never replay.");
        batches.Submit(retryRecycle with { Id = Guid.NewGuid() }, 44);
        Check(batchWorkers.Count == 2, "A later explicit recycle can be admitted after the prior helper exits.");
        Console.WriteLine($"PASS: {checks} Windows file-handler supervision checks; immutable target/owner, idempotency, concurrency, failures, no replay, scope, shutdown and x64 layouts. Native COM dialogs require Windows acceptance.");
    }
    private sealed class Worker(int id) : IWindowsFileProcess
    {
        public int Id => id; public bool HasExited { get; set; } public int ExitCode { get; set; } public bool Ready { get; set; }
        internal bool Disposed, Stopped, ThrowOnStop; internal int Admissions;
        public void Admit() => Admissions++;
        public void Stop() { Stopped = true; if (ThrowOnStop) throw new IOException("Injected shutdown refusal"); }
        public void Dispose() => Disposed = true;
    }
    private sealed class Clock : TimeProvider
    { internal long Ticks; public override long TimestampFrequency => TimeSpan.TicksPerSecond; public override long GetTimestamp() => Ticks; }
}
