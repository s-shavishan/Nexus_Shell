# Nexus Shell 1.5.0 — Windows build handoff

SOURCE-PATCH.md lists routes from the 199-file GitHub main 1.4.2 tree at `1cd328f83d28d606fe5e9da7250ee3d792725cd0`, prepared 1.4.3 and prepared 1.4.4. Apply one patch, then commit/push through the normal workflow. Complete source is an alternative; source ZIPs contain no compiled executable.

```powershell
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

Both CI configurations retain .NET core/platform checks, PowerShell parser/updater checks, native WinUI publishing, XBF/PRI resource generation and package verification. Host/UI/artifact versions match 1.5.0. Dependency pins and host/recovery/startup/updater implementation are preserved.

New core checks cover lifecycle cues without replay, rapid state sequences, preview geometry/corridor/aspect fit and preference persistence. Native checks use only owned test windows for maximize/restore/maximize-from-minimized, graceful WM_CLOSE and DWM registration/update/rejection/disposal. The thumbnail native case reports SKIP when composition is unavailable. A passing native relationship check does not verify rendering over WinUI.

Expected artifacts: `Nexus-Shell-1.5.0-win-x64.zip`, `Nexus-Shell-1.5.0-Update-win-x64.zip` and hashes. Exit the old Nexus before launching the new folder. Complete docs/MOTION-DOCK-1.5.0.md on Windows 10/11 and VirtualBox, plus existing dock/recovery checks. Native build, frame timing, hover/focus, DWM-over-WinUI rendering and memory acceptance remain pending.
