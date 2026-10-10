# Nexus 2.1.1 — Start and sign-in acceptance

This is a source update. Build the complete Windows package using README.md. Test in a snapshot of the Windows 10 VM before changing sign-in. Keep the entire compiled folder and its recovery helpers together.

## What the new evidence confirms

The supplied Nexus 2.1.0 logs contain no new unhandled UI exception in this short sample. Core opens acknowledged revisions 0, 4 and 7; the uploaded settings and backup are valid at revisions 8 and 7. The host records a deliberate desktop exit with code 20 (Return to Windows), not a crash. This is not a long-running stability measurement.

Two sign-in replacement attempts fail because the current user Shell policy does not match the saved Nexus recovery record. Automatic startup is then enabled; the host explicitly takes over the Windows taskbar while keeping Explorer's desktop behind Nexus. That startup mode loads Windows first. The attached logs do not contain the current conflicting Shell value or the current recovery record, so its owner cannot be established from these files.

The screenshot shows Windows Start on the Nexus desktop. The user confirmed that pressing the Windows keyboard key causes it; the purple Launchpad button already works. The date widget also displays a clipped locale-formatted date in a narrow day column. 2.1.1 changes that column to the two-digit day and exposes the full date to accessibility.

## Windows-key routing

Start `Launch-Nexus-Desktop.bat` for a supervised managed session. A standalone left or right Windows key should toggle Nexus Launchpad on release. Preview mode leaves the Windows key with Windows. All Windows key combinations pass through; this update does not remap Win+R/E/D/L/I, Win+Shift+S or native window-snap shortcuts.

Routing uses a dedicated hook/message thread, bounded held-key state, and an ordered mask/matching-key-up batch. It replaces the original standalone key-up only if the complete batch succeeds. If Windows blocks injection, the physical key-up passes through and Nexus shows a fallback message; use the dock button. No text, typed history or shortcut content is logged or persisted. The router is disposed before desktop teardown. A timer resynchronizes released keys outside the callback to recover after secure desktops hide key-up events.

Test both Windows keys, key repeats, rapid taps, Win+R followed by ordinary typing, Win+E, Win+D, Win+I, Win+Shift+S, Ctrl/Alt/Shift held before Windows, two Windows keys, and native snap combinations. Repeat after lock/unlock, UAC and returning to Windows. Verify Launchpad focus, typing, Escape dismissal and toggling with other applications foreground. Test with elevated applications: input blocked by Windows must leave keys usable. Native input delivery and the masking behavior cannot be verified on Linux.

## Open Nexus instead of Explorer after sign-in

Windows still boots and authenticates the account. The intended sequence is Windows boot → Windows sign-in → Nexus.DesktopHost → Nexus Core and desktop. The Custom User Interface policy selects the post-authentication shell; adding Nexus to Run does not remove Explorer's first desktop.

1. Open Control Center → Desktop → Review startup and sign-in setup. Personalize shows the active command and identifies conflicts before enabling replacement. Command case and outer whitespace no longer produce a false ownership conflict against the matching saved recovery record.
2. Run `Inspect-Nexus-Startup.bat` from the complete compiled folder if a conflict is shown. It reads user/machine shell values, the Nexus Run entry and recovery record, and makes no changes. Preserve its output for diagnosis. Do not delete the recovery record or blindly replace an unknown command.
3. If the command belongs to an earlier shell/Nexus installation, use its matching restore helper/recovery record. If it is an intentionally configured user policy, review User Configuration → Administrative Templates → System → Custom User Interface in Group Policy. Machine or organization policies need their owner's configuration. The uploaded logs do not establish which case applies.
4. Once the conflict is resolved and the current build passes session tests, choose **Start Nexus instead of the Windows desktop at sign-in…**. Use a stable folder path. The existing helper backs up the previous value before changing only this account's policy and removes the conflicting Nexus Run entry. Windows may request permission for that account.
5. Save other work, sign out/in, and check that Explorer's desktop/taskbar are not shown before Nexus. Test host failure/recovery and both helpers: `Restore-Nexus-SignIn.bat` restores sign-in policy; `Restore-Windows-Desktop.bat` restores the current Windows desktop session.

Nexus preserves unmatched policies. This update improves diagnosis and matching; it cannot safely clear an unknown policy on the VM without its actual value. Microsoft Shell Launcher v2 is a separate Enterprise/Education/IoT feature; the existing Nexus Pro route uses the user Custom User Interface policy.

Repeat [Control Center acceptance](CONTROL-CENTER-2.1.0.md) and existing desktop/startup regressions. Check Start routing after Core restart and after a full Windows restart. Capture updated core/host/Nexus logs and the read-only startup report.

## References

- [Custom User Interface policy](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-winlogon#customshell)
- [Shell Launcher editions and lifecycle](https://learn.microsoft.com/en-us/windows/configuration/shell-launcher/)
- [LowLevelKeyboardProc timing and dedicated-thread requirements](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)
- [SendInput ordering and Windows integrity restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
