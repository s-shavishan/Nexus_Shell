# Nexus Shell 1.8.0 — Supervised startup

A native C# / WinUI desktop environment for Windows. This source update adds a post-sign-in readiness screen, supervised automatic desktop startup, and fixes for the earlier Windows 10 sign-in failures observed in the VM logs. It retains the 1.7 Core and isolated Files foundation.

## What changed

- **Startup readiness:** real Core, saved-workspace, desktop, and dock completion, with a Return to Windows action. No fixed animation delay or fake percentage.
- **Automatic desktop startup:** Personalize offers a supervised launcher after Windows sign-in, independently from shell replacement. Unknown startup commands are preserved, and conflicting Nexus shell configuration is refused.
- **Windows 10 shell compatibility:** optional switcher requests tolerate the observed E_NOTIMPL while native tool-window styling remains in place. Other errors remain visible.
- **Policy recovery:** protected changes use an explicit Windows elevation helper that verifies the originating account. `Restore-Nexus-SignIn.bat` provides independent policy restoration; GUI recovery remains separate.
- **Core logging:** empty readiness-probe disconnects close quietly. Partial frames are still rejected and reported.

- **Core:** owns durable settings writes and launches/supervises Files. It starts with the desktop; it is not a machine-wide Windows service.
- **Files:** the browser and each picker run in separate `Nexus.Shell.exe --files-worker` processes. They do not create a desktop, register a taskbar, or write settings.
- **Picker lifetime:** closing the calling Sections window or the desktop cancels its outstanding picker. A tool failure completes the request with an error instead of leaving the desktop waiting indefinitely.
- **Persistence:** revision checks reject stale commits. Lost acknowledgements are reconciled using the original commit ID before newer snapshots are submitted. Core acknowledges only after an atomic, flushed save.
- **Recovery:** startup deadlines, UI heartbeats, an eight-process Files bound, bounded Core restarts, and Windows cleanup jobs manage component lifetime. External applications opened from Files are intended to outlive the Files cleanup job; a Windows test checks this behavior.
- **Shutdown:** ordered final saves use a deadline. If they cannot be confirmed, Nexus attempts a separate unsaved-session recovery copy and reports its location.
- **Delivery:** full packages and small updates include Core and the shared protocol assembly. Resource measurements include all Nexus components from the selected installation/session.

The Midnight Glass desktop, Files controls, Notes, Calculator, and existing Windows recovery controls remain available. Notes, Calculator, Sections, desktop, and dock still share the main UI process; further isolation is future work.

## Build and validate on Windows

Use the existing .NET 8 SDK and Windows app build tools:

```powershell
python scripts/validate-source.py
dotnet run --project tests/Nexus.Core.Checks/Nexus.Core.Checks.csproj -c Release
dotnet run --project tests/Nexus.Runtime.Checks/Nexus.Runtime.Checks.csproj -c Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

Keep the complete published folder together. It must contain `Nexus.Shell.exe`, `Nexus.DesktopHost.exe`, `Nexus.Core.exe`, `Nexus.Runtime.dll`, their supporting files, and compiled UI resources.

GitHub Actions and AppVeyor include the new runtime checks and package `Nexus-Shell-1.8.0-win-x64.zip` plus the smaller update ZIP.

## Validation status

Shan reported the 1.7 VM manual checks passing. Supplied logs corroborate the latest desktop session, Files launches, Core recovery, and committed settings; they also retain earlier sign-in failures. See [VM evidence](docs/VM-EVIDENCE-2026-10-10.md). The new 1.8 startup and policy paths still require their own Windows acceptance.

Portable state, recovery, and stream-connection checks pass, as do the existing desktop core checks. C# type checking against the pinned Windows/WinUI APIs passes using temporary XAML field declarations. PowerShell parsing and update fixtures pass under PowerShell 7 on Linux.

These checks do not establish Windows launch or desktop stability. Native XAML compilation, package launch, named-pipe identity checks, cleanup jobs, dock behavior, and Windows lifecycle acceptance are still required. See [the startup acceptance sequence](docs/STARTUP-1.8.0.md), [source validation evidence](docs/STARTUP-CHECKS-1.8.0.md), and [the foundation release gates](docs/FOUNDATION-1.7.0.md).

Only one Core writer may own a user's settings profile at a time, including across Windows sessions. A second session refuses to overwrite that profile.

Firmware/Windows boot branding, credential-provider integration, Windows service reductions, and durable file-operation jobs are later milestones. This startup UI appears after Windows authentication; it is not a Windows boot animation or sign-in replacement.
