# Nexus Shell 2.1.1 — Launchpad and sign-in setup

Nexus is a native C# / WinUI desktop environment on Windows. The existing Core-backed Control Center remains included. This focused update routes a standalone Windows key to Nexus Launchpad in managed sessions, exposes sign-in policy conflicts before setup, and corrects the clipped calendar date.

## Changes in 2.1.1

- **Start key:** standalone left/right Windows keys toggle Nexus Launchpad in managed desktop mode. Native combinations pass through. Injection denial preserves the physical key-up; the dock button remains available.
- **Sign-in setup:** active policy and recovery ownership are shown before replacement. Windows command casing/outer whitespace no longer create a false conflict. Unmatched policies and their recovery records are retained.
- **Read-only diagnosis:** `Inspect-Nexus-Startup.bat` reports shell policy, startup and recovery values. Startup alongside Windows and replacement of Explorer after authentication are explained separately.
- **Calendar:** a two-digit day fits its day column; the full date is available to accessibility.

Follow [Start/sign-in acceptance](docs/START-AND-SIGNIN-2.1.1.md). The uploaded 2.1 logs establish a sign-in policy mismatch but do not include its current value. The VM conflict needs that value before it can be resolved safely. Native input masking and rendering still need Windows acceptance.

## Included Core settings update

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

CI publishes `Nexus-Shell-2.1.1-win-x64.zip` and the smaller update ZIP. Keep the complete published folder together, including Nexus.Shell, DesktopHost, Core, Nexus.Runtime and compiled UI resources. See [source patch routes](SOURCE-PATCH.md) and [start here](START-HERE.md).

## Validation

The uploaded 2.1 sample contains two sign-in policy refusals, healthy Core revision loads and an intentional Return-to-Windows exit (20); it contains no new unhandled UI exception. Settings and backup parse at revisions 8 and 7. This short sample does not establish sustained stability. Source/API and portable checks are recorded in [2.1.1 validation evidence](docs/START-CHECKS-2.1.1.md). Native XAML compilation, input delivery, Explorer-independent sign-in, glass rendering and device behavior require [Windows VM acceptance](docs/START-AND-SIGNIN-2.1.1.md).

The [proposed Nexus 3.0 desktop pack](docs/NEXT-DESKTOP-PACK.md) is a roadmap, not included functionality. Its first milestone is independently recoverable shell panels.

## Session foundation

Core starts per user with Nexus and owns revisioned, atomic preferences plus the hardware settings service. Files browsers/pickers run in isolated `--files-worker` processes, with cancellation, deadlines, heartbeat supervision and a bounded process count. Lost save acknowledgements reconcile by commit ID; hardware writes deliberately use no such retry. Final-save failures attempt an independent recovery copy. Only one writer may own a user's settings profile at a time.

Desktop, dock, Sections, Notes and Calculator still share the main UI process. Windows continues to own boot, authentication, drivers, security and native application services. Boot/sign-in replacement, blanket Windows service removal and durable file-operation jobs are outside this release. The readiness UI starts after Windows authentication.
