# Start here — Nexus 1.4.1

1. Apply `Nexus-1.4.0-to-1.4.1.patch` to the existing 1.4.0 source; see SOURCE-PATCH.md. The Source ZIP is an alternative complete source tree.
2. Commit/push and wait for the Windows CI build. Extract the entire `Nexus-Shell-1.4.1-win-x64.zip` artifact into a new folder.
3. Return fully to Windows and close the old Nexus. Launch the new `Launch-Nexus-Desktop.bat`, or select Personalize → Use Nexus for this session in its preview.
4. Select Fast in Quick Settings and close panels. Compare dragging the same ordinary application window with Windows, 1.4.0 preview and 1.4.1 session, keeping VM graphics settings unchanged.
5. Confirm only Nexus's taskbar is visible on its primary display and that exit restores Windows. Complete docs/SESSION-TAKEOVER-1.4.1.md.

Source ZIPs/patches require compilation. The source change targets session takeover; Windows/VM acceptance is pending. Temporary session mode does not write protected sign-in policy.
