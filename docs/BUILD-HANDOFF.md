# Nexus Shell 1.4.0 — Windows build handoff

Baseline **b493314f5b9118c19077a5ea3be6c1996ba191d0** is verified against all 176 repository files. [AppVeyor build 54860759](https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54860759) has a successful commit status; the user reports temporary session mode works in the VM, with severe dragging lag. This evidence applies to 1.3.1.

The GitHub connection's earlier write was rejected with HTTP 403, “Resource not accessible by integration.” Prepared 1.4.0 source has not been pushed or built on Windows here. Apply SOURCE-PATCH.md, commit and push through the existing repository workflow.

Both CI configurations retain core/platform checks, PowerShell parser/updater checks, native WinUI publishing and resource/package verification. Full/small artifact names are updated to 1.4.0. No package pins were changed. Cached wallpapers are checked in, copied to publish and included in the resource hash report so full/update packaging verifies their bytes. Optional Python asset regeneration is not part of the build.

```powershell
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

Expected artifacts are `Nexus-Shell-1.4.0-win-x64.zip` and `Nexus-Shell-1.4.0-Update-win-x64.zip`, plus hashes. They retain the matching host, temporary launcher and independent Windows recovery helpers. Exit the old version before launching the new complete folder.

Complete QUICK-SETTINGS-AND-PERFORMANCE.md and TEST-DESKTOP-MODE.md. A successful compile alone does not prove smoother window dragging or correct native audio/display behavior. Permanent sign-in policy still requires Windows permission.
