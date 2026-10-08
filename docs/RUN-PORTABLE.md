# Run Nexus Shell 1.3.0

Extract the full runnable Windows ZIP into a new folder. Keep all DLL, PRI, XBF, runtime/deps files and Assets together with both Nexus executables. Run **Nexus.Shell.exe** to preview the desktop alongside Explorer. Sections opens only through its shortcut or Start.

After Windows acceptance, choose **Sections → Personalize → Use Nexus at sign-in** to make Nexus the desktop for this user. The full selected folder must stay in place. Save work and sign out when ready; setup does not sign out automatically.

In a Nexus desktop session, use **Start → Session…** to restart Nexus, return to Windows, or sign out. Closing Sections/Files does not close the shell. Settings, Control Panel and Task Manager are in PC controls. See NEXUS-DESKTOP-MODE.md for keyboard behavior, scope and recovery.

If the UI fails, run **Restore-Windows-Desktop.bat**. Use Ctrl+Alt+Delete → Task Manager → Run new task to reach it from an empty desktop. This restores the saved sign-in policy. Running explorer.exe alone does not repair future sign-in.

Data and recovery records live under `%LOCALAPPDATA%\WhiteDreams\NexusShell`. Updates create a separate version folder; preview it and select its host in Personalize before removing the old folder.

Source ZIPs cannot run directly or serve as a binary updater base. Native Windows compilation and acceptance are still required for the prepared source.
