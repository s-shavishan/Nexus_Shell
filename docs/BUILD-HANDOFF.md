# Nexus Shell 1.4.1 — Windows build handoff

Baseline: main commit **a9f813df250f8c5b8f4a3fdadb05804fae867025**, Nexus 1.4.0. AppVeyor build 54863707 succeeded for that baseline; the user reports it is fast with Windows' taskbar present and severely laggy after session takeover in VirtualBox.

The GitHub connection's earlier write returned HTTP 403, so apply SOURCE-PATCH.md and commit/push through your existing workflow. No repository write has been attempted for this update.

Both CI configurations retain core/platform checks, PowerShell parser/updater checks, native WinUI publishing and resource/package verification. Host/UI/manifest and artifact names are 1.4.1. Direct runtime dependencies remain pinned.

```powershell
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

Expected full/small artifacts are `Nexus-Shell-1.4.1-win-x64.zip` and `Nexus-Shell-1.4.1-Update-win-x64.zip`, plus hashes. They contain the matching host and recovery helpers. Exit the old version before launching the new complete folder.

Complete SESSION-TAKEOVER-1.4.1.md. Windows compilation and actual VM acceptance are pending; packaging/source checks do not establish that window-drag lag is resolved.
