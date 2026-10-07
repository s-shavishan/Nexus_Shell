# 0.8.0 quick start

Use the repository's latest 0.8.0 commit and let AppVeyor build it. After the entire build succeeds, download `Nexus-Shell-0.8.0-Update-win-x64.zip` to update a compatible existing full app folder, or the full `Nexus-Shell-0.8.0-win-x64.zip` for a fresh installation.

Fully exit Nexus before applying an update. On first upgrade, saved links migrate into Personal, notes/tasks/pins are preserved, a settings backup is made and Opal is selected. Check the current Windows acceptance list in docs/TEST-WINDOWS.md.

The older notes below describe the project history.

# Nexus Shell 0.7.0 — start here

This archive is source. Build it through AppVeyor to get the runnable Windows application.

1. If your repository contains **0.6.0 source**, extract `Nexus-Shell-0.7.0-Source-Patch.zip` and merge the extracted folder's contents into the repository root. Replace matching files and add new files; include `src`, `tests`, `scripts` and CI configuration. Do not nest the patch folder inside your repository.
2. If your source is earlier than 0.6.0, use the complete `Nexus-Shell-0.7.0-Source.zip` as the new source tree.
3. Commit and run AppVeyor. Wait for behavior checks, publish/resource checks and packaging to succeed.
4. Download `Nexus-Shell-0.7.0-Update-win-x64.zip`. Fully exit any running/hidden Nexus instance through Control center or its notification-area menu.
5. Extract the binary Update ZIP, run `Apply-Update.bat`, and choose your current full binary app folder containing `Nexus.Shell.exe` and its runtime files. The updater creates a new version folder and keeps the old one.
6. Launch the new folder. If runtime hashes do not match, use the full `Nexus-Shell-0.7.0-win-x64.zip` instead.
7. If startup still points to the old folder, choose **Use this version at sign-in** in Control center.

No local developer installation is needed for this route. Source ZIPs cannot be passed to the binary updater.

## First things to try

- **More → Personalize**: choose a mood, compact dock, clock format and visible Home cards.
- Navigate Home → Apps → Study; use Alt+Left/Right or the header arrows.
- Ctrl+K: try Apps, Saved, Workspaces and Windows categories. The current window list refreshes when search opens.
- Select an app, saved item or workspace in search, then reopen All to see recent choices. Clear/disable these in Personalize.
- Hide Home notes, return to Home and then show them again. The text should remain saved.

Prepared source checks pass. Windows compilation and native acceptance still need to run. Keep the complete published folder together; the existing resource publishing and accessibility safeguards remain in place.
