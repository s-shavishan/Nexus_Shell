# Startup Apps

Open **Startup Apps** from Launchpad, Search, desktop/Nexus Tools or Controls Home. This is a Nexus inventory and reversible registration manager; previously Nexus only linked to Windows startup settings.

Review each item's scope, registration state, exact command and location. Filter by **Can pause**, **Saved originals**, **View only** or **Conflicts**, search by app/command/scope, and page results. Commands are shown for review and are never launched by this manager. **Copy details** and **Export inventory** are explicit actions; reports contain commands and private paths.

## Supported controls and permissions

| Registration | Nexus behavior |
| --- | --- |
| Current-user `Run`, supported text/expandable text | Confirmed Pause and exact-original Restore |
| Current-user `RunOnce` | View only |
| Machine-wide `Run` / `RunOnce`, 32-bit and 64-bit views | View only |
| Current-user / shared Startup-folder files | View only; open their folder in Files |
| Nexus startup and recognized Windows infrastructure | Protected; use existing Nexus/Windows controls |
| Unsupported, unreadable or oversized values | Retained; no mutation |
| Services, scheduled tasks, packaged startup tasks and other autorun mechanisms | Use Windows startup settings, Task Manager or the app's own controls |

**Registered** means the registration exists. It does not claim Windows approval is enabled or that the app actually runs. Windows can independently disable or delay startup. Nexus does not read/write undocumented `StartupApproved` binary state. Restoring a registration does not override Windows approval. **Windows startup settings**, **Task Manager** and **Nexus sign-in settings** remain directly available, including when a read/change is busy.

Pause removes only the selected supported account registration after saving its exact original. It does not stop a running app, uninstall software or affect another user's registrations. Restore reinstates the exact original name, command and registry value kind, preserving environment-variable text. Changes affect a future sign-in; Nexus does not launch them to test them.

The backend opens a fixed current-user Run location with query/set rights. It never writes a machine/policy/RunOnce key, requests elevation, enables a privilege or accepts a UI-supplied registry path. Unknown/long/control-character commands and protected infrastructure are refused. Unsupported items show their restriction rather than an ineffective toggle.

## Durable originals and recovery

The new, independent schema-1 `startup-apps.json` lives beside Nexus settings in `%LOCALAPPDATA%\WhiteDreams\NexusShell`. Existing settings/IPC formats need no migration. Records are bound to the Windows account SID, bounded to 128 originals / 2 MiB, and serialized under a cross-process file lease. Atomic replacement retains a `.backup`; temporary files are flushed before acknowledgement.

1. Pause first saves the exact original with **Prepared** state.
2. The Windows backend compares and removes the value inside a registry transaction. A concurrent change or access refusal aborts it. It does not fall back to an unguarded deletion.
3. After acknowledgement, the original becomes **Paused**. If an acknowledgement/write is lost, **Prepared** still retains recovery data.
4. Restore first records **RestorePending**, then writes only if the startup name is still absent (or already exactly matches). A newer command is never overwritten. The record is removed only after acknowledgement.

Refresh observes interrupted states without applying them. If the original already exists, **Restore** only finishes clearing its saved record. If a newer command occupies the name, **Conflict** shows both commands, retains both records, and disables Restore until that conflict is resolved outside this manager. No command is automatically retried or restored on startup.

Corrupt/unknown-schema/foreign-account originals fail closed. Preserve the main file and `.backup` for manual recovery; the manager does not silently substitute an older backup that might omit a more recent pause. Do not remove these files while paused items still need their originals. Copy/export the selected original for manual review if necessary.

One physical startup read/change runs at a time across closed/reopened utility windows. Closing cancels inventory reading between OS calls. An already confirmed mutation finishes its durable protocol independently of the view; an interrupted process leaves its journal state for review. A slow filesystem/registry call retains the slot instead of accumulating blocked workers. Inventory is bounded to 1,000 entries, 2,048 values per registry location and 50 notices; unreadable/omitted locations report partial-inventory notices. Startup folders are enumerated only at their top level, without opening shortcut targets or file contents.

Inventory CSV uses UTF-8 with BOM, quoted/formula-safe cells, observation time and inventory notices. It writes through a flushed temporary file and refuses to overwrite an existing destination.

The implementation follows Microsoft's [Run/RunOnce contract](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys), [transactional registry API](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regcreatekeytransactedw) and [transaction lifetime rules](https://learn.microsoft.com/en-us/windows/win32/api/ktmw32/nf-ktmw32-createtransaction). If Windows refuses the transaction/access, the original remains saved and Windows controls are available.

## Acceptance and manual demo

Use a disposable account/data on Windows 10 19045 and Windows 11. Portable backend/journal tests cover recovery contracts and real atomic journal/export files; they do not prove native registry transactions or WinUI behavior. Native screenshots must come from the running app.

1. Open through Launchpad, Search and menus. Verify current-user/machine/RunOnce/folder scope and commands against Windows/Registry Editor. An already configured profile must also discover the utility. Pin it to the dock and open it: the existing pin owns its running window.
2. Test a harmless, dedicated current-user Run registration. A convenient temporary example in PowerShell is:

   ```powershell
   $run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
   New-Item -Path $run -Force | Out-Null
   New-ItemProperty -Path $run -Name 'NexusStartupDemo' -Value '"%SystemRoot%\System32\notepad.exe"' -PropertyType ExpandString -Force | Out-Null
   ```

   This registers Notepad for a later sign-in; it does not launch it now. In Nexus, select **NexusStartupDemo**, cancel Pause once, then confirm Pause. The value must be absent and **Paused by Nexus** must retain the exact `%SystemRoot%` command. Close/reopen the app and Restore; inspect `GetValueKind` / `DoNotExpandEnvironmentNames` in Registry Editor or PowerShell. Name, expandable type and literal command must match. Remove only the demo afterwards:

   ```powershell
   Remove-ItemProperty -Path $run -Name 'NexusStartupDemo' -ErrorAction SilentlyContinue
   ```

3. While a demo is paused, create a different command under that same name. Refresh: show **Conflict**, both commands and no available Restore. Neither record may be overwritten. Remove the conflicting disposable registration externally, refresh, then restore the saved original.
4. Change/remove a registered demo after Nexus read it but before confirming Pause. Refuse the stale change. Exercise a concurrent writer during a transaction: either the exact selected change commits or it aborts with retained original; never replace/delete the racing value.
5. Deny write access on a disposable test value/key. Pause must fail visibly and retain the original/current registration. Do not change production key ACLs. Verify machine/RunOnce/folder/unsupported/Nexus infrastructure rows never expose Pause.
6. With a debugger, stop after original-save, after registry commit, and before final journal acknowledgement. Restart/reopen and refresh: distinguish already-present originals from absent registrations; offer explicit recovery without automatic replay. Cancelled confirmations must not write anything.
7. Try a corrupt/foreign-account/unknown-schema copy of the disposable journal, a second writer and an unwritable profile. Refuse mutation and preserve files. Restore original test data before continuing.
8. Search/filter/page, copy details, open both Startup folders and Windows controls. Export CSV, including Unicode/quoted names and inventory notices; existing files remain byte-identical on export conflicts.
9. Resize to 500 DIP and a short window; all review/actions remain reachable through scrolling. Test keyboard focus, command selection/copying, 100–200% DPI, High Contrast and repeated close/reopen for bounded handles/threads.

Suggested recording: inventory → filter Can pause → cancel/confirm demo Pause → close/reopen → Restore → conflicting replacement refused → export. Finish by removing the demo registration and ensuring its saved original is no longer needed.
