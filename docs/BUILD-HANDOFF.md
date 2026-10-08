# Nexus Shell 1.1.0 — Windows build handoff

## Confirmed repository baseline

Repository: https://github.com/s-shavishan/Nexus_Shell

Default branch: `main`

Checked commit: `531c55f517eb4eba828806f923dcb058de2c231b`

Source version: **1.0.0**

All 111 tracked files in that repository match the saved complete 1.0.0 baseline. There were no conflicting source edits, missing baseline files or repository-only files when checked on 8 October 2026.

GitHub reports the AppVeyor check for that 1.0.0 commit as successful: https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54856850

This status applies to **1.0.0**. The 1.1.0 source has not been uploaded or built through Windows CI. GitHub rejected creating the review branch with HTTP 403, “Resource not accessible by integration.” No repository write succeeded.

## Apply and build

1. Extract `Nexus-Shell-1.1.0-Source-Patch.zip`.
2. Merge the **contents of `files/`** into the existing repository root, preserving relative paths and replacing matching files. Do not upload only the ZIP or add an extra `files/` directory inside the repository.
3. Commit the merged source. Check that `src/Nexus.Shell/Nexus.Shell.csproj` says `1.1.0`, `MainWindow.Polish.cs` exists and the AppVeyor artifact filenames say `1.1.0`.
4. Open the existing AppVeyor project and run **New build** for that exact commit if it has not started automatically.
5. Wait for the complete pipeline, including core checks, updater checks, native XAML build, resource verification and packaging. A local source-check pass does not replace this step.
6. Download `Nexus-Shell-1.1.0-Update-win-x64.zip`, fully exit Nexus, then apply it to the existing complete application folder. Source ZIPs cannot be passed to the binary updater.
7. Choose **Personalize → Solstice** or **Ember**, then follow the visual acceptance list in UI-POLISH.md. Test at 100%, 125% and 150% scaling and with keyboard, mouse and touch/pen where available.

## Follow-up corrections included

| Check | Original 1.1.0 source | Reviewed 1.1.0 source |
|---|---|---|
| Solstice selected sidebar label | 3.81:1 calculated contrast | 9.36:1 using selected text |
| Opal selected sidebar label | 3.92:1 calculated contrast | 9.74:1 using selected text |
| Native button press feedback | Ordinary pointer handlers can be skipped after ButtonBase handles the event | Handled events are observed for visuals; native click/capture behavior remains authoritative |
| Touch/pen press feedback | Mouse-left-button filter | Contact-based check with the left-button restriction applied to mouse input |

Contrast values use representative composited palette backgrounds without native acrylic. Core checks now cover selected rows across all six moods. Core execution, source validation and C# API compilation pass after these corrections. Actual XAML compilation requires Windows; invoking its compiler on Linux stopped at the kernel32 metadata-file dependency.
