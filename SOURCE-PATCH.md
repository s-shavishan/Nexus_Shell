# Updating source to 2.0.0

Use exactly one patch matching your clean baseline:

| Patch | Baseline |
| --- | --- |
| `Nexus-Shell-1.8.0-to-2.0.0.patch` | Previous delivered 1.8.0 source, commit `1b8646766c87d589ee7074f72c7c1b1e25b6958d` |
| `Nexus-Shell-main-1.5.0-to-2.0.0.patch` | Retrieved GitHub main, commit `0184d98d5a36b433995fbf8d1b9e6e982f434ac2` |

Extract the patch ZIP outside your repository. Preserve local edits first. Do not force an applicability error; choose the correct baseline or use the complete Source ZIP in a fresh folder.

```powershell
$nexusPatch = 'C:\Downloads\Nexus-Shell-2.0.0-Patch\Nexus-Shell-1.8.0-to-2.0.0.patch'
git apply --check --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch does not apply cleanly; inspect your baseline and local edits.' }
git apply --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed.' }
git diff --check
git status --short
```

Review, commit and push through the normal workflow. This delivery creates no remote branch or PR; earlier GitHub connector writes returned HTTP 403. Source delivery is not an executable update.

Build with README.md and complete [2.0 Windows acceptance](docs/MAJOR-2.0.0.md). Expected compiled packages are `Nexus-Shell-2.0.0-win-x64.zip` and `Nexus-Shell-2.0.0-Update-win-x64.zip`. Keep the current recovery helper with the current build; older helpers do not restore the newly journaled minimized-window arrangement.
