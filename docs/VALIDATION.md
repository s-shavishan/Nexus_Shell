# 1.3.1 validation

The verified 1.3.0 baseline at 8659dbb passed AppVeyor build 54859892 and was reported working in the user's VM. New 1.3.1 native behavior still requires a Windows build and VM acceptance.

Completed locally on prepared 1.3.1 source:

- .NET 8 core checks execute with the SDK's default platform analyzers and compiler/analyzer warnings as errors. New checks cover original visibility, newly discovered taskbars, reused handles, durable session work-area/visibility records, rejecting a foreign session/invalid work area, write-before-hide, refused/retried journal writes and independent recovery after lease loss.
- Denied sign-in policy, failed surface restoration and failed work-area actions do not stop later recovery actions. Ctrl+Esc repeat/release handling is checked alongside Task Manager/security pass-through. Existing policy/backup rollback, numeric registry kind compatibility, Files, state, keyboard and timeout checks still pass.
- All host C# compiles against .NET 8 references with default SDK analyzers and warnings as errors, including native visibility APIs and saved-state recovery. The independent PowerShell fallback's embedded C# compiles with C# 5 syntax and compiler warnings as errors.
- All application C# resolves against pinned Microsoft.WinUI 1.8.260803003, Foundation/InteractiveExperiences projections, Windows SDK NET references 10.0.19041.56 and .NET 8. Temporary XAML field/InitializeComponent stand-ins are used; known reference-unification and unused stand-in warnings remain.
- XML/XAML resource/name/handler/ordering, assets, publish wiring, independent native surfaces, startup/Sections lifetime, host/source/build/update ownership and C# syntax checks pass.
- The Git patch applies cleanly to the verified 172-file baseline, has no added trailing-whitespace errors, and reconstructs the complete 1.3.1 tree byte-for-byte. Source/patch ZIP contents and hashes are verified.

Pending: actual PowerShell parser/updater execution on Windows, WinUI XBF/PRI generation, native launch, Explorer window visibility, work-area restoration, auto-hide, keyboard hooks, host/UI failure recovery, lock/unlock, suspension, multi-display/DPI behavior, visual accessibility and resource use. Linux C# compilation is not a runnable Windows build and does not prove these Win32 actions.

CI must complete the actual parser/updater, native build and package/resource checks. TEST-DESKTOP-MODE.md defines VM acceptance. No Windows service, registry setting or VM was modified here.

Evidence: Core-checks.txt, CSharp-API-check.txt, Host-API-check.txt, Recovery-CSharp-check.txt and Source-checks.txt. Historical screenshots/illustrations and earlier version notes remain unchanged.
