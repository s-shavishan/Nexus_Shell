# Updating the source

This tree is the complete 0.8.0 source update, based on main commit `8a3e88d465212bc55b54b6848489a1ab24070286`. It still needs to be committed to GitHub and built on Windows.

When applying a source patch manually, merge its contents into the repository root and replace matching files. Preserve existing files that the patch does not mention. Commit the result and build that exact commit in AppVeyor.

The prepared patch contains new files as well as replacements. GitHub's Upload files view accepts the extracted folders; preserve their paths. Uploading the ZIP itself does not update the application source.

The AppVeyor binary Update ZIP is a separate deliverable. It reuses compatible runtime files in your existing full app folder; source files are not a runnable update.
