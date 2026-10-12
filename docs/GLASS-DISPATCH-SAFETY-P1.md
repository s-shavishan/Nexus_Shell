# P1 glass UI callback dispatch and fallback safety

Baseline: Nexus Shell native WinUI 3 `dbfefd00cde091673748a6d8e7f57a005aaf667a` (after PR #15).

## Failure mode

`WindowMaterial` registers asynchronous `LiquidGlassBackdrop.StateChanged` and `Failed` callbacks. While PR #15 rejects stale attachment *identity*, callbacks accepted onto `DispatcherQueue` still executed XAML mutations without an exception boundary. A closed/invalid XAML surface, failed HWND backdrop-detach API, or exception in a status observer could escape the dispatcher callback. This can cause a WinUI unhandled UI exception rather than the intended palette fallback.

## Changes

- Use a minimal production `GlassUiDispatch.TryPost` for state and failure notifications. It guards dispatcher admission, attachment validity, and UI update execution. It contains exceptions from all three; logging failure cannot turn a handled callback failure into an unhandled UI exception.
- Preserve `GlassAttachmentGate` checks so a stale controller cannot repaint or detach a newer attachment. No polling, retry timer, native hook, or background thread is introduced.
- When a live backdrop reports failure, revoke it and clear the reference, then attempt native detach. If native detach throws, still try applying the current solid palette, preserving readable UI.
- Catch exceptions thrown by `WindowMaterial.StatusChanged` observers so status feedback cannot abort appearance updates.
- Exceptional conditions continue to use the existing diagnostic log. Ordinary glass-state changes still avoid synchronous disk logging and remain traced with opt-in ETW.

## Core regression checks

Production `GlassUiDispatch` is compiled into Core checks. `GlassUiDispatchChecks` exercises:
1. Normal queued delivery.
2. Controller becoming inactive before a callback executes.
3. Exceptions in XAML update and attachment-validity checks.
4. Dispatcher returning false or throwing during enqueue.
5. The error-reporting callback itself throwing.
6. 500 queued updates invalidated by controller replacement/close.

These tests establish exception/identity behavior, not successful native acrylic rendering or compositor frame rate.

## AppVeyor + hands-on acceptance

1. Require Core and Runtime checks, Windows native diagnostic fixture, WinUI build, publish and package to pass on the exact PR revision. A canceled run or older `main` green run is not a pass.
2. Exercise glass-on/off, rapid theme changes, Reduced Effects and High Contrast across Launchpad, dock, Files and Control Center.
3. Repeatedly open/close surfaces during appearance transitions, resize with mixed DPI/negative-origin secondary monitor, and watch for UI crashes or opaque-paint regression.
4. Confirm a Windows 10 session lacking usable acrylic retains readable solid fallback. Review `Nexus-Shell-UI` event trace for controller-created/disposed/state event consistency.
5. Pixel-level black-edge/resize artifact resolution **remains unproven** and must be evaluated in the later joint Windows session with ETW/WPA/DWM evidence.

## Rollback

Revert this PR as a unit if native WinUI acceptance identifies regression. No schema, shell ownership, window positioning, shell hooks or privileges change.
