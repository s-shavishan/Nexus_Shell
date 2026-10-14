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
