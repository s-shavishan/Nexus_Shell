# Nexus Shell 3.1.0 — Midnight Glass desktop

Nexus is a native C# / WinUI desktop environment on Windows. This update redesigns the actual desktop surfaces and strengthens their lifecycle behavior. It builds on 3.0.0's supervised Control Center, coordinated motion and durable Nexus notification history.

## Changes in 3.1.0

- **Glass hierarchy:** translucent vertical surfaces, a restrained highlight and a fine gradient edge replace uniform fills. Disabled glass, reduced effects, high contrast and backdrop failure use readable solid surfaces. The Midnight landscape is a small, static native SVG with a cached PNG/solid fallback.
- **Menu bar and dock:** cleaner type/spacing, a Nexus menu icon, larger 40 DIP dock app icons, pin context actions and the existing floating/attached geometry. Compact menu bars now respect 12-hour time. Paint is reused instead of allocating a transparent brush for every window state change.
- **Launchpad:** a stronger heading, integrated search with visible keyboard focus, larger icons, a Pinned filter, direct pin/unpin actions, bounded page indicators and a useful empty state. Pins use the desktop's existing state writer.
- **Control Center:** six distinct navigation tiles, clearer device cards, restrained preference switches, inline failure feedback and activity indication. The progress slot stays fixed and its animation stops when idle. Unchanged network/Bluetooth/power snapshots keep focus and confirmations intact.
- **Notifications:** date groups, All / Warnings & errors filters, an empty state, clearer dismissal controls and saved history. Duplicate paint requests are coalesced and unchanged content is retained.
- **Files and calendar:** consistent traffic-light colors, a separated title strip, lighter sidebar/inspector frames and removal of a redundant full-window paint layer. The desktop date widget is now a keyboard-accessible button.
- **Stability:** responses and preference errors belong to a specific panel opening. Old results cannot replace a newer opening's device state or error. No-op alert dismissal/clear operations do not schedule unnecessary saves. Existing authentication, device epochs and no-replay rules remain in place.

See the [UI preview](docs/UI-3.1.0-preview.png) and [UI changes, validation and VM guide](docs/UI-3.1.0.md). The preview is a design projection using the repository's actual icons and wallpaper; it is not a Windows screenshot. Native Windows compilation, rendering and sustained resource measurements remain acceptance gates.

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

CI publishes `Nexus-Shell-3.1.0-win-x64.zip` and the smaller update ZIP. Keep the complete published folder together, including Nexus.Shell, DesktopHost, Core, Nexus.Runtime and compiled UI resources. See [source patch routes](SOURCE-PATCH.md) and [start here](START-HERE.md).

## Next milestones

The Control Center process is the first panel boundary. Launchpad/dock separation, dock per monitor, expanded file jobs/device controls, Windows app notifications and shell compatibility work remain in [the Session Core roadmap](docs/NEXT-DESKTOP-PACK.md), each with native acceptance gates. No new standalone app is added.
