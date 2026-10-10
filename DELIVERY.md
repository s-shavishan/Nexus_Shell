# Nexus Shell 1.6.0 source update

This is source, not a Windows executable. It upgrades the native Midnight Glass desktop with a mountain wallpaper, top menu bar, floating dock, Spotlight launcher, compact control center, Files grid/list sorting and image previews, shared Notes and a decimal Calculator.

Reliability changes cover native minimize/restore, fullscreen dock priority, own-window event tracking, navigation after failed loads, stale preview guards, note synchronization and theme migration.

The GitHub integration rejected write operations with HTTP 403, Resource not accessible by integration. The remote repository was not updated. Review and commit/push the delivered changes through your usual workflow.

## Complete source

Extract Nexus-Shell-1.6.0-Source.zip into a fresh folder. Start with README.md and docs/MIDNIGHT-GLASS-1.6.0.md. The source is prepared from main commit 0184d98d5a36b433995fbf8d1b9e6e982f434ac2. Dependency versions are preserved; a restore lock file is included.

## Patch for an existing checkout

Extract the patch ZIP outside the repository. The patch includes the wallpaper and all 54 changed/new files. It targets the baseline commit above. Back up local work and run from the repository root in PowerShell, replacing the path below with the actual extracted path:

```powershell
$nexusPatch = 'C:\Downloads\Nexus-Shell-1.6.0-Patch\Nexus-Shell-1.5.0-to-1.6.0.patch'
git apply --check --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch does not apply cleanly; inspect your source version and local edits.' }
git apply --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed.' }
git diff --check
git status --short
```

Do not force an applicability error or apply both the source replacement and the patch to the same checkout.

## Verification and Windows acceptance

All 15 portable test groups passed. All application C# type-checked against restored WinUI/Windows SDK assemblies using temporary XAML field declarations. The source/XML/assets validator passed. The patch applied to a clean baseline and reproduced the exact committed Git tree, including binary assets. SOURCE-VERIFICATION.json records commits and file hashes.

Windows XAML compilation, packaging and native UI acceptance remain pending. The provided source follows the reference concept; pixel-perfect matching has not been verified on Windows. Run the checked-in Windows pipeline and complete docs/MIDNIGHT-GLASS-1.6.0.md before treating a compiled build as validated.
