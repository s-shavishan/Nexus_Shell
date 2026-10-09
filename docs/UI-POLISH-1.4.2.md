# UI polish — Nexus 1.4.2

## Evidence and direction

The user's latest clarification is the basis of this update: a short test of **1.4.1 with Explorer still running** felt much smoother. The earlier answer describing a smoother Explorer-free test was corrected. The user also reports white rectangles during minimization with Explorer absent and unspecified remaining bugs; their exact cause is not established by a screenshot or source inspection.

Keep the 1.4.1 takeover implementation. Improve the actual WinUI/native windows instead of changing the Windows backend. The two generated macOS blueprint boards, the Sketch component board and the orange system/status references were re-inspected. Their useful patterns are rounded window silhouettes, consistent interior spacing, compact controls, quiet separators, grouped actions and stable interaction states. Reference artwork is not copied into the application.

| Requested improvement | Implementation |
|---|---|
| Floating taskbar | Centered, content-sized visible dock with desktop space on every edge; Compact and edge-to-edge options persist |
| No bright outer frames | Shared Nexus-only non-client cleanup and native window regions on Windows 10/11 |
| Rounded windows | 18-DIP app/popup corners; 22-DIP floating dock; maximized/fullscreen windows retain full rectangular geometry |
| Smooth motion | Short opacity/translation/scale entrances inside a fixed opaque frame; existing finite dock hover/press feedback |
| Cleaner minimize behavior | Sections/Files hide into their retained Nexus taskbar entries; normal and system-menu minimize requests use that route |
| Roomier controls | Wider Sections sidebar, consistent popup edges, grouped dock controls and a clock capsule |

## Native layout and performance

The taskbar retains a full-width appbar reservation. Its HWND region and XAML surface expose only the centered dock, so invisible margins do not capture clicks or display a white backdrop. This keeps the preview's Explorer appbar contract and the managed desktop's cached work-area reservation. Changing the number of open windows only changes the visible dock width; it does not rewrite the global work area.

WindowChrome subclasses only Nexus HWNDs. It removes the non-client outline, supplies edge/corner resize hit tests, respects the active monitor's work area during maximization, and caches window-region geometry. Region ownership transfers to Windows only after a successful SetWindowRgn; failed regions are released. Decoration failures are logged once and disable further region retries. Native input and closing are not gated on an animation completion.

Start, Quick Settings, Files and the switcher animate their content inside a fixed colored frame. The taskbar does the same for its inner content. No entrance animates HWND geometry. Fast mode, high contrast and Windows animation preferences suppress motion. Balanced enables finite motion without native glass; Full keeps existing glass behavior where supported.

Program.cs, WindowsDesktopSurfaces.cs, DesktopSessionRecovery.cs and DesktopSessionRecord.cs remain byte-identical to 1.4.1. Explorer is not terminated, suspended, injected into or reparented by this update. No Windows service or persistent sign-in policy mechanism is changed.

## Windows acceptance

Complete a successful CI build before this checklist. Keep the previous working folder. Test with Explorer retained; an Explorer-free session is not the performance baseline for this release.

1. **Dock placement:** try floating/edge-to-edge and Compact on/off. The floating dock must be centered with desktop visible around all edges. Click the empty margin and verify the desktop receives it. Open enough apps to overflow; Start and Quick Settings remain reachable and dock contents scroll.
2. **Work area:** maximize Notepad and Nexus Sections. Neither overlaps the reserved dock space. Open/close app windows repeatedly; monitor desktop-host.log/app.log for unexpected repeated global work-area changes. Compare dragging with 1.4.1 under identical VM settings.
3. **Window shape:** on Windows 10, confirm rounded Sections, Files, Start, Quick Settings and switcher without the bright rectangular outer outline. Resize all app edges/corners, move between available displays and test 100%, 125%, 150% and 200% DPI. Maximized/fullscreen windows fill the intended work/monitor area; restore returns corners.
4. **Tuck and return:** click Sections and Files minimize controls, then their retained taskbar entries. Repeat ten times with maximized and restored windows. There must be no stranded miniature white Nexus window, lost state or duplicate taskbar entry. Check Alt+Space minimize where available, keyboard cycling and the desktop toggle. External app behavior needs its own acceptance; this code does not intercept other processes' minimization.
5. **Motion and focus:** in Balanced/Full with Windows animations enabled, repeatedly open/close Start, Quick Settings, Files and the switcher. Click during entrances. Rapidly dismiss/reopen and switch focus; no white frame flash, blocked click or invisible interactive panel. In Fast/high contrast/Windows reduced motion, entrances remain immediate.
6. **Persistence:** restart Nexus after changing floating/edge-to-edge and Compact. Preferences and existing workspace/settings data survive. Old settings missing FloatingTaskbar migrate to floating; explicit false survives snapshots and JSON round trips.
7. **Recovery:** Return to Windows restores original taskbar visibility/work area. Repeat the existing TEST-DESKTOP-MODE.md crash/host failure, lock/unlock and Explorer-restart checks. Saved sign-in policy permissions retain their previous behavior.

## Validation status

Python source/XML/XAML/resource/publish checks pass. The 194-file 1.4.1 baseline matches the retrieved GitHub tree byte for byte. Patch reconstruction and ZIP/hash checks are performed during packaging.

DesktopUiChecks adds 1,008 geometry cases covering negative monitor origins, DPI, invalid scale/width inputs, overflow and popup placement, plus preference migration/persistence and independent visual profiles. **Their .NET execution is pending**. Native C#/WinUI compilation, PowerShell updater tests and the acceptance checklist above cannot run in the authoring environment. No native performance or complete minimized-rectangle fix is claimed from source checks.
