# Updating Nexus 0.7.0

## Small binary update

Download the CI-produced Nexus-Shell-0.7.0-Update-win-x64.zip after the entire build succeeds. Fully exit Nexus first, including a hidden resident instance. Use Exit Nexus in Control center or its notification-area menu.

Extract the Update ZIP and run Apply-Update.bat. Choose your current full runnable folder containing Nexus.Shell.exe. The updater verifies payload/runtime hashes, builds a new version folder, and leaves the old folder intact. Settings remain in the existing local app-data directory.

Launch the new folder. If sign-in startup still points to your old copy, use Use this version at sign-in in Control center. If runtime files differ, use the full binary ZIP once. The three runtime package references have not changed from 0.6.0, but actual compatibility is decided by their published file hashes.

## Source patch

The 0.7.0 source patch targets the complete 0.6.0 source from the previous release. Merge its contents into your repository root; replace existing files and add new files. Then commit and build through AppVeyor. No source deletions are required.

Documentation preview images are omitted from the small source patch and do not affect the app. The complete source includes them.

Both source packages require a Windows build. Neither is a runnable binary update.
