# Everyday file operations — Windows acceptance pending

Nexus Files now provides normal Cut, Copy, Paste and Move to Recycle Bin commands. These are browse-mode actions, separate from file-picker text editing and Save dialogs.

## Clipboard and transfers

Select up to 24 files/folders in either grid or list. Use the toolbar, Organize context menu, or Ctrl+C / Ctrl+X. Navigate to a destination and use Paste, the empty-folder context menu or Ctrl+V with the file list focused. Ctrl+Shift+N creates a folder. Address/filter/name text fields keep their own editing keys.

The native clipboard contains eager `CF_HDROP` paths and `Preferred DropEffect` Copy/Move intent. This supports existing filesystem items in Windows Explorer and other apps using the same Windows formats. Paths survive the writing Files window closing. Clipboard export does not read file contents, persist file history or turn text paths/virtual attachments into files. Link-only clipboard requests are rejected. The clipboard is read only when Paste is requested; a browse-only, one-second sequence check keeps button availability current and stops on unload/release.

Paste submits to Core's existing File Jobs queue. Keep-both names and SHA-256 file-copy verification remain the defaults. Progress, active pause/resume, queue hold and cancellation are available in File Jobs. Closing Files does not cancel accepted transfers. Refresh the destination/source listing after the job finishes. Same-volume folder cuts use the existing atomic rename path; cross-volume folder moves are refused without deleting the source. Use Copy across volumes. Windows file cuts retain the existing native source-handle commit rules.

Cut by itself only publishes paths. After definite admission, Nexus consumes that Cut only if the clipboard still has the captured sequence, comparing and clearing under one native clipboard lock. A newer text/file copy is preserved. Admission is not completion: a failed/interrupted move still needs review in File Jobs and may need a new explicit Cut.

An uncertain response retains the exact submission, source snapshot and ID. Paste the same selection in the same folder again to reconcile it; another destination/clipboard is refused while that receipt remains unresolved. Copy/Paste after definite admission intentionally creates another copy. A Cut derives its ID from Core epoch, clipboard sequence and normalized sources, so reopening Files and choosing another destination cannot admit the same cut a second time in that session. Core never replays jobs automatically on restart. Reopening Files after uncertainty requires reviewing File Jobs; a new intentional copy is a separate request.

Normal File Jobs path/source/depth/entry limits apply. Device paths, drive roots, overlapping parent/child sources, unsafe linked/encrypted/offline transfers and pasting inside a selected folder remain refused. Cut/Paste refuses a selection containing an item already in the destination, avoiding a surprise rename to a numbered copy.

## Recycle and restore

Select up to 24 separate items, then choose Recycle… / Organize → Move to Recycle Bin… or press Delete while the list has focus. Nexus confirms the captured names; selection changes cannot retarget the operation. Shift+Delete is not a Nexus permanent-delete shortcut.

The existing supervised Windows file helper stages all targets on its STA before command admission, then uses `IFileOperation` with `FOFX_RECYCLEONDELETE`, `FOFX_ADDUNDORECORD` and `FOF_WANTNUKEWARNING`. Windows owns confirmation, progress, permission prompts and cancellation. Nexus provides no direct/permanent deletion fallback and does not suppress Windows warnings. If Windows offers permanent deletion for an item it cannot recycle, choose Cancel to keep that item. Network shares and removable volumes are refused by this command; fixed local volumes only.

The UI waits for the helper to finish while its normal UI heartbeat continues, then refreshes the original folder even after partial failure. Closing Files does not replay a native action. Core rejects another active recycle request covering the same paths or their parents/children, even under a new request ID. Duplicate IDs retain their original receipt; failed/cancelled operations are never automatically retried. Windows may have recycled some items before cancellation/failure: review both the folder and bin before an explicit retry.

The Nexus Recycle Bin page now opens Windows' Recycle Bin to select deleted items and use Restore. Nexus's existing Empty bin action still requires its separate permanent-removal confirmation. Ctrl+Z remains the existing rename/new-folder undo, not an advertised recycle undo. Restore is provided by Windows rather than a fabricated Nexus listing.

## Verification and Windows acceptance

Portable checks exercise real verified copies, real atomic folder cuts with collisions, immutable receipt retries, stale acknowledgements, cut identity across Files restarts/destinations, invalid/bounded inputs, recycle target capture, helper overlap refusal and no replay after failure. Native C# API compilation and portable tests do not establish Windows clipboard/COM/WinUI behavior.

An opt-in native fixture is available on a disposable Windows test account:

```powershell
dotnet run --project tests/Nexus.Runtime.Checks -- --file-clipboard-native-checks
```

This explicitly replaces that account's clipboard with temporary file paths. Normal checks do not modify the clipboard or recycle user data. The fixture checks native Unicode file/folder clipboard data, Move preference, source preservation, sequence-guarded consumption, protection of a newer copy, and data surviving its writer window closing. It must still be run on Windows.

Required Windows 10/11 acceptance:

1. Copy and cut files/folders from Explorer into Nexus and back, including Unicode/spaces, multi-selection, a destination collision and closing the writing Files window. Confirm ordinary text Ctrl+C/X/V in address/filter/name fields remains normal.
2. Paste into Nexus, follow File Jobs progress, pause/resume/cancel, hold later jobs, and check actual source/destination bytes. Verify file-cut source-handle behavior and cross-volume folder refusal.
3. Lose a paste reply after Core admission; retry the identical paste and prove one job/transfer. Reopen Files and try the same Cut in another folder: no second move. Replace the clipboard while admission is pending and confirm the newer copy survives.
4. Recycle disposable files, a nonempty folder and a 24-item selection. Check selection capture through confirmation, native progress/cancel, refreshed Files listings and restoration to original paths through Windows Recycle Bin.
5. Cancel midway, deny permissions, use an unavailable item and try network/removable locations. Review partial results; never silently fall back to permanent deletion. Try a second overlapping recycle while the first native dialog is open; Core must refuse it.
6. Verify compact/high-DPI/High Contrast keyboard focus, menu budget, button scrolling, disabled actions in pickers, and clipboard polling cleanup after Files closes. Run the native UI preflight and exact-head AppVeyor XAML/build/package checks.

API references: [Shell clipboard formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard), [clipboard sequence numbers](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboardsequencenumber), [IFileOperation flags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags), [operation completion and cancellation](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-performoperations).
