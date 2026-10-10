# Updating source to 2.1.0

Use exactly one patch matching your clean baseline:

| Patch | Baseline |
| --- | --- |
| `Nexus-Shell-2.0.0-to-2.1.0.patch` | Previous delivered 2.0.0 source, commit `acf389f3ec92a1f28d84d9f4702caf7d98e928cb` |
| `Nexus-Shell-main-1.5.0-to-2.1.0.patch` | Retrieved GitHub main, commit `0184d98d5a36b433995fbf8d1b9e6e982f434ac2` |

Extract the patch ZIP outside your repository. Preserve local edits first. Do not force an applicability error; choose the correct baseline or use the complete Source ZIP in a fresh folder.

```powershell
$nexusPatch = 'C:\Downloads\Nexus-Shell-2.1.0-Patch\Nexus-Shell-2.0.0-to-2.1.0.patch'
git apply --check --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch does not apply cleanly; inspect your baseline and local edits.' }
git apply --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed.' }
git diff --check
git status --short
```

Review, commit and push through the normal workflow. This delivery creates no remote branch or PR; earlier GitHub connector writes returned HTTP 403. Source delivery is not an executable update.

Build with README.md and complete [2.1 Windows acceptance](docs/CONTROL-CENTER-2.1.0.md). Expected compiled packages are `Nexus-Shell-2.1.0-win-x64.zip` and `Nexus-Shell-2.1.0-Update-win-x64.zip`. Keep the complete current build and recovery helpers together; mixing an older Core with the new desktop leaves the settings service unavailable.
