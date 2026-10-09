# Start here — Nexus 1.4.2

1. For the original 1.4.2 source that failed with CS0509, apply Nexus-1.4.2-Build-Fix.patch once; see SOURCE-PATCH.md. The corrected full Source ZIP is an alternative source tree.
2. Commit/push and wait for all Windows CI checks. Extract the full `Nexus-Shell-1.4.2-win-x64.zip`, or apply the compatible-runtime Update package into a separate folder.
3. Return to Windows and exit the old Nexus. Launch the new Launch-Nexus-Desktop.bat, or choose Personalize → Use Nexus for this session in its preview.
4. Open Quick Settings. Floating taskbar defaults on; choose Balanced for motion or Fast for the dragging comparison. Animations respect Windows accessibility preferences.
5. Test Sections/Files tuck-and-return, resize/maximize/fullscreen, dock overflow, popup alignment and Exit to Windows. Follow docs/UI-POLISH-1.4.2.md.

This preserves the 1.4.1 taskbar-only takeover. Temporary session mode keeps Explorer running underneath Nexus and does not change saved sign-in policy. The new native frames and motion still require Windows/VM acceptance.
