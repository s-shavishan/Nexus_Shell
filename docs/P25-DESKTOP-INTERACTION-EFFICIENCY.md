# Nexus Shell P2.5 — Desktop Interaction Efficiency & Evidence

**Development branch:** `feat/p25-desktop-interaction-efficiency`  
**Base:** `7f90acf737f39637f70a973d2a820474fff3b968` (merged native dock opacity PR #26).  
**Status:** Source implementation and regression assertions are committed. **Native build, AppVeyor, Windows preview, and performance acceptance have NOT been executed on this branch.** Keep this in one draft feature PR until the owner elects to validate/merge.

## Stage 1: Native move/resize completion uses the geometry fast path

**Problem:** The P2.4 event pipeline already avoided full `EnumWindows` enumeration for `EVENT_SYSTEM_FOREGROUND`, but still treated `EVENT_SYSTEM_MOVESIZEEND` as a task-list mutation. Ending a normal drag or sizing gesture therefore started a costly native enumeration even if no application was opened, closed, renamed, minimized or restored.

- `WindowEventScanPolicy.RequiresEnumeration` treats `0x000B` (move/resize finished) and `0x0003` (foreground) as **non-structural**, while keeping a full scan for minimize, create, destroy, show, hide, name changes, cloaking and unknown event types.
- These events **still deliver a WinUI callback**, so `TaskbarWindow.RefreshFromNativeEvent` updates foreground indicators, fullscreen/attached geometry and dock visibility; the change only suppresses the unnecessary full task-list scan.
- A create/destroy/title event during the same coalesced burst upgrades the batch to a structural scan. Unknown/eligible foreground discovery still requests a scan. The five-second recovery heartbeat remains available if Windows notifications are missed.
- This optimization does **not** change shell integration, work-area registration, native window ownership, window placement APIs, shortcut hooks, persisted state, screen scaling or animation durations.
- Portable regression assertions cover geometry-only deliveries, a 700-event burst, foreground plus geometry, and a destroy event upgrading the pending batch.

**Boundary:** Some unusual applications change task-list eligibility *only* at the end of a sizing gesture without emitting the usual lifecycle/show/hide events. Their membership may be picked up at the next bounded recovery scan rather than immediately; test such applications explicitly.

## Stage 2: Trace fast-path admission without extra idle work

An enabled `Nexus-Shell-UI` EventSource now emits **event 5 `WindowDispatchEvent`** for each *delivered coalesced native UI callback*, not for each raw hook callback.

| Payload | Semantics |
| --- | --- |
| `processId` | Nexus process only |
| `foreground` | The batch observed an actual foreground WinEvent |
| `enumeration` | The batch contains a structural notification requiring full window enumeration |
| `discovery` | The batch found an unknown eligible foreground window and requested recovery |
| `geometry` | One or more completed interactive move/resize gestures require prompt foreground geometry repair |

This event never includes external HWND/PID, app titles, paths or user content, and writes no local log. Disabled tracing avoids the EventSource write, so ordinary desktop operation has no new timer or trace sink. Existing `dock-enumerate` and `dock-refresh` `WorkEvent` scopes provide synchronous costs when tracing is enabled.

Portable ETW assertions validate event 5 payloads for geometry-only, structural, and discovery cases and ensure disabled recording emits nothing.

## CI and Windows acceptance gates

1. Confirm **the latest AppVeyor build for the merged PR #26 commit** is green, including native WinUI resource verification. The source merge alone is not validation.
2. Build/test this P2.5 **branch SHA** in AppVeyor: Core/Runtime checks; native UI template/resources, XAML compilation/publish, packaging, update/restore scripts and `--verify-ui`. A prior main build cannot certify the branch.
3. On a native Windows preview (not shell takeover), move and resize Files and ordinary Win32 apps repeatedly, including snap/maximize, DPI changes and monitors with negative origins. Verify correct dock visibility, menu bar attachment, fullscreen hiding, app previews and no ghost buttons.
4. Open/close apps during sustained dragging/sizing; verify the immediate structural event still updates dock membership and no "stuck" UI after an event burst.
5. Enable an opt-in ETW trace as described in `docs/RENDER-RESPONSIVENESS.md`. Compare event 5 counts, `dock-enumerate` work scopes and compositor frame times against the previous build under the same workload. **Do not claim an FPS improvement from ETW callback counts alone.**
6. Verify shell shutdown/Windows recovery and that keyboard shortcuts keep routing to the native Windows desktop.

## Rollback

Drop this branch/PR or revert its small independent commits. No state schema, elevated privileges, installer migration, native taskbar registration or user data changes are required.

## Stage 3: Coalesced geometry completion must bypass polling throttle

The first P2.5 change exposed a regression: `EVENT_SYSTEM_MOVESIZEEND` could enter the foreground-only path, but the ordinary one-second `ForegroundRepairCadence` would suppress a native geometry repair if the last periodic poll was too recent. Closing a drag at the wrong time could delay maximized-window/work-area recovery by almost a second.

- `WindowEventDispatchGate` now retains **three independent signals** (foreground, structural enumeration and completed geometry) while preserving both existing two-signal overloads.
- A geometry-complete callback requests one forced `ForegroundRepairCadence` sample and runs the existing `DesktopEnvironment.ReconcileForeground` native work without enumerating every running application.
- A forced sample restarts the periodic repair budget. Normal 80/250 ms pointer polling still coalesces native geometry work and never runs without a foreground HWND.
- Mixed events (move/resize + foreground + destroy) preserve all three signals. Refused WinUI dispatch retains the geometry flag for the next accepted callback; ticket protection and crash recovery are unchanged.
- `WindowDispatchEvent` (ETW ID 5) includes a fifth `geometry` Boolean. This event is not a stable external telemetry contract: all P2.5 changes are still draft, and Core tests cover the final payload schema.
- New portable tests cover a 1,000-event mixed burst, stale-flag cleanup, refused admission recovery, inline delivery and forced foreground repair cooldown.

**CI guard:** app/WinUI native compile and P2.5 branch checks still require an actual AppVeyor run. The newly reported latest `main` status is green for the merged #26 baseline, but that is not proof of this branch.

## Stage 4: Recovery for missed window creation during geometry-only delivery

An event-only geometry fast path must not postpone recognition of an active window that was never added to the dock because its create/show WinEvent was missed. The existing unknown-foreground discovery policy is now consulted on **either** a foreground event **or** a completed move/resize event. It returns false for known HWNDs and invalid foreground values; the caller still performs native eligibility/ownership/cloaking checks before scheduling a task-list scan. All other non-foreground fast-path notifications are unchanged.

Portable checks cover known and unknown HWNDs during move/size completion; the regular five-second recovery scan remains in place.

## Stage 5: Animation lifecycle resilience during UI reconciliation

`DockMotionController.SetIndicator` now defers compositor work for indicators that have not yet reached WinUI `Loaded`, or that were temporarily unloaded during dock visual recovery. It discards stale compositor presence/animation tracking for those controls, while the owning `DockItem` keeps its latest logical `Presence`. `TaskbarView.PrepareMotion`, called on `Loaded`, reapplies that latest presence immediately with a settled baseline rather than animating from an obsolete state. Detach/reset also avoids asking WinUI for compositor visuals after unload. This is designed to prevent one transient unloaded-control failure from disabling motion for the whole shell session.

No animation durations, easing curves, dock indicators, pointer hit regions, preview behavior, accessibility policy or native window state transitions have changed. Windows acceptance must still check reduced-effects/high contrast, rapid app minimize/restore, dynamic pin changes, app deletion and dock unload/reload while animating.
