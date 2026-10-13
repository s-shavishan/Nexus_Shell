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
