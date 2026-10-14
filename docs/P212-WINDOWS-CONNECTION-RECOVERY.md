# Windows connection and shell recovery

Nexus uses Explorer for native Windows features while its own host supervises the desktop. This follow-up continues from the P3.0 dock/control reliability branch; it does not change Windows sign-in settings, kill Explorer or replace Windows panels.

## Usable recovery in Nexus Controls

Open **Nexus Controls → Core & recovery → Windows connection**, find **Windows connection** in Spotlight/settings search, or choose it from the clock/bell context menu.

- Observed status distinguishes both shell windows available, partially initialized, absent and start in progress. Finding desktop/taskbar HWNDs is not proof that a notification/calendar flyout rendered or that Explorer is responsive.
- **Check Windows connection** observes again and schedules desktop anchoring/layout recovery. **Start Windows shell** is enabled only for an observed absence with an available session; it never restarts or stops an existing Explorer.
- Calendar, Windows notifications and Quick Settings request the existing version-specific native routes. Date/time and notification settings are explicit fallbacks. Held keys/UIPI rejection still do not cause a panel-toggle replay.
- **Copy Windows diagnostics** and About's diagnostics include version, OS, architecture, desktop mode, shell status, attempts, shortcut routes, monitor and observed/desired work-area rectangles. They omit file paths, clipboard contents and external window titles.
- Windows-panel requests require registered session notifications and an unlocked, connected, awake session. Settings links remain usable when shell panels are unavailable.

## Managed desktop coexistence and recovery

The prior sign-in startup path contradicted its host: DesktopHost started Explorer, then DesktopEnvironment rejected Explorer's presence. The obsolete rejection is removed. Both managed modes still require the existing same-folder/session host validation, retained process handle and matching heartbeat event.

DesktopHost starts the fixed Windows explorer.exe image only if both shell windows are absent. It observes shell windows for up to eight seconds before capturing Windows' original work area. A partially initialized or late shell does not cause another launch or an infinite startup wait.

The existing five-second desktop heartbeat also observes shell identity. If Explorer disappears in a **managed** session, recovery gives Windows five seconds to recover itself, then permits one start at a time, at least ten seconds apart. Three attempts pause automatic recovery until five minutes of a stable shell; an explicit manual attempt remains possible after cooldown. Preview never starts Explorer automatically. Locked/disconnected/sleeping sessions and unknown session-notification availability cannot start it.

Explorer processes are not attached to Nexus's kill-on-close job. No native panel command is replayed after recovery. A start attempt checks for the desktop/taskbar for up to eight seconds after process launch and reports uncertainty rather than assuming success. A forced OS/driver failure, unresponsive existing Explorer or partially available shell still requires native Windows diagnosis; Nexus deliberately does not terminate it.

## Taskbar restart and long-session restoration

TaskbarCreated now queues integration recovery even when no tray icon was requested. It retains the existing preview tray re-registration. Windows documents the [taskbar restart broadcast](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar); existing preview appbar re-registration remains in TaskbarRegistration.

Coalesced recovery hides stale previews, reanchors the Nexus desktop, repositions its bars/panels and schedules the existing delayed, one-shot managed work-area verification after the host can hide the new taskbar. There is no per-frame work-area write or new global input hook. This remains the existing primary-work-area strategy, not complete arbitration with arbitrary third-party appbars or a universal multi-monitor guarantee.

The host's durable visibility lease prunes taskbar identities that no longer match before journaling newly discovered surfaces. Live hidden taskbars retain their original visibility, and journaling still precedes hiding. Without this pruning, repeated Explorer replacements could fill the 256-entry session record and force recovery. Exited cached Explorer process handles are also released. Restore still checks native owner/class identity and independently attempts every saved live surface.

## Validation

- Portable WindowsShellRecoveryChecks exercise changed HWND pairs, steady snapshots, grace/cooldown boundaries, a 1,000-request admission burst, unavailable sessions, partial startup, automatic pause, manual retry and stable-budget reset. No Explorer is launched by these tests.
- DesktopModeChecks exercise 600 simulated Explorer replacements, journal-before-hide behavior, unchanged-journal retention, latest-taskbar restoration and preservation of initially hidden taskbars. Existing refused-journal and failed-restore coverage stays active.
- Existing native Controls preflight constructs all six pages at wide/compact sizes and verifies every search anchor, including the new Windows connection card; fixture actions remain disabled.
- The audit also fixed a C# compilation fault in the incoming dock Open windows menu: MatchingWindows returns IReadOnlyList, so the menu must use Count rather than Length.

Local portable suites, production Core/DesktopHost compilation, Shell C# API checks and source checks are recorded with the published commit in the development ledger. Shell API checks use cached WinUI/Windows projections and substitute generated XAML fields; they are not a native XAML build. Windows UI/hardware behavior and exact-head AppVeyor remain separate release gates.

## Windows 10/11 acceptance

1. Test both supervised session and configured sign-in desktop with Explorer present. Nexus must start successfully, hide only Windows taskbars and retain the native Windows desktop infrastructure.
2. Check actual clock/calendar, bell/notifications and Quick Settings on Windows 10 19045 and Windows 11. Win10 retains Win+Alt+D/Win+A; Win11 retains Win+N/Win+A. Check held keys, locked session, Explorer absent and explicit Settings fallbacks.
3. In a disposable Windows VM, restart Explorer using Windows Task Manager. Verify native shell recreation is observed, Nexus remains anchored, the host hides recreated primary/secondary taskbars and maximized windows retain correct work area without toggling Floating Dock.
4. With a missing shell, verify one start admission, grace/cooldown, automatic attempt pause and explicit retry. With only one shell window present, verify no duplicate launch. Preview must never launch Explorer without the user's action.
5. Lock/disconnect/suspend during recovery; there must be no shortcut injection or repeated process launches while unavailable. Unlock/resume should refresh layout and status without replaying a native panel toggle.
6. Inspect copied diagnostics and actual GetMonitorInfo work/monitor rectangles at 100/150/200% DPI, including mixed/negative-origin monitors. Do not accept a cached desired rectangle as proof of observed geometry.
7. Exit/crash Nexus after repeated Explorer restarts. Verify the latest Windows taskbars restore and live taskbars that were initially hidden remain so. Retest host recovery, native file helpers, dock pins/previews, Overview Done/Undo and Display Keep/Revert from the combined branch.

Publish this as branch work. Merge requires the repository's native build and owner acceptance; this pass does not merge or claim a Windows VM run.
