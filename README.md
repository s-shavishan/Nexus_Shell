# Nexus Shell 1.4.0 — desktop performance and Quick Settings

This upgrade addresses likely causes of severe window-drag lag after **Use Nexus for this session**. It also adds a separate Quick Settings window, keeping desktop, taskbar, Start, Files and Sections independent.

The taskbar no longer repeatedly repairs Windows' working area on its half-second stacking timer. It reserves space when its geometry changes and notifies other applications asynchronously. The session host validates Explorer's executable once per retained process handle, rather than opening its modules on each maintenance pass. The six existing ribbon wallpapers use checked-in image caches instead of large live vector shapes.

Open **Quick settings** from the taskbar, desktop context menu or **Win+A** in a managed Nexus session. Adjust real output volume/mute, hardware brightness on supported DDC/CI displays, visual quality and compact taskbar size. Settings, Control Panel, Task Manager and more PC controls are directly available. Network/power status is read only; unsupported brightness remains disabled. Hardware reads/writes run away from the UI thread, slider changes are coalesced, and hidden panels stop polling.

**Fast** uses a simple background with glass and custom motion disabled; try this first in your VM. **Balanced** retains the cached wallpaper and motion with glass off. **Full** allows the existing glass effects where available. These profiles use existing saved preferences and preserve your content and chosen mood.

## Build and try

Apply the verified 1.3.1→1.4.0 patch, commit/push for AppVeyor, or build on Windows:

```powershell
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

Run **Launch-Nexus-Desktop.bat** from the complete published `Nexus-Shell-1.4.0-win-x64` folder, or open preview and choose **Sections → Personalize → Use Nexus for this session…**. Exit or Quick Settings → Return to Windows restores the saved Windows desktop/taskbars and working area. Windows services and other apps keep running.

The source targets verified commit **b493314f5b9118c19077a5ea3be6c1996ba191d0**. Its 1.3.1 AppVeyor build succeeded and session mode works in the user's VM. **1.4.0 requires a real Windows build and VM performance comparison.** Local core, platform-analyzer, C# API and source checks pass; no Windows drag timings have been measured here.

## Guides

- [Apply the source patch](SOURCE-PATCH.md)
- [Quick Settings and performance checks](docs/QUICK-SETTINGS-AND-PERFORMANCE.md)
- [Session/sign-in/recovery](docs/NEXUS-DESKTOP-MODE.md)
- [Windows acceptance](docs/TEST-DESKTOP-MODE.md)
- [Build handoff](docs/BUILD-HANDOFF.md)
- [Validation evidence](docs/VALIDATION.md)

Persistent **Use Nexus at sign-in** remains a separate Windows policy option requiring registry permission. A denied persistent-policy restore does not prevent current-session Windows recovery; its backup is retained. Nexus currently supplies desktop/taskbar on the primary display. Additional-display bars, notification-area replacement and broader file operations remain future work. The orange/Solstice/Ember styling and historical design references are retained.
