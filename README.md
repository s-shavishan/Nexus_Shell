# Nexus Shell 1.4.1 — session takeover repair

Nexus 1.4.0 is responsive with the Windows taskbar present, but the user's VirtualBox session still lags severely after takeover, including in Fast mode. This update narrows takeover: the host hides only Explorer's primary/secondary taskbars. It leaves Explorer's Progman/WorkerW desktop windows active and positions the opaque Nexus desktop above them, below application windows. The Windows desktop layer remains covered on Nexus's primary display.

Exit Nexus restores the original taskbar visibility and work area. New session journals own taskbars only; recovery still supports interrupted 1.4.0 journals containing desktop windows. Windows services, other apps and saved sign-in preferences retain their existing lifecycle.

This is a focused candidate repair. Smooth dragging in VirtualBox must be confirmed with a Windows build and the same windows/resolution used for 1.4.0. It does not establish that hiding Explorer desktop windows was the sole lag cause.

## Build and try

Apply `Nexus-1.4.0-to-1.4.1.patch` to main commit `a9f813df250f8c5b8f4a3fdadb05804fae867025`, commit/push for AppVeyor, or build on Windows:

```powershell
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

Extract the complete `Nexus-Shell-1.4.1-win-x64.zip` build to a new folder. Return fully to Windows and exit the old Nexus before launching the new **Launch-Nexus-Desktop.bat**, or choose **Sections → Personalize → Use Nexus for this session** in its preview. In Quick Settings choose Fast, close panels, and drag the same Notepad window for 20–30 seconds.

Quick Settings, Files, Sections, Start, the switcher, cached wallpapers and the 1.4.0 work-area/polling improvements remain available. Nexus still supplies the desktop/taskbar on the primary display. Additional-display Nexus surfaces and a notification-area replacement remain future work.

## Guides

- [Apply the source patch](SOURCE-PATCH.md)
- [Session comparison and acceptance](docs/SESSION-TAKEOVER-1.4.1.md)
- [Session/sign-in/recovery](docs/NEXUS-DESKTOP-MODE.md)
- [Build handoff](docs/BUILD-HANDOFF.md)
- [Validation status](docs/VALIDATION.md)

The 1.4.0 baseline succeeded in AppVeyor build 54863707. The 1.4.1 Windows build, native layer ordering and VM smoothness remain pending. Persistent sign-in is separate from temporary session mode and still follows Windows policy permissions.
