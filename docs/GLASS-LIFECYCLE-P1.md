# P1 Liquid Glass attachment lifecycle hardening

Baseline: Nexus Shell main `b2b08ff5b0d44eed6b80466fb6b012f1ec272a26` (C#/WinUI 3 native OS surface).

## Source findings

- Native `LiquidGlassBackdrop` state and failure events schedule callbacks through the owning frame's dispatcher. Those callbacks previously captured the ShellTheme/ShellState from the **initial attachment**. After a palette or accessibility change, an arriving callback could repaint the surface with stale theme resources.
- Failed native attachments detached `window.SystemBackdrop`, but retained `WindowMaterial._backdrop` until another full appearance transition. Its state events could therefore remain connected to an orphaned native instance.
- `WindowMaterial.UpdateStatus` wrote routine status changes to `nexus.log` synchronously on the WinUI thread. Normal controller transitions should not require disk I/O.

## Implementation

- Add production `GlassAttachmentGate<T>`, an identity gate tracking the sole current backdrop instance. It rejects old-generation state/failure callbacks; a failed current source is revoked immediately; window close makes all queued notifications inert.
- Track the most recent theme/state/role in `WindowMaterial.Apply`, and use those values when asynchronous callbacks apply native fallback or active surface paint.
- Clear the retained `_backdrop` reference after native failure, appearance exceptions, and window close.
- Remove routine synchronous status-file writes. Actual controller state still has opt-in `Nexus-Shell-UI` ETW diagnostics via `LiquidGlassBackdrop.UpdateState`; exceptional failure details remain in existing `Log.Write` paths.
- Do not change the acrylic recipe, controller target API, visual design, shader, corner radius, window registration, native shortcuts, appbar or user preference storage.

## Regression checks

`GlassAttachmentGateChecks` compiles the exact production lifecycle gate and covers:

1. Unattached state and first native controller attachment.
2. Replacing controller A with B while a late failure from A arrives.
3. Current failure detaching B, duplicate failure idempotence and reattachment after toggle.
4. Turning off glass and suppressing delayed callbacks.
5. 500 rapid attachment replacements with stale failure notifications and post-close terminal behavior.

The tests validate identity/lifecycle policy, **not actual WinUI acrylic rendering**. The existing Windows native UI probe and real graphics acceptance remain required.

## Windows acceptance

After AppVeyor Core/Runtime, native diagnostics and packaging pass:

1. Start Nexus with native glass enabled. Open Launchpad, Files, Quick Settings and window previews.
2. Repeatedly toggle native glass on/off and switch between light/dark palettes while windows stay open. Exercise Reduced Effects and High Contrast.
3. Resize and move windows on mixed-DPI and negative-origin monitors; look for stale palette flashes, prolonged black frame edges and lost acrylic attachment.
4. Close a window while toggling glass; ensure no late status notifications, crashes, orphaned backdrops or window resurrection.
5. In ETW, inspect the `Nexus-Shell-UI` provider for `controller-created`, `controller-disposed` and state transitions; correlate with existing `material-apply` timing. A passed headless probe does not prove transparency or pixel fidelity.
6. If the system only supports the solid fallback, confirm readable colors and correct status without claiming glass rendering works.

## Risks / rollback

This changes asynchronous error handling and attachment identity. On a real Windows 10/11 device, inspect whether the compositor emits state callbacks out of order during detach/reconnect. If problems arise, revert this PR in one commit; it includes no settings migration or new permanent state.
