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
