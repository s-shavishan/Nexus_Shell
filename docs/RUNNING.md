# Running and recovery

Extract the full Windows ZIP into a new folder and keep every executable, dependency, compiled resource, asset and helper together. Source ZIPs must be built before use.

| Entry point | Behavior |
| --- | --- |
| `Launch-Nexus.bat` / `Nexus.Shell.exe` | Preview Nexus alongside the Windows desktop |
| `Launch-Nexus-Desktop.bat` | Supervised temporary Nexus desktop session |
| `Restore-Windows-Desktop.bat` | Independent current-session Windows restoration |
| `Restore-Nexus-SignIn.bat` | Restore Nexus-owned persistent sign-in policy from its backup |

Nexus keeps Explorer running and restores the recorded Windows taskbar/work area when ending a managed session. Windows continues to own native keyboard shortcuts, authentication and security UI.

Nexus Controls opens from Launchpad, the Nexus menu or Search. Closing Controls, Files, File Jobs or System Monitor closes that window rather than the desktop session. Accepted file jobs continue through Core while their window is closed; stopping Nexus cancels the worker and unfinished work requires review after restart.

## Persistent startup

Use Nexus Controls → Startup & session for supported sign-in and startup choices. Keep the selected complete application folder in place. Sign-in policies are modified only through the existing ownership/backup/elevation flow; foreign policies are preserved.

Recovery helpers remain in `tools/` in source and beside the executables in a published app. Startup inspection and diagnostics read the current Windows/Nexus state; diagnostics omit the personal settings file.

From an unusable desktop, use Ctrl+Alt+Delete → Task Manager → Run new task → `explorer.exe`, then run `Restore-Windows-Desktop.bat`. Restore sign-in separately if its policy is still active. Logs and recovery records remain under `%LOCALAPPDATA%\WhiteDreams\NexusShell`.

## Updates

Prefer the full release ZIP when the runtime or package dependencies change. A compatible-runtime update reuses only files with matching lengths and hashes and creates a separate version folder. It preserves the old application folder and user data; it does not overlay a running installation.

Exit the old Nexus instance before launching the new version. Select the new complete host folder in Controls → Startup & session before relying on it at the next sign-in.
