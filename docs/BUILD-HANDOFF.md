# Nexus Shell 1.4.2 — Windows build handoff

Source baseline: **d98e09ff916c16ee1321bbf285a228bc785f9653**, the 194-file 1.4.1 GitHub tree. Use SOURCE-PATCH.md to apply the matching small patch, then commit/push through the existing workflow. The earlier GitHub write was denied by the integration, so this delivery is source archives/patches.

```powershell
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

Both CI configurations retain .NET platform/core checks, PowerShell parser/updater checks, native WinUI publishing, XBF/PRI resource generation and package verification. Host/UI/manifest and artifact names match 1.4.2. Direct runtime dependencies remain pinned.

Expected artifacts: `Nexus-Shell-1.4.2-win-x64.zip`, `Nexus-Shell-1.4.2-Update-win-x64.zip` and their hashes. The update package reuses runtime files only when their hashes match the selected complete folder. Exit the old version before launching the new folder.

New native frame/region APIs and geometry/migration tests have not been compiled or executed here. CI and the Windows acceptance in UI-POLISH-1.4.2.md are required. Keep the working 1.4.1 build while testing this update.
