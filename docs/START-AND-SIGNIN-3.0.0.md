# Start and sign-in in Nexus 3.0.0

Begin with [Session Core Windows acceptance](SESSION-CORE-3.0.0.md). Preview uses `Launch-Nexus.bat`; a managed Nexus session uses `Launch-Nexus-Desktop.bat`. Test the new work-area/menu-bar behavior in the managed session.

Standalone left/right Win toggles Nexus Launchpad in managed sessions; Win+E, Win+R, Win+L and other native combinations pass through. Check repeats, held modifiers, UAC/secure-desktop return and an elevated foreground app. If input injection is denied, the physical key release passes through and the dock Launchpad button remains available.

Sign-in replacement remains a separate user-policy operation after Windows authentication. First run `Inspect-Nexus-Startup.bat` and inspect the selected command and recovery owner in Personalize. Foreign policy is preserved. Startup alongside Windows uses the Run entry and can show Windows before Nexus; it is not sign-in shell replacement. Keep `Restore-Nexus-SignIn.bat` and `Restore-Windows-Desktop.bat` from this same compiled build available.

The prior VM logs showed a policy-ownership conflict. No matching diagnostic output was attached to this request, so this release does not assert that that machine's sign-in configuration has been repaired.
