# Nexus Shell 1.4.2 — Windows build handoff

Build-fix baseline: **af26198808fbc31f92275847cabfdf461093e35f**, the 199-file original 1.4.2 GitHub tree. Use SOURCE-PATCH.md to apply Nexus-1.4.2-Build-Fix.patch, then commit/push through the existing workflow. The earlier GitHub write was denied by the integration, so this delivery is source archives/patches.

```powershell
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

Both CI configurations retain .NET platform/core checks, PowerShell parser/updater checks, native WinUI publishing, XBF/PRI resource generation and package verification. Host/UI/manifest and artifact names match 1.4.2. Direct runtime dependencies remain pinned.

Expected artifacts: `Nexus-Shell-1.4.2-win-x64.zip`, `Nexus-Shell-1.4.2-Update-win-x64.zip` and their hashes. The update package reuses runtime files only when their hashes match the selected complete folder. Exit the old version before launching the new folder.

The supplied AppVeyor log records passed core/platform/geometry/migration checks and PowerShell updater checks. UI compilation failed at TaskbarView's sealed Border inheritance (CS0509), before publish/package completion. The fix uses a Grid containing a Border and adds a source guard for that mistake. The corrected native build and Windows acceptance in UI-POLISH-1.4.2.md remain required. Keep the working 1.4.1 build while testing this update.
