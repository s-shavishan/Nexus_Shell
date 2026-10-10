# Nexus Shell 3.2.0 — Responsive desktop controls

Nexus is a native C# / WinUI desktop environment on Windows. This update continues from 3.1.0 with direct menu controls, faster panel interaction, revised glass rendering and useful Launchpad features. Its supervised Control Center, coordinated motion and durable notification history remain part of the Session Core foundation.

## Changes in 3.2.0

- **Direct menu controls:** Network, Sound, Bluetooth and Display open compact Nexus panels beneath the menu bar. All controls expands the same supervised panel into the full Control Center.
- **One Launchpad:** duplicate app-search buttons and the separate search layout are removed. Launchpad keeps its embedded search, Pinned filter and paging. Legacy app-search dispatches focus its search field. The standalone Windows key opens Launchpad in the managed desktop; native Windows combinations still pass through.
- **Responsiveness:** transient panels dismiss immediately; motion stays inside their painted frame. Native content-window minimize commands run immediately. Launchpad warms after desktop startup, reuses unchanged tiles and shared vector sources, and briefly coalesces typed filtering.
- **Control Center updates:** an authenticated, cancellable revision wait wakes the worker when its state changes. Its bounded idle reply keeps supervision alive. Device polling remains limited to an open panel.
- **Glass recovery:** the acrylic target initializes the Windows system dispatcher, gets its palette before attachment, checks attachment success and uses a fully opaque matching fallback. Thin acrylic and restrained highlights replace the broad gray gradient. Windows transparency and accessibility policy remain respected.
- **Desktop features:** a bounded, persistent Recent apps category, installed-app refresh, asynchronous Windows icons, and an independent Interface animations switch. Turning animations off keeps Nexus glass enabled. Recent history follows the existing privacy preference.

See [changes and Windows VM acceptance](docs/RESPONSIVENESS-3.2.0.md). The older [3.1 design projection](docs/UI-3.1.0-preview.png) remains a design reference with example content. Native Windows rendering and sustained resource/timing measurements must be verified on the VM.

## Integrated controls

Core-backed sound, display, network, Bluetooth, power and Nexus preferences remain included, with separate device lanes, fresh service epochs, bounded requests and explicit unsupported capabilities.

| Section | Integrated controls | Remaining advanced controls |
| --- | --- | --- |
| Sound | Master and per-session volume/mute; current output state | Selecting the default output, input/microphone and enhancements |
| Network | Ethernet/IP/gateway/DNS status; Wi-Fi radio, scan, saved connections, new open/WPA2-Personal AES connections; forget Nexus-created user profiles | New WPA3/enterprise profiles, VPN, proxy and IP configuration |
| Bluetooth | Classic radios, cached/scanned devices, connection/paired state, discoverability | Radio power, pairing, BLE management |
| Display | Active resolutions/refresh rates; hardware brightness when exposed by the monitor | Resolution/scaling changes, night light and projection |
| Power | Battery/AC status, existing power-plan selection, lock | Sleep timers and plan editing |
| Desktop | Nexus wallpaper, dock, effects, widgets, clock and quiet-alert preferences | Windows-wide personalization and app-toast capture |

The earlier Launchpad, dock/window overview, supervised startup, independent Files/picker process, Start-key routing, sign-in policy inspection and recovery helpers remain included. Glass uses supported Windows acrylic blur/tint with a solid fallback. The existing foreign sign-in policy needs diagnostic output before ownership can be reconciled; it is preserved.

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

CI publishes `Nexus-Shell-3.2.0-win-x64.zip` and the smaller update ZIP. Keep the complete published folder together, including Nexus.Shell, DesktopHost, Core, Nexus.Runtime and compiled UI resources. See [source patch routes](SOURCE-PATCH.md) and [start here](START-HERE.md).

## Next milestones

The Control Center process is the first panel boundary. Launchpad/dock separation, dock per monitor, expanded file jobs/device controls, Windows app notifications and shell compatibility work remain in [the Session Core roadmap](docs/NEXT-DESKTOP-PACK.md), each with native acceptance gates. No new standalone app is added.
