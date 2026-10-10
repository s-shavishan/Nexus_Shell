# Control Center source validation — 2.1.0

Validation date: 10 October 2026. Checks ran on Linux against the delivered source. Native Windows build and VM acceptance remain pending.

| Check | Result |
| --- | --- |
| Source XML, runtime templates, handlers, resources, ownership, settings routes, backdrop callback guard, build wiring and lock version | Pass: 109 required Shell project files, 140 named elements, 70 handlers, nine native runtime templates, and the new service/test files |
| Existing managed desktop checks | Pass: 17 groups, including 1008 monitor/DPI/layout cases and 25 startup checks |
| Major desktop checks | Pass: 80 assertions, including the larger Control Center's bounds across monitor origins and DPI |
| Settings service checks | Pass: 98 assertions covering command allowlists, restart epochs, deadlines, cancellation, late native completion, section isolation, bounded final slider intents, settings routes, preference persistence, x64 interop layouts and Wi-Fi profile validation |
| Runtime checks | Pass: 101 assertions, including 30 simulated Files crashes, durable revisions, uncertain-save reconciliation and cancellation; additional production framed-stream tests cover settings reads/writes, role rejection and stale-command rejection |
| Runtime, Core, DesktopHost and both check projects | Direct Roslyn compilation passes with zero errors or warnings |
| All Shell C# against restored Windows/WinUI projections | Pass: zero errors; 19 check-setup warnings |
| PowerShell parsing and synthetic update fixtures | Pass under PowerShell 7.4.13; Windows-only environment guard mocked solely within the fixture subprocess |
| Whitespace | git diff --check passes |

## Evidence and limits

The desktop checks and Shell API compilation were repeated after the final control-lifetime guards and panel-size changes. The unchanged runtime service and native-backend source retain their passing compilation and test results. See the [captured source/API and managed check transcript](CONTROL-CHECKS-2.1.0.txt).

The compiler used .NET SDK 8.0.419, .NET 8.0.25 reference assemblies and restored Windows App SDK/Windows SDK projections. Temporary named-field and InitializeComponent declarations replace generated XAML code only for API/type checking. Six reference-unification warnings and thirteen unused temporary-field warnings belong to this setup. This check does not generate a distributable Windows package, compile XBF/PRI resources, exercise visual composition or run a Windows native driver.

Runtime checks used `--no-native-ipc`. Production framing and routing ran over a substitute stream. Actual named-pipe peer identity, Windows job cleanup, native HWND ownership and hardware API reads were explicitly skipped on Linux. Windows CI runs the full suite without that option, including read-only settings probes. An unavailable device is reported as a capability result; manual hardware acceptance is still needed for writes.

The source validator ran without optional tree-sitter support; Roslyn checked C# syntax and type resolution. Synthetic PowerShell fixtures used an isolated `OS=Windows_NT` environment on Linux. That checks update payloads, compatibility, hashes, destinations and resource validation, not Windows process detection or executable launch. No production guard was changed.

The uploaded logs identify the invalid backdrop target exception and failed Windows Settings activations. They do not prove the underlying Windows activation cause, and the fixes have not yet been exercised against those failures on Windows. No sustained stability or performance measurement follows from these portable checks.

## Required Windows acceptance

Follow [CONTROL-CENTER-2.1.0.md](CONTROL-CENTER-2.1.0.md): build the complete Windows package, test in preview and supervised desktop mode, exercise Core restart and stale controls, confirm actual sound/Wi-Fi/Bluetooth/display/power behavior, and repeat glass focus/close interactions. Complete the existing 2.0 desktop and startup regressions and a sustained session before treating this source update as a stable release.

Nexus Core is the per-user service process already started with the desktop. Windows continues to own drivers and its system services. Advanced pairing, BLE, new enterprise/WPA3 profiles, VPN, resolution/scaling and other unsupported settings remain explicitly linked to Windows controls.
