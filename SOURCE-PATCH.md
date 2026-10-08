# Updating the source

This tree is the complete 1.0.0 source update, based on main commit `3678211db5d66f30ceebd58a4672ba076a02fa0a` (the uploaded 0.9.0 source). It adds the native PC controls workspace and WinUI refinements. Commit this update to GitHub and build that exact commit on Windows.

When applying a source patch manually, merge its contents into the repository root and replace matching files. Preserve existing files that the patch does not mention. Commit the result and build that exact commit in AppVeyor.

The prepared patch contains new files as well as replacements under `files/`. Merge the contents of that directory into the repository root. Include the new PC services, UI partial, tests, project changes and CI configuration. Preserve the existing `Assets/Icons` directory. GitHub's Upload files view accepts the extracted folders; preserve their paths. Uploading the ZIP itself does not update the application source. `PATCH-MANIFEST.json` records each file's hash.

The AppVeyor binary Update ZIP is a separate deliverable. It reuses compatible runtime files in your existing full app folder; source files are not a runnable update.
