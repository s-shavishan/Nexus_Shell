# Nexus Shell 1.3.0 — Windows build handoff

The source includes the independent desktop/Sections foundation plus the Pro desktop sign-in route, supervising host, Nexus Files/pickers, keyboard/window switching and recovery script. It is not a runnable Windows artifact.

On 8 October 2026, [s-shavishan/Nexus_Shell](https://github.com/s-shavishan/Nexus_Shell) main was `b8a5071857241ea115012d2e2bf68c8460c31f65` (1.1.0). All 124 repository files matched the saved 1.1.0 source by Git blob hash. Its [AppVeyor Windows build](https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54856956) has a successful commit status. This validates the older baseline, not 1.3.0.

The [GitHub Actions run for that commit](https://github.com/s-shavishan/Nexus_Shell/actions/runs/37723254387) failed before any steps ran: no runner was assigned, and no artifacts or job logs were available. The retrieved evidence does not identify the cause. Use the existing AppVeyor route for this upgrade.

The connected GitHub integration previously rejected branch creation with HTTP 403, “Resource not accessible by integration.” The current read-only connection check returned no GitHub App installations. No upload or new Windows build for 1.3.0 has been performed here.

Use the complete Source ZIP in a new folder, or exactly one Git patch matching your unchanged source baseline: 1.0.0→1.3.0, 1.1.0→1.3.0 or 1.2.0→1.3.0. Check `src/Nexus.Shell/Nexus.Shell.csproj` for the version and run `git apply --check` before applying. The cumulative older patches delete the obsolete MainWindow.Canvas.cs; the 1.2 patch does not need that deletion. Preserve local edits and stop if the check fails.

For the verified repository commit, extract `Nexus-1.1.0-to-1.3.0.patch` from the Source-Patch ZIP. Follow [SOURCE-PATCH.md](../SOURCE-PATCH.md), review the resulting changes, then commit and push them to main. AppVeyor can build the pushed source using the included `appveyor.yml`. Keep the patch outside the repository so it is not committed as source.

Both Windows CI configurations name 1.3.0 artifacts and run core checks, PowerShell updater/parser checks, WinUI publishing and resource/package verification. `build.ps1` also publishes the host and rejects any shared .NET runtime file that differs from the shell publish. The full and small-update packages must contain the new host, and the full package contains the independent recovery BAT/script.

```powershell
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

A successful run creates `Nexus-Shell-1.3.0-win-x64.zip`, its SHA-256 file, and the runtime-compatible `Nexus-Shell-1.3.0-Update-win-x64.zip` with SHA-256. Keep the entire runnable output folder together. Small updates reuse only hash-matching runtime files and create a separate version folder; otherwise use the full runnable ZIP.

Preview with Nexus.Shell.exe first. Complete TEST-DESKTOP-MODE.md on Windows Pro before selecting Nexus at sign-in through Personalize. Recovery is in NEXUS-DESKTOP-MODE.md. No Windows account or registry setting changed during source preparation here.

Local evidence: executed .NET 8 core suite, app API compilation with XAML stand-ins, standalone host C# compilation, source/XML/resource/C# syntax validation and byte-checked source patches/ZIPs. Actual Windows XAML generation, launching, policy permissions, sign-in/recovery, low-level keyboard hooks, fullscreen/work area, DPI and native visual/performance acceptance remain pending.
