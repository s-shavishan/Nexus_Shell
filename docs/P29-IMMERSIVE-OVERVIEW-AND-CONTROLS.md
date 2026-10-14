# P2.9 — Immersive window overview and native control fidelity

> Engineering state: SOURCE IMPLEMENTATION ONLY. No Windows AppVeyor, native XAML or VM acceptance result is claimed for this branch. Do not merge until the tests below pass.

## Starting point

P2.8 / PR #31 was already merged to main when P2.9 work began. P2.8 changed desktop and Files selection, Windows Tools dismissal, dock pin deduplication, tool panel geometry, Bluetooth visibility and one-shot managed work-area verification. Its native validation remains important; source presence does not prove the user-observed symptoms disappeared.

## Implemented in this branch

### Immersive window overview (reuses Window board)

- Switch Windows opens a borderless **monitor-size** immersive Nexus-owned surface, instead of a 1060×740 centered tool dialog.
- Dragging a window preview creates a **staged** native-screen-coordinate position; no real app window moves during the gesture.
- Free positioning, monitor left/right-half snaps, corner-quarter snaps and top-edge fill use the selected monitor work area and DPI.
- The live miniature layout map shows actual and staged window coordinates and highlights the pending placement. User can choose another display in the monitor selector.
- Existing Grid / Columns / Stack, Move to display and saved-layout Restore now **stage** their placements instead of rearranging apps immediately.
- **Done · arrange windows** calls the existing validated WindowLayouts apply/undo backend, closes the overlay and keeps the new positions. **Cancel** and **Escape** discard staging without moving apps; **Undo** first discards staging, otherwise restores the previous committed placement.
- WindowLayouts rollback attempts restoration in reverse native-order and reports partial rollback failures instead of silently declaring success. Native HWND/PID and resize validation remains mandatory; no simultaneous atomic Windows transaction is available.
- Maintains a maximum of 12 staged windows and a maximum of six live DWM thumbnails on the current page; other windows can use accessible icon/title fallbacks. Fullscreen/high-contrast chrome has no rounded frame corners.

### Control Center and native system controls

- Six compact, consistent, three-column category controls replace tall two-tier feature tiles; navigation reflows when Bluetooth hardware is confirmed absent.
- Network Ethernet details expand on request instead of consuming the default panel height. Network Settings and classic adapter control routes are available.
- Display shows hardware brightness only when supported. Display settings, native projection and Night light shortcuts are real fixed Windows destinations.
- Bluetooth and its menu-bar control hide only on **confirmed classic-radio absence**; transient enumeration failure preserves controls. A supported radio retains its existing Core-backed actions and Windows pairing route.
- Sound links to Windows volume mixer; Power links to Task Manager. Existing Core-owned commands and guarded sliders are not replaced with decorative toggles.
- All external OS routes are closed allowlists; user-provided strings cannot become executable commands.

### Dock, previews and visuals

- Async STA IShellLink resolution allows simple local .lnk pins to reuse the corresponding running process icon without blocking the WinUI refresh/hover path.
- Window preview fallback now has a legible app title, icon and visual surface when DWM cannot expose a live thumbnail. Native DWM destination writes are skipped when unchanged.
- Tooltips inherit Nexus's theme colors instead of an isolated default popup scheme.
- Native glass continues to be Windows acrylic when supported. Midnight now has a cached, **opaque gradient fallback** when real compositor blur is unavailable; this does **not** claim to reproduce optical refraction or actual background blur on an unsupported VM.

## Explicitly not certified or still open

- User screenshots need new native tests to verify the strange Ctrl+F-like tooltip no longer appears. The global theme fixes *appearance*, not a proven root cause of every flyout/key-hint anomaly.
- Windows Tools pointer/focus/close behavior from merged P2.8 must be re-tested on real Windows; no screenshot-only evidence proves it fixed.
- Floating-dock gap: P2.8's bounded delayed repair is not a complete Explorer appbar/work-area arbitration design, particularly in managed shell sessions with Explorer alive.
- DWM previews can be unavailable on virtualized/remote/low-driver systems. A graceful fallback is supported, but universal live thumbnails cannot be guaranteed.
- Complex packaged-app shortcut identity, high-DPI pointer coordinates across several *simultaneously visible* monitors, snap collision avoidance, card auto-scroll and full keyboard-dragging parity warrant further iterations.
- Liquid Glass on a GPU without DesktopAcrylicController support is **not** an actual native glass material. Additional compositor profiling, design acceptance and frame pacing work remain.

## AppVeyor / Windows acceptance (required)

1. **AppVeyor:** restore, warnings-as-errors Core/Runtime/WinUI compile, x64 publish, resource PRI/XBF checks, portable Core and Runtime regression checks, real UI preflight and update scripts. Do not merge on a failed or unknown stage.
2. **Win10 19045 and Win11:** Windows Tools accepts real clicks; close button and Escape work. Native Control Center category buttons, native settings destinations, Wi-Fi/Ethernet/Display/Bluetooth reads and writes work on supported hardware.
3. **First session:** cold sign-in with Floating Dock already enabled; without toggling settings, record taskbar/AppBar visibility, `GetMonitorInfo(WORK)`, `SPI_GETWORKAREA`, monitor rectangle and maximized Firefox/Explorer/Notepad bounds. Record Explorer restart and monitor/DPI change.
4. **Overview:** drag an ordinary window to free position, left/right, all four corners and top fill. Before Done, apps remain unchanged. Done applies; Undo restores. Cancel/Escape do not apply. Test one closed/reused HWND during staging and failure rollback.
5. **Monitors/DPI:** 1 monitor at 100/150/200%, dual monitors including negative origins and mixed scaling. Verify map remains aligned with actual committed rectangles and no offscreen windows appear.
6. **Dock:** pinned Nexus Calculator and .exe/.lnk shortcuts, extra windows from same process, 20+ windows, overflow scrolling without visible scrollbar, live thumbnail plus fallback for minimized/not available, no DWM Notification Window artifacts.
7. **Selection:** desktop and Files Ctrl/Shift/Ctrl+A, empty-space drag rectangles in all directions, item double-click/context menus and drag pointer-capture recovery.
8. **Visual:** compare on-screen screenshots to Midnight Glass reference in normal/High Contrast/reduced effects. Check no black resize artifacts or frame spikes; report whether native acrylic controller is Active or Fallback.
9. **Release:** record failures with first AppVeyor error or VM screen recording; retest regressions, then merge via squash only after explicit acceptance.

## Engineering boundaries

Explorer remains running for the Windows notification/calendar ecosystem. Nexus must not kill Explorer or seize native keyboard shortcuts. No hardware setting is faked; unsupported operations are absent or routed to Windows. The native window manager remains Windows/DWM; Nexus is a desktop surface and placement orchestrator.

## Windows 10 VirtualBox early-build feedback — 2026-10-14

The user **confirmed on NT10@VBOX** that Windows Tools now responds, the first-session dock gap no longer occurs, the active title clears after closing windows, the dock's visible horizontal scrollbar is gone, Ctrl/Shift multi-selection works, and drag-selection works. Treat those as user-confirmed for the specific tested early build, not every device and version.

The user **did not accept** Bluetooth capability hiding, visual Network menu improvement, actual Display controls, dock visual quality, or pinned/running icon grouping (partial only). The user also showed four detail crops with heavy dark/light border stacks around traffic-light captions, sidebar panels, Launchpad/dock islands, and the menu bar. Mouse-edge resize of Nexus-owned panels was unavailable.

### Follow-up source changes added AFTER early-build feedback

- Replaced the default bright-blue rubber-band fill with a retained semi-transparent diagonal gradient and softly rounded hairline. A release of an empty-canvas click clears selected entries on Desktop and Files; Ctrl/Shift and drag selection remain intact. The marquee is *glass-like paint*, **not** actual DWM blur.
- Reduced oversized shared surface radius metrics; removed redundant native region +1-pixel overdraw and double-painted XAML frame borders; stopped painting one-pixel hard outlines around every dock island and softened the top glint. High Contrast retains its explicit high-visibility outline.
- Ordinary Nexus tool panels use native OverlappedPresenter resizing plus WindowChrome WM_NCHITTEST edge/corner hit tests with usable minimum sizes. Spotlight and immersive Overview explicitly remain fixed-size; full Control Center becomes resize-capable, while compact menu-bar flyouts remain anchored and nonresizable. Native preflight verifies these distinctions.
- Compact Network/Bluetooth/Display panel default heights are lower, with inner scrolling. Full Control Center default footprint is smaller.
- Added fixed, direct Windows DisplaySwitch projection buttons only when multiple displays are present. Brightness still appears only if Core confirms a supported hardware backend. Resolution, scaling and Night light settings remain native Windows routes (not Nexus-native sliders); no claims of complete Display management.
- Kept the P2.9 existing Bluetooth classic-radio probe that hides its menu-bar and Control Center controls only on confirmed absence, with a diagnostic route for uncertain enumeration.

### Mandatory specific Windows tests for this follow-up

1. Drag a marquee through selected desktop icons, then click **empty desktop**; selections must clear. Repeat in Files list and grid. Compare the selected region to the requested subtle glass look; no heavy native-blue rectangle.
2. Hover every corner and edge of **Windows Tools, desktop panels and expanded Control Center**, drag each to resize (100% and 150% scaling), then test keyboard focus, minimize/restore, scroll clipping and closing. Compact Network/Bluetooth flyouts must not resize.
3. Inspect **traffic-light caption, panel/dock/menu corners** at 100% and 150% with glass on/off, high contrast, reduced effects. No double rim, black/white protruding pixels or loss of corner clipping.
4. On VBox with no Bluetooth radio: neither menu-bar Bluetooth icon nor full Control Center tile should appear; a direct hardware diagnostic may report why. On real supported hardware, scanning/discovery must retain correct Core-backed behavior.
5. On single-monitor VBox: do not claim active projection, resolution or brightness widgets if unsupported. On multi-monitor Windows 10/11, verify PC Screen/Duplicate/Extend/Second Screen actions actually change Windows projection without spawning a decorative settings panel.
6. Network quick panel must fit Ethernet status without a tall empty bottom area; Details expands readable IP/gateway/DNS text and Network adapters opens a native Windows dialog. Verify real Wi-Fi radios separately.
7. Retest pinned .lnk/.exe Calculator and multiple document windows. Do not remove a real second window to hide a duplicated pin.
8. AppVeyor: compile **all** native Windows projects and preflight. None of these source edits is accepted as runtime verified until AppVeyor passes.

### Still open after follow-up

- True OS-level pixel-perfect Liquid Glass/DWM depth is not solved by removing duplicate lines. VM GPU, DesktopAcrylic availability, per-pixel antialiasing of Win10 GDI regions and compositor fallback must be profiled.
- A complete direct resolution/refresh-rate/orientation settings transaction with preflight and timed revert is **not implemented**. Native Windows Settings routes are not a replacement for it.
- App layout transition animations and cross-monitor drag staging, unusual Ctrl+F-style keyboard hint popup root cause, broader file batch operations and universal packaged/UWP pin identity still require separate native validation and further source work.

## Follow-up while P2.9 remains unmerged — native display and Bluetooth reliability

**Branch-only work:** The project owner cannot run their current AppVeyor release workflow until source reaches `main`, but requested that development continue instead of merging an unfinished pull request. Keep PR #32 a draft; no merge should be issued without explicit direction.

### Actual Display controls (not Settings links)

- Core now enumerates the **display driver's own** 32-bpp-or-better available screen modes (resolution + refresh rate) and returns typed `DisplayModeOption` values with each connected adapter. Modes are restricted by enumeration, not user-provided shell commands.
- Expanded and compact Display surfaces expose a resolution/Hz picker **only where alternatives exist**. Display-mode changes go through the authenticated Nexus Core Settings operation, not a new app/service or an arbitrary Settings link.
- Choosing **Preview** tests the requested DEVMODE and applies it temporarily using `ChangeDisplaySettingsExW` (no Registry write). It starts an **18-second automatic revert** timer owned by Core. **Keep** explicitly persists only the selected mode, **Revert** restores the prior mode immediately, and shutdown/disposal attempts restore. If Windows rejects rollback, the pending state remains and a 3-second retry is scheduled; do not falsely claim restoration.
- Native mode changes set **only** width/height/refresh DEVMODE flags; no orientation, monitor position, DPI setting or primary-monitor assignment is requested. The user may still need Windows Settings for scaling, orientation, HDR and night light. Drivers/VMs can report zero usable alternative modes.
- Warning: If the Core process is forcibly terminated, display drivers or the GPU reset unexpectedly, the timeout is not a guarantee of restoration; Windows Recovery/Display settings may be required. A real Windows 10/11 full-screen hardware acceptance run is mandatory before release. Do not test novel display modes on a production machine before the VM passes.
- Microsoft documents `CDS_TEST` for preflight, `CDS_UPDATEREGISTRY` for persisting, and 0 for a temporary dynamic change: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw

### Remaining network/Bluetooth/pinned-app fixes

- Bluetooth shell probe and Core radio enumeration both recognize Windows 10 "no adapter" error codes including `ERROR_FILE_NOT_FOUND` (2), while access denied/driver failures remain "unknown" rather than silently hiding hardware. The absence policy is now located in `Nexus.Runtime` so Core and Shell do not reference each other's executables.
- Network Ethernet card shows a one-click copy of the real IP address and expandable IP/Gateway/DNS details.
- Resolved pinned `.lnk` executable identity now updates running-window visibility via `ApplyDensity()` immediately, not merely the active indicator. Ordinary additional windows of the same process are still accessible.
- Portable typed Settings rules validate `display-preview`, `display-keep` and `display-revert` intents and reject malformed settings and dangerous toggles.

### Additional validation required

- **AppVeyor (once a build can be run):** .NET 8 Runtime/Core project compilation and checks, Windows x64 Shell/WinUI compile, native XAML preflight, packaging.
- **Display:** On a single-display Windows 10 VBox instance, enumerate real supported modes, preview a non-current one, let timeout expire and verify exact mode restored. Repeat Keep (including Windows restart), Revert, switch between disconnected monitors and Core shutdown during an active preview. On a dual-monitor setup verify no monitor moves or primary-display changes.
- **Bluetooth:** Confirm device-absence returns 2/259/1168 on VBox and completely hides menu bar + full Control Center Bluetooth tile; verify an actual Bluetooth radio is still shown and functional.
- **Network and Dock:** Copy a legitimate IP and inspect/expand details. Launch pinned native Calculator and a local .lnk app, verifying one combined indicator and correct running entry hide.
- **Safety:** Do not merge until native mode rollback and UI tests pass; fallback Windows recovery should remain available.
