# Windows test checklist

Record your Windows build, monitor resolution/scaling, and whether this is a local or GitHub build.

## Basic operation

1. Start `Nexus.Shell.exe` normally. Confirm the menu, clock, Home, and dock render.
2. Open Files and an installed browser from the dock. Confirm ordinary applications can appear above Nexus.
3. Open Apps. Search for an installed program. Right-click to pin it. Confirm the dock updates.
4. Add an `.exe` or `.lnk`; cancel the picker once to check cancellation.
5. Add an installed game. Launch it. Confirm normal Windows/game focus and Alt+Tab behavior.
6. Open Running apps and select a window. If Windows blocks focus, confirm the message is visible and Alt+Tab remains available.
7. Open each Windows-settings link. Confirm no simulated Wi-Fi/volume state is being shown.
8. Change the display name. Close and reopen Nexus. Confirm name and pins persist.
9. Unpin every app, then reopen. Confirm the empty pinned list stays empty.
10. Move a manually pinned executable. Confirm launching its stale pin reports an error and leaves the UI usable.

## Display and recovery

1. Toggle full screen with F11; return with Escape.
2. Red hides the workspace; reopen with the N dock button. Yellow minimizes Nexus; restore through the Windows taskbar.
3. Toggle the green expand control and Focus view.
4. Check your normal resolution at 100%, 125%, and 150% scaling where available.
5. Resize to narrower/shorter windows. Confirm the sidebar scrolls or collapses, shortcut cards stack, the dock fits, and Control center scrolls. Confirm Exit and workspace navigation remain reachable.
6. Try a second monitor, move the window between monitors, and disconnect/reconnect one.
7. Suspend/resume Windows. Confirm the UI recovers and usage does not accrue the suspended duration.
8. Open Nexus twice. Confirm only one instance remains and the existing window is brought forward when Windows permits.
9. Close normally with the Exit action; also test Alt+F4.

## Motion and keyboard

1. Navigate quickly between Home, Apps, Games, and Activity. Confirm pages never remain faded, displaced, or disabled.
2. Move repeatedly across dock icons and away; confirm icons settle without leaving the dock bounds.
3. Enable Reduced effects. Confirm artwork/custom motion stop and principal surfaces become opaque. Restart to confirm the preference persists.
4. Disable Windows animation effects, reactivate Nexus, and repeat navigation. Re-enable the Windows setting afterward if preferred.
5. Toggle a Windows contrast theme and check primary text, focus, cards, menus, and exit controls. Verify system colors and report any remaining contrast problems.
6. Open Control center with mouse and keyboard. Click outside, press Escape, and confirm focus returns to its anchor. Escape should dismiss the flyout without also minimizing Nexus.
7. Press Ctrl+K. Confirm Apps opens with the search box focused. Use arrow keys/Enter in the grid and Shift+F10 on a tile to open its context menu.
8. Check with Narrator where available. App names should be announced and every actionable control must be reachable.

## Collection and persistence

1. Search before discovery completes, navigate away, and return. Discovery completion must not force navigation or clear the active query.
2. Scroll a large discovered library repeatedly. Watch memory after warm-up; item containers should be recycled rather than creating all tiles at startup.
3. Rapidly pin/unpin and change preferences, then close immediately. Reopen to confirm the final settings are preserved.
4. Remove a game from Games using its context menu; confirm it remains an ordinary pin. Unpin it to remove the pin entirely.
5. Open Add app twice quickly, cancel, and try again. Confirm only one picker is active and controls recover after cancellation.

## Tracking and startup

1. Confirm usage tracking starts off on a clean profile.
2. Opt in. Use another app for two minutes; check Activity for an approximate foreground-time total.
3. Leave the PC idle for more than 90 seconds and compare the change. Tracking only counts intervals after sampling confirms activity, so allow a small boundary error.
4. Disable tracking; totals should stop increasing.
5. Clear history/totals. Cancel once, then confirm once.
6. Enable startup, sign out/in, and confirm Nexus opens alongside Windows. Disable it after testing. Moving the executable requires updating this entry.

## Resource measurements

Compare at least these states using Task Manager, not just the footer:

| State | What to record |
|---|---|
| Home idle for 60 seconds | Nexus working set, private memory, CPU, GPU |
| App library after discovery | Same metrics and app count |
| Nexus minimized while gaming | Same metrics; game frame rate before/after |
| Full screen and rapid navigation | Input responsiveness and rendering artifacts |
| Reduced effects, tracking off | Same memory/CPU metrics and feature behavior |
| Ten minutes idle, repeated page changes | Cache plateau versus continued memory growth |

The footer reports this process's working set and CPU sampled approximately every five seconds while active/visible. Its tooltip adds private bytes. It excludes GPU/DWM memory and is not evidence of a measured improvement over Electron. `measure-resources.ps1` captures CSV/JSON process samples; instructions are in `DESIGN-AND-PERFORMANCE.md`.

## If something fails

Run `diagnostics.ps1` from the portable folder, or `scripts\diagnostics.ps1` from source. Send `environment.json`, `nexus.log`, the first build error if relevant, and a screenshot of the issue. Review the log before sharing: it may contain local file paths and application names.

For build failures, the GitHub run uploads `Nexus-build-logs`; local builds write `artifacts\logs`.
