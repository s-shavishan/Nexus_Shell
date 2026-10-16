# Storage Analyzer

Open **Storage Analyzer** from Launchpad, Search, desktop/Nexus Tools or Controls Home. It explains where logical file size accumulates within a chosen folder or drive. No scan begins automatically. Choose a root, set the entry limit, choose whether to include hidden items, then **Analyze**.

The results provide folder totals with size bars, folder drill-down/Up/Scan root navigation, the 100 largest observed files, a file-type breakdown, and sampled skipped paths. Filter and page each section. **Open in Files** navigates to a folder or a file's parent; **Copy path** explicitly copies the selected path. Double-click a folder to drill down. Largest-file double-click opens its containing folder.

## Accuracy and boundaries

- Sizes are logical file lengths, using binary KiB/MiB/GiB units. They are not allocated disk usage or reclaimable space: sparse/compressed files, alternate streams and filesystem metadata differ, and hard links may be counted more than once. Compare drive free space in System Monitor when needed.
- The scan reads directory metadata and file lengths/times using your account. It does not open file contents, delete/move source data, change permissions or request elevation.
- Links/junctions/reparse points and offline/cloud-recall entries are skipped. A reparse/cloud root is refused. Inaccessible/disappearing items produce partial totals and skipped-path feedback; an unavailable root reports an error. Hidden items can be included or excluded explicitly.
- Each scan is bounded to the selected 100,000 / 500,000 / 1,000,000 entries, 25,000 retained folders, depth 128, 100 largest files, 256 type groups and 100 skipped-path samples. Reaching an entry/folder/depth limit clearly marks a partial result. Excluded and inaccessible items are included in the skipped count. Totals are observations of a changing filesystem, not a transactional snapshot.
- Cancel is checked between filesystem calls. Windows/remote/device calls may not return immediately. One physical scanner slot is retained across closed/reopened utility windows until the previous call actually finishes, preventing accumulating blocked workers. Close cancels the request; reopening reports when an earlier scan still owns the slot.
- Cancelled/limited results remain useful and are explicitly partial. A failed scan retains earlier results with their observation time. Results/path history are held in memory and released at close.

The skip policy follows Microsoft's [file attribute definitions](https://learn.microsoft.com/en-us/windows/win32/fileio/file-attribute-constants). No undocumented disk-cleanup operation is used.

## Export

**Export CSV** asks for a new `.csv` destination through the existing Save picker. The report includes observation/state/skip details, retained folder totals, largest files, type totals and skip samples. UTF-8 with BOM supports Unicode names in spreadsheet tools; quotes are escaped and formula-like cell prefixes are treated as text. Reports may contain private folder paths: export is explicit and no report is uploaded.

A temporary file is flushed and moved into place only after the complete report is written. Existing destination files are never overwritten, including files that appear after the picker. Choose a different name if the destination already exists. Report generation does not restart or mutate the scan.

## Acceptance criteria and manual demo

Use a real AppVeyor package on Windows 10 19045 and Windows 11. Local portable tests exercise real temporary files, nested/empty/Unicode folders, symlink cycles, cancellation, bounds, exports and scanner admission; native rendering/Windows permission and cloud behavior require the checks below.

1. Create disposable nested folders with known file sizes, an empty folder and Unicode names. Scan; verify root/child rollups and top-file ordering against file Properties. Empty folders remain visible. Drill down/Up/root and filter/page all four sections.
2. Repeat on Downloads and a drive root. Show useful progress while the window remains responsive; include then exclude hidden files. A protected subtree must be reported as skipped, with partial totals; a denied/unavailable root must fail without replacing earlier results with invented values.
3. Create junction/symlink loops and cloud/offline placeholders. Their contents must not be traversed or hydrated. Check the skip count and sampled reasons. Try a linked root: refuse it with a clear message.
4. Select a small entry limit for a sufficiently large location. Show **Limited** and partial totals. Cancel another scan: show **Cancelled**, preserve observed results, and allow another scan after the worker finishes. Close/reopen during a slow network/device call; refuse a second physical reader while the old one remains active.
5. Open a large folder/file result in Files and copy its path. These commands must target the selected row. No cleanup/delete command runs.
6. Export a new CSV with Unicode/comma/quote names; inspect totals and state/skip metadata. Export to an existing filename: retain its exact bytes and report the conflict. Permission/disk-full errors must leave sources and existing reports intact, with temporary-file cleanup where possible.
7. Resize to 500 DIP; use keyboard focus, horizontal form scrolling, 100–200% DPI and High Contrast. Scan/cancel/close repeatedly and observe threads/handles/memory for bounded resource use.

Suggested recording: choose Downloads → Analyze/progress → largest files → folder drill-down → Open in Files → CSV → Cancel a second scan. Native screenshots must be captured from the running Windows app; none are inferred from source compilation.
