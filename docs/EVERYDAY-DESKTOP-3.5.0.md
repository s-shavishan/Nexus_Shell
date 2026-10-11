# Nexus 3.5.0 — Everyday Desktop Pack

This release adds desktop organization, quick folder access and reversible everyday controls to the existing Nexus environment. The delivery contains source. Native Windows build, rendering, focus and extended runtime acceptance remain pending.

## What you can use

| Feature | Where to find it | Behavior |
| --- | --- | --- |
| App collections | Launchpad → + Collection; right-click an app → Collections | Up to 12 named collections, 100 app references per collection. Rename or remove a collection without touching installed apps. Membership resolves against available apps; missing apps are not fabricated. |
| Hide and restore apps | Launchpad app context menu; Hidden tab | Hidden entries leave normal Launchpad sections. Restore one or all from Hidden. Dock pins and installed applications remain intact. |
| App and dock order | Launchpad A–Z / Catalog order; right-click a dock pin | A–Z is the default; Recent keeps recency and Pinned keeps dock order. Move dock pins left/right without removing and repinning them. |
| Places | File or View menu → Places; desktop Ctrl+Shift+P | Up to 24 favorite folders, plus common folders. Add through the Nexus folder picker, label, reorder and unpin. A desktop folder can also be pinned from its context menu. |
| Folder quick look | Select a folder in Places | Up to 24 preview rows from a bounded 1,000-entry directory scan. Open items or continue in Nexus Files. The preview is a snapshot; Refresh reads it again. |
| Desktop file actions | Desktop item context menu or F2; background → New folder | Rename local files/folders, copy a path, open the containing folder, or create a folder on the user's desktop. Filename extensions are shown while renaming. |
| Undo local edits | Desktop Ctrl+Z or context menu; Files → Undo edit | One prior rename or new-folder creation per surface, in session memory. Undo never overwrites an occupied original path. A created folder must remain empty and have unchanged metadata. Changed/replaced/unavailable items are refused. |
| Desktop organization | Desktop background → Sort desktop icons / Show desktop icons | Name, Kind or Newest; system entries remain first. Hiding icons leaves files intact and is separate from Focus desktop. Existing icon count limits still apply. |
| Automatic desktop refresh | Runs with the desktop; status in Session status | Watches user/common desktop roots, coalesces events at 250 ms, and keeps one enumeration active. A request arriving during enumeration triggers another pass. F5 refreshes manually; Check services retries failed observation. |
| Notification control | Notification panel | Search across Nexus history; All, Unread, Issues and Snoozed filters; explicit Mark shown read; 15-minute snooze and Bring back now; one undo for dismiss/clear. At most 20 cards are built per page. |
| Desktop shortcuts | Top menu bar → Help; desktop F1 | An integrated reference with direct access to Launchpad, Places, window layouts, clipboard, notifications and session status. Local keyboard commands do not register new global hooks. |

## Reliability and data behavior

- Collection membership, hidden targets, favorite paths/labels and icon preferences are saved through the existing Core writer. Snapshots deep-copy collection members. Older settings retain their wallpaper, notes, pins and existing preferences.
- Places does folder enumeration on a worker. Switching selections cancels/discards stale results and keeps only the latest pending folder. There is at most one active read, including when a device or network share does not respond. The panel reports a slow read after three seconds; a blocked filesystem call is not forcibly terminated. Hiding the panel cancels work and discards preview rows. No directory tree is indexed in the background.
- Favorite labels do not rename folders. Unpinning does not delete them. Favorite paths can remain saved while unavailable. The Downloads shortcut uses the current profile's `Downloads` path; a redirected location can be added as a favorite.
- File mutation runs off the UI thread, one operation per surface. Local rename and new-folder creation are supported; UNC mutation and reparse-point rename are refused. Case-only rename is not implemented. New-folder creation uses an exclusive final move, so it cannot silently adopt another process's existing folder. Undo uses the post-operation metadata and refuses changed items; it is not a persistent transaction journal or a file-ID-based guarantee against concurrent external replacement. No recursive delete, copy/move queue or Recycle Bin deletion is added by this release.
- A filesystem edit already accepted by its worker may finish if the containing surface closes; undo history is session-only. Verify the actual folder after an interruption. Nexus does not claim to roll back completed filesystem actions on process termination.
- Notification history and snooze deadlines persist with the existing bounded 80-alert history. Due alerts return unread within the desktop's five-second service tick; snooze changes the Nexus inbox/badge, not Windows notifications. Alerts are no longer marked read just by opening the panel. Mark shown read applies only to the current filtered page. Dismiss/clear undo is memory-only, restores at most the history capacity, and expires when replaced by another dismissal or the desktop closes.
- New panels use the existing glass profiles, reduced-motion/high-contrast paths and Escape dismissal. Dock hover previews are suppressed while these panels, the clipboard or session-status panel are open.

## One Windows VM acceptance pass

Keep the previous compiled folder. Build 3.5.0 on Windows using the root README, then run `Check-Nexus-UI.bat` interactively before `Launch-Nexus.bat` or `Launch-Nexus-Desktop.bat`. The native construction probe now includes Places with sample rows, desktop shortcuts, Launchpad collections and the paged notification panel. It never starts Core, a folder watcher, clipboard capture or a settings writer. A headless SKIP is not a UI pass.

1. **Organize:** create two Launchpad collections; assign the same app to both; rename one; hide the app; restore it from Hidden. Change sort order and move a dock pin. Restart Nexus and verify these choices and existing preferences survive. Remove a collection and confirm its apps remain installed and launchable.
2. **Places:** add a normal folder, rename its favorite label and reorder it. Open a preview item and Open in Files. Unpin and confirm the folder remains. Test an empty folder, a folder with more than 1,000 entries and an unavailable/removable location. Switch between folders quickly, close/reopen, and confirm no stale folder contents are shown. Observe session CPU/memory after repeated opens.
3. **File safety:** use disposable files. Rename on the desktop and undo; create an empty folder and undo. Add a file inside a newly created folder and ensure undo refuses. Occupy a rename's old path before undo and verify both files stay intact. Edit a renamed file and verify undo refuses. Repeat rename/undo in Nexus Files. Test invalid names, permission denial and closing while an edit is pending. Do not use valuable files for first native acceptance.
4. **Desktop flow:** create/rename/delete a disposable desktop item in another program and check icons update. Change Name/Kind/Newest sorting; hide and restore icons; use F5, F2, Ctrl+Shift+N and Ctrl+Z while icons have focus. Check text-box Ctrl+Z is still text undo. Test desktop folder redirection and restart folder observation through Session status if it fails.
5. **Alerts:** produce a Nexus alert, search for it, mark it read, dismiss and undo. Clear and undo; then generate another alert and verify it is preserved by undo. Snooze for 15 minutes, inspect Snoozed, bring it back, and also test a deadline through a Nexus restart. Quiet alerts must suppress desktop error banners while keeping inbox history. Windows app toasts remain separate.
6. **Presentation:** at 100% and 150% scaling, test a small VM viewport, all glass styles, high contrast, reduced motion, mouse and keyboard navigation. Check flyouts stay visible, the focused panel receives input, dock previews do not steal focus, and Escape dismisses panels. Recheck 3.4 window layouts and opt-in clipboard capture, especially lock/unlock cleanup. `Check-Nexus-Desktop.bat` and `measure-resources.ps1` can collect geometry and bounded resource observations.

## Source-side evidence

The portable suite includes real temporary-file rename/create/undo and conflict checks, a real local FileSystemWatcher event, organization caps/migration/deep snapshots and notification snooze/dismiss recovery. The C# API check uses actual restored WinUI/Windows reference assemblies with temporary generated XAML field stubs. It does not run the Windows XAML compiler or compositor.

Native Windows IPC, desktop layer focus, redirected/remote filesystem behavior, actual glass output, accessibility and sustained performance require the VM pass above. This feature pack does not change Windows boot/authentication policy, remove Windows services, implement app-toast interception or separate Launchpad/dock into worker processes. Those architectural milestones remain on the roadmap.

API references: [File.Move and its non-overwrite overload](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-8.0), [FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-8.0).
