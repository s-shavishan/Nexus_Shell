# 3.0.0 validation evidence

Validation was performed against the complete source in the Linux workspace using the .NET 8.0.419 Roslyn compiler and restored Windows/WinUI API references. This is a type/API check; it does not run the native Windows XAML compiler or a desktop session.

- Runtime, Core, DesktopHost and both portable check projects compile with warnings treated as errors.
- The Shell API compilation has no errors. Its 19 warnings are six mixed validation-reference warnings and thirteen unused fields in the temporary generated XAML field stubs. These are not a native Windows build result.
- Core checks pass, including 66 Start-key checks, 31 startup checks, 1,008 layout/work-area cases and 118 major desktop checks. The latter cover floating/attached bar bounds, snap recognition, borderless work-area clamping and actual notification-history save/reload/clear.
- Runtime checks pass: 101 existing runtime assertions with 30 simulated isolated Files crashes, 98 settings assertions and 121 new Control Center assertions. The new cases include authorization, owned windows, sole-writer preferences, hide/reopen sequencing, lost acknowledgments, cancellation, queue bounds/expiry, heartbeat/readiness deadlines, cooldown, termination failure and repeated worker crashes.
- Production framing/router tests include the panel role, rejection of full-state commits, panel-owned HWND validation, device reads, substitution of the desktop-owned display HWND and worker dismissal. Only the OS transport is replaced in these stream tests.
- Native named pipes, Windows job cleanup/breakaway and hardware reads are explicitly skipped here and remain enabled in Windows CI.

Source/XAML/template/resource validation passes: 115 required files, 140 named elements, 70 handlers and nine runtime XAML templates. All PowerShell scripts parse, whitespace checks pass, and the synthetic update fixtures pass runtime reuse, hashes, folder preservation, canonical paths and app/resource payload requirements. The update fixtures simulate the Windows environment; they do not install this candidate on Windows.

Real Windows rendering, native input/foreground behavior, process-job recovery, hardware, resource measurements and the screenshot reproduction require [VM acceptance](SESSION-CORE-3.0.0.md). Do not label this candidate stable merely because portable checks pass.
