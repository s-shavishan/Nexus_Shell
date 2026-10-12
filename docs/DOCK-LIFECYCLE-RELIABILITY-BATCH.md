# P1 dock lifecycle reliability and responsiveness pack

Baseline: `a07c956541491af514516caeb2add5981668bd79` (Nexus Shell native WinUI 3, not the separate Electron app).

## Problems addressed

1. **Event storms and dispatch admission.** Windows task-list events can arrive in bursts. `DesktopEnvironment` previously tracked pending dispatch and foreground priority using ordinary boolean fields, with no synchronization around native callback delivery. The new `WindowEventDispatchGate` coalesces at most one outstanding UI callback, carries foreground priority across events, and recovers when the WinUI dispatcher rejects or throws. It does not install any keyboard hook or modify Windows shortcut behavior.
2. **Repeated process metadata reads.** `NativeMethods.RunningWindows` used to open a process for each HWND, even when dozens of windows belong to one PID. `WindowProcessSnapshot` resolves each PID once per enumeration, including foreign-session or failed lookups, then drops the cache after the enumeration. There is no durable process database or cross-scan PID cache.
3. **Redundant dock layout reconciliation.** `DockWindowSet.Reconcile` now reports whether identities, order-preserving metadata, or membership changed. `TaskbarView.Refresh` still updates active/minimized states, but when both pins and windows are unchanged, it avoids building a new live-ID hash set, reconciling window previews and reapplying dock sizing/theme/clock XAML. Title changes and PID reuse still trigger a full reconciliation.
4. **Observability.** Opt-in `Nexus-Shell-UI` ETW event 3 now measures synchronous native window enumeration as `dock-enumerate` alongside the existing `dock-refresh` UI work. This records no titles or paths, adds no default disk logs, and is inactive without a tracing listener.

## Safety boundaries

- Do not change Windows Explorer's taskbar, appbar registration, native keyboard shortcuts, shell takeover, privileged services or user policy.
- No injected input, no foreign-window mutation, no direct bitmap capture.
- Keep lifecycle ownership (HWND + PID) and original tool-window, cloaked-window, own-process, title and session filters.
- Native callback dispatch is nonblocking. `WindowEventDispatchGate` never holds its internal lock while calling the dispatcher or delivering a UI callback.
- On queue refusal, the foreground flag is retained until a later event admits a callback. Existing 5-second desktop rescan remains the loss-recovery fallback when the dispatcher is unavailable.
- No frame-rate or CPU improvement is claimed until collected on real Windows graphics hardware.

## Test coverage

The Core suite uses the exact production helper classes and covers:

- 1,000 sequential and 1,500 concurrent native event requests coalescing into one UI callback, with foreground priority preserved.
- Refused/throwing dispatcher admission and reentrant delivery, with recovery and no permanent queued state.
- Repeated HWNDs belonging to one PID; negative caching for foreign-session or unavailable metadata; new-scan PID reuse; invalid IDs.
- Unchanged z-order vs real title, process ownership, closure and pin/window changes.

## Windows acceptance after AppVeyor

1. Run both Core and Runtime check projects, updater regression, native diagnostics, WinUI build and packaging. Keep the JSON/log artifacts for failures. A cancelled job is not a pass.
2. Open at least 15 windows from three different applications. Minimize, restore, activate, close, rename documents and reopen windows rapidly. Ensure all entries remain in the same stable order except actual new/closed windows. Repeat with an application showing multiple HWNDs.
3. Switch quickly between a maximized browser and desktop. Verify floating dock reveals at the bottom edge, hides for borderless fullscreen, and stays responsive to right-click previews and actions.
4. Compare ETW `dock-enumerate` and `dock-refresh` work durations and allocations on identical workloads before/after the patch. These events measure synchronous work, **not** FPS or displayed frame time. See [render diagnostics](RENDER-RESPONSIVENESS.md).
5. Check Windows 10 and 11 where available, negative-origin secondary displays, 100%-200% DPI, Accessibility/High Contrast and Safe Mode behavior. Confirm no callback runs after shutdown.
6. Run at least 20 live minimize/restore cycles on a real desktop, then the full acceptance set after the accumulated batch is ready.

## Rollback

Revert this feature PR as one unit. There are no persistence/schema changes or migrations. All dispatch and metadata caches are volatile and per-session or per-enumeration.
