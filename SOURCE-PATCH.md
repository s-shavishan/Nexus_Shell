# Updating the source

This tree is the complete 0.9.0 source update, based on main commit `3b6ea3af9ddae35a14d93df92e9e519cf50e3e9e` (the uploaded 0.8.0 source). It includes both build corrections and the desktop redesign. GitHub integration writes returned HTTP 403, so this prepared update still needs to be committed to GitHub and built on Windows.

When applying a source patch manually, merge its contents into the repository root and replace matching files. Preserve existing files that the patch does not mention. Commit the result and build that exact commit in AppVeyor.

The prepared patch contains new files as well as replacements under `files/`. Merge the contents of that directory into the repository root. Include the new `Assets/Icons` SVG files, project changes and CI configuration. GitHub's Upload files view accepts the extracted folders; preserve their paths. Uploading the ZIP itself does not update the application source. `PATCH-MANIFEST.json` records each file's hash.

The AppVeyor binary Update ZIP is a separate deliverable. It reuses compatible runtime files in your existing full app folder; source files are not a runnable update.
