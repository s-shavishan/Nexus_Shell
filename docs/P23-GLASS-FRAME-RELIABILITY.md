# Nexus Shell P2.3 — Liquid Glass & Native Frame Responsiveness

Branch: `feat/p23-glass-frame-reliability`  
Base: `0c8bfb59339ec265a0ddfe36d570e557d2db6bba` (squashed P2.2 PR #21)  
Status: **IN DEVELOPMENT / HOLD MERGE**. Windows compilation, CI and pixel-level acceptance are required.

## Engineering findings and implemented stages

### Stage 1 — Prevent an unnecessary synchronous GDI repaint during live sizing

- Existing `WindowChrome` already temporarily releases stale native rounded clips on `WM_SIZING`, and restores the clip on `WM_EXITSIZEMOVE`, `WM_CANCELMODE` or `WM_CAPTURECHANGED`.
- Previously the *release* used `SetWindowRgn(hwnd, null, redraw: true)`. A forced paint while the HWND is resizing can create extra invalidation and potentially visible edge flicker. It now uses `redraw: false`; the subsequent native sizing geometry messages still repaint the surface, and restoring the **final** rounded clip still uses `redraw: true`.
- `LiveResizeRegionPolicy` documents the two redraw modes; Core checks assert the policy. Native `NativeChromeChecks` exercises all three terminal paths with its own Win32 HWND, checking that the clip is suspended and restored with the intended paint flags.
- Dock custom clips, system-frame fallback, noninteractive resizes, HWND ownership and standard WinUI rendering remain unchanged.
- **Caveat:** A GDI region test proves state/lifecycle, not absence of a black outline or dropped frames on a particular GPU. User visual testing and ETW/DWM capture remain necessary.

### Stage 2 — Retain the Liquid Glass XAML bevel

- `GlassDecorations.Apply` retains a single non-interactive rim and its last effective edge brush, inset radius, opacity and visibility.
- Repeated calls with unchanged palette, quality and radius do not rewrite XAML dependency properties, invalidate layout or reconstruct the rim visual.
- `GlassRimAppearance` preserves stronger Ultra highlights, Balanced highlights and the Solid/Essential/High Contrast/Reduced Effects fallback semantics. Core tests cover all quality and accessibility combinations.
- WinUI `UiResourceProbe` applies the current effective material quality **100 times per quality tier** and fails if any unnecessary rim dependency-property writes occur.
- Palette/quality changes and XAML CornerRadius change notifications still repaint when the appearance **actually** changes.

### Stage 3 — Retain solid/transparent window material paint

- `GlassPaintRetention` avoids assigning the same background brush, border brush or requested XAML element theme repeatedly.
- `WindowMaterial` uses the retained update path for initial opaque attach paint, Active-state paint, fallback and reapply paths. The per-HWND native acrylic controller remains owned by the window; controller creation, failure callbacks, attachment generation and theme/power fallback rules are unchanged.
- `UiResourceProbe` registers real WinUI Background and BorderBrush property-change callbacks, runs 100 repeated material updates in a stable opaque tier and rejects spurious dependency-property writes.
- The solid fallback remains fully opaque for Solid/Essential and unavailable native-backdrop states; repeated no-op material calls do not take over foreground input.

### Stage 4 — Avoid redundant responsive dock chrome layout

- `TaskbarView.SetAvailableWidth` previously set its native floating-frame corner radius and four control visibility properties on every call, regardless of whether dock width or mode actually changed.
- Frame corners and the four layout thresholds are now written only when their values differ. Launchpad remains available at all widths; the 440/480/600/620 DIP behavior and floating-clock policy are unchanged.
- `ApplyAppearance` shares the same retained corner update path, preserving square corners in High Contrast.
- The existing native `VerifyRetainedDensity` WinUI fixture now asserts **100** identical dock-width updates cause zero frame/visibility dependency-property writes; switching across thresholds must still cause appropriate writes and restore the original layout.

### Stage 5 — Enforce one native dock material owner

The actual dock HWND already calls `WindowMaterial.Apply` to manage acrylic attachment, controller failure and opaque fallback. However `TaskbarView.ApplyAppearance` independently wrote its frame background with a requested glass brush, irrespective of whether the *native* controller was still preparing or had fallen back. This could temporarily replace the solid fallback with an alpha gradient during visual/theme transitions.

- `TaskbarWindow` marks the dock view as window-material-owned before attaching its backdrop. `TaskbarView` stops writing that frame's background or edge brush; only `WindowMaterial` controls actual acrylic vs solid paint.
- A standalone `TaskbarView` (including native resource probes) renders with a fully opaque palette base, not a simulated translucent native backdrop.
- The native `UiResourceProbe` checks that standalone fallback is opaque and that 100 repeated window-owned appearance refreshes do **not** overwrite the native material/background/border.
- No changes to dock buttons, thumbnail previews, appbar ownership, system event hooks or theme preference persistence.

### Stage 6 — Avoid unnecessary menu bar native repositioning

`MenuBarWindow.Position` previously made a full `SetWindowPos` move-and-resize request even if the actual HWND had already reached the expected monitor rectangle. This could generate avoidable WM_WINDOWPOS/WM_SIZE callbacks and XAML invalidations during desktop/appbar events.

- `MenuBarNativePlacement` reads current HWND geometry and sets `SWP_NOMOVE | SWP_NOSIZE` when it matches the desired native band. The operation still specifies `HWND_TOPMOST` and `SWP_NOACTIVATE` to preserve essential menu ordering and avoid focus theft.
- Invalid/unreadable or changed HWND rectangles still trigger the full native move/resize path; external monitor moves remain recoverable.
- `MenuBarWindow.ApplyAppearance` reuses the paint-retention helper to avoid duplicate border-brush writes after `WindowMaterial.Apply`.
- Portable Core checks verify unchanged/changed/unavailable geometry and preserved no-activation behavior.

### Stage 7 — Coalesce native acrylic state bursts without dropping controller changes

- `WindowMaterial` now gives every native `LiquidGlassBackdrop` attachment its own `GlassStateDispatchGate`. Multiple StateChanged events that arrive before the next WinUI callback read the **latest** state in one retained frame update.
- The per-controller gate prevents stale queued notifications from the previous attachment from suppressing a replacement controller's first frame update. The existing `GlassAttachmentGate` still rejects any late update from an obsolete/disposed backdrop.
- Rejected or throwing dispatcher admission, stale callbacks, UI mutation exceptions and diagnostic failures all release the gate. Status/failure callbacks retain their existing independent safety paths.
- New portable `GlassStateDispatchChecks` tests 1,000 notifications with one queued repaint, 500-style controller lifecycle coverage through the existing dispatcher tests, replacement, reentrancy, queue failures and recovery.
- Avoid quantitative FPS claims without ETW and compositor frame-time evidence; this reduces avoidable UI callback work, not proven native GPU rendering latency.

### Stage 8 — Retain dock indicator animations across unchanged presence

- `DockMotionController.SetIndicator` now uses `DockIndicatorUpdatePolicy`: a real presence change animates with retained compositor templates when allowed; a stable state does nothing rather than reassigning scale, opacity and center point.
- An in-flight animation continues when the state is unchanged; turning off animation, unloading a control, disabling effects or changing presence still applies the correct static final state. Dispose, detach and global motion-reset clear tracked animation state.
- Portable dock checks cover animation vs settle decisions and accessibility/reduced-effects changes. Test actual minimize/restore cues in the Windows preview before accepting this change.

### Stage 9 — Native glass dispatcher races

- The per-controller `GlassStateDispatchGate` now uses atomic admission tickets. A late completion or error from an old ticket cannot release the pending callback belonging to a newer event.
- Regression tests include **1,500 parallel native state requests** (exactly one admitted pending update) and a simulated inline-completion/late-rejection race. They verify that the newer update is not lost or accidentally unblocked.
- This is defensive concurrency work. Real controller callback timing and actual frame pacing remain part of Windows acceptance.

### Stage 10 — Dock density geometry retention

- `TaskbarView.ApplyDensity` now only writes button/icon dimensions and divider visibility when the target values differ. Window-list changes can still reconcile running apps but do not relayout every previously stable button.
- The production native WinUI density probe now calls `ApplyDensity` **100 times** and asserts no unchanged button/icon height/width, separator-visibility or preferred-width notifications. Compact↔normal geometry is still validated separately.

## Required AppVeyor validation

1. Build this **exact branch head SHA**, not the already-green P2.2 revision.
2. Core and Runtime suites pass, including native `NativeChromeChecks`; WinUI XAML generation, publish and packaging pass.
3. The Core runner executes `GlassStateDispatchChecks`, `DockExperienceChecks` and the existing glass dispatch tests; WinUI native probe passes `glass-quality-selection-and-fallback`, `glass-retained-paint` and the retained-motion tests. Record headless skips separately.
4. Follow any errors to their actual source and fix the development branch. Do not merge on an older CI result.

## Your Windows acceptance checklist

- Run Nexus Shell as a **preview**, preserving the working desktop and a way to restart Explorer; do not change the default shell until validated.
- Drag/resize Files and at least two other Nexus windows 20 times; cancel with Escape and switch focus during a drag. Confirm rounded corners restore, no persistent black outlines, native resize edges still work, and the UI doesn't freeze.
- Resize the dock/menu bar with multiple pinned/running icons; maximize/minimize a normal app; test full-screen video/game and automatic dock reveal.
- Cycle Ultra → Balanced → Essential → Solid and Clear/Balanced/Frosted material styles. Confirm text stays legible, Solid stays opaque and unsupported acrylic has a readable fallback.
- Repeat at 100%, 125%, 150% and (if available) 200% scaling, including a secondary display with a negative virtual-screen origin. Test both High Contrast and Reduced Effects.
- Rapidly minimize/restore apps with the floating dock. Verify indicators still animate once on real state changes, settle in Reduced Effects and never stick mid-transition.
- Observe CPU/GPU usage and perceived lag; collect optional Nexus-Shell-UI ETW plus DWM frame-time evidence before making **quantitative** performance claims.
- Keep the exact branch build artifacts, probe log, failure stages and any screenshots of visual defects. Report affected GPU/OS/DPI if a problem persists.

## Merge policy

No P2.3 PR or merge until the changes accumulate into a meaningful integrated batch, AppVeyor is green for the exact revision, and you report Windows preview acceptance. Keep `main` untouched; fixes will be committed on this branch.
