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
