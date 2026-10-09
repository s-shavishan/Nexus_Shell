# Nexus desktop sessions — 1.4.1

## Use Nexus now and restore Windows on exit

Extract the full runnable folder. Close any existing Nexus preview, then run **Launch-Nexus-Desktop.bat**. The launcher starts `Nexus.DesktopHost.exe --nexus-session`. From preview, **Sections → Personalize → Use Nexus for this session…** closes the preview and starts the same supervised desktop session.

After Nexus's desktop/taskbar and keyboard hook are ready, the host hides only matching primary and secondary Windows taskbar windows owned by the real Explorer process in the current interactive session. Explorer's Progman/WorkerW desktop windows remain active behind the opaque Nexus desktop on the primary display; Nexus is positioned above them and below application windows. Other applications and Windows services keep running. Explorer remains in the background for reversible restoration; this is not a service shutdown or Explorer process termination.

**Start → Exit Nexus**, desktop right-click → Exit Nexus, or **Personalize → Return to Windows** closes all Nexus surfaces and restores the saved Windows visibility and primary work area. Closing Sections or Files alone keeps the desktop active. No sign-in policy is written by temporary launch or exit.

The original working area and taskbar visibility are stored under `%LOCALAPPDATA%\WhiteDreams\NexusShell\desktop-session-<session-id>.json` before hiding any surface. Unwritable recovery data rejects takeover while Windows stays visible. Recovery validates Explorer ownership, window handle, process ID and class before changing visibility; dead/reused windows are skipped. Originally hidden taskbars remain hidden. Recovery also accepts old Format 1 journals containing Progman/WorkerW windows from interrupted 1.4.0 sessions. The completed session record is removed after restoration.

## Host and recovery

The host starts only the matching Nexus executable in its folder and supervises a private UI heartbeat. First-heartbeat timeout is 45 seconds; an established UI timeout is 90 seconds. Two short crash retries are permitted before Windows recovery. The host terminates only its spawned UI, preserving other apps. Between restarts it releases Windows visibility and the work area.

If the host ends while the UI remains responsive, the UI notices on its five-second tick, starts an independent recovery helper and exits. The helper waits for the UI to finish closing before restoring the session record, so UI cleanup cannot undo the recovered work area. If both host and UI are terminated together, use the recovery BAT manually.

Run **Restore-Windows-Desktop.bat** to stop matching Nexus processes and restore the desktop from outside WinUI. Registry policy failure is reported, but surface restoration, work-area repair and starting Explorer when absent are still attempted. The script uses the 1.3.1 native helper when available and has an independent PowerShell/Win32 fallback, including support for older hosts that lack the new recovery argument. Even a failed Add-Type step cannot skip attempting Explorer startup.

From a blank desktop: **Ctrl+Alt+Delete → Task Manager → Run new task → explorer.exe**. Then run the recovery BAT from your complete Nexus folder. Starting Explorer alone does not repair an earlier persistent sign-in setting.

## Persistent desktop at sign-in

**Use Nexus at sign-in** is separate from temporary session mode. It uses per-user Windows Custom User Interface policy on supported Pro, Enterprise, Education and IoT Enterprise builds. The existing eligibility checks and numeric registry backup format are preserved. Windows Home is not supported for this policy route.

The previous Shell value/type and Nexus Run entry are saved before policy writes. A foreign desktop setting is never overwritten. Changing or restoring a protected policy still requires Windows permission; session mode does not bypass that restriction.

In a persistent Nexus session, **Start → Session…** offers restart, return to Windows or sign out. Return to Windows attempts sign-in restoration and GUI restoration independently. If `Policies\System` access is denied, Windows desktop recovery proceeds for the current session and the sign-in backup is retained. Future sign-in can remain configured for Nexus until the policy is successfully restored.

Keep the entire selected published folder in place. Return to Windows before launching a new version. Select **Use this version at sign-in** in the new version only if you want persistent sign-in; the original pre-Nexus backup survives upgrades.

## Integration and current scope

Desktop, taskbar, Start/context menus, Quick Settings, Files, switcher and Sections remain separate native windows with shared state/theme. Temporary and persistent modes use the Nexus taskbar's primary working area and keyboard/window switcher. Preview retains Explorer coexistence.

| Shortcut | Managed Nexus desktop |
|---|---|
| Win / Ctrl+Esc | Nexus Start |
| Win+E | Nexus Files |
| Win+D | Nexus Show desktop |
| Win+I | Windows Settings |
| Win+R / Win+S | Nexus search |
| Win+A | Nexus Quick Settings |
| Alt+Tab / Alt+Shift+Tab | Nexus window cycling |
| Win+Tab / Ctrl+Alt+Tab | Nexus window overview |
| Ctrl+Shift+Esc | Windows Task Manager |
| Win+L / Ctrl+Alt+Delete | Windows secure session controls |

The Nexus desktop/taskbar currently occupy the primary display only. Windows taskbars on other displays are hidden during takeover; additional Nexus taskbars are future work. Other applications, secure prompts and Windows facilities can still show UI. Replacement notification-area icons, every Windows overlay, full file operations and individual Recycle Bin restoration are not implemented. This is not an access-restriction policy.

Actual taskbar visibility, native work-area changes, auto-hide, Explorer restart, lock/unlock, display/DPI changes and host failure require the VM checks in TEST-DESKTOP-MODE.md. Prepared source/API tests cannot prove those Windows behaviors.
