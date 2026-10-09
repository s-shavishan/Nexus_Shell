# Nexus 1.5.0 — motion and dock previews

This update adds animation as feedback for an operation, plus a useful preview/control surface. It builds on the supplied burgundy/orange/rose design and the independent desktop/dock/menu foundation.

## Motion behavior

| Trigger | Feedback | Duration |
| --- | --- | --- |
| Successful dock pin open request | Small lift and expansion, then settle | 333 ms |
| Window first discovered after initial reconciliation | Compact arrival and settle | 250 ms |
| Background running window becomes active | Small activation lift | 167 ms |
| Window minimizes / Nexus window tucks | Downward cue and brief squash | 250 ms |
| Minimized/tucked window restores | Upward cue and stretch | 250 ms |
| Active/running/minimized state changes | Underline scale and opacity | 167 ms |
| Overview navigation | Card entrance stages, capped at a 66 ms delay | 240 ms movement / 180 ms fade |

Windows motion guidance uses a downward taskbar icon cue for minimize and upward cue for restore. These are original Nexus keyframes following that behavior; they do not redirect native app-window minimize geometry.

All keyframes are finite, cached per controller and run through the compositor. The underline uses a fixed-width layout slot. The icon is nested inside its hover host: hover/press, lifecycle cues and underline motion have separate visual properties. There is no CompositionTarget.Rendering callback, animated width/layout loop, endless bounce, spinner or delayed native hide. Rapid state changes replace a cue from its current presentation value and settle to the current state. No cue replays for identical states, title-only updates or theme repaint. Existing apps at startup get their correct static state.

Fast/Reduced effects, high contrast and Windows' animation preference disable custom motion and reset affected transforms. Switching back does not replay old events. Unloaded/removed elements detach their motion; shutdown disposes cached composition objects.

## Preview behavior

Hover an open app for 280 ms. The independent preview window stays above that icon within the dock's active monitor. Crossing the card-to-icon gap keeps it reachable; leaving the area for at least 240 ms dismisses it (checked on the existing 120 ms visibility timer). The native hover window does not take focus. Press Down on a focused dock item, or choose Window preview in the context menu, to explicitly activate the card for keyboard access. Tab reaches its actions; Escape dismisses it. Other Nexus menus, dock press/scroll/reposition, source disappearance, fullscreen and shutdown dismiss it.

The card has restore/switch, minimize, maximize/restore-size and close actions. Click the image/card to switch. Each command revalidates HWND and process identity. Close reveals/activates the target and posts WM_CLOSE; the application's save/close logic remains responsible. It never terminates the process. An asynchronous maximize from an iconic window is not followed by a conflicting SW_RESTORE.

Balanced/Full try one DWM thumbnail relationship from the app's top-level window to Nexus's top-level preview. Native aspect-fit coordinates come from the actual WinUI viewport and rasterization scale. DWM supplies updates; there is no bitmap capture, preview screenshot timer or image persistence. Hide/close/source loss unregisters it. Fast, high contrast, minimized/hidden/cloaked sources and registration/update failures use a static icon/title/control card. Protected apps may still provide blank contents even when registration succeeds. The existing preview preference remains independent of visual quality.

Hover previews default on and are saved in Quick Settings and Personalize. Turning them off cancels pending hover requests, dismisses the card and restores normal dock tooltips. The context menu still provides all window actions.

## Required Windows acceptance — pending

Run after a successful CI compile/package on Windows 10/11 and VirtualBox. Retain the previous working build. Record OS build, resolution/DPI, VM graphics settings and visual profile; compare the same windows/workload.

| Case | Expected |
| --- | --- |
| Start with active, inactive and already minimized apps | Correct indicators; no fake minimize/restore cues |
| Open a pin / invalid pin | One bounded launch cue after an accepted open request; an invalid target reports failure without a success cue |
| Open, activate, minimize, restore, repeat rapidly | Appropriate small cues; no stuck transforms or repeating bounce; indicators end in current state |
| Title changes / theme refresh / five-second reconciliation | Titles update; state cues do not replay; dock ordering remains stable |
| Hover a dock icon and cross the gap | One card after delay; it stays reachable; no tooltip competing with it |
| Pass rapidly over several icons / press or scroll dock | No stale delayed card after leaving; press/scroll cancels hover request |
| Balanced live preview of browser/Notepad/Files | Image actually appears within viewport with correct aspect ratio and no white outer frame; API success alone is insufficient |
| Minimized app / Fast / high contrast / protected app | Icon/title/actions remain available; no claim of live contents for unavailable sources; protected app blank content remains a documented limit |
| Down key / context Window preview / Tab / Escape | Explicit preview activation; accessible action names; Escape closes; switching restores the intended app |
| Preview restore/minimize/maximize and context equivalents | Correct app, retained maximize placement, maximize-from-minimized not immediately undone |
| Close Notepad with unsaved text | Normal application save prompt; cancel retains its dock entry; no forced termination |
| Source exits / closes while preview/menu open | Card/menu dismisses; no stale HWND action; next app gets its own entry |
| Turn hover previews off and restart | Preference persists; normal tooltips and window actions remain; no delayed popup |
| Fast / Windows animations off / high contrast while moving | Motion stops and transforms settle; no old cue when re-enabled |
| Maximize/fullscreen and bottom-edge reveal | Floating dock keeps zero reserved strip; preview interaction holds an intentionally revealed dock; fullscreen dismisses card |
| Repeated card open/close and app churn | No growing thumbnail count, handler count or unbounded private memory; hidden card holds no thumbnail |
| Drag apps with no preview and with preview open | Compare against previous build; record latency/frame timing rather than infer smoothness from code |
| Exit / host recovery | Full Windows taskbars/desktop restore as before; preview and motion resources close |

Use existing docs/DOCK-RELIABILITY-1.4.3.md and TEST-DESKTOP-MODE.md for overflow, native shortcuts and recovery regressions. Theme/reference acceptance remains in UI-STABILITY-1.4.4.md.

## Sources

- [Microsoft motion guidance](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion)
- [DwmRegisterThumbnail: top-level destination owned by the caller](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail)
- [DWM_THUMBNAIL_PROPERTIES](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ns-dwmapi-dwm_thumbnail_properties)
- [DwmQueryThumbnailSourceSize](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmquerythumbnailsourcesize)
- [DwmUpdateThumbnailProperties](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmupdatethumbnailproperties)
- [Composition keyframe delay](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.composition.keyframeanimation.delaytime?view=windows-app-sdk-1.8)

Native WinUI/host compilation, .NET/Win32 and PowerShell execution, DWM-over-WinUI rendering, accessibility/focus and measured VM performance are pending. Additional-display shell surfaces, full tray/Jump Lists, app-native icon extraction and native window minimize-target redirection remain outside this update.
