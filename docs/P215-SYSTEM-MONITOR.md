# P2.15 — System Monitor

Implemented on the open draft feature branch; Windows acceptance remains pending. This utility is part of the native Nexus Shell desktop, independent of the separate Electron Nexus application. Explorer keeps running and Windows continues to own native keyboard shortcuts.

## Everyday behavior

Open **System Monitor** from Launchpad, Spotlight/Search, desktop or Nexus menu → Tools, or Nexus Controls → Home. It uses the existing independently closable, resizable utility window and appears in the Nexus dock/window board. Existing pins are preserved; the built-in Launchpad catalog includes it for existing installations as well as new profiles.

The summary reports observed system CPU, physical memory in use/total and Windows uptime. Processes show executable name, CPU percentage, working set and PID. Sort by CPU, memory, name or PID; use **Ctrl+F** to filter by literal name/PID/path words. All sampled processes are searchable; Previous/Next shows 200 matches per page to bound UI collection work. **My Windows session** filters by both token owner and session ID. Unchecking it allows inspection of other processes, with unavailable counters left unknown rather than shown as zero.

Selecting a process shows its path, working set and private committed memory. **Copy details** explicitly copies just that selection to the clipboard. **Open file location** navigates in Nexus Files; monitoring never launches the selected executable or loads its icons/extensions. CPU is normalized to the machine's logical processor count; shared pages are included in working set, so process memory is not additive physical memory. The first observation and observations after pause/inactivity or counter reset have no process rate yet.

Processes and system summary update every two seconds while the utility is active. **Pause** and window deactivation/minimization stop polling; reactivation/resume resets CPU baselines. Closing releases the view and cancels/discards unfinished reads. No monitoring history, paths or process details are written to the profile or logs by this feature. Existing Windows read access rules remain in force; Nexus does not request debug privilege or elevation for monitoring.

## Process controls

**Close windows** posts a standard Windows close request to accessible visible top-level windows of the selected process. An application can ask to save or decline. A windowless background process is not force-ended by this action.

**End process…** shows a confirmation identifying the executable and PID, warns about unsaved work and defaults to Cancel. It targets one process, without recursively terminating children. After confirmation, the action opens an OS process handle, verifies its original creation time/PID and current token owner/session/critical status, then acts on that same handle. A reused PID, inaccessible identity or refused query cannot silently select another process. Success means either the process handle actually signals exit or Windows accepted a termination request; the latter remains explicitly unconfirmed. No failure or uncertain outcome automatically replays an action.

The surface refuses itself, Nexus processes, Explorer, named Windows desktop/session/security infrastructure, critical/unknown-critical processes and other users/sessions. These are conservative UI safeguards, not a security boundary or a complete replacement for Task Manager. Windows may still deny a permitted action; use the existing Windows Task Manager tool for advanced administration. Actions are disabled after stale/failed observations and while updates are paused; execution always rechecks identity and authority even after a fresh observation.

## Storage

The Storage page reports drive letters and type, capacity and available space for the calling Windows account. The bar is **available space**, not disk activity. Windows quotas can limit reported account capacity; the display does not claim SMART health, physical-disk use, I/O throughput or a recursive folder-size scan.

Local/removable/optical/RAM drives are refreshed every 15 seconds while Storage is active, with an explicit Refresh action. Empty/unready/denied drives show an unavailable state. Native drive calls suppress critical-error/insert-media dialogs on their own worker thread and restore its prior error mode. Network drive capacity is not probed; explicitly choosing Open in Files can still navigate to it under normal Windows permissions.

One process read and one storage read may run at a time across all monitor views, even if a closed view's driver call is still finishing. A blocked drive does not block process reads or queue more driver workers when the window is reopened. Native driver I/O is not forcibly terminated; cancellation takes effect at the next boundary and obsolete results are discarded. Process snapshots/materialization are capped at 8,192 entries and filter input at 160 characters. Unchanged visible rows retain their objects and notify only changed text properties; the bounded-height ListView virtualizes containers.

## Verification

Portable checks cover CPU normalization and unknown/reset/gap semantics; new/reused/exited identities; materialization bounds; literal filters and ordering; unknown capacity/quota arithmetic; protected/other-user/critical targets; creation-time mismatch during confirmation; one action on the same disposable target; query-failure cleanup; cancelled/faulted readers; and repeated reopened views while a fake drive call is blocked. The normal Windows check run additionally reads the current process's real times/path/memory/token/session and read-only drive state.

Run the focused group with:

```powershell
dotnet run --project tests/Nexus.Runtime.Checks -- --system-monitor-checks
```

The following opt-in fixture starts a new windowless test child using the installed dotnet host, refuses a mismatched creation identity, verifies Close windows leaves it running, then ends only that child through the production handle path. It never chooses a pre-existing user application:

```powershell
dotnet run --project tests/Nexus.Runtime.Checks -- --system-monitor-native-checks
```

The native WinUI preflight constructs both production pages at wide/narrow sizes with fixture data, all actions disabled and no OS sampling. Portable/API compilation does not execute those Windows fixtures or establish usable native rendering.

Required Windows 10/11 acceptance: exact-head AppVeyor/MSBuild/XAML/package checks; compare CPU/memory trends with Windows Task Manager under idle and load; first sample/pause/resume/lock/window focus/minimize/close; rapid filter/sort with selection retained by creation identity; close/cancel-save/force-end on disposable apps; PID churn and process exits during confirmation; denied/protected/other-session controls; NTFS quotas, empty/removed USB/optical drives and offline network mappings; slow drive isolation and repeated close/reopen; dock/window-board routing; keyboard, 100–200% DPI, narrow layout, High Contrast and reduced-motion usability. Measure sustained CPU/allocation/handle use on real Windows before claiming performance acceptance.

References: Microsoft [process timing](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes), [process memory](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getprocessmemoryinfo), [critical process query](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-isprocesscritical), [termination semantics](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess), [account/volume space](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getdiskfreespaceexw) and [thread error mode](https://learn.microsoft.com/en-us/windows/win32/api/errhandlingapi/nf-errhandlingapi-setthreaderrormode).
