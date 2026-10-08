# Nexus Shell 1.3.1 — Nexus for the session, Windows on exit

Run **Launch-Nexus-Desktop.bat** from the full published folder to use the Nexus desktop and taskbar for the current session. Alternatively, preview `Nexus.Shell.exe`, then choose **Sections → Personalize → Use Nexus for this session…**. Exit Nexus to restore the Windows desktop and taskbars. This route makes no sign-in registry changes.

Desktop, taskbar, Start, context menus, Files, window switcher and Sections remain independent native windows. Sections opens from its desktop shortcut or Start; it is never embedded into the desktop or opened automatically. Closing Sections preserves the desktop and shared state.

## What changed

- The supervising host waits for Nexus's first UI heartbeat before hiding Explorer's desktop and primary/secondary taskbar windows. It leaves Windows services, Explorer's background process and other applications running.
- Original surface visibility and the primary working area are saved before takeover. Normal exit restores them; a lost UI is restarted under a bounded budget, then Windows recovery is requested. If the host dies while the UI remains responsive, the UI launches an independent recovery helper and exits.
- Registry policy errors no longer prevent surface restoration, working-area repair or starting Explorer when needed. The previous sign-in backup is retained if its protected setting cannot be restored.
- Temporary sessions use Nexus's existing taskbar work area, Start/search, Files/pickers and window switcher. Ctrl+Esc opens Nexus Start; Ctrl+Shift+Esc, Win+L and Windows security controls remain available.
- Both full and compatible-runtime update packages include the new desktop launcher and refreshed recovery tools.

This upgrade is prepared source. The verified 1.3.0 baseline passed AppVeyor and was reported working in the user's VM. **1.3.1 still needs a real Windows build and VM acceptance.** Core behavior and C# API checks pass locally; this Linux environment cannot execute the native desktop.

## Build and try

```powershell
.\scriptsuild.ps1 -UseMSBuild
.\scripts\package.ps1
```

Use the complete published `Nexus-Shell-1.3.1-win-x64` folder. Start `Launch-Nexus-Desktop.bat` with any Nexus preview closed, or use the in-preview session button. Source ZIPs are not runnable releases.

`Nexus.Shell.exe` remains preview mode alongside Windows. The separate **Use Nexus at sign-in** option still uses Windows Custom User Interface policy on supported Pro/Enterprise/Education builds and requires permission to modify that policy. It is optional for temporary sessions.

## Recovery and scope

Use **Start → Exit Nexus**, the desktop context menu's Exit Nexus, or **Personalize → Return to Windows** in a temporary session. In a persistent desktop session use **Session → Return to Windows**. The independent **Restore-Windows-Desktop.bat** also restores the current desktop even if policy access fails. From an empty desktop, Ctrl+Alt+Delete → Task Manager → Run new task → `explorer.exe` remains the immediate fallback.

Nexus supplies its own primary-display desktop and taskbar. It hides matching Explorer taskbars on other displays, but Nexus taskbars on additional displays are not implemented. Other applications, secure prompts and Windows facilities can still display their own windows; this is not a lockdown policy. File operations, notification-area replacement and system overlay coverage keep their existing 1.3.0 limits.

- [Session, sign-in and recovery guide](docs/NEXUS-DESKTOP-MODE.md)
- [Windows acceptance checks](docs/TEST-DESKTOP-MODE.md)
- [Source patch instructions](SOURCE-PATCH.md)
- [Windows build handoff](docs/BUILD-HANDOFF.md)
- [Validation evidence and limits](docs/VALIDATION.md)

The existing orange/Solstice/Ember design assets and historical reference illustrations are preserved.
