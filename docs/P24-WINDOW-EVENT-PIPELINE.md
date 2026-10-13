# P2.4 — Native event dispatch ownership (in progress)

**Starting revision:** `main` at `2bd8debe81f861d7be2dd32b6f519cf525c7c7e6` (squash merge of P2.3 PR #22).

## Stage 1 — Make native dispatch generations immutable

The two existing dispatch gates had a narrow reentrancy vulnerability: if an enqueue implementation invoked its callback inline, a new notification could queue its own callback, and the original enqueue could then return false or throw. The old cleanup path unconditionally cleared the pending flag, implicitly unlocking the new notification. Another edge case occurred when a rejecting enqueue retained a callback and invoked it after a later accepted request: the old callback could consume the newest event.

- `WindowEventDispatchGate` now owns a per-admission ticket under its lock. Only its matching queued callback can consume foreground priority and deliver. Refusal/exception cleanup releases the same ticket, never a successor. Rejected callbacks cannot be replayed.
- `CoalescedUiDispatch` uses the same ticketed ownership semantics for UI-thread-owned dock/window position notifications. Its exceptions, optional error reporter, and reentrant callback behavior remain supported.
- The existing Core check runners gain deterministic regression scenarios for late rejected callbacks, inline delivery followed by rejection, preserved foreground signals, and prevention of duplicate UI work. Existing 1,500-event concurrent coverage stays in place.

## Validation position

The project owner elected to skip AppVeyor and manual Windows-preview gates during ongoing development. Accordingly **this stage has not been compiled, executed, or visually verified** in this session. It is maintained on a separate feature branch. Do not claim runtime performance, visual fixes, or release readiness from source changes alone.

## Next integrated development targets

- Audit foreground/window enumeration scheduling and dock lifecycle transitions for redundant scans or missed updates.
- Extend regression coverage before batching P2.4 into a single meaningful pull request.
- Keep GitHub Actions out of the development workflow; AppVeyor remains the configured optional CI platform, not a merge requirement for this requested workflow.

## Stage 2 — Distinguish foreground from structural window events

- Pure `EVENT_SYSTEM_FOREGROUND` notifications update the selection/active indicator and dock visibility immediately **without** queuing the additional 50 ms `EnumWindows` scan. The existing five-second independent fallback enumeration remains intact for missed events.
- Show/hide/create/destroy/title/cloak/minimize/move-end and unknown notifications still schedule full window enumeration. A new `WindowEventScanPolicy` keeps that distinction explicit and testable.
- The native dispatch gate now coalesces two separate signals: foreground priority and structural enumeration. An incoming structural notification can never be lost just because it shares one dispatcher callback with a foreground event. Rejected queues retain both signals; ticket ownership from Stage 1 still rejects stale callbacks.

## Stage 3 — Retain stable window enumeration results and dock renders

- `WindowListSnapshotPolicy` compares the immutable `RunningWindow` records (including title and process identity) **in z-order**. Matching scans keep the previous snapshot reference so foreground lookup caching is not needlessly invalidated by the five-second recovery pass.
- The latest complete dock snapshot is also retained. When it is identical to the previous snapshot, no redundant `TaskbarView.Refresh` lifecycle/pin reconciliation runs; the cheaper `RefreshWindowStates` still updates activation/minimize/restore cues and accessibility actions.
- Window board refresh, foreground visibility, work-area/maximized-window repair and the recovery scan itself are **not** skipped by the dock-layout optimization.
- Tests cover 800 coalesced foreground events, mixed foreground/structural batches, rejected-callback recovery, z-order differences, title updates, process-ID reuse and closed windows.

As requested, these changes have **not** undergone AppVeyor compilation or a Windows visual preview in this session. Preserve the branch and consider the implementation unverified until a future build is available.

## Stage 4 — Dock hide/reveal state recovery

- `DockVisibilityTransitionPolicy` distinguishes desired visibility, native HWND visibility and a pending compositor hide. The existing edge hold/fullscreen rule still decides whether the dock should show.
- Windows can externally reveal a logically hidden tool window. The dock now corrects that mismatch rather than remaining exposed. An in-flight hide is not restarted on every pointer poll, while fullscreen/taskbar registration suppression forces immediate hiding.
- Asynchronous hide callbacks carry a generation number. A cancelled animation cannot hide a subsequently revealed dock or clear another hide's pending state. Current-generation completion rechecks native visibility and invokes a native fallback if needed.
- `TaskbarWindow.RefreshFromNativeEvent` shares one foreground HWND snapshot between dock indicator and visibility updates; the timer caches that identity to avoid the immediate duplicate foreground repaint.
- `ShowBar` avoids a duplicate visibility call since `Position` already invokes it.
- Pure Core tests cover normal hide, pending animation, external show/hide, fullscreen override, reveal cancellation and no-op states.

No Windows compilation, native compositor test or AppVeyor result has been verified for this stage.

## Stage 5 — Minimize/restore cue lifetime and unknown foreground recovery

- `DockCueDeliveryPolicy` treats compositor cues as transient feedback, not durable window state. Only a newly created icon's **first arrival** may be buffered until the initial XAML load. Minimize, restore and activation updates observed after an icon unloaded are never replayed later.
- `TaskbarView` tracks whether each window button was previously mounted. Unload clears any deferred cue; reload settles the live presence indicator without manufacturing a lifecycle animation. Real updates to loaded icons still animate.
- `TaskbarWindow.RefreshFromNativeEvent` returns its already-captured foreground HWND. Known-app activation remains enumeration-free. If an unknown focused HWND is from an eligible external app, non-cloaked and not a desktop/tool surface, Nexus schedules its existing 50 ms coalesced enumeration to recover a missed create/show event.
- Existing structural-event enumeration and independent five-second recovery pass remain active.
- New Core assertions cover loaded/unloaded cue decisions and unknown/known foreground classification.

No AppVeyor run or Windows preview was performed by owner request. Static source edits and assertions do not establish successful compilation, visual correctness or measured latency improvements.

## Stage 6 — Rapid window lifecycle and targeted foreground-state refresh

- Native EVENT_OBJECT_DESTROY notifications retain bounded HWND tombstones across cross-thread WinEvent coalescing. The next UI-thread scan drains these handles. If Windows recycles an HWND and PID between scans, the dock discards the previous control/animation and appends a fresh entry.
- DockWindowSet.Reconcile(snapshot, destroyedHandles) invalidates recycled handles. TaskbarView.Refresh removes invalidated button controls instead of reusing their dictionary keys.
- Foreground-only events visit dock items in managed memory but perform Win32 state checks only for previous/current foreground HWNDs. Structural events and the five-second fallback still inspect all entries, including out-of-focus minimize/restore.
- A follow-up production commit corrected the extracted per-item refresh method to use an early return on stable state.
- Portable Core regression checks now cover same-process close/reopen, unchanged order, rapid deletion/reinsertion, and two-HWND foreground selection.

This stage has not undergone Windows compilation, AppVeyor execution, or desktop visual tests at owner request. Runtime frame-time improvement remains unmeasured.

## Stage 7 — Async scan ownership, retry and bounded overflow

The prior destroy-handle collection was drained **after** the worker returned from EnumWindows. Events that arrived during that worker's enumeration could therefore be consumed by an older window snapshot; if dock reconciliation then threw, the consumed identity information was lost.

- `DockLifecycleJournal.BeginScan` atomically captures the deduplicated, pre-scan destroy events and starts a generation. Native destroy hooks keep writing to an independent pending set throughout enumeration and UI application.
- `CompleteScan(batch, applied)` consumes successful generations only. Failure restores the batch alongside newer destroys, with ticket checking to reject late/double completions. Events arriving mid-scan prompt a follow-up scan even if a WinUI dispatcher callback is delayed.
- Beyond 2,048 distinct pending handles, the journal requests a one-time complete dock identity reset rather than silently dropping an event. A failed reset keeps that demand pending for retry.
- The coordinator retains the previous 50 ms event coalescing on successful scans; a failed scan that has pending lifecycle work retries at 500 ms, preventing a permanent rapid failure loop. The five-second independent recovery cycle and foreground-only lightweight path are preserved.
- Portable checks cover mid-enumeration notifications, duplicate handles, failed rollback, stale completion, overlapping scan refusal, forced full-reset retry, and 1,800 parallel destroy notifications.

**Status:** Production and portable-test source committed only; AppVeyor and native Windows preview remain skipped by request. These tests have not been executed in this session.

## Stage 8 — Integration audit: native scan backoff and dock invalidations

After PR #23 was opened, a source review identified two retry/rendering edge cases.

- The former scan failure path scheduled a retry only if another window refresh or destruction was pending. An isolated `EnumWindows` or UI application failure could therefore wait until the independent five-second heartbeat to recover.
- The dispatcher timer's `Interval` was also mutable between normal scans and failed-scan retries, so a failure could leave subsequent ordinary window notifications delayed by an old 500 ms interval.
- `WindowScanRetryBackoff` now gives every failed scan a bounded retry interval: 500, 1000, 2000 and then 5000 ms. Recovery resets to the normal 50 ms event coalescing window. The timer tick resets its interval before starting the next scan so the previous failure interval does not leak into unrelated events.
- `WindowListSnapshotPolicy.RequiresDockRefresh` skips full XAML dock reconciliation if the window snapshot is identical and the destroyed HWNDs belong only to incidental native tool windows. Matching dock destruction, metadata changes and full-reset overflow still rebuild affected entries.
- Portable regression coverage confirms the retry progression/reset, isolated recovery and irrelevant-destroy versus genuine-dock-destroy classification.

**Verification remains source-only:** no installed local .NET SDK, and AppVeyor/Windows preview have been excluded from this iteration by request. The draft PR must not be called build-verified.

## Stage 9 — Scan admission and retry ownership audit

Integration review uncovered two new hazards on top of Stage 8's scan-retry backoff:

- A call to `DockLifecycleJournal.BeginScan` was located outside `UpdateTaskbar`'s exception-handler region. A reentrant admission exception could bypass the `finally` block and permanently leave `_refreshing` true, preventing all subsequent dock scans.
- A new 50 ms window event could arm the timer while the current scan was still running. On failure, the prior `finally` logic left that already-enabled fast timer untouched even though the failure counter requested 500–5000 ms backoff, potentially creating repeated rapid native scans during persistent errors.

The scan's lifecycle admission is now inside the protected `try` with a nullable generation handle. Cleanup only calls `CompleteScan` for successfully acquired batches, while the guarded `finally` always releases the refresh gate. `WindowScanTimerPolicy` explicitly determines whether a scan completion should leave its timer alone, start one, or restart one. Failed scans replace even an already-armed timer with backoff; successful scans do not interrupt legitimate pending native events. Shutdown schedules nothing.

Portable checks cover exception recovery prerequisites, active-timer replacement after failure, success-with-pending work, idle no-op, and every stop-state combination.

**Status:** Committed source and portable test assertions only; the installed environment has no .NET SDK and the owner chose not to run AppVeyor or a Windows preview. The phase is not build or runtime verified.

## Stage 10 — Skip dock reconciliation on pure native z-order changes

The previous Stage 8 `RequiresDockRefresh` helper used the order-sensitive native snapshot comparer. That behavior is correct for the window board, but each foreground activation changes `EnumWindows` z-order without changing the dock's retained lifecycle order. It still triggered `TaskbarView.Refresh` and its managed pin/window reconciliation on a structurally unchanged dock.

- `EquivalentDockEntries` compares all dock entries by HWND and PID **and** immutable record metadata, ignoring their enumeration order. Duplicate identities fail closed instead of silently dropping an entry. This is O(n) with one short-lived dictionary.
- `RequiresDockRefresh` uses the dock-specific comparator. Native window-board snapshots still use the original order-sensitive `Equivalent`, preserving fresh z-order and foreground lookup correctness.
- Explicit destroy/recreate invalidation and full-reset overflow still force reconciliation. Any actual added, removed or renamed app/window also triggers reconciliation.
- Portable regression assertions cover Alt+Tab reordering, duplicate identities, HWND/PID reuse, changed process metadata, destroyed app HWNDs and 180-window reversed enumeration.

**Validation:** Source changes and regression assertions are committed; no .NET compilation, AppVeyor execution or Windows preview has been performed.

## Stage 10 follow-up — Defensive identity validation and shutdown-safe HWND capture

- The new dock comparator intentionally checks duplicate HWND/PID identities even when both operands are the same list instance. A reference-identity shortcut could otherwise incorrectly certify a malformed duplicate window collection as safe to retain.
- `UpdateTaskbar` captures the desktop HWND before transferring enumeration to the worker thread; it no longer accesses the WinUI desktop object from that worker during shutdown.
- Added a portable regression case for a reference-identical duplicated snapshot. The existing ordered native-window comparer retains its fast reference comparison, as it serves a distinct purpose.

No .NET/WinUI build or AppVeyor tests were executed in this development batch.

## Stage 11 — Repair partial WinUI dock reconciliation after a failed pass

- An asynchronous window scan may get a valid native snapshot, then fail partway through `TaskbarView.Refresh` (for example during native icon materialization or a WinUI child mutation). The retained `DockWindowSet` may already reflect the new identities, while `_items` and `_windows.Children` only partially reflect them. On the next retry, `DockWindowSet.Reconcile` returns unchanged; the old early exit could therefore preserve invisible/missing dock buttons indefinitely.
- `TaskbarView` now marks its XAML reconciliation incomplete before mutating retained controls and clears the marker only after a completed refresh. A prior failure, mismatched item count or mismatched child count triggers the full UI recovery path even if native window metadata did not change.
- Recovery reattaches existing current HWND buttons in retained lifecycle order. It does not replay a full dock rebuild during normal active-window switching or stable recovery scans.
- A pure `DockVisualRecoveryPolicy` captures the decision and portable tests cover missing dictionary entries, missing child controls, interrupted updates with correct counts, first-baseline rendering, pin/window updates and the stable no-op path.
- The actual WinUI outcome remains unverified without a Windows build. No AppVeyor or Windows preview was performed by owner preference.

## Stage 12 — Avoid redundant recovery scans and stop async timer work at shutdown

The five-second desktop heartbeat calls `UpdateTaskbar` as a recovery guard. When a prior `EnumWindows` pass was still running, the old coordinator treated the heartbeat like an explicit application change and set `_windowRefreshPending`, forcing a second scan regardless of whether any new native event or user action had arrived.

- `WindowScanAdmissionPolicy` distinguishes idle scans, real notifications/user commands arriving during an active enumeration, and recovery heartbeats already covered by that enumeration. Only the latter are safely absorbed; structural events retain their follow-up scan.
- The public/internal zero-argument `UpdateTaskbar()` entry point is preserved for all existing callers and method-group compatibility. The private asynchronous coordinator accepts the heartbeat classification, and only the five-second timer opts into that mode.
- An enumeration completing after `Shutdown` started still completes its lifecycle journal generation and releases `_refreshing`, but no longer updates native refresh timer intervals, failure backoff counters or retry scheduling.
- Portable tests cover idle heartbeat admission, overlapping heartbeat suppression, explicit structural follow-ups, and a 1,000-heartbeat burst while a scan remains in flight. Existing native event and shutdown timer policies remain unchanged.

**Validation:** Source and regression checks were committed on the draft P2.4 branch. No .NET SDK compilation, AppVeyor run or interactive Windows desktop test was performed here.

## Stage 13 — P2.4 integration-readiness: stale visual identity recovery

The earlier partial-render fallback checked `_refreshIncomplete`, number of `DockItem` entries, and number of attached XAML child controls. That does not catch a wrong set of HWND/PID identities with the *same* number of buttons, a same-count child ordering error, or a prior manually requested refresh that was interrupted while the native snapshot remained unchanged.

- `TaskbarView.NeedsRecovery` now checks the incomplete-render marker, compares expected `DockWindowSet` HWND/PID keys against live `_items`, and validates the WinUI children against those identities in retained lifecycle order.
- `DesktopEnvironment.UpdateTaskbarCore` invokes `TaskbarView.Refresh` when `NeedsRecovery` is true even if native window enumeration and dock snapshot are unchanged. This also covers incomplete settings-triggered refreshes.
- `DockVisualRecoveryPolicy.HasConsistentIdentities` keeps this decision pure/testable. The comparison is order-insensitive for `EnumWindows`, while the UI property independently validates the preserved dock child ordering. It handles missing/duplicate entries and same-HWND/different-PID reuse.
- Portable checks cover unchanged identities, pure z-order changes, equal-count stale HWNDs/PIDs, duplicate expected/actual identities, missing buttons, renamed metadata and empty snapshots.
- The main desktop continues its five-second recovery heartbeat and 50 ms native event coalescing; the new identity validation is only part of the periodic UI snapshot check.

### Readiness conclusion

Source-level integration and regression assertions are present in GitHub, but **P2.4 has not passed a .NET build, portable Core execution, AppVeyor compilation or real Windows desktop preview** in this session. GitHub's mergeability status checks Git history compatibility, not WinUI correctness or performance. Keep PR #23 a draft and let the project owner explicitly decide whether to validate and merge; do not represent it as release-ready.
