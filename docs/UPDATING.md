# Desktop session updates — 1.3.1

Full and compatible-runtime update packages carry the host, Launch-Nexus-Desktop.bat, Restore-Windows-Desktop.bat and recovery script as new app payload. Published host and shell runtime bytes must agree; only matching runtime files may be reused. The updater always creates a separate new version folder and preserves the base folder/settings.

Exit the old Nexus desktop before launching a new version. Apply the compatible update or extract the full new Windows ZIP. Run Launch-Nexus-Desktop.bat for a temporary session, or Nexus.Shell.exe to preview. Temporary launch/exit leaves sign-in policy unchanged.

If your earlier version was selected for persistent sign-in, keep that folder. Return to Windows from the old session, then select Use this version at sign-in in the new Personalize page if desired. A denied policy restore still allows current-session Windows recovery but leaves the old future sign-in setting requiring permission. See NEXUS-DESKTOP-MODE.md.

Source patch instructions are in SOURCE-PATCH.md. Source ZIPs are not runtime updates. The historical hash/resource implementation notes below retain their original version context.

# Updating Nexus 0.7.0

## Small binary update

Download the CI-produced Nexus-Shell-0.7.0-Update-win-x64.zip after the entire build succeeds. Fully exit Nexus first, including a hidden resident instance. Use Exit Nexus in Control center or its notification-area menu.

Extract the Update ZIP and run Apply-Update.bat. Choose your current full runnable folder containing Nexus.Shell.exe. The updater verifies payload/runtime hashes, builds a new version folder, and leaves the old folder intact. Settings remain in the existing local app-data directory.

Launch the new folder. If sign-in startup still points to your old copy, use Use this version at sign-in in Control center. If runtime files differ, use the full binary ZIP once. The three runtime package references have not changed from 0.6.0, but actual compatibility is decided by their published file hashes.

## Source patch

The 0.7.0 source patch targets the complete 0.6.0 source from the previous release. Merge its contents into your repository root; replace existing files and add new files. Then commit and build through AppVeyor. No source deletions are required.

Documentation preview images are omitted from the small source patch and do not affect the app. The complete source includes them.

Both source packages require a Windows build. Neither is a runnable binary update.
