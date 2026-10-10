# Run Nexus Shell 2.0.0

Extract the full runnable Windows ZIP into a new folder. Keep Nexus.Shell.exe, Nexus.Core.exe, Nexus.DesktopHost.exe, Nexus.Runtime.dll, their dependencies, PRI/XBF resources, Assets and helper files together. Core starts with the desktop and Files windows use isolated worker processes.

- **Launch-Nexus-Desktop.bat:** temporary Nexus desktop/taskbar. Windows taskbars are hidden when Nexus is ready; the Nexus desktop covers the active Windows desktop on the primary display. **Exit Nexus** restores Windows; no sign-in policy is changed.
- **Nexus.Shell.exe / Launch-Nexus.bat:** preview alongside Windows. Sections → Personalize → Use Nexus for this session switches to the temporary session.
- **Restore-Windows-Desktop.bat:** independent current-desktop recovery, with sign-in restoration attempted separately if a prior persistent backup exists.

Sections opens only from its shortcut or Start. Closing Sections or Files does not exit the desktop. Quick Settings opens from the taskbar, desktop menu or Win+A in a managed session. Settings, Control Panel and Task Manager are available there; More controls opens Sections. Choose Fast for the first VM dragging comparison.

Persistent Use Nexus at sign-in is optional, requires a supported edition/build and policy permission, and needs the complete selected folder to stay in place. A denied policy restore does not stop GUI recovery but can leave future sign-in set to Nexus. See NEXUS-DESKTOP-MODE.md.

From a blank desktop, Ctrl+Alt+Delete → Task Manager → Run new task → explorer.exe. Then run the recovery BAT. Data and recovery records remain under %LOCALAPPDATA%\WhiteDreams\NexusShell.

Updates create a new version folder. Exit the old Nexus instance before launching the new one. Source ZIPs require a Windows build; they are not executable updates. Complete MAJOR-2.0.0.md and TEST-DESKTOP-MODE.md on the VM before using this build as a daily desktop.
