# P2.8 — Screenshot-driven Nexus Shell usability repairs

## User-observed baseline (Windows 10 VM; Nexus 3.6.0 screenshots, 2026-10-14)

Observed and reported: visible horizontal dock scrollbar, a DWM Notification Window in dock previews,
Calculator duplicated between pinned/running zones, stale menu title after closing a window,
Windows tools interaction/close failure, oversized near-empty Network/Bluetooth panels,
unusable Display brightness slider on unsupported hardware, and missing browse multi-selection.
The user also reported a first-session floating-dock bottom work-area gap, faulty preview rendering,
weak Liquid Glass depth, and the need for an Apple-style fullscreen draggable window overview.

## Implemented in this proposed UI repair branch

- Hidden scroll-bar chrome, retaining mouse-wheel/touch/keyboard scrollable dock overflow
  and real running-window identities.
- Exclude the exact `dwm` / `DWM Notification Window` helper. Do not suppress other DWM-titled
  windows or unrelated application windows.
- Pinned Nexus Calculator, Notes, Files, Controls and File Jobs icons may reuse matching
  running windows. Exact executable basename matching supports simple .exe pins.
  Pinned click toggles/restores; duplicate running artwork is hidden, not its HWND.
  Running pins have small active/running indicators. Complex .lnk shortcuts still
  need a separately verified executable target resolver.
- Closed foreground windows are cleared in the menu bar even when an internal
  tool window receives focus.
- Windows Tools uses explicit Close/Escape instead of auto-dismissing on
  Deactivated, so UI interaction and launching native tools are not disrupted.
- Display panel hides the unsupported brightness slider and offers a working
  native Windows display-settings action via the existing advanced route.
- The full Control Center hides its Bluetooth tile after an authoritative
  classic Bluetooth snapshot reports zero usable radios; compact Bluetooth
  requests can still explain the missing adapter. Network, Power, Display and
  unavailable-Bluetooth panels use shorter geometry, with scrollable content.
- Files Browse enables standard Ctrl/Shift/ Ctrl+A selection for ListView and
  GridView. Multiple selection has an inspector summary; single-item rename
  and targeted right-click actions are disabled when several items are selected.
- Portable dock-policy checks and native WinUI preflight checks cover identity,
  scrollbar behavior and real Browse selection mode.

## Still open — do not claim completed

1. Floating dock bottom work-area gap on first session. Requires actual
   Explorer-managed vs Nexus-managed appbar work-area measurements; avoid
   silently resetting SPI_SETWORKAREA while Explorer owns the taskbar.
2. Native dock thumbnail rendering failure, including minimized-window
   fallback quality; DWM helper filtering is not a fix for preview rendering.
3. Genuine Apple-like fullscreen window overview with drag-to-position and
   atomic commit on exit, plus undo/rollback for any closed/replaced HWND.
4. Rubber-band mouse-drag selection on desktop and in Files. Native Ctrl/Shift
   selection is an intermediate improvement, not a completed rectangle selector.
5. Complete Bluetooth capability discovery (including the menu-bar icon),
   broader Display shortcuts, consistent native Windows notification surfaces,
   tooltip investigations and substantially higher-fidelity Liquid Glass 3.
6. WinUI/AppVeyor compilation, real VM tests, screenshot diff, DPI/High Contrast,
   keyboard focus and native drag behavior.

### Windows acceptance protocol

Boot a fresh Windows 10 session *without changing the Floating Dock option*.
Observe native work area vs monitor bottom, minimize/maximize two apps, open and
close Windows Tools (including Escape), click Calculator pinned before/after it
is running, hover a real running app, open Display on an unsupported VM adapter,
open/close Control Center and Bluetooth, verify Files Ctrl+A/Ctrl/Shift click,
then drag on empty desktop/Files and record the still-unimplemented behavior.
Confirm no visible dock scrollbar with >20 windows and verify overflow still
scrolls. Repeat at 100% and 150% scale and compare with Midnight reference.

**Release gate:** draft PR remains unmerged until native Windows build and
owner acceptance. Source inspection and portable checks are not a native pass.
