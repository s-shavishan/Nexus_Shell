# Desktop session updates — 1.4.2

Full and compatible-runtime update packages include the host, launch/recovery helpers, Quick Settings code and all six cached wallpaper assets. The resource report fingerprints the wallpapers; both portable and update packaging verify their bytes. Runtime reuse remains governed by matching hashes. The updater creates a separate version folder and preserves the old folder/settings.

Exit the old Nexus session before running the new full folder. Temporary session launch/exit leaves sign-in policy unchanged. Permanent sign-in configuration remains optional and still requires Windows policy permission. Keep an old folder selected for sign-in until that setting is updated successfully.

For the current small binary update, download `Nexus-Shell-1.4.2-Update-win-x64.zip` after CI succeeds. Return to Windows, extract that Update ZIP and run Apply-Update.bat, selecting your existing complete 1.4.1 or 1.4.0 runnable folder. The updater checks runtime hashes and creates a separate folder; if they differ, use the full ZIP.

Source patch instructions are in SOURCE-PATCH.md. Complete UI-POLISH-1.4.2.md before comparing visual profiles. After a successful Windows build, compare Fast/Balanced/Full in QUICK-SETTINGS-AND-PERFORMANCE.md; no native timing improvement is claimed from source checks alone. Historical update notes follow.

# Updating Nexus 0.7.0

## Small binary update

Download the CI-produced Nexus-Shell-0.7.0-Update-win-x64.zip after the entire build succeeds. Fully exit Nexus first, including a hidden resident instance. Use Exit Nexus in Control center or its notification-area menu.

Extract the Update ZIP and run Apply-Update.bat. Choose your current full runnable folder containing Nexus.Shell.exe. The updater verifies payload/runtime hashes, builds a new version folder, and leaves the old folder intact. Settings remain in the existing local app-data directory.

Launch the new folder. If sign-in startup still points to your old copy, use Use this version at sign-in in Control center. If runtime files differ, use the full binary ZIP once. The three runtime package references have not changed from 0.6.0, but actual compatibility is decided by their published file hashes.

## Source patch

The 0.7.0 source patch targets the complete 0.6.0 source from the previous release. Merge its contents into your repository root; replace existing files and add new files. Then commit and build through AppVeyor. No source deletions are required.

Documentation preview images are omitted from the small source patch and do not affect the app. The complete source includes them.

Both source packages require a Windows build. Neither is a runnable binary update.
