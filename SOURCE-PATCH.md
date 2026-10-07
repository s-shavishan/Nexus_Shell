# Apply the 0.7.0 source patch

Base: the complete **0.6.0** source from the previous Aura update.

Merge the patch folder's contents into the repository root. Replace matching files and add new files; no source deletions are required. Commit and run AppVeyor. Use the complete 0.7.0 source ZIP if your repository is an earlier version.

PNG/SVG design previews are omitted from the small patch. The UI is drawn by native XAML/code and does not use these documentation images.

`PATCH-MANIFEST.json` includes the base/target versions and changed-file SHA-256 hashes. Applying the patch to 0.6.0 was checked against every non-preview file in the complete 0.7.0 source.

This patch updates source; the CI-produced binary Update ZIP updates the runnable application.
