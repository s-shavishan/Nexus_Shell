# Start here — Nexus 1.4.2

1. Apply the patch matching your existing source: 1.4.1 → 1.4.2 for current GitHub main, or the cumulative 1.4.0 → 1.4.2 patch for an unchanged older checkout. Apply exactly one; see SOURCE-PATCH.md.
2. Commit/push and wait for all Windows CI checks. Extract the full `Nexus-Shell-1.4.2-win-x64.zip`, or apply the compatible-runtime Update package into a separate folder.
3. Return to Windows and exit the old Nexus. Launch the new Launch-Nexus-Desktop.bat, or choose Personalize → Use Nexus for this session in its preview.
4. Open Quick Settings. Floating taskbar defaults on; choose Balanced for motion or Fast for the dragging comparison. Animations respect Windows accessibility preferences.
5. Test Sections/Files tuck-and-return, resize/maximize/fullscreen, dock overflow, popup alignment and Exit to Windows. Follow docs/UI-POLISH-1.4.2.md.

This preserves the 1.4.1 taskbar-only takeover. Temporary session mode keeps Explorer running underneath Nexus and does not change saved sign-in policy. The new native frames and motion still require Windows/VM acceptance.
