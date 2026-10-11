# Nexus 3.4.0 — Desktop Command Pack

This update adds three integrated desktop panels and persistent window layout metadata. It preserves Windows authentication, drivers, app processes and system services. It adds no standalone apps, input hook, virtual-desktop replacement or Windows service removal.

## Window board and layouts

Open **Window → Window board & layouts**, the dock's window overview button, or the desktop context menu's Window board command.

- Type a window title or app name to filter. Choose All displays or a display. Click a preview to switch; minimized windows show an icon and restore action instead of a fake live thumbnail.
- Check up to four resizable windows, then choose Grid, Columns or Stack. With a display selected, Move to display maps up to twelve selected windows to that work area. Arrangements request native asynchronous placement without activation. Restoring a saved maximized state can activate that app under Windows; reopen the board to use Undo if it is dismissed.
- Save layout opens a compact editor. Save selected windows, or the filtered view if none are selected, with a name. Eight named layouts retain twelve positions each. Saving an existing name replaces that layout.
- Select a layout and Restore. The button shows the currently matchable count; identities and monitors are checked again before the operation. The result reports unmatched/unavailable windows. Open missing apps yourself, then try again. Duplicate same-app titles or uncertain executable paths are skipped.
- Undo requests the previous native placements of the last successful group operation. A closed or changed window is not restored. Individual active-window placement retains its separate undo.

Positions use work-area proportions and clamp to the available display. If a saved display is absent, restoration uses the selected display, the app's current display, or an available display. Resizing a VM before explicit Restore therefore keeps requested positions visible. This is not an automatic display-unplug migration service, a dock per display or a Windows virtual-desktop controller.

A board page owns at most six DWM thumbnail relationships. Smaller viewports show two/three columns and one/two rows. Relationships are released before paging, hiding and closing. Nexus does not capture or save preview screenshots. Background identity refresh is single-flight and unchanged lists keep their current cards. Live preview content stays in fixed bounds; surrounding chrome uses the existing motion policy.

Apps can impose their own minimum sizes, resist moves, deny access, or ignore queued commands. A successful API reply means a command was accepted, not that an app finished drawing at that size. Restore and Undo remain explicit actions.

## Clipboard shelf

Open **View → Clipboard shelf** or its desktop context-menu command. **Remember copied text** starts off. Turning it on keeps new text copies in session memory; it does not import the previous clipboard or Windows history.

The shelf retains up to 32 entries, with eight pins and 16,384 UTF-16 characters per entry. Search is local; clicking an entry copies it back for normal paste in an app. Nexus does not inject a paste shortcut or persist any clipboard entry, preview, pin or search to settings/logs. Only the on/off preference is saved. Replayed shelf text carries Windows' monitor-exclusion marker.

All entries, including pins, clear on session lock/disconnect, disabling and shutdown. Clear all removes current retained entries without erasing Windows' current clipboard. Late reads invalidated by clear, lock or disable cannot repopulate old text. After unlocking, only new copies are retained.

Copies with `ExcludeClipboardContentFromMonitorProcessing` are ignored. `CanIncludeInClipboardHistory` must contain an explicit one if present; denied or malformed markers are skipped. Applications that do not mark passwords or other secrets can still expose them to an enabled shelf. The opt-in panel describes this limitation.

Native clipboard notifications drive capture. Reads and writes run off the UI thread; delayed rendering is allowed only one outstanding reader, never a chain of replacement threads. After three seconds, the panel reports a slow source. Nexus cannot forcibly interrupt an external app's clipboard rendering: capture may wait until that source returns. If Windows session notifications cannot register, capture stays unavailable rather than promising lock cleanup it cannot observe. Session status → Check services can retry registration.

## Session status

Open **Nexus → Session status** or **View → Session status**. It displays the latest observed Core/Control Center reply and age, file worker count, persistence queue, clipboard state, session duration and desktop app-window count. First replies and idle workers are explicitly shown; the panel does not manufacture a healthy state.

Check services requests fresh checks through existing service gates and retries unavailable clipboard notification attachment. Repair work area invokes the existing bounded desktop work-area/caption repair. Neither action disables Windows services or resets restart budgets.

A three-second resource sampler runs only while the panel is open. It measures the desktop process working set and CPU across all logical processors. It is not a complete process-tree benchmark or a frame-time measurement. Glass status comes from this panel's actual material controller.

## Build and one Windows acceptance pass

Source/API compilation and portable policy tests passed in the development environment. A native Windows publish, the expanded WinUI probe, real DWM rendering, clipboard behavior and performance remain pending until performed on Windows.

Build using README.md. Interactive builds run the native probe; headless builds record SKIP. The probe constructs production menu/Launchpad surfaces, all eight literal templates and the three new panels with window cards at two sizes and all three glass profiles. It starts no Core service, desktop takeover, clipboard listener, input hook or settings writer. It checks construction/layout, not pixels or timing.

1. Run **Check-Nexus-UI.bat** from the compiled folder and confirm PASS in its report. Start preview once, then use the managed desktop if preflight passes.
2. Open two normal resizable apps. In Window board, switch with a preview, minimize one and restore it from its fallback, filter by title, select both, choose Grid and Undo. With more than six windows, page forward/back and watch for previews left behind after hiding.
3. Save a named layout, move the apps, Restore and Undo. Change the VM resolution and Restore again. With multiple displays, repeat on a negative-origin monitor/mixed DPI and unplug a saved display before Restore. Close one app or create duplicate same-app titles; verify the result reports skipped matches rather than moving the wrong app.
4. Turn Clipboard shelf on. Copy two harmless text snippets in a normal app, pin one, search, copy it back and paste normally. Clear all and confirm both disappear. Copy another, lock/unlock, and confirm the shelf is empty. Turn it off and verify further copies are absent. Restart Nexus and verify only the on/off preference survives.
5. Open Session status. Confirm observed replies, close/reopen Control Center, and watch its worker status. Check services and repair the work area. Run **Check-Nexus-Desktop.bat** once with a normal window maximized, then retain that report alongside the UI report.
6. Keep the desktop running for a normal session and check responsiveness, high contrast/reduced effects, material profiles, process/thread counts and memory. Test slow/private clipboard sources where available. A test pass on a small VM does not establish real-machine GPU or hardware behavior.

Keep your previous compiled folder and Restore-Windows-Desktop helpers available. Startup/sign-in inspection remains documented in START-AND-SIGNIN-3.4.0.md.

## Supported API references

[DWM thumbnail relationships](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail), [clipboard privacy formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats), [clipboard notifications](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener), [session notifications](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification), [native window placement](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowplacement).
