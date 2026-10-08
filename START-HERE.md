# Nexus Shell 1.1.0 — start here

This is source for the Solstice UI upgrade. Build it through AppVeyor to get the runnable Windows application.

1. If your repository contains the saved **1.0.0 source**, extract `Nexus-Shell-1.1.0-Source-Patch.zip` and merge the **contents of `files/`** into the repository root, preserving paths and replacing matching files. The patch adds files and replaces files; it deletes none.
2. For **0.9.0 or earlier**, use the complete `Nexus-Shell-1.1.0-Source.zip` so the native PC controls from 1.0.0 are included. Commit the extracted source, rather than only uploading the ZIP to the repository.
3. Run AppVeyor for that exact commit. Wait for the Windows build, behavior checks, resource verification and packaging to succeed.
4. Download `Nexus-Shell-1.1.0-Update-win-x64.zip`. Fully exit Nexus, extract the update and run `Apply-Update.bat`, selecting the existing full application folder. If its runtime verification fails, use the complete `Nexus-Shell-1.1.0-win-x64.zip` instead.
5. Open **More → Personalize → Solstice** for the warm light direction, or **Ember** for warm dark surfaces. Existing saved mood choices stay selected after updating.

Notes, tasks, pins and Explore spaces retain their existing storage. Source ZIPs are not binary app updates and cannot be used with the binary updater.

Read [BUILD-HANDOFF.md](docs/BUILD-HANDOFF.md) for the confirmed repository and AppVeyor handoff. Read [UI-POLISH.md](docs/UI-POLISH.md) for the reference analysis and Windows visual review. Core checks, C# API checks and source validation pass locally; the full Windows XAML build and native appearance still need validation. The included previews are source-derived layout illustrations with sample content.

Use [PC-CONTROLS.md](docs/PC-CONTROLS.md) and [TEST-WINDOWS.md](docs/TEST-WINDOWS.md) to check existing audio, window controls, input and update behavior on Windows.
