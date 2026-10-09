# Nexus Shell 1.4.2 — a softer desktop

Nexus now has a centered floating taskbar, rounded native windows on Windows 10 and Windows 11, clean window edges, and brief compositor animations when motion is enabled. Desktop, taskbar, Start, Quick Settings, Files, switcher and Sections remain independent windows with shared state.

The user's short VirtualBox test reported much smoother dragging in **1.4.1 while Explorer was still running**. This update keeps that host takeover and recovery code unchanged: only Windows taskbars are hidden, and Explorer's desktop stays active underneath Nexus. Explorer is not stopped by temporary session mode.

## What changes

- Floating taskbar is the default for existing and new settings. Its visible width follows its contents; blank margins show the desktop and pass input through. Maximized windows reserve room above it. Compact and edge-to-edge layouts remain available.
- Nexus Sections, Files, Start, Quick Settings and the switcher share rounded native edges. Bright Windows outer frames are removed; resizing, dragging, maximizing and fullscreen remain available on app windows.
- Sections and Files tuck away through Nexus's taskbar instead of invoking legacy minimized-window rendering. Click their taskbar entries to return. Ordinary Windows applications retain their normal behavior.
- Start, Quick Settings, Files and the switcher have short entrance motion inside a fixed frame. Dock hover/press feedback and Sections navigation remain finite compositor animations. Fast mode and Windows accessibility preferences disable motion.

## Build and try

For the failed 1.4.2 source at commit `af26198808fbc31f92275847cabfdf461093e35f`, apply `Nexus-1.4.2-Build-Fix.patch`, then commit/push for AppVeyor. The taskbar now contains its rounded Border inside a Grid; it no longer inherits WinUI's sealed Border class. SOURCE-PATCH.md gives the exact commands. The complete Source ZIP is an alternative source tree.

```powershell
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

After CI succeeds, extract the complete `Nexus-Shell-1.4.2-win-x64.zip` into a new folder, or use its compatible-runtime Update package. Exit the old Nexus first. Launch **Launch-Nexus-Desktop.bat** or select **Sections → Personalize → Use Nexus for this session** in the preview. Use Balanced to inspect motion; compare Fast if testing window-drag performance.

The supplied AppVeyor log for the original 1.4.2 commit records passed .NET core checks, including all 1,008 placement cases, and PowerShell updater checks. The UI build then failed with CS0509. Source/resource checks and patch reconstruction are verified here; the corrected native Windows build and VM UI acceptance remain pending.

## Guides

- [Source patch](SOURCE-PATCH.md)
- [UI changes and Windows acceptance](docs/UI-POLISH-1.4.2.md)
- [Session/sign-in/recovery](docs/NEXUS-DESKTOP-MODE.md)
- [Startup privileges](docs/STARTUP-PRIVILEGES.md)
- [Build handoff](docs/BUILD-HANDOFF.md)
- [Validation status](docs/VALIDATION.md)

Nexus desktop surfaces currently cover the primary display. Additional-display Nexus surfaces and a replacement Windows notification area remain future work. Saved sign-in settings are separate from temporary session mode.
