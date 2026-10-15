# P2.10 — Nexus File Jobs replaces workspace profiles

The owner requested complete removal of Nexus Workspaces and a usable replacement. Nexus now provides a single copy/move queue with real file operations, progress, cancellation and destination access. It is an independently closable utility alongside Notes and Calculator, integrated with Files and Nexus Controls.

## Removed workspace behavior

- Deleted `Services/DesktopWorkspace.cs`, the workspace profile/launch-plan records, profile defaults and normalization, active-profile and last-page fields, and workspace resume state.
- Retired workspace/profile search categories and recent-command references. No workspace screen or launcher is present.
- Old profile JSON is ignored when settings load and omitted from subsequent writes. The normal atomic settings backup preserves the previous readable file. Notes, tasks, independent saved content, desktop settings and saved window layouts remain.
- Startup/status now refer to saved settings. `WorkspaceState`, `ExploreWorkspace` and `NotesWorkspace` remain internal saved-content helpers used by Notes; they do not expose a Nexus Workspaces app.

## Everyday use

Open **Nexus → File jobs**, find it in Launchpad/Spotlight, or use the shortcut on Controls Home. Add files or folders, choose a destination, select Copy/Move, then **Add to queue**. Sources can be removed individually. The form and queue become vertically stacked at compact widths, with independently scrolling content.

In Nexus Files' normal browsing mode, right-click a selected file/folder and choose **Copy to…** or **Move to…**. The destination uses the existing isolated folder picker. The Files window reports whether Core accepted the job and directs uncertain submissions to File Jobs for review; it never automatically repeats a timed-out request.

The queue shows operation/state, current phase/path, bytes, committed/skipped files and errors. **Open destination** is always available. **Hold next jobs** lets the current job finish and holds later jobs; **Run next jobs** releases that hold. **Pause transfer / Resume transfer** controls the current job independently. **Cancel job** requests cancellation, including while paused. **Clear finished jobs** clears terminal history, not files. **Load into form** fills a terminal job's sources/options for review; it does not execute or resume anything. Adding it again creates a new job.

## Core ownership and file behavior

Core owns one sequential transfer worker, shared by desktop and Files callers. A request is durably admitted before any transfer starts. Stable job IDs prevent an uncertain RPC reply from starting duplicate work; payload changes under the same ID are rejected. An epoch check prevents old commands from changing a restarted Core. Session tombstones survive history clearing/eviction, with a 4,096-admission budget.

| Behavior | Implementation and limits |
| --- | --- |
| Copy a file | Stage beside its destination, flush to disk, optionally verify copied bytes with SHA-256, preserve modification time, then commit without replacing an existing item. Verification defaults on. |
| Copy a folder | Plan bounded entries first, include empty directories, stage the tree, then commit a final folder name. An incomplete tree is never published under that final name. |
| Move a file | Copy and verify first. The Windows backend holds the source against writes/renames and deletes that exact opened file only after the destination commits. A refused deletion leaves both copies and reports failure. |
| Move a folder | Same-volume directory rename. Cross-drive moves fail with the source retained; choose Copy to transfer across drives. The verification option applies to copied contents, not directory renames. |
| Name collision | Keep both adds a numbered name; Skip leaves the existing item. Commit-time collisions are checked again. No overwrite mode. |
| Cancellation | Clean unfinished staging where possible. Already committed items remain; no whole-job rollback. If cleanup fails, report the staging path for manual review. |
| Bounds | 24 retained jobs, 24 selected source roots, 10,000 file/folder entries and 64 folder levels per job. A full queue evicts only terminal history. One transfer worker; copy progress publication at most four times per second. |
| Unavailable items | Linked/reparse paths and ancestors, encrypted/offline entries, device namespaces, alternate streams, drive-root sources and destination-inside-source selections are rejected. Use Windows/provider tools for these items. |

The service uses ordinary user permissions. It is not an installed Windows service. The queue continues while File Jobs is closed or another window is active; exiting Nexus/Core cancels it. File Jobs polls once per second only while active and allows one status read at a time. Queue actions and form submissions are serialized in the view.

Only ordinary file contents and modification timestamps are copied. ACLs, alternate streams, source attributes, provider semantics and full directory metadata are not preserved. Folder rename uses the existing filesystem operation and does not freeze a mutable source tree. Copy speed and estimated copying time are measured within the current copy phase; verification and atomic rename time are additional. Crash-resume, overwrite and transactional rollback remain outside this implementation.

## History and restart recovery

`file-jobs.json` is separate from settings under the existing Nexus data directory. It contains paths, operation/options, status and progress, never file contents. Writes are bounded to 2 MiB and use a flushed temporary file and atomic replace with a backup. Unreadable history can recover its backup; damaged originals are preserved on replacement. A refused admission/planning write cannot start a transfer. A later history failure is disclosed; explicit live cancellation is still honored.

After a Core restart, queued/planning/running history becomes **Interrupted** and is never automatically repeated. Review the destination before loading a job into the form. A hard crash can leave uniquely named `.nexus-job-*.partial` staging; it is not automatically deleted at startup. Current cancellation cleans its own staging or reports a cleanup failure. Accepted jobs whose history was later cleared are deduplicated for that Core epoch; a new epoch requires user review before retrying an uncertain local request.

## Validation

- Portable .NET 8 Roslyn compilation treats Runtime, production Core and portable check warnings as errors. Shell API compilation uses cached WinUI/Windows projections and generated XAML field fixtures; it is not a native XAML build.
- 36 Core groups and 12 portable Runtime groups pass, including 45 File Jobs assertions with real temporary Unicode files, SHA-256 copies, modification time, collisions, empty directories, folder rename, source-deletion refusal, cancellation cleanup, path rejection, sequential queue/backpressure, duplicate payloads/tombstones, journal failure/recovery and component permissions.
- Windows-only tests exercise the real source-handle sharing contract, verified file move and native ABI. They are conditional and were skipped on the Linux validation host. Runtime uses the existing `--no-native-ipc` option here; Windows CI configuration is unchanged.
- Source/XML/resource guards pass. The native UI probe now instantiates the production File Jobs form/queue at wide and compact sizes using an isolated fixture, with polling, transfer actions and pickers disabled. That probe has not run here.
- AppVeyor, native WinUI build/preflight, packaging, visual acceptance and Windows interaction/handle behavior remain pending. No PR merge or main change is performed by this work.

Windows APIs: [CreateFile sharing and flags](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew), [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), and [FILE_DISPOSITION_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_disposition_info).

## Windows acceptance

1. Build/run both portable check projects on Windows without the native-IPC skip option, then run the existing native UI preflight. Verify the new File Jobs stage and existing dock/Controls stages.
2. Launch through the menu, Launchpad, search and Controls; confirm dock participation, close/reopen, minimize/restore, keyboard focus, compact layout, DPI, High Contrast and reduced effects.
3. Copy temporary Unicode files and trees with empty folders; compare hashes, timestamps and existing colliding destinations in Keep both/Skip modes. Test drive roots, nested sources, links, encrypted/offline paths and unavailable locations.
4. Exercise native file moves and same-drive folder moves. Confirm a cross-drive folder move leaves its source and reports failure; use Copy across drives. Test locked/read-only files and denied destination permissions.
5. Cancel during copy and verification, pause with multiple queued jobs, clear terminal history, close/reopen File Jobs and Files, and check cleanup plus committed-item retention.
6. Stop Nexus during a temporary transfer, restart, inspect Interrupted history and destination/staging, and review a job using Load into form. Simulate damaged history and a refused history save in disposable data directories.


## Active transfer control and estimates — 2026-10-15

- **Pause transfer** requests a cooperative pause of the currently active copy/move. The card first shows **Pausing**, then **Paused** after the worker reaches a checkpoint. **Resume transfer** continues the same job, streams and staging; it does not submit another transfer.
- Copying checks pause between 128-KiB buffers. SHA-256 verification now also checks between buffered reads. Directory creation, source-file boundaries, destination commit and source deletion have checkpoints. A same-drive folder rename can pause before its atomic operation, not partway through it.
- Planning, in-flight disk/network I/O and flush/rename calls can finish before pause is acknowledged. A job that already finishes may report Complete. No thread is suspended or killed.
- **Hold next jobs / Run next jobs** remains independent from the current transfer's pause. A paused transfer keeps Core's sole worker slot, so later work cannot overtake it. Closing/reopening File Jobs preserves a live pause.
- Cancel and Core shutdown interrupt a paused checkpoint and clean uncommitted staging where possible. Output committed before cancellation remains; pause/resume never turns cancellation into automatic retry. Interrupted/paused history is marked Interrupted after Core restart and requires review.
- Copy cards show a monotonic average speed for the current copy phase and an estimated copying duration after at least one second of samples. Snapshot reads update that average as time passes, including stalls. Pausing, resuming, counter/clock resets and non-copy phases clear the sample. Verification and directory rename do not fabricate a copying ETA.
- New Pausing/Paused enum values append to the existing wire/history values; optional rate/ETA fields default to unknown for old snapshots. Exact job-ID/epoch checks and existing desktop-only queue-control permission remain. The internal transfer-backend contract now receives a flow object; ship the complete matching Core/Runtime/Shell build.

**Validation:** portable production-flow/coordinator/engine tests use real temporary files. They verify pause while a read is in flight, exact acknowledged progress despite UI throttling, resume without a second source open, independent queue hold, pause during SHA-256 verification, cancellation/cleanup, same-volume folder boundaries, shutdown while paused, no automatic restart replay, pause-generation races and meter/JSON compatibility. The native File Jobs preflight fixture includes the new active control and rate/ETA card, with actions and polling disabled.

**Windows acceptance pending:** AppVeyor native build/preflight/package, Windows source-handle and drive behavior, large-file and network-path pausing, locked/removable disks, real file moves (including pausing after destination commit), control focus, compact widths/DPI/High Contrast and UI responsiveness. The portable file tests do not certify native Windows UI or driver behavior.
