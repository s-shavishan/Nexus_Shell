# Nexus Search — Phase 02 / 3.6.0

This phase connects a Nexus desktop search panel to an independently installed Everything client. It also searches existing Nexus desktop controls and open windows. Launchpad remains the app launcher. There is no new standalone app, file index, downloaded engine or third-party DLL inside Nexus.

## What is included

- A centered Search panel using the desktop's existing glass, high-contrast and reduced-motion settings.
- File categories: all results, files, folders, documents, images, audio, video, archives and source code. Commands and Windows categories work independently of Everything.
- **My files** (the Windows profile folder) by default, or an explicit **Indexed drives** scope. The latter includes local drive paths already indexed by Everything. Redirected folders outside the profile need Indexed drives; UNC/network and device paths are excluded in this phase.
- Literal filename words, including Unicode; words must all match. Typed wildcards, regex and engine commands are treated as name text. File contents are not searched.
- Open, Open folder in Nexus Files, Copy path, 50-file paging, refresh and useful missing-engine/index-loading/timeout states. Results are alphabetically sorted by Everything; they are not a new relevance index.
- Menu-bar Search, desktop/Nexus/File menus, the `nexus:search` target and the keyboard routes below.
- Session status reports the last observed engine status/version and whether the global shortcut was registered. It does not run continuous engine probes.

| Where | Shortcut | Action |
| --- | --- | --- |
| Across the Windows session | Ctrl+Alt+Space | Toggle Nexus Search, if no other program owns the combination |
| Nexus desktop focused | Ctrl+Shift+F | Open Search |
| Search | Up / Down | Select a result while typing |
| Search | Enter | Open the selected item or activate the selected window |
| Search | Ctrl+Enter | Open the selected file's folder in Nexus Files |
| Search | Ctrl+Shift+C | Copy the selected file/folder path |
| Search | Ctrl+L | Focus and select the query |
| Search | F5 / Escape | Refresh / close |

Native Windows key combinations keep their existing routing. If another program owns Ctrl+Alt+Space, Nexus leaves it alone; use the menu-bar icon or the desktop shortcut. Launchpad and its Ctrl+K shortcut remain available.

## Everything setup

1. Obtain Everything from the [official voidtools download page](https://www.voidtools.com/downloads/). Phase 02 requires the 1.4.1 query interface or newer, using the default **unnamed instance**. Named instances, including separately named alpha instances, are not selected automatically.
2. Start its normal client in the same Windows session as Nexus. Its indexing service alone does not expose the client IPC endpoint. The client can remain in the background.
3. Wait for its index to load, then search a known filename in Nexus. Choose Indexed drives if the file is outside your Windows profile.
4. If Nexus replaces Explorer at sign-in, verify that Everything's **client** actually starts in that session. This update does not take ownership of Everything installation, service configuration or client startup. You can launch the installed client from Launchpad; Engine setup includes a shortcut to Launchpad.

Nexus does not install, patch, rebrand or redistribute Everything in this release. It uses the documented local IPC interface. Opening results uses the existing Nexus file/shortcut routes and the permissions of the running session. The engine's index can be stale or omit excluded/unindexed folders; it is not proof that a file still exists or can be opened. Lexical profile scoping is a convenience filter, not a filesystem permission boundary; junctions can point elsewhere.

## Stability implementation

- The authenticated desktop asks Nexus Core over its existing bounded pipe. Files and Control Center worker roles cannot invoke search.
- A Core-side adapter communicates with Everything through QUERY2W / WM_COPYDATA. No indexer work runs on the shell UI thread. The native query thread uses a four-second query budget and individual sends capped at 500 ms. Cancellation/timeout then attempts to replace the old Everything query on the same reply window, requesting zero rows, with at most 100 ms for that cleanup send. An unresponsive engine may not acknowledge cancellation.
- One Nexus engine query can be active. Cancellation retains that slot until the actual native operation ends. UI typing is debounced for 140 ms; only the latest pending search can run. Busy responses have two brief, cancelable retries.
- Each query has a private reply window and random reply ID. Late/wrong-ID replies are ignored; results are bounded to 50 items / 512 KiB. Unicode lengths, offsets, terminators, local paths and the selected scope are checked before results reach the UI.
- Only the private reply window accepts lower-integrity WM_COPYDATA, supporting differently elevated clients. A provided sender HWND is checked against the chosen engine PID; a null sender is allowed by the reply contract. This IPC is not authentication against hostile processes in the same Windows session. Received paths are never executed automatically.
- Changing a query clears obsolete actions immediately. Closing the panel cancels active work, clears its query/results and prevents later replies from updating it. Query history is not written to Nexus state or logs. Only engine status/version/time are retained in session memory.
- Arriving file results preserve an already selected local desktop result. Blank-area double-clicks do not open an old selection. Search participates in normal panel dismissal, theme refresh, display repositioning, lock cleanup and shutdown.

## Validation status

Completed in the delivery environment:

- 273 new portable search assertions, including every truncated prefix of a valid multi-result reply, Unicode, scope escape attempts, unsafe paths, malformed offsets/lengths, bounded queries, cancellation, failure recovery, role rules and production Core routing.
- Four local command matching assertions plus the existing desktop and runtime regression suites, including the Phase 01 rapid-navigation/queue and state-writer checks.
- C# compilation against the restored .NET/WinUI/Windows reference assemblies for Runtime, Core, DesktopHost, Shell and both check projects. The Shell API check uses temporary generated-field stubs; it does not run the Windows XAML compiler or render windows.
- Source/resource consistency, PowerShell parsing, template export and synthetic update fixtures. Source archive integrity and each patch route are checked against the committed tree at export.

Included for Windows execution, not claimed as run in the Linux delivery environment:

- `tests/Nexus.Runtime.Checks/WindowsSearchChecks.cs` drives the production adapter against a test-owned native IPC window. It covers query format, replies, version/readiness, malformed data, nonce rejection, null sender, deadline, cancellation and recovery. It does not require or replace Everything.
- `UiResourceProbe` now constructs and populates the production Search panel at both validation sizes along with the other panels. The Windows build/UI helper executes this where an interactive desktop is available; headless skips remain explicit.
- Real Everything acceptance, differing elevation, actual glass/rendering, DPI changes, focus and sustained interaction need Windows. No measured latency or memory improvement is claimed.

## One Windows acceptance pass

Use the complete 3.6.0 Windows build in a separate folder and keep the prior build for rollback. Begin with **Check-Nexus-UI.bat**, then preview via **Launch-Nexus.bat** before using a managed session.

1. **No engine:** exit the Everything client, open Search and type `speaker`. Sound controls should still appear and open. A file query should explain the missing engine; the desktop and dock must keep responding.
2. **Connect:** start the default Everything client. Search an existing file under your profile. Test Sinhala/non-Latin text, spaces, brackets and a dot in a filename. Compare with Everything's own results. Refresh after index loading if needed.
3. **Scope/categories:** compare My files with Indexed drives using a known file outside your profile. Test Documents, Images, Folders and Source code. A search entered as `content:test` must search that literal name text, never scan file contents.
4. **Actions/paging:** open a disposable document; open its containing folder; copy its path. Test Enter, Ctrl+Enter and Ctrl+Shift+C. Use a query with over 50 matches and go Next/Previous. Query history should be empty when reopening the panel.
5. **Rapid interaction:** type/delete/change filters quickly, select a desktop result while file results are pending, close/reopen Search and switch to Control Center. Old results must not reappear or change the chosen action. Double-click blank results space.
6. **Recovery:** exit/restart Everything with Search open and refresh. Test an engine rebuild/loading state. Search should report availability/readiness and recover on refresh without restarting Nexus. A removed file may remain briefly in the index; opening it must report the launch failure rather than crashing the desktop.
7. **Native integration:** use the global shortcut from another app; test a conflicting shortcut owner, lock/unlock and sign-out/sign-in. Check Everything client startup separately in managed mode. Test Nexus/Everything at the intended elevation levels.
8. **Display/accessibility:** resize the VM display, test 100%/150% scaling, high contrast and reduced effects. Check the Search result scroll area, filter/action scrolling, keyboard focus and glass fallback. Confirm dock, menu bar, Files, saved settings and notifications still work.

If a step fails, capture **Check-Nexus-Desktop.bat** output, the exact query/category/scope, Everything version and whether its client was running/elevated. Do not include sensitive filenames unnecessarily.

## Next phase

Explorer++ remains a separate proof of concept, and transfer-engine integration follows its data-integrity gates. Neither is bundled or substituted by this Search release. See [the engine integration plan](ENGINE-INTEGRATION-PLAN.md).

Protocol references: [Everything SDK/IPC](https://www.voidtools.com/support/everything/sdk/ipc/), [QUERY2W/LIST2 specification](https://www.voidtools.com/forum/viewtopic.php?t=5724), [search syntax](https://www.voidtools.com/support/everything/searching/), [database readiness](https://www.voidtools.com/support/everything/sdk/everything_isdbloaded/).
