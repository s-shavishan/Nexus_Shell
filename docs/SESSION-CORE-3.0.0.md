# Nexus 3.0.0 — Session Core foundation and Windows acceptance

This is the first Session Core milestone. Version 3.0.0 continues from the delivered 2.1.1 source.

## Delivered in this build

| System | Behavior |
| --- | --- |
| Floating menu bar | 4 DIP from the top, 8 DIP from each side, 10 DIP corner radius. A constant 38 DIP work-area band prevents application relayout when the visible bar changes shape. |
| Attached bar | In managed desktop mode, maximizing or snapping an app to the work-area edges changes the bar to a full-width straight frame with a 220 ms compositor transition. Returning to normal placement restores the floating frame. True full-screen apps hide the bars. |
| Title controls | Borderless Nexus maximized client bounds are clamped to the monitor work area. In preview, the top bar hides above maximized **and snapped** apps because Explorer still owns that work area. This corrects a path capable of covering the title controls shown missing in the screenshot. |
| Window and panel motion | Nexus window openings animate their frame; close uses a 120 ms fade/scale, minimize uses 160 ms movement and native minimization, and restore/resize uses a 190 ms settle. Launchpad, notifications and Control Center have interruptible closing transitions. Existing external applications keep Windows' native window rendering and animations. |
| Control Center supervision | Core starts a separate `--control-center-worker` on demand, verifies its PID/window, observes its UI heartbeat, and recovers failed workers. Twelve seconds to become ready, ten seconds without a heartbeat, at most three launches per minute. A replacement waits when termination fails. |
| Preference ownership | The panel submits allowlisted preferences/actions, with bounded pending commands and acknowledgment identities. The desktop remains the sole state writer; notes, pins and unrelated concurrent edits are retained. Device mutations still use Core's settings service and are never replayed automatically. |
| Notification history | The last 80 Nexus alerts, read state and dismissals save through the existing atomic Core state writer and survive restart. Clear stays cleared. Windows application toast capture is a later identity/permission milestone. |

Reduced effects, Windows animation settings, high contrast and compositor failure retain immediate native actions and a solid material fallback. The glass is supported Windows acrylic blur/tint. This build does not implement Apple-style optical refraction or replace Windows security/authentication.

## Test on the Windows 10 VM

1. Snapshot the VM. Build the complete source with the README commands, or extract a **compiled** `Nexus-Shell-3.0.0-win-x64.zip` produced by Windows CI. The downloadable Source ZIP is source, not a runnable build. Keep Shell, Core, Runtime, DesktopHost, compiled UI resources and recovery helpers together. Keep the 2.1.1 build in its original folder.
2. Start `Launch-Nexus.bat` in preview. Open Files, maximize, restore, then use Win+Left and Win+Right. Title controls and folder tools must stay reachable. The menu bar hides over maximized/snapped apps in preview. The dock should auto-hide above full-height snapped windows and reveal at the bottom.
3. Close preview and start `Launch-Nexus-Desktop.bat` for the managed Nexus session. This is the mode that owns the work area and provides the attached menu bar. Check the subtle top/side gap on the desktop, square attached edges on full and side maximize, and animated floating restoration. Do not configure sign-in replacement solely to test this UI.
4. Repeat maximize/restore and left/right snap on Files, Sections and an external app. Close with the window control and Alt+F4. Minimize/restore from the dock. Rapidly reverse each action. No stale minimize/close may execute after a newer restore, and titles must never remain beyond the work area. External apps use native Windows motion.
5. Rapidly open/dismiss/reopen Launchpad, notifications and Control Center. Click outside during a closing transition. Hidden panels must stop accepting input immediately; reopening must cancel the older hide. Test Escape and keyboard navigation without visible key tips.
6. In Control Center test volume/mute, app volume, supported monitor brightness, Wi-Fi and power. Test Desktop preferences while Sections is also open. Notes/pins must survive these changes. Unsupported VM hardware must report its capability rather than display a successful change.
7. Test **panel-only** recovery with Control Center open. Identify the worker from its command line, not just the shared executable name:

   ```powershell
   $nexusPanel = Get-CimInstance Win32_Process | Where-Object {
       $_.Name -eq 'Nexus.Shell.exe' -and $_.CommandLine -match '--control-center-worker'
   }
   $nexusPanel | Select-Object ProcessId, CommandLine
   # In this test VM, after checking that this is the panel worker:
   $nexusPanel | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
   ```

   The desktop/menu bar/dock must remain open. A visible panel should recover with a new PID and the latest preferences. Three launches in one minute deliberately pause further recovery for up to one minute. A closed panel waits until requested. Open apps must survive panel and Core shutdown; the cleanup job contains only Nexus workers. Do not terminate the main desktop when testing this boundary.
8. Restart Core separately in the VM, then reopen Control Center. The old panel must exit; device writes pending across the restart must not replay. Refresh before repeating an uncertain hardware change. Core may also recover the existing isolated Files worker; this is the older shared Core boundary, not independent Files-service survival.
9. Generate a Nexus warning, dismiss a different alert, leave one unread, and restart Nexus after the state save. Verify order, badge/read state and dismissals. Clear all and restart again. History has the same two-second save debounce as existing desktop preferences; abrupt power loss during that interval can lose the latest edit.
10. Repeat at 100%, 150% and 200% scale, with a second display to the left/above, monitor removal, high contrast, animations disabled and VM graphics acceleration disabled. Bar/window/panel bounds must remain on screen. This release still has one dock; it does not claim a dock on every monitor.
11. Leave the desktop running for one hour while repeatedly opening panels, switching windows and changing settings. Record idle and active CPU, memory/handle count, visible artifacts and input response. There should be no steady resource growth. These measurements are an acceptance gate, not results already obtained in this environment.
12. Confirm normal Return to Windows, host recovery and existing standalone Win/Win+E/Win+R/Win+L behavior. The earlier foreign sign-in policy needs `Inspect-Nexus-Startup.bat` output before ownership can be reconciled; this build does not overwrite an unidentified custom shell.

Send a screenshot/video of any remaining visual issue and the matching tail of `nexus.log`, `core.log` and `desktop-host.log` in `%LOCALAPPDATA%\WhiteDreams\NexusShell`. Include the failing mode, display scale and app. A still image cannot verify animation timing.

## Remaining Session Core milestones

Independent Launchpad and dock processes; dock per monitor; drag/snap zones and named layout restore; persistent interrupted file transfer jobs; richer microphone/pairing/display controls; Windows toast bridge with package identity/access; full notification-area compatibility; safe device removal, clipboard/capture integrations and measured rendering budgets remain in [the roadmap](NEXT-DESKTOP-PACK.md). They are not represented as completed features of this candidate. They require separate native API, hardware and failure acceptance gates after this recovery boundary.

The relevant native framing contract is Microsoft's [WM_NCCALCSIZE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-nccalcsize) and [NCCALCSIZE_PARAMS](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-nccalcsize_params). No system binary, boot loader, credential provider or Windows service list is patched.
