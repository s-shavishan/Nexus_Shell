# Nexus Shell 2.1.0 — Core settings and Control Center

Nexus is a native C# / WinUI desktop environment on Windows. This update addresses the five desktop crashes in the supplied Windows 10 logs and moves everyday device controls into the existing per-user Nexus Core service. Control Center is a desktop surface; this release adds no standalone application.

## Changes in 2.1

- **Glass lifecycle:** the failing default-configuration callback no longer calls back into the detached native target. Connection, tint, configuration and cleanup errors use a solid fallback. Queued callbacks check their backdrop owner before touching a window.
- **Control Center:** six accessible sections with glass cards, a fixed navigation/header/footer, scrolling content, inline device messages and monitor-bounded placement.
- **Core settings service:** typed hardware snapshots and allowlisted commands, separate request lanes, an eight-second device deadline, fresh epochs after restart, and no automatic replay of uncertain hardware writes.
- **Settings routes:** root Settings, Sound, Network, Bluetooth, Display, Power and personalization shortcuts open the corresponding Nexus controls. Advanced Windows controls remain an explicit escape for unsupported features.
- **Desktop preferences:** wallpaper palettes, glass, reduced effects, dock layout/previews, clock/widgets and quiet Nexus alerts save through Core. Quiet alerts now survive restart.
- **Stability:** coalesced final slider intents, bounded command buffers, cancellation before queued device mutations, polling only while open, password-entry protection from polling, and one diagnostic for the expected optional switcher API fallback.

| Section | Integrated controls | Remaining advanced controls |
| --- | --- | --- |
| Sound | Master and per-session volume/mute; current output state | Selecting the default output, input/microphone and enhancements |
| Network | Ethernet/IP/gateway/DNS status; Wi-Fi radio, scan, saved connections, new open/WPA2-Personal AES connections; forget Nexus-created user profiles | New WPA3/enterprise profiles, VPN, proxy and IP configuration |
| Bluetooth | Classic radios, cached/scanned devices, connection/paired state, discoverability | Radio power, pairing, BLE management |
| Display | Active resolutions/refresh rates; hardware brightness when exposed by the monitor | Resolution/scaling changes, night light and projection |
| Power | Battery/AC status, existing power-plan selection, lock | Sleep timers and plan editing |
| Desktop | Nexus wallpaper, dock, effects, widgets, clock and quiet-alert preferences | Windows-wide personalization and app-toast capture |

The earlier Launchpad, menu bar, dock/native window behavior, Nexus notification inbox, supervised startup and Files isolation remain included. See [2.0 desktop acceptance](docs/MAJOR-2.0.0.md) for those regressions. Glass uses Windows acrylic blur/tint and may fall back on unsupported graphics or accessibility settings.

## Build on Windows

Use .NET 8 and the Windows app build tools. Source ZIPs contain no compiled executable.

```powershell
python scripts/validate-source.py
dotnet run --project tests/Nexus.Core.Checks/Nexus.Core.Checks.csproj -c Release
dotnet run --project tests/Nexus.Runtime.Checks/Nexus.Runtime.Checks.csproj -c Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

CI publishes `Nexus-Shell-2.1.0-win-x64.zip` and the smaller update ZIP. Keep the complete published folder together, including Nexus.Shell, DesktopHost, Core, Nexus.Runtime and compiled UI resources. See [source patch routes](SOURCE-PATCH.md) and [start here](START-HERE.md).

## Validation

The uploaded 2.0 logs show five backdrop callback exceptions and matching native exits, six failed Windows Settings activations, and healthy durable Core revisions. The valid settings/backup files provide no evidence of settings corruption; the logs do not establish the cause of Windows' activation failure. Source/API and portable checks are recorded in [2.1 validation evidence](docs/CONTROL-CHECKS-2.1.0.md). Native XAML compilation, device APIs, Explorer-independent use, glass rendering and sustained stability still require [Windows VM acceptance](docs/CONTROL-CENTER-2.1.0.md).

## Session foundation

Core starts per user with Nexus and owns revisioned, atomic preferences plus the hardware settings service. Files browsers/pickers run in isolated `--files-worker` processes, with cancellation, deadlines, heartbeat supervision and a bounded process count. Lost save acknowledgements reconcile by commit ID; hardware writes deliberately use no such retry. Final-save failures attempt an independent recovery copy. Only one writer may own a user's settings profile at a time.

Desktop, dock, Sections, Notes and Calculator still share the main UI process. Windows continues to own boot, authentication, drivers, security and native application services. Boot/sign-in replacement, blanket Windows service removal and durable file-operation jobs are outside this release. The readiness UI starts after Windows authentication.
