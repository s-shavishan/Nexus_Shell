# Windows test checklist

Record your Windows build, monitor resolution/scaling, and whether this is a local, AppVeyor, or GitHub Actions build. Test Windows 10 build 19041 or newer and Windows 11, x64.

## Startup regression

The AppVeyor build must first complete the MakePri MainWindow index check and compiled-resource ZIP hash check. Save the `resources-check.json` artifact; this is packaging evidence, not UI acceptance.

1. Extract 0.4.0 into a fresh folder and start `Nexus.Shell.exe` on Windows 10 build 19045. Confirm the latest log says `Nexus Shell 0.4.0` and contains `MainWindow.xaml loaded; configuring window` and `MainWindow activation completed`.
2. Confirm the actual desktop is visible and interactive; log markers alone are not UI acceptance. Try both Control center anchors and repeated app-tile context menus while scrolling/recycling the library.
3. If startup fails, include the resource report and root-PRI log inventory along with the error. Confirm a native dialog identifies the stage/HRESULT/log path. Close it, then collect the latest startup section, including `RestrictedDescription` and resource messages if present. Do not mark the XAML bug fixed on the strength of a compile alone.

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

## 0.3.0 additions

1. Confirm the build's .NET core checks and PowerShell update checks pass, followed by resource/package checks. The build's checks do not replace this UI test.
2. Open Ctrl+K. Search a pinned app; select the second matching row with Down and open it with Enter. Search a workspace and a saved item; Esc closes only the palette. Open/close repeatedly during Start-menu discovery.
3. In Explore, save a valid web link and a local file/folder. Check duplicate handling, search, open, remove, and persistence after restart. A removed board shortcut must leave the original file intact. An invalid web address must keep the save dialog open.
4. Start a Study session, switch pages or apps, return and pause/resume. Change preset; verify it stops/resets. Complete a break and verify it does not increase focus completion count. Close/restart; no active timer should resume silently.
5. Edit notes in Home and Study, navigate quickly, then close/restart. Check the latest text is preserved. Test multiline text near the 10,000-character limit.
6. Change Orbit/Aurora/Slate, restart, and check persistence. Toggle Reduced effects and Windows animation settings. Check every action works with animation unavailable.
7. Resize to narrow/short windows and test at 100/150/200% DPI. Notes headings, command panel, timer presets and saved-board controls must stay usable. Check High Contrast and keyboard navigation.
8. Test the small update from an extracted prior full folder. Confirm old bytes remain unchanged, resources in the new folder match the report, and the new app opens. An incompatible runtime or damaged patch must fail without creating a usable partial destination. Reuse the new folder as the next update's base.
9. If sign-in startup is in use, turn it off/on from the new folder so its registry entry points at the new executable. Verify after the next sign-in.

## 0.4.0 acceptance

1. Confirm Windows CI passes the .NET core checks, actual PowerShell fixture checks, compilation, MakePri and both archive checks before download. Treat this separately from native launch verification.
2. Upgrade older settings with notes, pins and saved items. Confirm preservation, Personal collection defaults, and `settings.before-0.4.0.json` where backup succeeded. Close before copying/restoring settings.
3. Add tasks with Add and Enter. Choose a task through its options or Ctrl+K; check Home and Study agree. Complete/reopen/remove it. Test empty, duplicate and 160-character titles, completed filtering, and the 100-task bound.
4. Start focus, pause, change preset, reset and complete a session. Confirm break sessions do not increase the focus count, and completing focus does not check off a task. Close mid-session and reopen: the same remaining time is paused and does not advance while Nexus was closed. Resume it and confirm completion is recorded once. Exercise sleep/delayed ticks and day rollover.
5. Save/edit links with valid and invalid URLs. Change collection and favorite state; filter and search; confirm favorite ordering, Home shortcuts and Ctrl+K task/item search. Removing a board item must leave the underlying file/folder intact.
6. Open Ctrl+4 with several apps and multiple windows from one app. Search title and process, activate/minimize/restore/close a target, and refresh. Resize from 1440 to 1280/1024/800/640/320 logical pixels and test 100/125/150/200% scaling. Confirm cards wrap without horizontal clipping and selection survives refresh. Refreshes stop outside the active visible overview.
7. Leave/re-enter overview while a refresh is pending; confirm older results do not replace another view. Check keyboard-only card invocation, native focus outlines, high contrast, reduced effects and rapid navigation. Study/Explore input must retain its text/caret when merely switching focus between apps.
8. Use the small update with a matching base, then mismatched/corrupted files and an existing target. Confirm rejection does not change the base or publish an incomplete new folder. Confirm new EXE/DLL/PRI/XBF come from the update and startup-folder repair is offered only for an existing registration pointing elsewhere.
9. Measure Home, Study (paused/running), Explore, overview, minimized and reduced-effects scenarios. Repeatedly navigate and inspect idle memory after ten minutes. Verify stable caches, inactive CPU and game impact on the actual machine.
