# Windows desktop acceptance — 1.4.0

These checks need a complete successful Windows build and a VM/test account. The working 1.3.1 VM result does not establish 1.4.0 performance or Quick Settings behavior. Complete QUICK-SETTINGS-AND-PERFORMANCE.md alongside these recovery regressions. Run core checks and scripts/test-update.ps1 as CI already does; retain the actual build logs.

## Temporary takeover and return

- With Windows visible and all Nexus instances closed, launch Launch-Nexus-Desktop.bat. Verify the Windows desktop/taskbar disappear only after the independent Nexus desktop/taskbar become ready. No Sections window should open automatically.
- Open Settings, Control Panel and ordinary apps. Verify they stay available and continue running. Windows services and explorer.exe should remain running; no sign-in policy should have changed.
- Use the Sections desktop shortcut, close and reopen Sections, open/close Files and Start. Verify independent window lifetime, saved content and consistent Solstice/Ember styling.
- Test Win, Ctrl+Esc, Win+E/D/I/R/S/A, Alt+Tab/Shift+Tab and Win+Tab. Ctrl+Shift+Esc, Ctrl+Alt+Delete and Win+L must still work. Lock/unlock must keep the desktop usable.
- Exit Nexus from Start, desktop context menu and Personalize in separate runs. Each must close all Nexus surfaces and restore Windows' desktop, taskbars, original visible/hidden surfaces, original primary working area and ordinary shell shortcuts. Existing apps keep running; a maximized window must fit above the Windows taskbar.
- Repeat with Windows taskbar auto-hide enabled, then disabled. Verify its prior behavior returns. Test display scaling at 100%, 150% and 200%, resolution changes and secondary displays. Nexus currently supplies only a primary-display taskbar; hidden secondary Windows bars must return on exit.
- In preview Nexus.Shell.exe, use Personalize → Use Nexus for this session. Verify the preview saves/closes, one host/UI pair takes over, and exit restores Windows. Re-launch the desktop BAT while already managed: there must be no second host/UI instance.
- Deny writing Policies\System on a test account. Temporary takeover/exit must still work without changing that policy. Deny writing the session recovery file separately: takeover must abort without hiding Windows.

## Failure recovery

- End only Nexus.Shell.exe in Task Manager. Host must restore surfaces/work area between attempts and restart its own UI. Repeated short failures must exhaust two retries and return to Windows.
- End only Nexus.DesktopHost.exe while UI is responsive. Within the next five-second tick, the UI should request recovery and close. Verify the independent helper restores visibility/work area after UI cleanup, with no registry write for a temporary session.
- End host and UI together. Run Restore-Windows-Desktop.bat. Verify saved visibility/work area and Windows UI return; originally hidden wallpaper WorkerW windows must remain hidden.
- Run the recovery script against an older 1.3.0 folder. It must use the independent fallback, not pass an unknown --restore-session option to that old host. Test absent host/dependencies as well.
- Simulate an invalid session record, stale handles, an Explorer restart, a missing first heartbeat (45 seconds), and a stalled UI heartbeat (90 seconds). Recovery must not alter unrelated app windows. Check desktop-host.log and app logs.
- Suspend/resume the VM, fullscreen an app/video/game, and change display/DPI settings. Confirm the Nexus bar and later Windows bar/work area remain usable. Record any native behavior that differs from these expectations.

## Persistent sign-in regression

On a supported Windows Pro VM with permission to write the policy, configure Use Nexus at sign-in, sign out/in, and verify Nexus runs as the selected desktop. Restart, sign out and return-to-Windows must keep their existing behavior. Preserve the original policy value/type across upgrades.

Deny policy writes before Return to Windows or recovery BAT. The current Windows desktop/taskbars must still return; the warning and original sign-in backup must remain, and no successful future sign-in restoration should be claimed. Remove the denial through the VM's normal administration and retry restoration. A foreign policy set after setup must not be overwritten.

Also run TEST-WINDOWS.md for Files/pickers, Explore, settings, PC controls, keyboard accessibility and shared-state regressions. Native visual/performance results require actual Windows evidence.
