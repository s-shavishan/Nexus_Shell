# Nexus startup acceptance — 1.8.0

Status: prepared source update. Native 1.8 Windows acceptance is pending.

## Behavior

The new readiness window appears after Windows authentication and app resource loading. Its rows complete when Core connects, the saved workspace loads, the desktop is constructed, and desktop/dock surfaces are shown. It closes when desktop startup finishes. Return to Windows cancels startup and asks the managed host to restore Windows. There is no fixed animation duration or arbitrary percentage.

Personalize offers Start Nexus desktop after Windows sign-in. This selects the supervised temporary session launcher in the current user's Run entry; Windows remains the sign-in shell. Windows can delay Run programs. The existing preview startup option remains supported. Unknown commands are preserved, and an owned Nexus shell policy prevents enabling a conflicting startup entry.

Shell replacement remains a separate choice on supported editions. If a confirmed policy action is denied, Nexus requests Windows elevation through its host. The helper verifies that the elevated account SID matches the requesting account before accessing HKCU, and supports only enable/restore. It cannot change policy for another administrator account or override machine-enforced policy. It does not elevate Core, Files, or the desktop.

Restore-Nexus-SignIn.bat requests the same policy-only restore action independently. Restore-Windows-Desktop.bat continues to restore the current desktop separately. Keep the original recovery backup: copying or deleting it is not a policy restore.

## VM sequence

1. Snapshot the VM and record Windows edition/build. Build both check projects, update fixtures, and the full 1.8 package using Windows CI.
2. Close the old Nexus and launch the complete 1.8 folder in preview. Confirm the readiness screen exits into one desktop/Core pair; no artificial startup delay is expected.
3. Open Files and a picker, then repeat the 1.7 isolation, cancellation, Core restart, persistence, and dock checks.
4. Temporarily rename Nexus.Core.exe in the test folder, launch preview, and restore the file afterward. Confirm the startup failure reports clearly and leaves Windows usable. During a slow start, test Return to Windows and closing the readiness window.
5. Run Launch-Nexus-Desktop.bat and exit normally. Confirm saved Windows taskbars and work area return. Force a desktop startup failure in this mode and verify bounded host retries followed by independent Windows recovery.
6. If an older Nexus shell is selected, restore it with Personalize or Restore-Nexus-SignIn.bat. Test accepting and cancelling UAC. A different administrator account must be rejected; the recovery record must remain.
7. Enable Start Nexus desktop after Windows sign-in. Sign out/in or restart. Confirm Windows authenticates normally, Nexus starts through its host, and Exit Nexus restores Windows. If Nexus does not launch, check whether Windows Startup settings or Task Manager disabled its entry. Disable startup in Nexus and repeat sign-in: Nexus must no longer launch automatically.
8. Only after those pass, test the optional shell-replacement path on a supported edition. Confirm the earlier SetShownInSwitchers E_NOTIMPL no longer aborts startup, including Start, Quick Settings, previews, and overview with Explorer absent. Test policy restoration and current-session restoration independently.

Inspect nexus.log, core.log, and desktop-host.log. Empty Core connection probes should no longer be reported as failures; malformed or partial frames should still be rejected. Confirm notes/settings remain intact and no startup cancellation leaves Core or Files running.

Firmware boot graphics, Windows credential UI, service tuning, and durable file-operation jobs remain later milestones.
