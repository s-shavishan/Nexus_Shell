# Session takeover acceptance — 1.4.1

## Change and evidence

1.4.0 main commit a9f813df250f8c5b8f4a3fdadb05804fae867025 passed AppVeyor. The user's VirtualBox VM is fast with Windows' taskbar present but lags after takeover, including Fast mode. This narrows investigation to the switch; it does not prove VirtualBox configuration or a single Win32 call caused the problem.

1.4.1 hides only Shell_TrayWnd/Shell_SecondaryTrayWnd windows owned by the validated Explorer process. It leaves Progman/WorkerW visible to Windows and covers the existing desktop with Nexus's opaque desktop, positioned immediately above it and below application windows. With no Explorer desktop, the existing anchor falls back to the bottom of the window order.

No Explorer process is terminated and no Windows services are stopped. The desktop window is not reparented or made always-on-top. The current taskbar work-area cache and Fast profile remain in use.

## Compare in the VM

1. Keep the same guest resolution, scale, VirtualBox controller, video memory, Guest Additions and 3D setting throughout the initial comparison. Keep old/full new builds in separate folders.
2. In plain Windows, drag a Notepad window around for 20–30 seconds, resize it, maximize/restore it and observe responsiveness.
3. Launch 1.4.1 preview and repeat, retaining Windows' taskbar.
4. Choose **Use Nexus for this session**, select **Fast**, close panels, and repeat the same drag actions. Confirm the primary-display desktop and taskbar show Nexus.
5. Repeat with Balanced only after Fast is stable. The system graphics configuration must be the same for preview and takeover.
6. Exit Nexus. Confirm Windows' original taskbar, icons and work area return. Re-enter temporary mode and repeat once.

Report which stage starts lagging and whether Notepad, Nexus windows or both lag. The optional Nexus-Window-Drag-Check.ps1 report collects display/Guest Additions metadata and per-process CPU. Those counters do not measure GPU utilization or frame time.

## Layering and recovery

- Click desktop icons, open Start/Quick Settings/Files/Sections, switch among external windows and use Win+D/Win+Tab. Nexus's desktop must stay behind applications; Windows' desktop must remain covered on Nexus's primary display.
- Lock/unlock, resize the VM display and change DPI. Confirm desktop bounds and the taskbar work area update without returning to a repeated reservation loop.
- In the VM/test account, close only the spawned Nexus UI through Task Manager. Verify host restart/recovery restores Windows when its retry budget is exhausted. Keep Restore-Windows-Desktop.bat available.
- Return to Windows before replacing binaries. The new host can restore old Format 1 journals containing Progman/WorkerW after an interrupted 1.4.0 session, before starting a new taskbar-only journal.
- Originally hidden taskbars retain their saved visibility. Newly discovered primary/secondary taskbars are journaled before hiding. Other apps and desktop windows are not part of the new takeover lease.

Broader native acceptance remains in TEST-DESKTOP-MODE.md and QUICK-SETTINGS-AND-PERFORMANCE.md. Multiple-display Nexus surfaces remain outside the current primary-display scope.

## Validation status

Source, metadata and patch checks are recorded in VALIDATION.md. New C# behavioral checks cover preserved desktop visibility, no steady-state hide writes, reappearing/secondary taskbars and legacy-journal restoration. Windows CI must execute them. No claim of fixed lag or native-tested performance is made until the VM comparison succeeds.
