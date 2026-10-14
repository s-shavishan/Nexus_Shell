# P2.8 Windows integration pack

Implementation baseline: P2.7 UI draft, commit `9de8b89f99062b0b4569f0a1dc6cd7cad94e1b47` on `feat/p27-visual-composition`. Before final validation, the additional Files-panel and Launchpad fixes through `6b79da872e6297f5fc9780f4b17cc97a66629932` were incorporated. This pack extends that work in the same reviewable PR. Windows 10 build 19045 is the primary acceptance target; CI remains AppVeyor. No version/package name is changed.

## User-visible behavior

| Entry point | Behavior |
| --- | --- |
| Nexus menu, View menu or desktop menu → Windows tools | Integrated glass panel with filtered groups, system utility actions and Keep awake controls. Also available as `nexus:system`. |
| Spotlight | Searches the same allowlisted tool catalog. Search text never becomes a command line. |
| Keep awake | 15/30/60/120-minute native system power request, optional display request, restart/stop buttons and an active menu-bar indicator. |
| Menu-bar language indicator | Reads the retained active app's keyboard layout; lists loaded layouts and requests a change for that captured app. Opens Windows language settings. |
| Session status | Reports the Windows event bridge, sleep-notification registration, last wake recovery and Keep awake state. |

The 14 tools are screen snip, Task Manager, display projection, classic volume mixer, installed apps, startup apps, storage, default apps, language/keyboard, accessibility, Windows Update, Resource Monitor, System Information and Control Panel. These launch the actual Windows utilities or Settings pages. Missing Windows components produce an actionable Nexus notice. Selecting a tool does not silently change its settings.

## Integration and ownership

### Power and session lifecycle

`WindowsPowerRequest` uses `PowerCreateRequest`, `PowerSetRequest` and `PowerClearRequest`, with a SafeHandle closing the request object even after a partial failure. The full `REASON_CONTEXT` union is represented, including its x64 padding. There is no registry write, power-plan edit, synthetic mouse input, persistent background service or automatic keep-awake at startup.

`KeepAwakeSession` uses monotonic time. Wall-clock changes cannot extend or prematurely finish a timer. Replacing a timer acquires the new request before releasing the previous one; a failed acquisition leaves the previous timer intact. Expiry runs on the existing five-second desktop heartbeat, so normal expiry can lag the deadline by up to one heartbeat. A blocked UI can delay cleanup; process termination releases the OS handle.

Lock/disconnect notifications reuse the clipboard service's existing WTS registration even when clipboard capture is off. No second component registers/unregisters the same HWND for WTS events. Keep awake refuses to start if that registration is unavailable or the session is suspended. Locking from Nexus also releases it immediately. No clipboard capture preference is enabled by this feature.

Suspend releases the request immediately; resume leaves it off. Windows policy, explicit sleep and Modern Standby on battery may override a request. The UI reports an accepted request/timer, not guaranteed execution under every power policy. Usage sampling resets at session boundaries and does not count locked/disconnected/asleep intervals.

### Windows events

The existing desktop subclass observes power, display, device, clock, theme and input-language messages. Explicit `RegisterSuspendResumeNotification` is owned/disposed by `DesktopIntegration`; ordinary desktop broadcasts remain a fallback if registration fails. Payload pointers are not dereferenced.

Display/device work is coalesced into a 250 ms one-shot timer. Repeated notifications do not push its deadline forward. The observer ignores Nexus's own `SPI_SETWORKAREA` notifications to prevent a feedback loop. Device-removal queries pass through; no removal is vetoed. Native message processing continues through `DefSubclassProc`.

Wake refreshes desktop data and service health, resets usage timing, closes temporary panels and refreshes dock/menu/panel geometry. Display changes discard stale compact-Control-Center anchor coordinates and reposition live surfaces. Failed refreshes retain their pending work for the existing five-second heartbeat to retry. This is recovery for the existing desktop surfaces, not a dock-per-monitor implementation.

### Keyboard and Windows actions

Input layout enumeration is bounded to 64 entries. The app/child HWND, PID and thread are rechecked before `WM_INPUTLANGCHANGEREQUEST` is posted. An unresponsive app cannot block Nexus through a synchronous send. The language indicator reads the actual layout rather than optimistically showing the selection. Windows integrity restrictions and apps declining the request remain possible.

The indicator refreshes on app changes, Nexus input notifications and the existing five-second heartbeat. A language change inside another app can therefore take a heartbeat to appear. HKLs are not a complete Text Services Framework profile model: transient/custom IDs display `IME`, duplicate language names retain their layout ID, and Windows' own input switcher/settings remain available. No keyboard hook or undocumented virtual-desktop API is introduced.

System executables come from the Windows system directory using fixed catalog filenames, not PATH lookup or a shell command. Settings use the existing `Windows.System.Launcher` route. Screen snip hides the Nexus panel before requesting `ms-screenclip:`; if Windows declines the URI, the classic Snipping Tool executable is tried. Nexus does not read, retain or upload screenshots.

## Verification performed in this environment

- Production `Nexus.Runtime`, Core checks and Runtime checks compiled with .NET 8 Roslyn/reference assemblies, with warnings treated as errors.
- The full Core check executable passed, including existing dock/foreground/glass/geometry/state tests and new timer ownership, denied replacement, expiry, clock-change, cleanup, catalog, input and ABI checks.
- Combined validation caught an older major-desktop assertion still expecting 24-item Launchpad pages after the separate P2.7 change to 18. Updated its full-page, final-partial-page and stale-page expectations to match the three-row layout; the production pagination behavior is unchanged.
- Runtime checks passed with their existing `--no-native-ipc` option. An initial native-pipe attempt was blocked by this Linux environment's socket permission restriction; no claim is made for OS pipe transport. **AppVeyor still runs the native checks; its configuration was not weakened.**
- Full shell C# API compilation passed against cached WinUI/Windows projections. This check substitutes generated XAML field declarations and reported projection-version/fixture warnings. It does **not** run the WinUI XAML compiler, package the app or launch Windows UI.
- `python scripts/validate-source.py` passed. Its pre-existing attached-menu-bar guard was corrected to follow `TaskbarWindow → DockForegroundGeometryPolicy → MenuBarLayout`, preserving its check after an earlier refactor. The untouched P2.7 baseline reproduced the old failure.
- The native `--verify-ui` probe now constructs the Windows tools panel at both existing test sizes. That probe has **not** run here.

The normal .NET SDK/MSBuild entrypoints failed on unavailable process metadata in this environment. Direct Roslyn compilation was used for the source/API checks above. The optional tree-sitter syntax mode was unavailable; the actual C# compiler supplied type/syntax checking. Native AppVeyor, Windows graphics and integration acceptance are outstanding.

## Windows acceptance

1. Build this PR's exact head in AppVeyor. Require normal Core/Runtime checks, native XAML build, `--verify-ui` and packaging. The new panel must pass construction/layout without activating any system action or power request during the probe.
2. On Windows 10 19045, open Windows tools from each menu and Spotlight. Try Task Manager, screen snip, projection, volume mixer, startup apps and Control Panel with Explorer present and during a supervised Nexus session. Confirm missing tools on a modified Windows installation report failure.
3. Start Keep awake with display-off allowed, then replace it with display-on. Inspect `powercfg /requests`, stop the timer, lock/unlock, disconnect/reconnect and suspend/resume. The Nexus request must disappear and never silently restart. Check expiry and Nexus process exit too. No power plan should change.
4. With two layouts installed, switch between Notepad and a browser, use both Windows input switching and the Nexus menu, then close a target while its menu is open. Observe the actual indicator, delayed updates, rejected/elevated targets and the Windows-settings fallback. Do not assume all IME/TSF profiles have unique language IDs.
5. Sleep/wake, unplug/reconnect an external monitor, change scaling, change the clock/time zone and toggle High Contrast. Check dock/menu placement, open panel bounds, compact Control Center anchor recovery, notification deadlines and clear session-status reporting. Repeat with a negative-origin monitor and full-screen app.
6. Confirm P2.7 compact Spotlight, Launchpad artwork, dock sizing and accessibility styling remain intact. Check the additional menu-bar buttons at narrow widths; input language hides below 750 DIP and the active awake indicator below 650 DIP, with Windows tools still available from the Nexus menu.

## Remaining boundaries

Windows app-notification capture, a Shell_NotifyIcon-compatible tray, independent dock/Launchpad processes, a persistent file-transfer queue and per-monitor docks are not implemented by this pack. Existing session takeover/recovery and native Windows shortcuts are retained. Merge remains the owner's decision after assessing the Windows build/test evidence.

Rollback: revert this pack's commit to return to P2.7. It creates no persistent schema or machine setting to migrate back. Exit Nexus first to release any active power request.

## Documented Windows contracts

- [PowerCreateRequest](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powercreaterequest), [PowerSetRequest and power-policy limits](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powersetrequest), [REASON_CONTEXT](https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-reason_context).
- [Suspend/resume registration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registersuspendresumenotification), [WM_POWERBROADCAST](https://learn.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast).
- [Keyboard layout enumeration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeyboardlayoutlist), [GUI thread information](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo), [input-language requests](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-inputlangchangerequest).
- [Windows Settings URIs](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings), [Windows 10 screen-snipping protocol](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-screen-snipping).
