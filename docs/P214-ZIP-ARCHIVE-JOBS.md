# ZIP archive jobs — Windows acceptance pending

Nexus Files and File Jobs can create ZIPs and extract a ZIP into a new folder. The operations run in Core's existing single-worker queue with progress, cooperative pause/resume, cancellation, queue hold, job receipts and interrupted-history recovery.

## Use

In Files browse mode, select one to 24 files/folders and use **Archive… → Create ZIP…** or **Organize → Create ZIP…**. Edit the `.zip` output name. Select one ZIP to use **Extract ZIP…**, then choose the new folder name. These commands use the current folder; the captured selection and destination do not change while the dialog is open. They are not offered in file pickers.

File Jobs adds Create ZIP / Extract ZIP to its operation list and exposes the archive/folder output name. Add sources, choose the destination and collision behavior, then Add to queue. Load into form retains archive parameters for an explicit reviewed request. Extract ZIP requires exactly one ZIP source; unsupported archive types keep their normal Windows file options.

Files confirms admission and points to File Jobs. A lost response retains the exact archive request ID; retrying the same selection/name/destination checks that receipt. A different request is refused until the prior receipt is reconciled or reviewed before reopening Files. File Jobs' existing pending-request form also retains its original request. No uncertain request is automatically submitted with a new ID.

## Output and controls

- Create ZIP preserves each selected root, including empty folders. Same-named roots from different source folders get separate numbered archive names. Sources are never removed or rewritten.
- Extraction always builds a fresh folder and publishes it as one complete result. It does not merge into an existing folder. Nested ZIPs remain ordinary files.
- Both operations stage output beside the final destination, then rename it without replacing an existing item. Keep both numbers the whole archive/folder; Skip skips the whole result. Cancellation/failure cleans only this job's uncommitted staging where possible. If cleanup fails, the job reports its exact staging path for review.
- Pause is cooperative between content buffers, entry/directory work, central-directory scans, verification reads and final commit. Planning and in-flight I/O/flush/atomic rename may finish before acknowledgement. Resume retains the same job/staging. Closing Files or the utility leaves accepted work running; Core restart marks unfinished work Interrupted without replay.
- Progress counts uncompressed content bytes, with committed-file counts advancing only after the complete result is published. Rates/estimates cover compression or extraction time, independently from verification; empty archives can complete with zero files/bytes. Source names and empty directories remain part of the result.
- Create ZIP uses streaming Deflate at Fastest level. Default verification rereads every expected ZIP entry, compares content SHA-256 and CRC, and verifies the entry set. Extraction always verifies entry CRC and exact length; the default SHA-256 option additionally rereads staged files. Disabling SHA readback does not disable CRC checks.
- ZIP timestamps use local DOS time with two-second precision and the format's 1980–2107 range. Same-timezone round trips preserve representable timestamps; ZIP does not retain a universal timezone offset.

On Windows, packaging retains the highest existing source `ZoneId`; extraction propagates the ZIP's existing `Zone.Identifier` zone to each output file before publication. A destination that cannot retain a required zone fails the job instead of silently dropping it. Origin reads are bounded even if a metadata stream changes. This preserves zone classification, not arbitrary alternate streams, URL/referrer fields, ACLs or a full backup's metadata. Actual Windows zone behavior still needs the native test/acceptance below.

## Supported scope and bounds

Stored and Deflate ZIP entries, including ZIP64 directory envelopes, are supported through .NET's ZIP reader/writer. Split disks, encrypted entries and other compression methods are refused; use the original archive tool for those formats. No external executable is downloaded or launched, and no extracted content is automatically run.

Bounds are 24 source roots, 10,000 materialized files/folders including implied directories, 64 path levels, 240 characters per name component, 1,024 characters per archive entry path and existing File Jobs full-path limits. Each input/result ZIP and total uncompressed contents are limited to 64 GiB. Central-directory metadata is limited to 16 MiB before .NET materializes entries; ZIP64 extension records also have a 16 MiB bound.

The envelope scan checks actual directory record count/extent, not just advertised counts. All extraction names and the complete file/folder tree are checked before staging. Traversal/absolute/device/alternate-stream names, Windows reserved/trailing names, duplicate or case-aliased paths, file/folder conflicts, linked/special entries and materialization beyond the limits are refused. Existing linked, encrypted or offline source/destination paths keep File Jobs' restrictions. Files are created without overwrite in a new staging tree, with parent/link checks and a final tree check before publication. These checks do not claim protection against a hostile process with authority to replace the destination concurrently.

## Verification and acceptance

The portable archive group uses real ZIP/ZIP64 files and independent ZIP reads. It covers Unicode/empty files and folders, collisions and Skip, exact bytes/timestamps, duplicate root names, empty/nested archives, invalid paths/tree aliases/links, forged directory metadata, encryption/method/split-disk refusal, CRC/length/expansion limits, legacy JSON defaults, phase rates and Core receipt idempotency. Real archive work pauses/resumes/cancels during compression, extraction, verification and precommit, with source/staging checks. The archive group also runs with `TZ=Asia/Colombo` to check local-time conversion.

Run only the archive group with:

```powershell
dotnet run --project tests/Nexus.Runtime.Checks -- --archive-checks
```

Windows on an NTFS temporary volume also exercises source-zone → ZIP → extracted-file zone propagation using disposable files. Linux skips those native stream checks. Native UI fixtures include the archive form/rate card and verify that Extract ZIP appears for ZIP targets, while unsupported archive types keep their original handlers. Compilation/portable checks are not native WinUI usability acceptance.

Required Windows 10/11 acceptance: create and extract ordinary Explorer/7-Zip ZIPs (Stored/Deflate and ZIP64); Unicode/empty folders, collisions, CRC failures and unsupported/encrypted archives; default verification and origin-zone preservation on NTFS; refusal when a required origin cannot be retained; source/destination permissions and disk-full cleanup; pause/cancel and close/reopen during real drive I/O; interrupted-history review; output-name editing and loaded jobs; compact/DPI/High Contrast keyboard/menu behavior. Run exact-head AppVeyor, WinUI/XAML preflight and packaging. No merge or native usability result is implied by this feature batch.

References: [Microsoft archive API and validation guidance](https://learn.microsoft.com/en-us/dotnet/standard/io/zip-tar-best-practices), [ZIP entry CRC property](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchiveentry.crc32?view=net-8.0), [Windows downloaded-file origin stream](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/unblock-file).
