# Startup source validation — 1.8.0

## Completed

- .NET 8.0.419 Roslyn compilation of Runtime, Core, DesktopHost, and both check projects with nullable warnings treated as errors: zero warnings/errors.
- All 15 existing portable check groups plus 25 startup-policy assertions pass. New checks cover supervised and legacy commands, folder selection, foreign-entry preservation, shell conflict refusal, command bounds, E_NOTIMPL fallback, unrelated error propagation, and policy account binding.
- 101 runtime assertions and the production stream-connection checks pass. Empty readiness probes do not dispatch or log errors; partial frames still report failure without dispatch.
- Shell C# API compilation against the pinned Windows/WinUI projections passes using temporary XAML declarations. Its 19 warnings comprise six projection-reference unification warnings and 13 unused temporary fields. No application-source warnings are emitted.
- Source/resource/build-reference checks, whitespace checks, PowerShell parsing, and update fixtures pass. PowerShell fixtures ran on Linux with their Windows platform guard enabled for fixtures.

## Still required

Native Windows XAML/PRI/XBF compilation, 1.8 executable launch, readiness-window rendering/cancellation, startup registration/sign-in timing, actual UAC cancellation and same-account elevation, shell-less native style fallback, independent policy recovery, real named-pipe/job tests, and measured sustained use.

Linux named-pipe socket creation is restricted here. Runtime checks use the explicit --no-native-ipc option in this environment; Windows CI runs without that option. C# API checking does not compile XAML or establish a native build.

See STARTUP-1.8.0.md for the VM acceptance sequence and VM-EVIDENCE-2026-10-10.md for the earlier user-supplied 1.7 evidence.
