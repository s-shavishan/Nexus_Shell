# Midnight Glass 1.6.0 — Windows acceptance

## Design and features

The desktop uses the supplied reference's compact menu bar and blue-black night palette. Spotlight sits near the top center; Control Center sits at the upper right. The floating dock omits the duplicate clock, which remains in the top bar. The date card contains actual unfinished Study tasks. Focus desktop hides icons and this card; it does not change Windows notification settings.

Files defaults to a grid in browsing mode and a list in pickers. Image previews decode selected local raster files only, up to 20 MB, bounded to 440 × 440 pixels. Unsupported, damaged or unavailable images retain their icon. Selection version checks prevent an older decode from replacing a newer selection. No thumbnails are saved.

Notes reuses existing saved board notes and the existing QuickNote field. Quick note supports 10,000 characters; board notes retain Explore's 2,000-character limit. Editing queues a shared save after a short debounce; settings retain the existing atomic write and recovery backup. Delete asks for confirmation and removes the same item from Explore. The new Calculator uses decimal arithmetic in entry order.

Each floating window owns its Windows-managed Desktop Acrylic backdrop and palette fallback. Availability follows Windows and accessibility settings. The desktop bar remains on the desktop layer rather than reserving another strip of work area. It can be covered by maximized external apps; the dock remains the shell access path.

The bundled wallpaper at `src/Nexus.Shell/Assets/Wallpapers/Midnight.png` was produced with the built-in imagegen tool from the supplied image. Prompt: remove all UI, text and logos; reconstruct the midnight alpine landscape with snow mountains, blue-violet aurora, warm village lights, lake reflections and dark pines. It is artwork, not a runtime screenshot.

## Acceptance sequence

1. Build and package with the checked-in Windows scripts. Start in preview mode and select Midnight Glass. Confirm no XAML or WinRT exceptions in the log.
2. Open every desktop menu, Spotlight and Control Center. Resize/scale to 100%, 150% and 200%, including 1366 × 768. Check keyboard focus, hover and text contrast in all three visual profiles and high contrast.
3. Open Files, Notes, Calculator and two external apps. Minimize with each window's yellow control and via its dock menu. Restore from the dock and Alt+Tab. Maximize/restore each window. Verify indicators update promptly and identities survive title changes.
4. Maximize an external app. Check dock hide and bottom-edge reveal. Enter borderless fullscreen after opening a dock menu/preview; the dock must stay hidden. Exit fullscreen and confirm recovery.
5. In Files: grid/list, each sort, filter, back/forward, a deleted history target, rapid navigation, inaccessible folder, drive root, a 1,000+ entry folder, long file names and a large/corrupt image. Change selection rapidly; an older preview must not win.
6. Exercise OpenFile/OpenFiles/Folder/SaveFile pickers, including switching view mode, multiple selection, overwrite confirmation and cancellation. Browse Recycle Bin and return to a normal folder.
7. Edit Quick note from Notes and Study in both directions. Search and pin a board note, edit it from Explore, delete it elsewhere while selected in Notes, close/reopen Notes, exit/restart Nexus and verify saved content. Confirm unreadable settings recover from backup.
8. Calculator: digits, decimal point, numpad operators, percent, repeated equals, zero division, overflow, clear and backspace. Confirm focus stays inside the floating utility.
9. In managed desktop mode: Show desktop/restore with all four Nexus windows, close Nexus, Return to Windows and host recovery. Confirm the taskbar and work area restore. Test Win+R, Win+D and Alt+Tab without sticky modifier behavior.
10. Compare a native Windows screenshot with the supplied concept. Record layout/material differences and measure responsiveness and memory before calling this release accepted.

The desktop-host policy, native shell recovery contracts and unsupported tray/Jump List limitations remain documented in [desktop mode](NEXUS-DESKTOP-MODE.md).
