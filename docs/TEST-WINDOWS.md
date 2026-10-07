# 0.5.0 desktop acceptance

Run these checks on your Windows 10 PC after a successful CI build. Use a copy of your app folder if experimenting with updates.

| Check | Expected |
|---|---|
| Upgrade with existing settings | Notes, pins, saved items and tasks survive; four starter profiles appear and residency stays off |
| Home at 1440px and 900px | Cards/strips adapt without overlap; app and window grids retain finite viewports |
| 125%, 150%, 200% display scale | Text remains readable and controls can be reached by scrolling |
| Desktop layout off/on | Workspace panel and desktop canvas switch without losing data |
| Configure Study | Save selected apps and saved items, rename the preset, then restart; choices survive |
| Enter a preset | Home and pinned dock change; no app or file opens |
| Open a preset, then Cancel | Nothing opens |
| Open and confirm | Each distinct target gets one open request; missing paths are reported while remaining items continue |
| Change preset during focus | Shared timer/tasks stay intact; the preset switch does not start or reset a focus session |
| Ctrl+Alt+Space from Firefox | Nexus becomes visible and opens search; disabling the switch releases the shortcut |
| Shortcut already in use | Nexus opens normally and reports shortcut unavailability |
| Keep Nexus available on | Alt+F4 hides the window; the tray icon can reopen it or exit completely |
| Focus timer while hidden | Timer continues; completion is recorded once; reopening shows the current state |
| Usage tracking off while hidden | No foreground-time sampling is enabled implicitly |
| Start Nexus again while hidden | Existing instance reopens; a second instance does not remain running |
| Explorer restart | Tray icon is restored, or Nexus reopens if registration fails |
| Wide running dock | Up to three visible windows appear; clicking switches to the intended window |
| Narrow running dock | Extra running tiles collapse; Ctrl+4 still lists windows |
| Exit Nexus | Process exits, tray icon disappears, hotkey is released, timer is saved paused |
| Update after residency enabled | Fully exit first; small updater reuses verified runtime files and retains the old app folder |
| High contrast and reduced effects | Native controls remain usable; color/layout switching does not disable workspace actions |

Windows APIs can decline a foreground-window switch. In that case Nexus reports it and Alt+Tab remains available.

The core CI smoke check exercises native subclass invocation and cleanup using a hidden test window. Actual hotkey input, tray interactions, WinUI layout, focus handling and fullscreen still require manual acceptance above.

---

# Windows test checklist

Record your Windows build, monitor resolution/scaling, and whether this is a local, AppVeyor, or GitHub Actions build. Test Windows 10 build 19041 or newer and Windows 11, x64.

## Startup regression

The AppVeyor build must first complete the MakePri MainWindow index check and compiled-resource ZIP hash check. Save the `resources-check.json` artifact; this is packaging evidence, not UI acceptance.

1. Extract 0.4.1 into a fresh folder and start `Nexus.Shell.exe` on Windows 10 build 19045. Confirm the latest log says `Nexus Shell 0.4.1` and contains `MainWindow.xaml loaded; configuring window` and `MainWindow activation completed`.
2. Confirm the actual desktop is visible and interactive; log markers alone are not UI acceptance. Try both Control center anchors and repeated app-tile context menus while scrolling/recycling the library.
3. If startup fails, include the resource report and root-PRI log inventory along with the error. Confirm a native dialog identifies the stage/HRESULT/log path. Close it, then collect the latest startup section, including `RestrictedDescription` and resource messages if present. Do not mark the XAML bug fixed on the strength of a compile alone.

## Desktop accessibility regression (0.4.1)

1. On Windows 10 build 19045, confirm `Desktop settings ready` is logged, followed by `MainWindow activation completed`, and that Home is actually visible and interactive. The previous fatal `AccessibilitySettings.add_HighContrastChanged` call must be absent.
2. Toggle high contrast in Windows Settings while Nexus is active. Within the existing five-second UI tick, programmatically created Study/Explore/Activity text and cards must use high-contrast resources and custom motion must stop. Toggle it off and confirm the Nexus theme returns.
3. Change the Windows animation preference. Custom motion must follow the preference within five seconds while Nexus is active. This does not override Nexus's Reduced effects setting.
4. Minimize or deactivate Nexus, change the preferences, and return. The preferences must refresh on activation; no additional background appearance timer is used.
5. With unchanged settings, type into notes or a saved-item dialog for longer than five seconds. Polling must not rebuild the page, lose text, or reset the caret. Test normal close and reopen with notes/tasks retained.
6. CI's core-check executable calls the actual Win32 high-contrast and animation query functions on Windows. That validates native interop access on the build host; it does not validate WinUI rendering on this PC.

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

## Aura 0.6.0 acceptance

1. Build/publish on AppVeyor; run core/native/resource/update checks. Launch from the complete publish folder on Windows 10 19045.
2. Start with the default solid material. Choose Pearl, Lagoon and Graphite; verify menu, cards, inputs, buttons, search, dialog actions and dock all update. Restart and verify the chosen mood/name/notes persist.
3. Enable Native glass. Verify active material and inactive fallback. Toggle Reduced effects and high contrast while running, including with a dialog and Control center open. Verify the UI stays readable and usable; fallback must not crash startup. Test with Windows transparency disabled.
4. Open add-app, saved-item, task, workspace editor/new-workspace and launch-preview dialogs. Check primary/close button text, hover/press, Tab/Enter/Escape, validation and cancel/save behavior.
5. Try 1440×900, 1366×768, 1024×600 and 320×480 logical window sizes; repeat at 100/125/150/200% display scaling and increased text size. All pages must remain reachable through compact navigation. Check hero actions, profiles, library/window scrolling, palette and control scrolling, and dock fit.
6. Use mouse and keyboard for every menu, search row and Windows-settings shortcut. Confirm these shortcuts open Windows settings; no in-app volume/network state is claimed.
7. Test fullscreen, panel/desktop layout, focus mode, hotkey, notification-area hide/reopen/exit and existing workspace/task/notes behavior after the theme changes.
8. Run `measure-resources.ps1` with glass off/on and reduced effects. Capture actual memory/CPU; no preview-based performance estimates are acceptance evidence.

## Shell Experience 0.7.0 acceptance

1. Build and run the core/native/resource/update checks in AppVeyor. Launch the complete publish folder on Windows 10 19045.
2. Visit Home → Apps → Study. Back returns Apps then Home; Forward returns Apps. After going Back, choose Explore and verify the old forward branch is gone. Page rebuilds and theme changes must not add history entries. Test header arrows and Alt+Left/Right, including with a dialog/picker active.
3. Open Personalize from More, the compact menu, panel sidebar and Control center. Change each setting; return Home, close/reopen Nexus and verify persistence. Resume Personalize when it was the last page.
4. Hide/show notes and essentials independently. Confirm notes/pins survive and remaining cards pack without holes. Test reduced effects, high contrast, 12/24-hour clock including localized AM/PM text, and compact dock with no/five pins and zero/three open-window entries.
5. Verify All/Apps/Saved/Workspaces/Actions/Tasks/Windows categories, title-prefix ordering, no matches, mouse selection and keyboard typing/arrows/Enter/Escape. After a filter choice, typing should continue in the search field. Tab must reach filters and results.
6. Open an app, saved item and workspace from search. Reopen empty All; recent choices should appear once at the top. Type an unrelated query and verify recency does not force unrelated results. Remove a saved item/change catalog entries and verify stale recent references do not launch old targets.
7. Clear recent items. Disable remembering and select more search items; none should return as recent. Separate Activity/usage controls must retain their existing behavior.
8. Open and close Notepad while search is open; verify titles appear/disappear on refresh, current selection survives when possible, and unavailable-window selection reports gracefully. Check global hotkey reopening and notification-area reopening.
9. Check 320×480, 1024×600, 1366×768 and 1440×900 logical windows at 100/125/150/200% display scaling and larger text. Personalize should stack its cards, search categories should scroll, and palettes/control center should stay bounded. Inspect native switch tracks/knobs in normal/hover/pressed/disabled and high-contrast states.
10. Measure actual memory/CPU with compact dock, materials off/on and reduced effects. No source or preview result is a runtime performance claim.
# 0.8.0 acceptance

1. On Windows 10 build 19045, extract the new binary into a fresh folder and launch it. Confirm the latest log reports 0.8.0 and the desktop responds to input.
2. Upgrade with existing notes, tasks, pins and saved links. Check their contents; confirm `settings.before-0.8.0.json` exists and Opal is selected on the first upgrade only.
3. Open Explore, Study, Apps and Personalize; use the red panel button to return Home. Test 1366×768 and 1920×1080 at 100%, 125% and 150% scaling, then a narrow window. Menus, search, scrolling, Quick Look and the dock must remain reachable.
4. Create spaces, capture a URL and note, drop a local file/folder and try the same link in two spaces. Test collection/tag search, favorites, moves and both views. Ctrl+N creates a note; Space/Enter on a card opens Quick Look.
5. Preview a small text file/image, then change selection/page before loading finishes. No old preview should appear. Missing files should report an unavailable shortcut without freezing.
6. Export/import a space. Reject malformed/oversized/unknown-format files and an over-capacity import without changing the board. An imported space is independent; originals stay in Windows.
7. Start/pause focus from the desktop, continue in Study, close and reopen. The remaining time must restore paused. Test optional notification-area residency and global summon independently.
8. Switch all four moods from both Personalize and Control center. Enable Reduced effects, high contrast and the Windows animation preference; confirm clear controls and usable dialogs. Appearance changes must not drop notes or disable input.
9. With Nexus fully closed, make a copy of its data folder and deliberately damage `settings.json`. Reopen: the last backup should restore with a message, and an unreadable copy should remain. Restore your original data afterward.
10. Fully exit Nexus and apply the small binary update; verify runtime checks, old-folder preservation and sign-in startup repair. Measure memory/CPU using the existing resource script after native acceptance.

Automated CI compilation and packaging cannot establish how the UI renders or performs on this PC.

## 0.9.0 acceptance

1. Build the exact 0.9.0 commit in AppVeyor. Require core/native/updater checks, C# and XAML compilation, publishing, the thirteen SVG asset checks, MakePri and both archive checks to succeed. Confirm all artifact names use 0.9.0.
2. Extract the complete binary into a fresh folder on Windows 10 build 19045 and Windows 11. Confirm version 0.9.0, visible interactive startup and all dock/desktop icons loading. Keep the Assets directory with the executable.
3. In desktop mode, open My files, Explore, Study and App Library from the canvas, using mouse and keyboard. Favorite/unfavorite saved cards and verify up to four desktop favorites update and open the intended Explore selection. Check long titles, context menus and Tab/focus states.
4. Change workspace from the compact desktop card and spaces widget. Set up an empty workspace, then open a configured one. Start/pause focus from its widget and verify Study shows the same session. Close/reopen panels with the traffic lights and dock. Home panel mode must still show its configured cards.
5. Compare Explore board and list modes with notes, links, files and folders. List rows must fit title, single-line subtitle and kind without clipping. Check selection, hover, keyboard focus, Space/Enter Quick Look, long paths and multiline notes. Board, capture, import/export and scrolling must retain their behavior.
6. Test 320×480, 1024×600, 1366×768 and 1920×1080 logical windows at 100/125/150/200% scaling and increased text size. Check narrow shortcut wrapping, scroll access, widget visibility, workspace-card fit, sidebar/inspector collapse and compact dock. No required navigation or exit action may become unreachable.
7. Switch every mood, native glass, reduced effects and Windows transparency/animation preferences. Deactivate/reactivate the window and toggle high contrast. Check clock, shortcuts, toolbar pills, text fields, menus, disabled controls and dialogs; high contrast must use the panel fallback and system colors.
8. Upgrade a compatible existing full folder with the small binary update. Confirm new SVGs and compiled resources are present, runtime checks pass and the old folder remains intact. Reopen with existing notes, tasks, pins and spaces intact. Repair the sign-in path if it pointed at an older folder.
9. After acceptance, measure idle, minimized, Explore scrolling, rapid navigation and glass-on/off resource use with the existing measurement script. Record native screenshots and failures rather than treating the included layout illustrations as rendered-app evidence.
