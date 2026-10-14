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
- Desktop and Files Browse use native Extended selection (Ctrl-click, Shift-click,
  Ctrl+A) without unwanted toggle-on-every-click behavior. A shared non-hit-testable
  selection marquee draws on empty space in both Files list/grid and desktop,
  with direction-independent intersection checks. Desktop selection is preserved
  when icon preferences rebind; Files has a multi-selection inspector. The marquee
  implementation is a first WinUI pass, not pointer-tested on Windows yet.
- Single-item rename and targeted right-click actions are restricted when
  multiple Files entries are selected.
- Managed desktop verifies its expected Windows work area **once**, 1.4 seconds
  after dock initialization, and re-applies the intended area only when a
  differing effective work area is observed. This is a bounded startup repair,
  not a guarantee against Explorer re-reserving the taskbar.
- Portable dock-policy and marquee geometry checks plus native WinUI preflight
  checks cover identity, scrollbar behavior and Extended Browse selection mode.

## Still open — do not claim completed

1. Floating dock first-session gap is **not yet confirmed fixed**. The one-shot
   retry must be measured against Explorer-managed appbar behavior, physical
   monitor bounds and both managed/preview sessions. Do not add polling writes
   or silently reset SPI_SETWORKAREA while Explorer owns the taskbar.
2. Native dock thumbnail rendering failure, including minimized-window
   fallback quality; DWM helper filtering is not a fix for preview rendering.
3. Genuine Apple-like fullscreen window overview with drag-to-position and
   atomic commit on exit, plus undo/rollback for any closed/replaced HWND.
4. New desktop/Files rubber-band behavior is **source only**. Verify pointer
   capture/loss, reversed drag, empty-space hit testing, virtualized lists,
   auto-scroll edges, 100/150% DPI, selection persistence and context menus.
   File batch actions beyond safe selection are not yet implemented.
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
open/close Control Center and Bluetooth, verify Ctrl+A/Ctrl-click/Shift-click on
Files and Desktop, then drag a selection box from empty desktop and Files list/grid
background in all four directions. Check only intersecting icons become selected,
and verify button clicks/double-clicks remain functional.
Confirm no visible dock scrollbar with >20 windows and verify overflow still
scrolls. Repeat at 100% and 150% scale and compare with Midnight reference.

**Release gate:** draft PR remains unmerged until native Windows build and
owner acceptance. Source inspection and portable checks are not a native pass.
