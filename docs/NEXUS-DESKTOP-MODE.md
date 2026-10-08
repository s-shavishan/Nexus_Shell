# Nexus 1.3.0 desktop mode

## What this changes

On a supported Windows Pro account, Nexus becomes the visible desktop at sign-in. Explorer is not started as the desktop. Windows remains the operating system: it still supplies the kernel, services, DWM, storage, networking, audio, input, application execution and security.

| Piece | Owner |
|---|---|
| Wallpaper and desktop shortcuts | Nexus DesktopWindow |
| Taskbar, clock and running apps | Nexus TaskbarWindow |
| Start and app search | Nexus MenuWindow |
| Desktop context menus | Nexus WinUI flyouts |
| Folder browsing and Sections' file pickers | Nexus FilesWindow |
| Alt+Tab and Win+Tab | Nexus SwitcherWindow |
| Study, Explore, apps and workspaces | Nexus Sections window |
| Session supervision and recovery | Nexus.DesktopHost.exe |
| Settings, Control Panel, Task Manager, sign-in, UAC and lock | Windows |
| System services and application windows | Windows |

Each Nexus surface owns its UI and HWND. A shared session coordinates state and saves; a tool window can close without closing the desktop. The warm orange Solstice appearance remains shared across these surfaces.

## Supported route on Windows Pro

This version uses the **per-user Custom User Interface** setting. Microsoft documents it for Windows Pro, Enterprise, Education and IoT Enterprise. It starts the chosen interface instead of Explorer. The implementation checks the edition/build: Windows 10 builds 19041–19043 require revision 1202 or later; newer builds are accepted. This differs from the **Shell Launcher** optional feature, whose supported editions exclude Pro.

The local policy-backed value is:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System
Shell (REG_SZ) = "C:\your-complete-release\Nexus.DesktopHost.exe"
```

Setup changes only this user's `Shell` value and the Nexus-owned `WhiteDreamsNexusShell` Run value. It saves their existing strings/types first. No HKLM shell value or service setting is changed. An existing unrelated custom desktop blocks setup. A managed policy can override or deny this setting; Nexus reports that failure rather than attempting to bypass it.

Sources: [Microsoft CustomShell policy](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-winlogon#customshell), [Shell Launcher editions](https://learn.microsoft.com/en-us/windows/configuration/shell-launcher/).

## Preview and select

1. Build on Windows and retain the complete output folder, including `Nexus.Shell.exe`, `Nexus.DesktopHost.exe`, their DLL/deps/runtimeconfig files, WinUI runtime, PRI/XBF resources and Assets.
2. Run **Nexus.Shell.exe** directly. This previews the independent Nexus desktop alongside Explorer and changes no sign-in policy.
3. Complete the Windows test checklist on a VM/test account. In particular, verify recovery before selecting your everyday account.
4. Open **Sections → Personalize → Use Nexus at sign-in**. Read the version-folder path shown in the Nexus dialog and choose the action.
5. Save work in your apps, then sign out and sign back in. Setup never signs you out automatically.

At sign-in the host starts Nexus with `--desktop-shell` and a session heartbeat token. Desktop mode refuses to start if Explorer is already providing the desktop. This keeps preview/coexistence distinct from replacement. It does not terminate or hide Explorer to simulate replacement.

In desktop mode, the taskbar uses `SPI_SETWORKAREA` without persistent INI changes. It polls foreground/fullscreen placement twice per second and releases the area on clean shutdown. The host resets the primary work area between restarts and before returning to Windows.

Win opens Nexus Start; Win+E opens Files; Win+D toggles app-window minimization; Win+I opens Settings; Win+R/S opens Nexus search; Win+A opens PC controls. Alt+Tab, Win+Tab and Ctrl+Alt+Tab use Nexus's own switcher. Text input is not stored or logged by the keyboard hook. Lock/security shortcuts and other unassigned modifier chords are left with Windows.

## Session and recovery

**Start or desktop context menu → Session…** offers:

- **Restart Nexus:** saves state and restarts only the shell UI. Other apps keep running.
- **Return to Windows:** restores the saved sign-in policy, releases Nexus's work area, closes its surfaces and explicitly starts Explorer for recovery.
- **Sign out:** first asks you to save your work, then delegates sign-out to Windows.

A UI heartbeat is sent from the dispatcher every five seconds. If startup produces no heartbeat within 45 seconds, or a running UI has no heartbeat for 90 seconds, the host ends only its own shell child process. It retries two short failures and then attempts policy recovery and Explorer startup. Five minutes of stable running resets the failure budget. Other apps are not killed.

If the UI is unavailable, run **Restore-Windows-Desktop.bat** from the published folder. Its PowerShell script reads the saved record, restores the exact original per-user value/type and preserves a later external desktop-policy change. It then stops only Nexus executables from the selected folder in this session, resets the primary work area and explicitly starts Explorer. It does not stop other apps. With `-NoStartExplorer`, it repairs sign-in settings only. It does not need a working WinUI app. The backup lives at:

```text
%LOCALAPPDATA%\WhiteDreams\NexusShell\desktop-shell-backup.json
```

For an empty desktop: press **Ctrl+Alt+Delete → Task Manager → Run new task**, run the recovery BAT from its full path, then sign in again. Running `explorer.exe` alone can bring back the current Windows desktop but does not repair the saved sign-in policy. If the version folder is removed before sign-in, the host cannot start to recover automatically; the independent recovery script or Task Manager route is necessary.

A successful restore preserves the existing Nexus notes/settings. Host diagnostics are in `desktop-host.log`; UI diagnostics remain in `nexus.log` in the same Nexus data folder.

## Update an active desktop

Build/extract the new version into a separate folder. Keep the old folder and currently selected host until the new build is checked. Runtime-compatible update packages include **new** Nexus.DesktopHost binaries; the updater verifies hashes and creates a new version folder. It uses a path prompt, without Explorer or a Windows folder picker.

Only one Nexus version can run in this user session. If a Nexus desktop is active, first use **Session → Return to Windows**; this closes the old shell and keeps other apps running. Then preview the new build and select its **Use Nexus at sign-in** action. The recovery record retains the original pre-Nexus desktop setting across version changes. A failed setting update rolls back to the old host and keeps recovery valid. Then save work and sign out. Selecting a new folder does not replace the shell running in the current session.

## Current limits

This is an initial one-display desktop mode, not a policy that blocks other apps/Windows facilities. Secure system UI and necessary administrative tools stay available. Applications can still invoke OS dialogs or Explorer on their own; Nexus routes its own folder, pin, shortcut and picker actions through Nexus Files. Other Windows overlays are not all replaced.

Nexus Files provides browsing/opening, folder creation and selection, with a 1,000-entry bound. Type an available file name/full path in a picker to select a file outside the displayed subset. It does not implement copy/move/delete, drag/drop, shell extensions or all Explorer namespace locations. Namespace-only/UWP shortcuts without a resolvable executable target are rejected in desktop mode; pin a supported executable target instead. The Recycle Bin currently provides total count/size and emptying after a Nexus confirmation; item browsing/restoration is pending. The third-party notification-area host, file-manager parity and additional display surfaces are future work.

Native compilation and Windows sign-in testing remain required; local source checks do not validate these operating-system interactions.
