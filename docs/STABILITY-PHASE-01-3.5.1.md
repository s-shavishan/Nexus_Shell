# Stability Phase 01 — Nexus 3.5.1

This is a source release. Build and package on Windows using README.md. Keep the prior compiled folder and run the native UI probe before the acceptance pass below. No third-party search/file/copy engines have been added in this release.

## Confirmed defects and changes

| Area | Defect | Change |
| --- | --- | --- |
| Files reads | Each navigation started another worker even if a cancelled read was still blocked on IO. | One active folder read; intermediate pending requests are cancelled. Only the newest destination can publish its result. |
| Image preview | Fast selection changes could accumulate simultaneous file opens and decodes. | One active preview; cancellation checks between stages and stale-result rejection. Streams are disposed. |
| File controls | Rename could cancel a load without restoring Choose/New folder; generic button cleanup could re-enable unavailable Back/Forward/Undo actions. | Central action-state refresh; edits cannot begin during navigation, and controls recover on cancellation or failure. |
| File activation | Double-clicking blank space could open the previous selection. | Activation uses the actual item under the pointer. |
| Recycle Bin | The previous folder's selection, preview and rename state could remain; emptying could overlap another action. | Clear the old folder view and preview, disable unavailable controls, serialize the confirmed empty operation. |
| Settings | An oversized/rejected snapshot could remain pending and block corrected saves. | Reject oversize locally; Core identifies definitive validation rejection and the writer drops that rejected request. Uncertain transport failures still reconcile/retry with their original identity. |
| Save ordering | A queued writer copied its snapshot only after waiting, allowing later mutations to change the queued save. | Capture immediately before waiting. |
| Alerts | Long repeated notices escaped deduplication; a backwards clock suppressed notices; severity escalation could be swallowed. | Compare bounded text and severity, and require a nonnegative age within the five-second window. |
| Paths | Forward-slash Windows device/UNC spellings bypassed restrictions; normalization errors could affect workspace recovery. | Canonicalize each favorite defensively, reject device aliases, discard only invalid favorites and enforce UNC restrictions with either separator. |
| Panel placement | The common desktop panels were omitted from dock/work-area repositioning. | Include window board, clipboard, session status, Places and Help; center panels within the new usable area with bounded DPI. |

Folder cancellation remains cooperative: an OS call already blocked on a disconnected device may take time to return. The desktop remains independent of Files. Files shows a slow-location message after five seconds; closing and reopening the supervised Files process is the recovery route. No claim of universal IO interruption or crash-free operation is made.

## Evidence

The 3.5.0 compiled baseline reproduced: two long duplicate notices instead of one; a missing repeat after clock rollback; a missing error after severity escalation; invalid-path processing despite an already-cancelled token; and corrected saves blocked by an oversized pending snapshot.

The new portable suites add 29 desktop and eight state-writer assertions. They exercise 201 queued requests, uninterruptible/failing simulated reads, queue disposal, actual temporary settings files, malformed favorites, notice history, 28 display geometries, immutable saves and the production Core rejection/conflict routes. All existing Core and portable Runtime suites pass, including lost acknowledgements, recovery policies, file edits, startup, Control Center and simulated worker crashes.

C# source/API compilation uses restored Windows references and temporary XAML field stubs. It is not native WinUI compilation or execution. Windows rendering, real display events, HWND focus and hardware/IPC integration still require the following pass.

## One Windows VM acceptance pass

1. Build all components together. Run `Check-Nexus-UI.bat`, then launch preview and managed desktop. Confirm Start opens Launchpad, advanced Settings opens, and Restore Windows still works.
2. In Files, switch quickly between local folders and image selections. The final destination/preview must win. Try a disconnected test location: the desktop must remain responsive; close/reopen Files if the device call remains blocked.
3. With disposable files, cancel Rename and New folder, then complete each and Undo. Confirm Back/Forward disable at history boundaries. File edits must be disabled during folder loads and recover after a failed path.
4. Select an image, open Recycle Bin and confirm the old selection/preview is gone and Rename/New folder are unavailable. Double-click blank space in a normal folder: no file should open. Empty only a disposable test bin and confirm other mutations cannot overlap it.
5. Change preferences, collections and favorites; restart Nexus and confirm they persist. Keep the test output for the rejected-save checks; do not corrupt the real profile to reproduce them manually.
6. Exercise alert search/snooze/undo. With Places, clipboard, window board, Help or session status open, change VM resolution/DPI or detach a secondary display. Verify the visible panel remains reachable. Check high contrast and reduced motion once.

If a step fails, retain `Check-Nexus-Desktop.bat` output, relevant Nexus logs, display scaling/resolution, the exact step and a screenshot. Do not replace the last working compiled folder until this pass succeeds.
