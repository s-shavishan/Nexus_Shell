# Run Nexus Shell 1.3.1

Extract the full runnable Windows ZIP into a new folder. Keep both executables, their dependencies, PRI/XBF resources, Assets and helper files together.

- **Launch-Nexus-Desktop.bat:** temporary Nexus desktop/taskbar. Windows surfaces are hidden when Nexus is ready. **Exit Nexus** restores Windows; no sign-in policy is changed.
- **Nexus.Shell.exe / Launch-Nexus.bat:** preview alongside Windows. Sections → Personalize → Use Nexus for this session switches to the temporary session.
- **Restore-Windows-Desktop.bat:** independent current-desktop recovery, with sign-in restoration attempted separately if a prior persistent backup exists.

Sections opens only from its shortcut or Start. Closing Sections or Files does not exit the desktop. Settings, Control Panel and Task Manager remain available from PC controls.

Persistent Use Nexus at sign-in is optional, requires a supported edition/build and policy permission, and needs the complete selected folder to stay in place. A denied policy restore does not stop GUI recovery but can leave future sign-in set to Nexus. See NEXUS-DESKTOP-MODE.md.

From a blank desktop, Ctrl+Alt+Delete → Task Manager → Run new task → explorer.exe. Then run the recovery BAT. Data and recovery records remain under %LOCALAPPDATA%\WhiteDreams\NexusShell.

Updates create a new version folder. Exit the old Nexus instance before launching the new one. Source ZIPs require a Windows build; they are not executable updates. Complete TEST-DESKTOP-MODE.md on the VM.
