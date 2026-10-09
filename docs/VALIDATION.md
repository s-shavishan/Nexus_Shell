# 1.4.0 validation

The 176-file baseline at b493314 passed AppVeyor build 54860759. The user's VM runs its session takeover/restoration but reports severe dragging lag. New 1.4.0 native behavior and improvement require a Windows build and comparison.

Completed locally on prepared source:

- Actual .NET 8 core checks execute with SDK platform analyzers and compiler/analyzer warnings as errors. New checks cover one work-area write across 1,000 unchanged refreshes, geometry changes, explicit display invalidation, failed-write retry, existing-preference visual profile persistence and content preservation, right-anchored small/DPI/negative-origin panel bounds, and brightness range/numeric rejection.
- Existing state, Files, keyboard, policy/backup rollback, numeric registry kind, durable session recovery, denied-policy recovery and restart/heartbeat checks remain passing.
- All host C# compiles with .NET 8 default analyzers and warnings as errors. The independent recovery fallback's embedded C# compiles with C# 5 syntax and warnings as errors.
- All application C# resolves against the pinned real WinUI/Windows/.NET references with platform analyzers and warnings as errors. Only known reference-unification warnings (CS1701/CS1702) and unused generated stand-in fields (CS0414) are excluded in this local check. Temporary XAML field/InitializeComponent stand-ins are used.
- Source XML/XAML resource/name/handler/order, native surface ownership, startup/Sections lifetime, cached wallpaper dimensions/publish wiring, host/source/build/update ownership and C# syntax checks pass. Wallpaper bytes are verified and the selected Solstice asset was visually inspected.
- The Git binary patch applies cleanly to the verified baseline, introduces no trailing-whitespace errors, and reconstructs the full 1.4.0 source byte-for-byte. Source/patch archive entries and SHA-256 hashes are verified.

Pending: real Windows PowerShell parser/updater execution, native XBF/PRI generation, app launch/rendering, dragging/frame timing, CPU/GPU/memory measurements, work-area interaction with Explorer/appbars, actual audio/brightness drivers, focus/dismissal, failure recovery, lock/unlock and display/DPI behavior. No Windows services, sign-in setting or VM was modified here.

CI must complete native build/resource/package checks. QUICK-SETTINGS-AND-PERFORMANCE.md and TEST-DESKTOP-MODE.md define acceptance. Local API compilation is not a runnable Windows release and does not prove that the user's lag is resolved.

Evidence: Core-checks.txt, CSharp-API-check.txt, Host-API-check.txt, Recovery-CSharp-check.txt and Source-checks.txt. Historical design screenshots and earlier release notes retain their original context.
