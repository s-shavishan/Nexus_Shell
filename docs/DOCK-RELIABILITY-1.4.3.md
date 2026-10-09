# Nexus 1.4.3 — dock reliability

This release addresses delayed running-app updates, disappearing/reshuffling dock entries, the floating dock's bottom reservation, global shortcut interference and the native desktop outline. Desktop, dock and menus remain separate windows.

## Findings in 1.4.2

- The coordinator discovered windows on a five-second timer. Requests during enumeration returned without retaining a pending refresh.
- The dock rebuilt its window buttons in EnumWindows z-order. Activating a window changed that order. Only the first 20 of at most 80 discovered windows appeared.
- A running-app click always attempted activation. There was no active-window click-to-minimize behavior or minimized-state indicator.
- Floating mode still reduced the work area by a full-width strip, even though only a centered dock was visible.
- The keyboard hook passed modifier presses through but consumed some releases. Win+R was mapped to Nexus search. This can explain the reported modifier sticking; removing the hook eliminates that asymmetric delivery.
- The desktop HWND did not use the shared native frame cleanup applied to other Nexus windows.

## Implementation

WindowEventObserver receives foreground, minimize, create/destroy, show/hide, title, cloak and completed move/resize notifications through out-of-context SetWinEventHook. Its callback forwards no keyboard input and performs no file, process-image or UI work. It ignores control/cursor events and does not subscribe to per-pixel location changes. The callback is rooted until hooks are released on their installing UI thread.

The coordinator batches notifications into a fixed 50 ms refresh window. It discovers windows on a worker thread, preserves requests received during enumeration, and retains the five-second scan as reconciliation/fallback. A subscription failure is reported and periodic refresh remains available. This is event-driven scheduling, not a guarantee that every application finishes its own minimize operation within 50 ms.

DockWindowSet maintains lifecycle order keyed by HWND and process ID. Title/focus changes update an existing button; newly discovered windows append; removed windows disappear; a reused HWND from another process becomes a new identity. All discovered windows and pinned launchers are reachable through horizontal overflow. External minimized windows remain discoverable because visibility and minimization are different Win32 states. Cloaked windows on other virtual desktops are excluded. Owned dialogs do not create duplicate task entries unless WS_EX_APPWINDOW requests one.

Click an active running-app entry to minimize it; click an inactive/minimized entry to switch or restore. Right-click provides explicit Restore/switch and Minimize actions. Indicators distinguish active, running and minimized/tucked windows. Native commands revalidate HWND/process ownership and use ShowWindowAsync so an unresponsive app cannot block the dock's UI thread. SW_RESTORE preserves native maximized placement. Sections, Files and file pickers use their existing tuck/return behavior and remain in the dock while hidden.

Floating mode overlays the desktop and reserves zero work-area pixels. The native window strip exists only to host the clipped, rounded dock; it is no longer treated as a work-area reservation. Edge mode continues to reserve its actual height. Layout writes are cached and invalidated only for layout/display/DPI/taskbar recreation. Revealing or hiding the dock does not resize the desktop or change the work area.

A small visibility timer reads only the foreground window and cursor. A maximized foreground app on the dock's monitor hides the floating dock. The bottom edge reveals it; a corridor across the floating gap and a 450 ms hold prevent flicker. Start, Quick Settings, overview and dock context menus keep it visible. Fullscreen apps hide it without hover reveal. A maximize on another monitor does not hide this dock. Brief reveal motion follows the existing Windows animation and Nexus performance preferences.

Global Windows/Alt shortcut hooks and Ctrl+Alt+Space registration are removed. Nexus's local app shortcuts remain local. Windows handles Win+R, Win+D, Alt+Tab and other native chords. The desktop protects only its own HWND against app minimization/hiding, reapplies its position when the Explorer desktop becomes foreground, and uses the shared frame cleanup over its full rectangular viewport.

The existing desktop host and durable takeover/recovery logic are preserved. Managed session mode hides Windows taskbars and keeps Explorer's desktop infrastructure behind Nexus; exiting restores saved visibility and work area. No Windows service or Explorer process is stopped. Preview continues to coexist with Windows. In permanent sign-in mode, native shell shortcuts depend on Explorer being present; this release does not add an Explorer bootstrap or replace absent native shell handlers. Windows-native shortcuts can open Windows-native UI, including Start and Explorer.

## Research and boundaries

Microsoft documents taskbar window eligibility and taskbar recreation, event delivery, asynchronous show-state commands and appbar reservations. Stardock documents Start11 v2 auto-hide and ObjectDock taskbar hiding; those pages describe behavior, not source code. Cairo's ManagedShell is a public implementation reference. Nexus does not copy its task-manager takeover or global minimized-metrics changes.

Minimized app availability and the OS minimize animation's destination are separate concerns. RegisterShellHookWindow exposes HSHELL_GETMINRECT, but Microsoft marks the API as not intended for general use and subject to change. No animation redirection or undocumented Explorer injection is added here. A window's OS animation may still target the hidden native taskbar position rather than Nexus's specific icon. This release establishes reliable tracking and commands first; it does not claim every Windows taskbar extension, Jump List, tray icon, badge or animation contract is replaced.

Sources reviewed:

- Microsoft: [The taskbar](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar)
- Microsoft: [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
- Microsoft: [ShowWindowAsync](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindowasync)
- Microsoft: [Application desktop toolbars](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars)
- Microsoft: [RegisterShellHookWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registershellhookwindow)
- Stardock: [Start11 v2 auto-hide and ObjectDock taskbar hiding](https://www.stardock.com/blog/527119/how-to-hide-the-taskbar-in-windows-11)
- Cairo: [ManagedShell TasksService](https://github.com/cairoshell/ManagedShell/blob/master/src/ManagedShell.WindowsTasks/TasksService.cs)

## Windows acceptance — required before release

Build through AppVeyor or the Windows commands in README.md. Source checks cannot prove native interaction or VirtualBox performance. Test Windows 10 in the user's VirtualBox VM and on a regular machine; also test Windows 11 when available.

| Scenario | Required result |
|---|---|
| Start with apps already open, then launch new apps | Each eligible window has one running entry; it appears without waiting for the old five-second refresh |
| Minimize/restore Notepad, browser, Settings and terminal repeatedly | Entry stays available, indicator updates, click restores the correct HWND; no stuck or duplicate buttons |
| Click active and inactive entries | Active window minimizes; inactive window switches; clicks do not activate another process after HWND reuse |
| Minimize a maximized app, then restore | Previous maximized placement returns |
| Open more than 20 windows; change focus and titles | All entries remain reachable, buttons do not reshuffle, changed titles update tooltips |
| Sections, Files and a file picker | Tucked windows remain available; clicking/right-click restoring returns the same window and its state |
| Floating mode, maximize a normal app | No reserved bottom strip; app uses full work area; dock hides |
| Move pointer from bottom edge across gap onto dock | Dock reveals without gap flicker; leave it and it hides after the hold |
| Open Start, Quick Settings or dock context menu | Dock remains available during interaction |
| Fullscreen app; app on a second monitor | Fullscreen hides the dock; maximizing only on another monitor does not hide the primary dock |
| Switch floating/edge mode, compact mode and 100/125/150/200% DPI | Only edge mode reserves height; dock and popups remain inside the active display; no repeated global layout changes |
| Win+R, release both keys, type R/D in a text editor | Windows Run opens; subsequent plain letters remain ordinary input; no stuck Windows modifier |
| Win+D twice; Alt+Tab; Ctrl+Shift+Esc; Win+L | Native behavior is preserved; Nexus root returns above the Windows desktop and below applications |
| Explorer restart during managed session | Native taskbars are re-hidden by the host; work-area intent recovers; dock entries reconcile |
| Exit, UI crash, host failure and recovery BAT | Windows visibility/work area recover using the existing recovery paths |
| Fast/Balanced, continuous dragging for 60 seconds | Compare responsiveness to user-tested 1.4.2; no per-pixel enumeration or work-area writes |

## Validation status

Added .NET policy checks for lifecycle order, title changes, 130-window overflow, handle reuse, removal and auto-hide transitions. Geometry checks now assert zero floating reservation across the existing 1,008 cases. Windows-only own-window checks cover minimize/restore events after GC, maximized placement, stale-identity rejection and observer cleanup.

These .NET/Win32 checks and the WinUI build must run on Windows CI. They are not reported as executed in the Linux editing environment. Available source/XML/resource/syntax checks and exact patch reconstruction are recorded in the delivered verification file. No executable is included in the source archives.
