# Updating the 1.1.0 source

This is the complete 1.1.0 source, built from the saved 1.0.0 source archive. The companion Source-Patch ZIP targets that exact 1.0.0 tree. If your repository is still on 0.9.0 or earlier, use the complete source so the PC controls services and tests are included.

For the patch, merge the **contents of `files/`** into the repository root, preserving paths and replacing matching files. Keep unrelated existing files. `PATCH-MANIFEST.json` records each replacement's SHA-256. No source files are deleted.

Commit the resulting source and build that exact commit in AppVeyor. Uploading a ZIP file to the repository does not update its extracted source. Once the Windows build succeeds, download `Nexus-Shell-1.1.0-Update-win-x64.zip`, fully exit Nexus, run `Apply-Update.bat` and select the existing full app folder. The updater creates a fresh version folder and verifies compatible runtime files. Dependencies are unchanged from 1.0.0.

Source ZIPs are not runnable app updates. The layout previews are illustrations; native Windows rendering remains to be checked.
