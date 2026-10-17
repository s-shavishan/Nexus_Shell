# Quick Look

Select a file in normal Nexus Files and press **Space**, use **Quick Look** in the toolbar or **Organize → Quick Look** in its context menu. This opens a native read-only preview dialog. File pickers and Recycle Bin retain their existing contracts; folders are not previewed.

## Browse and open

- With one selected file, Previous/Next browses the visible, sorted and filtered files from that folder. With multiple selections, Space/the toolbar captures only selected files, excluding folders. A context action on one file captures its visible neighbours.
- The capture holds at most 1000 files around the initial selection. Its count and omitted items are shown; it is not silently extended by later filesystem/selection changes.
- Use Previous/Next, **Alt+Left/Right**, or bare Left/Right when focus is outside text/password/encoding/media controls. Boundaries do not wrap. Esc/Close exits; Files refreshes and selects the last previewed item.
- **Open externally** or **Ctrl+Enter** closes the preview, releases its resources and uses the existing Windows file association. This explicit action may launch the selected file's application; opening a preview alone never does. **Copy path** copies the full path.

## Actual content previews

| Content | Behavior |
| --- | --- |
| Images | Windows image decoding, a still frame for animated formats, Fit/100/150/200% preview zoom and scroll/pan. Source dimensions are shown. |
| PDF | Native Windows raster rendering, Previous/Next page, explicit page number, zoom and optional password/reload. No links, scripts or attachments are activated. |
| Text/code | Read-only selectable text, BOM/strict UTF-8 auto detection, explicit UTF-8/UTF-16 LE/BE/Windows-1252 selection and bounded prefix/line display. HTML/SVG/scripts are plain text. |
| Audio/video | Native Windows media player with play/pause, seek and volume transport controls. Playback starts only after Play; no system media-command ownership is taken. |
| Other formats | Real file name/size/modification metadata and an explicit unsupported-format message; no content read, shell preview provider, web viewer or execution. |

Image and media formats depend on installed Windows codecs. Common image formats include PNG/JPEG/BMP/GIF/TIFF; WebP/HEIC/AVIF may need codecs. Audio routes include MP3/WAV/M4A/AAC/FLAC/OGG/Opus/WMA; video routes include MP4/M4V/MOV/AVI/WMV/MKV/WebM. An extension is a routing hint, not proof of valid content: corrupt, mismatched, DRM-protected or unsupported-codec files show a failure and can be opened externally. Remote playlists, URLs and executables have no built-in content renderer.

## Bounds and permissions

- Text reads at most 256 KiB; displays at most 65,536 UTF-16 characters and 2000 lines. Partial previews are marked. BOMs include UTF-8/16/32; incomplete trailing characters at a truncated prefix are omitted, never split. Invalid selected encodings and binary-looking text report a reason. Newlines are normalized for display; source bytes are unchanged.
- Images are limited to 32 MiB and 80 million source pixels. PDFs are limited to 64 MiB and 10,000 pages; only the current page is rendered. Raster destinations are capped at 2400 pixels per edge. Zoom enlarges the bounded preview; it is not an unrestricted original-resolution editor.
- Audio/video stream from a seekable local read handle rather than buffering the entire file; the source limit is 4 GiB.
- The Windows adapter opens with read access, read-only sharing, open-reparse-point and no-recall flags, then verifies disk type, attributes and the final opened path before reading content. It refuses network/optical/alternate-stream paths, folders, aliases/links, offline/recall items and denied/sharing failures. There is no elevation, source mutation, profile writer or new RPC operation. Close the preview to release its read lock before editing/renaming that file elsewhere.
- Local open/metadata/text work runs off the UI thread. Image/PDF WinRT operations request cancellation through their native async interfaces. Initial read/render attempts request cancellation after 30 seconds; a blocked filesystem/decoder call can still take longer to return. Only one physical preview load/render is admitted until its actual completion, including after close/reopen. No thread is killed and no abandoned read is silently replayed.
- Every item switch/reload releases previous content and stops/disposes media. Closing during a read requests cancellation and keeps the dialog open until the attempt returns; closing Files releases the view and prevents late UI/media callbacks from painting another item. PDF passwords stay in the current dialog and are cleared on file switch/close. Previews, text and passwords are not saved in Nexus settings or history.

## Manual Windows acceptance

Use the exact AppVeyor package on disposable standard-user Windows 10/11 files. Linux policy/actual-file checks and API/ABI compilation do not execute WinUI, native file handles, Windows codecs, PDF rendering or media playback. Real screenshots and native interaction remain pending.

1. Open valid PNG/JPEG/GIF and a large image with Space, toolbar and context action. Check dimensions, still-frame behavior, zoom/pan, narrow/short windows, DPI and High Contrast. Compare source bytes before/after preview.
2. Preview a multi-page PDF, jump to valid/invalid pages and zoom repeatedly. Test an encrypted PDF with missing/wrong/correct passwords; switch files and verify the password clears. Test corrupt/oversized/over-page-limit PDFs and confirm no stale page remains after failure.
3. Preview real Sinhala/emoji UTF-8, UTF-8/16/32 BOM files, Windows-1252, empty text, HTML/SVG/scripts, wrong encodings, binary NUL content, huge/long-line files and a character cut at the prefix boundary. Check partial labels, text selection/copy and no source edits or execution.
4. Preview WAV/MP3 and MP4, start playback explicitly, seek/pause/change volume, navigate to another item and close. Audio must stop and handles release. Test unsupported/missing codecs and corrupt/DRM media; error feedback must remain readable and file navigation responsive.
5. Check filtered/sorted neighbour order, multiple selected files with folders, 1000-item capture window/omissions, first/last boundaries, keyboard focus, Alt+arrows, Ctrl+Enter and external opening only after explicit action. File pickers, bulk/single rename, Undo, tabs and native Windows shortcuts must retain their behavior.
6. Test linked/junction/cloud/offline/network/denied/locked/deleted/changed paths and slow removable I/O. Cancel/time out, close/reopen and repeat file/page switches. Verify no stale image/page/player callback, no accumulating physical reads and deterministic cleanup after completion.

Capture actual native images → PDF page/zoom → text/encoding/partial result → media controls → failure/close states. Record package SHA, OS, codecs and observed outcomes.

## Source ownership

Runtime owns classification/MIME hints, limits, text decoding, immutable capture/navigation and physical-work admission. Shell owns the verified Windows read handle and WinUI/image/PDF/media presentation. The feature stays inside the isolated Files worker and does not add a launchable utility, dependency, test/probe project or global input hook.

API references: [PdfDocument.LoadFromStreamAsync](https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfdocument.loadfromstreamasync), [PDF render dimensions](https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfpagerenderoptions.destinationwidth), [MediaPlayer playback](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/play-audio-and-video-with-mediaplayer), [CreateFile sharing/flags](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew).
