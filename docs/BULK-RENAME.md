# Bulk rename

Select 1–100 files or folders in one normal Files location. Open **Bulk rename…** from the toolbar, **Organize** context submenu or **Ctrl+Shift+R** while the file list has focus. F2/Rename with several items opens the same workflow; single-item Rename and **Undo edit** keep their existing behavior. File pickers and Recycle Bin do not offer bulk rename.

## Preview and apply

1. Set a prefix/suffix, literal Find/Replace, optional case-insensitive matching, and optional sequential numbers with a start, minimum digit width and separator.
2. **Preserve file extensions** starts enabled. Only the final extension is preserved (`archive.tar.gz` keeps `.gz`); dotfiles such as `.gitignore` are whole names. Folder names are always whole names.
3. Choose **Build preview**. Every original → target mapping appears, including unchanged and blocked rows. Numbering follows original names in ordinal case-insensitive order, with ordinal ties, independently of selection order. No filesystem change occurs during preview.
4. Resolve blocked rows, review every target, check **I reviewed every target name**, then choose **Apply rename**. Changing any rule invalidates Apply until a fresh preview is built.

Windows reserved/device names, invalid Unicode/control characters, separators/streams, trailing dots/spaces, names over 255 UTF-16 characters, duplicate outputs, duplicate physical identities and occupied names are refused. Case-only changes and swaps/chains into existing selected names require separate batches with distinct vacant names; this workflow does not stage temporary renames behind the user's back.

## Permissions and execution

The Windows adapter accepts available local-drive siblings whose parent is case-insensitive and whose filesystem can supply stable 128-bit identities. Network/optical locations, aliases, linked/reparse paths, offline/cloud-recall items, unavailable identity/case information and access/sharing failures are refused with a reason. It never requests elevation, changes ACLs, hydrates file contents, moves across directories, replaces existing targets or deletes data. Some removable filesystems therefore remain unsupported.

Apply first rechecks every changing source and destination. A stale/occupied/denied preflight refuses the whole batch before any rename. Each admitted item then opens and verifies the same source identity/metadata, retains an opened parent directory and calls Windows' handle-based rename with replacement disabled. Existing unshareable writers and competing renames are refused. A destination created after preflight cannot be overwritten by that rename.

The batch is sequential, **not a filesystem transaction**. Later items may fail after earlier successes. Cancel takes effect between filesystem calls; it cannot interrupt a blocked kernel call or reverse an already admitted rename. Closing the dialog during work requests cancellation and waits for completion; closing Files cancels the view and never queues a replay. One physical bulk operation occupies the Files worker until it actually returns, including after cancellation or view release.

## Results, Undo and receipts

**Last batch receipt** records every selected item as Renamed, Unchanged, Failed, NotAttempted or Cancelled. Copy a selected result for full original/target paths and details. Successful renames retain post-operation metadata for **Undo remaining**, independently of the older single-edit Undo button.

Undo walks successful outputs in reverse order. It rechecks volume/file identity, parent identity, creation/write/change metadata, size and attributes, then claims the original name without replacement. Replaced/edited outputs, occupied original names and permission failures stay untouched and retain individual refusal details. Resolve an original-name/access conflict and retry Undo for the remaining eligible items. A post-rename metadata failure still reports Renamed but makes that item's Undo unavailable. These guards inspect the renamed object's metadata; they do not hash file contents or recursively snapshot a folder's children.

Receipts/Undo live in the current Files tab, including across dialog close/reopen. They are lost when the tab/process closes or crashes. There is no durable mutation journal, automatic rollback, automatic restore or restart replay. Export first when a persistent audit is needed; after a crash, inspect actual original/target names before starting another batch. CSV is a report, not an executable restore recipe.

Export to a **new absolute `.csv` path**. The writer publishes a flushed UTF-8-BOM report atomically, quotes cells and protects spreadsheet formula prefixes; an existing destination is never overwritten. The report preserves original Apply outcomes and separate Undo outcomes/times. A previous receipt cannot be silently replaced by another Apply: explicitly review/export and confirm **Discard this receipt and its remaining Undo**, then Clear. The panel follows the current batch's resulting paths after Apply/Undo; close and reselect to work on a different selection.

## Windows acceptance and demo

Use a disposable standard-user Windows 10/11 account with the exact PR/AppVeyor package. Native UI and kernel rename execution were not available in the Linux implementation workspace; compiled API/ABI and portable backend-contract checks do not establish Windows acceptance. No native screenshots are claimed.

Create a fresh empty demo folder with several distinct text files (`photo-a.txt`, `photo-b.txt`, `notes.txt`), a Unicode name and a folder containing a child. Keep a separate copy for comparison.

1. Select files in reverse order; preview prefix `holiday-`, suffix `-edited`, numbering from 9 with width 3. Verify original-name ordering and preserved extensions. Change a field and verify Apply disables until preview/confirmation are renewed.
2. Include the folder and a dotfile; check whole folder/dotfile names. Try reserved names, illegal separators, duplicate targets, case-only names, an occupied selected name and 101 items. No invalid batch should Apply.
3. Modify a source or create a destination externally after preview. Apply must refuse all changing items during preflight. For a change occurring during execution, inspect every success/failure and ensure external bytes were not overwritten.
4. Apply to the disposable selection. Compare every file/child's bytes. Close/reopen the dialog in the same tab; inspect/copy/export the receipt. Re-export to the same path and verify refusal without any byte change.
5. Create a different file under an original name and edit another renamed output. Undo must refuse those rows while restoring unchanged eligible outputs. Remove only the disposable conflicting file, retry Undo, and verify the edited output remains untouched.
6. Cancel before/during a larger batch and during Undo. Verify outcomes for every item, no automatic rollback, no duplicate replay, and recoverable remaining Undo. Check a locked file, denied permissions, links/junctions, offline/cloud items, case-sensitive folders and removable/unsupported filesystems.
7. Verify toolbar/context/F2/Ctrl+Shift+R discovery, single rename/Undo edit, native Ctrl+Z, isolated file pickers, blocked tab switching during the dialog, folder refresh on exit, and release/close during slow I/O. Test keyboard focus, 500-DIP/short windows, DPI, High Contrast and repeated open/close resource use.

Capture actual native screenshots of rules → full preview/blocked rows → receipt → Undo conflict → CSV result. Record package commit, OS/filesystem and observed outcomes.

## Source ownership

Runtime owns bounded rules/plans, preflight, immutable receipts, cancellation, operation admission and CSV. Shell owns the native Windows backend and WinUI dialog; Files keeps its isolated worker, existing settings writer and RPC contracts. No test/probe project or dependency is added.

API references: [FILE_RENAME_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info), [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), [CreateFileW sharing](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew), [FILE_ID_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info), [FILE_BASIC_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_basic_info).
