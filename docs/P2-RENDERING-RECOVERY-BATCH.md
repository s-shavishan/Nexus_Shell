# Nexus Shell P2 — Rendering Recovery & Idle UI Performance

Branch: `feat/p2-rendering-recovery-batch`  
Base: `8ba177e065c883029c40ab7358e51983b93b477f` (merged P2 plus retained-motion PR #17).  
Status: **DEVELOPMENT — NOT MERGED, NO PR YET**.

## Stage 1 — Recover transient DWM thumbnail failures

**Source observation:** An open `DockPreviewWindow` only retried `WindowThumbnail.TryCreate` when its tuple of valid/minimized/cloaked/maximized/fast/contrast states changed. If an otherwise valid HWND could not register a DWM thumbnail temporarily (e.g., during compositor transition), an unchanged state would retain the static icon for the entire card session.

- `DockThumbnailRetry` permits immediate first acquisition and bounded retries at 250, 500, 1000, 2000 and 4000 ms (max 4 s). Native pointer/visibility polls do **not** recreate the same DWM relationship every 80 ms.
- `DockPreviewWindow.RefreshState` still updates controls only on actual state transitions, but evaluates thumbnail eligibility on later polls even when the state tuple is unchanged.
- Minimize, cloaking, invalid HWND, High Contrast, Reduced Effects, hide and opening another preview reset the retry deadline; a successfully attached thumbnail resets it as well.
- `UiTrace.Surface` opt-in events: `thumbnail-attempt`, `thumbnail-active`, and `thumbnail-unavailable` (own preview HWND only, no window title, file path or target PID).
- Core tests exercise the production retry policy, deadline progression, state reset, clock rollback, and long-overflow protection.

**Caveat:** This is *recovery* from transient failures, not proof that DWM previews work on every Windows version. A static card remains the fallback if DWM registration is unsupported.

## Stage 2 — Avoid global relayout for dock-only changes

**Source observation:** `TaskbarWindow.Position` previously re-ran maximized-app repair, desktop-surface work-area layout and all desktop-panel positioning after every callback, including a repeated `ABN_POSCHANGED` with identical geometry or a change only to floating-dock width.

- Extend `DockPlacementSnapshot` with scale (DPI). Monitor, reservation, work area, float mode or DPI changes require full desktop reconciliation.
- `DockPlacementPolicy.RequiresDesktopRelayout` skips these expensive desktop-wide operations for unchanged placement or **dock-bounds-only** changes, while retaining all native dock positioning, XAML geometry assignment, rounded clipping and visibility updates.
- Add opt-in synchronous `dock-desktop-relayout` timing via the existing ETW `WorkEvent`. This trace measures synchronous managed/native execution, **not** displayed frame time or FPS.
- Core checks validate no-op, width-only, monitor move, reservation/work-area, dock-mode and fractional-DPI cases.

**Caveat:** An unrelated app's geometry must still be repaired on its normal lifecycle/foreground event or bounded fallback. Validate that we do not miss layout recovery when the user changes work area externally.

## Stage 3 — Avoid repeated XAML/compositor writes

- The dock's five-second clock heartbeat no longer writes identical time/date strings to two TextBlocks on every tick. Minute changes, time-format changes and date rollover still update immediately on the next heartbeat.
- `MotionController.SetEnabled` and `DockMotionController.SetEnabled` no longer reset visuals repeatedly when the enabled/disabled state has not changed. A **transition** from enabled to disabled still resets animations, pointer state and indicators.
- No animation duration, window motion endpoint, app state machine, accessibility option or Windows input behavior is changed.

## Validation gates before proposing a major PR

1. Current branch: run `python scripts/validate-source.py`, Core and Runtime checks, native desktop diagnostics and resource probes.
2. Run actual Windows AppVeyor compilation, WinUI XAML generation/publish and packaging **on this branch SHA**. Branch creation and commits are not evidence of a successful build.
3. On native Windows, open preview of an application while its compositor thumbnail is unavailable, then restore DWM availability; verify automatic recovery and no 80 ms native-retry storm. Collect opt-in `Nexus-Shell-UI` traces.
4. Toggle floating/attached dock and compact density, pin 10+ apps, move across negative-origin monitors and 100–200% DPI. The maximized-app work area and Control Center must remain correctly aligned. Verify no visible dock flicker.
5. Exercise high contrast, reduced effects, virtual desktops, minimize/restore, fullscreen and display topology transitions. The preview must retain readable static fallback when live DWM cannot attach.
6. Compare before/after ETW samples (`dock-desktop-relayout`, `dock-refresh`, `dock-enumerate`, `chrome-refresh`, motion-resource events) and independent compositor frame-time sampling before claiming a measured performance improvement.
7. Inspect AppVeyor cancellation/failure individually; never treat `pending` or a previous main build as a pass.

## Integration & rollback

No new preferences, persistent schema, elevated permissions, keyboard hooks, Explorer taskbar takeover or file-system migrations. Keep the branch unmerged until several more improvements and Windows CI are complete. It can be reverted/abandoned independently of `main`.
