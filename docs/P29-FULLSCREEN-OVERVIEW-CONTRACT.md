# P2.9 proposal — Fullscreen window overview and layout staging

**Status: design/acceptance contract only. This feature is NOT implemented in P2.8.**

## Outcome

Replace the current centered *Window board* utility with an immersive,
Apple-style overview. Users drag preview cards to reposition windows in a
visual workspace. Dragging changes only a staged layout until the user chooses
**Apply layout** (or exits with the explicit apply action). **Cancel / Escape**
restores the exact pre-overview state. A successful apply retains a one-step
**Undo**. The shell keeps Explorer alive, does not kill any apps and never
registers global mouse hooks to implement the overview.

## Reuse rather than rebuild

- Keep DesktopWindowCatalog for stable HWND + PID + process-path identity,
  monitor topology and capability filtering.
- Reuse WindowLayouts planning, SavedWindowLayouts matching and WindowThumbnail
  DWM preview infrastructure; redesign presentation, not the backend catalog.
- Factor a transactional placement coordinator from the existing WindowLayouts
  native placement mechanism, with snapshot, preflight, apply, rollback and undo.

## Interaction contract

1. The overview is a dedicated borderless fullscreen Nexus-owned surface over
   the selected monitor; offer an explicit all-monitor selector and monitor
   miniatures. Menu bar/dock are hidden or visually subordinated while open.
2. Show every eligible user app window, including minimized apps. A failed DWM
   thumbnail falls back to its *actual application icon, title and status*.
   Never treat DWM notification/helper windows as user apps.
3. Click a window to focus its staging card. Drag it into flexible empty canvas
   space, a half/quarter/third snapping zone, another monitor, or a replaceable
   tile position; show a translucent, accurate drop preview before release.
4. Multiple window cards can be selected with Ctrl/Shift or a marquee. Keyboard
   users can tab, move a focused card with arrows and cycle snap targets.
5. Apply commits **all** staged position changes, not the currently selected
   card only. A successful commit closes overview and leaves windows arranged.
   Cancel/Escape discards staging without moving app windows.
6. On any error, closed HWND, reused HWND/PID mismatch, invalid monitor,
   denied resize, nonresizable app or changing DPI during the commit, stop
   safely, restore all previously changed eligible windows from captured
   placements, and report which placements could not be restored. Never
   claim atomicity when a native rollback fails.
7. Reopening overview should use current actual window positions, not stale
   coordinates from a previous session. Treat fullscreen/UWP/special surfaces
   according to capability checks rather than forcibly resizing them.

## Engineering phases

### P2.9-A: Placement engine and tests

- Separate layout staging from commit; create immutable snapshots of all
  editable windows and their monitor/work-area mapping.
- Harden native validation: PID+HWND, resize support, valid monitor geometry,
  minimized/maximized restore logic, DPI and bounds.
- Transactional apply, failure rollback, Undo, and durable error telemetry.
- Portable geometry tests for negative monitors, 100–200% scaling, tiny work
  areas, occupied tiles, off-screen windows, closed/reused HWNDs and 12+ apps.

### P2.9-B: Fullscreen presentation

- New fullscreen host; close/escape/keyboard focus and accessible actions.
- Optimized thumbnail lifecycle and fallback; never update DWM thumbnails on
  every pointer-move frame.
- Preview drag, snap target selection, multi-monitor selector and staging.
- High Contrast/reduced motion, keyboard access and concise labels.

### P2.9-C: Acceptance

- 1, 2 and 3 monitors (including negative origins) at mixed DPI.
- At least 12 windows and multiple windows of one process.
- Normal, maximized, minimized, always-on-top and nonresizable windows.
- Test dragging without committing, Apply, Undo, concurrent window close,
  Explorer restart, and forced native placement failure.
- Run AppVeyor first, then Windows 10 19045 and Windows 11 native tests.
- **Release gate:** no drag should move an external app before Apply; failures
  must not silently scatter windows or close applications.

## Related quality gates (P2.8/P3)

- First-login floating work-area reconciliation must be measured with Explorer
  active and taskbar hidden; avoid repeated SPI_SETWORKAREA writes.
- Dock previews must suppress DWM helper entries and retain an icon fallback
  when native thumbnail acquisition fails.
- Control Center should move from oversized category pages to compact,
  capability-aware, task-centered controls, with direct OS settings fallbacks.
- Liquid Glass needs native compositor/opacity/clip profiling at 100/150/200%
  DPI before claiming reference-quality materials.
