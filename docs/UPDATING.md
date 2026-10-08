# Desktop-mode updates — 1.3.0

The new release includes Nexus.DesktopHost.exe and its DLL/deps/runtimeconfig. They are app-owned update payload, not reusable runtime files. The full release also includes Restore-Windows-Desktop.bat and its independent PowerShell script.

Extract a new full build or apply the compatible-runtime update to create a separate version folder. The updater requests the base path in its console and never opens Explorer. It checks all runtime/payload hashes. Keep the old selected folder. If its Nexus desktop is active, use Session → Return to Windows to close it; only one Nexus version runs in a user session. Preview the new version. In the new build, choose Personalize → Use this version at sign-in, then save work and sign out. The original pre-Nexus desktop backup is retained across version changes. See NEXUS-DESKTOP-MODE.md.

The historical updater implementation notes below remain useful for hash and resource checks; current desktop sign-in controls are in Personalize.

# Updating Nexus 0.7.0

## Small binary update

Download the CI-produced Nexus-Shell-0.7.0-Update-win-x64.zip after the entire build succeeds. Fully exit Nexus first, including a hidden resident instance. Use Exit Nexus in Control center or its notification-area menu.

Extract the Update ZIP and run Apply-Update.bat. Choose your current full runnable folder containing Nexus.Shell.exe. The updater verifies payload/runtime hashes, builds a new version folder, and leaves the old folder intact. Settings remain in the existing local app-data directory.

Launch the new folder. If sign-in startup still points to your old copy, use Use this version at sign-in in Control center. If runtime files differ, use the full binary ZIP once. The three runtime package references have not changed from 0.6.0, but actual compatibility is decided by their published file hashes.

## Source patch

The 0.7.0 source patch targets the complete 0.6.0 source from the previous release. Merge its contents into your repository root; replace existing files and add new files. Then commit and build through AppVeyor. No source deletions are required.

Documentation preview images are omitted from the small source patch and do not affect the app. The complete source includes them.

Both source packages require a Windows build. Neither is a runnable binary update.
