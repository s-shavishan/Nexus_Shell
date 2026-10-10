# Nexus Shell 2.0.0 — Major desktop update

Nexus is a native C# / WinUI desktop environment on Windows. This release rebuilds everyday desktop surfaces and addresses the Settings, minimized-caption, corner and accelerator-tooltip defects in the supplied Windows 10 VM screenshot. It retains supervised startup, the Core settings owner, isolated Files workers and independent Windows recovery.

## Desktop changes

- **Liquid glass styling:** per-window Windows desktop acrylic, translucent surfaces and restrained highlights for Files, Sections, utilities, Launchpad, dock, menu bar and panels. Windows transparency, graphics support and accessibility can select a solid fallback. This uses Windows blur/tint; it does not reproduce Apple's refraction shader.
- **Menu bar:** an independent, macOS-inspired top bar with Nexus menus, network, sound, search, Control Center, notifications and date. Managed sessions reserve its height for maximized windows; fullscreen applications hide the shell overlays. Menus control Nexus, not arbitrary Windows applications.
- **Launchpad:** Start opens a paged app grid with category filters and keyboard navigation. Search remains a separate entry. Only 24 app tiles are realized per page.
- **Control Center:** network/Bluetooth settings, actual output volume/mute, brightness when supported, display projection, night light, lock, Task Manager, Windows Settings and Control Panel. Its dock icon is removed.
- **Notifications:** an 80-entry Nexus inbox with unread badge, dismissal, clear and quiet mode. These are session-local Nexus alerts; Windows app toast capture and cross-session history are future work.
- **Window fixes:** Settings uses the Windows URI launcher. Native minimized captions are moved off screen during supervised takeover, with journaled recovery. Content windows use matching 14-DIP corners; maximized windows have square edges. Automatic Esc/Ctrl+K hover tips are hidden without disabling the shortcuts.
- **Motion:** retuned dock cues and hover, animated dock hide/reveal and finite Nexus minimize/restore transitions. New actions cancel stale transitions. Reduced-motion preferences skip these effects; other applications use Windows' native animation.
- **Menus:** desktop and tray right-click menus no longer offer Exit Nexus. Explicit recovery remains available from the top Nexus menu and the shipped restore helpers.

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

CI publishes `Nexus-Shell-2.0.0-win-x64.zip` and the smaller update ZIP. Keep the complete published folder together, including Nexus.Shell, DesktopHost, Core, Nexus.Runtime and compiled UI resources. See [source patch routes](SOURCE-PATCH.md) and [start here](START-HERE.md).

## Validation

Shan reports the earlier 1.8 tests passing; the screenshot shows additional UI/native defects. Source/API and portable checks for 2.0 are recorded in [validation evidence](docs/MAJOR-CHECKS-2.0.0.md). Native XAML compilation, Windows launch, blur quality, animation timing, Settings activation, native recovery and a sustained-use test still require [the Windows acceptance sequence](docs/MAJOR-2.0.0.md). There is no measured performance claim from source checks.

## Session foundation

Core starts per user with Nexus and owns revisioned, atomic settings writes. Files browsers/pickers run in isolated `--files-worker` processes, with cancellation, deadlines, heartbeat supervision and a bounded process count. Lost save acknowledgements reconcile by commit ID; final-save failures attempt an independent recovery copy. Only one writer may own a user's settings profile at a time.

Desktop, dock, Sections, Notes and Calculator still share the main UI process. Windows continues to own boot, authentication, drivers, security and native application services. Boot/sign-in replacement, blanket Windows service removal and durable file-operation jobs are outside this release. The readiness UI starts after Windows authentication.
