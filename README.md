# Nexus Shell 1.3.0 — Nexus desktop, Windows underneath

Nexus can be selected as the visible desktop at sign-in on supported **Windows Pro** builds. Windows supplies the operating system and services; Nexus supplies the desktop, taskbar, Start, context menus, file browsing, file pickers and window switcher. Settings, Control Panel, Task Manager and secure system prompts remain available.

This is a **source upgrade**. Full Windows compilation and desktop sign-in acceptance are pending. It has not been installed on your Windows account.

## The desktop foundation

Desktop, taskbar, Start, Files, window switcher and Sections have independent native Windows. Sections opens from its desktop shortcut; it is never embedded in the desktop or opened automatically. Closing it preserves the live desktop and saves shared state.

- **Desktop mode:** a per-user Custom User Interface policy starts `Nexus.DesktopHost.exe` in place of Explorer. The host starts Nexus, watches its UI heartbeat, retries short failures twice and then restores the previous desktop setting.
- **Preview mode:** opening `Nexus.Shell.exe` directly keeps Explorer available for build review. No sign-in setting changes at launch.
- **Nexus Files:** folders, known places, drives, filtering, new folders and file launching. Nexus file/folder selection replaces the Windows common pickers used by Sections. Read work happens off the UI thread, cancellation discards old navigation and lists have a finite viewport and a 1,000-entry bound.
- **Window switcher:** a separate Nexus window for Alt+Tab and Win+Tab, also available from the taskbar. Window activation validates the process identity.
- **Taskbar:** its desktop-mode work area uses user32 directly, without Explorer's appbar manager. Fullscreen apps can cover it; preview mode retains normal appbar coexistence.
- **Show desktop:** Nexus minimizes and restores matching app windows using their native placement, without `Shell.Application`.
- **System access:** PC controls expose Settings, Control Panel, Task Manager, audio, window arrangement and live system data.
- **Session controls:** restart Nexus, return to Windows or sign out. An accidental shell close requests recovery rather than leaving an empty desktop.
- **Appearance:** Solstice's warm pearl/orange treatment, Ember, the existing moods, native vector icons, high contrast and reduced effects remain shared across surfaces.

## Build and configure

On a Windows build host with .NET 8 and the WinUI C# tools:

```powershell
.\scripts\build.ps1 -UseMSBuild -Run
.\scripts\package.ps1
```

The full portable ZIP includes both executables and their dependencies. Preview the build first. After native acceptance, open **Sections → Personalize → Use Nexus at sign-in**. It saves the previous per-user desktop and Nexus startup values before selecting this version. Save work and sign out when ready; Nexus never signs out automatically during setup.

The recovery BAT/PowerShell script works independently of the WinUI UI. Keep the complete version folder in place while selected for sign-in.

## Shortcuts

| Shortcut | Desktop mode |
|---|---|
| Win | Nexus Start |
| Win+E | Nexus Files |
| Win+D | Minimize/restore app windows |
| Win+I | Windows Settings |
| Win+R / Win+S | Nexus app search |
| Win+A | Nexus PC controls |
| Alt+Tab / Alt+Shift+Tab | Cycle Nexus window switcher; release Alt to select |
| Win+Tab / Ctrl+Alt+Tab | Nexus window overview; Enter selects, Esc cancels |
| Win+L / Ctrl+Alt+Delete | Windows secure session controls |
| Ctrl+Alt+Space, when enabled | Nexus Start search |

Preview mode leaves Windows' global shell shortcuts with Windows. The taskbar's window-switch button remains available.

## Scope and validation

The sign-in route is **Custom User Interface**, supported on Pro. It does not use the Enterprise-only Shell Launcher optional feature. This is a desktop replacement, not an access-restriction policy: other apps and OS facilities can still display their own UI. Nexus does not disable services or suppress secure prompts.

This foundation covers one desktop/taskbar display. The Recycle Bin view shows count/size and supports emptying after a Nexus confirmation; individual item browsing/restoration is not implemented. Files does not yet implement copy/move/delete, drag/drop or shell extensions. Windows namespace-only/UWP shortcuts without a resolvable executable target need a supported app target. A replacement for other applications' Windows notification-area icons and all Windows overlays is not implemented.

Core checks execute locally. All app C# compiles against the pinned WinUI/Windows API references with XAML field stand-ins; desktop-host C# compiles against .NET 8 APIs. Native XAML generation, sign-in, keyboard hooks, fullscreen, DPI and runtime appearance still require Windows testing.

- [Setup, recovery and architecture](docs/NEXUS-DESKTOP-MODE.md)
- [Windows acceptance](docs/TEST-DESKTOP-MODE.md)
- [Evidence and limits](docs/VALIDATION.md)
- [Windows build handoff](docs/BUILD-HANDOFF.md)
- [Desktop illustration](docs/Nexus-1.3.0-Desktop-reference.png)
- [Files and Start illustration](docs/Nexus-1.3.0-Files-and-Start-reference.png)

Illustrations use the source palette, wallpaper and icons with sample content. They are not Windows screenshots. Earlier design references and version notes remain in `docs` as history.
