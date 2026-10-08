# Windows acceptance — Nexus 1.3.0

Run on a Windows Pro VM/test account first. Record OS build/revision, edition, display size/scaling, build commit and result. This checklist is pending, not a record of completed Windows testing.

## Build and preview

- Run the source/core/updater checks, `scripts\build.ps1 -UseMSBuild`, and `scripts\package.ps1`. Require a clean Windows XAML/resource build and ZIP verification. The host's shared .NET runtime must match the shell publish byte-for-byte.
- Confirm the complete folder contains the shell, host, host deps/runtimeconfig, WinUI DLLs, app PRI/XBF resources, Assets and independent recovery BAT/script.
- Start Nexus.Shell.exe with Explorer running. Desktop/taskbar appear, Sections stays closed, and Personalize reports preview mode. The Windows taskbar and Windows global shell shortcuts remain available.
- Open Sections, Files, Start and window overview. Close each tool separately; desktop/taskbar continue. Verify notes/timer state survives Sections reopening, and new Files pins survive workspace save/reload.
- Browse local/user folders and drives; type paths, Up/Refresh/filter, create a folder, open files and folder shortcuts. Test unavailable/network paths, renamed files, cancellations, a >1,000-entry folder and a picker file name outside the displayed subset. Navigation must remain responsive and never select a misleading old folder path.
- Add an app, capture multiple files/folders, export/import an Explore space. Require only Nexus picker UI; test file-extension filtering, 100-file bound, Cancel and existing-file replacement confirmation.
- Open Recycle Bin through desktop/Files. Check count/size and empty confirmation with disposable test items. No Explorer window or Windows progress/confirmation appears. Individual restore is not part of this version.

## Sign-in and normal use

- Capture the current user's Shell and Nexus Run values/types. Enable Nexus from Personalize and inspect the recovery record. The record must precede the registry change; another user's/HKLM settings must remain unchanged.
- Sign out after saving work, then sign in. Require only the Nexus desktop/taskbar/Start. Confirm Explorer's taskbar/desktop is absent, rather than hidden beneath Nexus.
- Win opens Start; Win+E Files; Win+D minimizes/restores the same app identities/placements; Win+I Settings; Win+R/S search; Win+A PC controls. Ordinary typing, Ctrl/Shift shortcuts, Alt+F4, Win+L and Ctrl+Alt+Delete remain usable.
- Alt+Tab and Alt+Shift+Tab cycle Nexus's independent switcher once per key press; release Alt to activate. Win+Tab opens it persistently; Enter/click selects and Escape cancels. Include minimized, closed and reused-handle app windows, plus Nexus Files and Sections. Ctrl+Alt+Tab opens the persistent Nexus overview; releasing Alt must keep it open.
- Start deactivates on app click, supports search/Down/Enter/Escape and stays above the bar. The switcher and Start do not steal focus after being dismissed.
- Verify Settings, Control Panel and Task Manager explicitly open from PC controls. Test audio, lock, power/network data and arranging/undoing windows.
- Maximize several apps: their content stops above Nexus's bar. Fullscreen an app/video/game: it covers the bar. Leave fullscreen: the bar returns. Start/menu interaction from fullscreen must remain usable. Test DPI/display changes, focus and keyboard navigation.
- Test Solstice, Ember, all legacy moods, high contrast and reduced effects across open Files/pickers/Start/switcher/Sections. Inspect text/selection contrast and fonts at 100%, 150%, 200% scaling and a small display. Measure idle CPU/private working set; source previews are not performance evidence.

## Recovery and upgrade

- Session → Restart Nexus saves shared state, restarts the shell and leaves other app processes running.
- Session → Sign out requires the Nexus confirmation and returns through Windows sign-in. Cancel keeps the session.
- End only Nexus.Shell.exe in Task Manager: host restarts it. Repeat short failures: after two retries the previous policy is restored and Windows desktop returns. Check logs and work area.
- In a test build, block the UI dispatcher: no heartbeat for 90 seconds triggers child-only termination and recovery. Simulate startup never reaching its first heartbeat: 45-second timeout. Do not use daily work for these tests.
- Session → Return to Windows restores the original value/type and Nexus Run setting, closes all Nexus surfaces, releases the work area and starts Explorer. Verify the next sign-in uses the previous desktop.
- Run the independent recovery script after UI failure. Verify it restores only owned policy and completes a partial restore retry. Set an unrelated Shell policy after Nexus setup: recovery must refuse to overwrite it.
- Return to Windows before previewing a version in a different folder; verify only one Nexus version can run in a session. Enable the new version. Verify upgrade preserves the original backup. Simulate a failed Run/value write: prior host/policy and recovery remain valid. Keep old folders until success.
- Test a removed/renamed host folder and use the Task Manager recovery-script route. The host cannot recover if it cannot start.
- Restore Windows and confirm Explorer's normal desktop, taskbar, work area and usual shortcuts return.

Also run the existing tool regressions in TEST-WINDOWS.md. The historical desktop-foundation checklist describes 1.2 preview behavior; this checklist owns 1.3 desktop-mode acceptance.
