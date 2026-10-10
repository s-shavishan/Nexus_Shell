# Major desktop source validation — 2.0.0

Validation date: 10 October 2026. These are portable/source checks on Linux, not Windows executable acceptance.

| Check | Result |
| --- | --- |
| Source XML, runtime templates, handlers, resources, native icons, ownership, build wiring and lock version | Pass: 109 project files, 140 named elements, 70 handlers |
| Existing managed desktop checks | Pass, including 1008 monitor/DPI/layout cases and 25 startup checks |
| New major desktop checks | Pass: 65 assertions covering minimized arrangement ownership/recovery, durable journal and 1.8 compatibility, notification bounds/read/dedup/concurrency, Launchpad paging/filtering and top-bar/panel geometry |
| Runtime portable checks | Pass: 101 assertions, including 30 simulated Files crashes, durable revision writes, uncertain saves, cancellation and framed stream connections |
| Runtime, Core, DesktopHost and both check projects | Direct Roslyn compile passes with zero warnings/errors |
| All Shell C# against Windows/WinUI projections | Pass: zero errors; 19 reference-unification/temporary-XAML-field warnings |
| PowerShell parsing and update fixtures | Pass under PowerShell 7.4.13 using synthetic temp payloads; Windows-only environment guard mocked within the fixture subprocess |
| Whitespace | git diff --check passes |

## Method and limits

The C# compiler used .NET SDK 8.0.419 reference assemblies and the project's restored Windows App SDK/Windows SDK projections. Temporary named-field and InitializeComponent declarations replaced generated XAML code solely for API/type checking. Six reference-unification and thirteen unused temporary-field warnings belong to this check setup. This does not produce a distributable WinUI build or validate XBF/PRI resources.

The source validator ran without its optional tree-sitter mode, whose Python dependency is unavailable here. C# syntax and type resolution were checked by Roslyn instead.

Runtime checks ran with `--no-native-ipc`: production framed protocol connections were exercised over a substitute stream, while actual OS named-pipe identity and Windows job/external-app breakaway checks were explicitly skipped. Windows CI runs the complete check suite without that flag.

The PowerShell fixture's isolated `OS=Windows_NT` environment setting permits synthetic payload checks on Linux; it does not verify Windows process detection, executable launch or production installation. No production guard was changed.

## Required Windows acceptance

Native XAML/PRI/XBF compilation, the 2.0 package launch, Windows Settings activation without Explorer, actual minimized-caption arrangement and restoration, backdrop support/fallback, maximized/snap corner rendering, keyboard tooltips, Launchpad input, panel/device controls, rapid dock/window transitions, fullscreen, multi-display/DPI behavior and sustained use remain unverified here. Follow [MAJOR-2.0.0.md](MAJOR-2.0.0.md), including previous startup/foundation regressions.

The inbox holds Nexus alerts for the current session, not Windows app toasts. External apps retain native Windows animations. No measured performance or stability guarantee follows from the portable checks.

See [the captured check transcript](MAJOR-CHECKS-2.0.0.txt).
