# Updating source to 2.1.1

Use exactly one patch matching your clean baseline:

| Patch | Baseline |
| --- | --- |
| `Nexus-Shell-2.1.0-to-2.1.1.patch` | Previous delivered 2.1.0 source, commit `5b5a11538c2c6195fc6fde50751890c67c531378` |
| `Nexus-Shell-main-1.5.0-to-2.1.1.patch` | Retrieved GitHub main, commit `0184d98d5a36b433995fbf8d1b9e6e982f434ac2` |

Extract the patch ZIP outside your repository. Preserve local edits first. Do not force an applicability error; choose the correct baseline or use the complete Source ZIP in a fresh folder.

```powershell
$nexusPatch = 'C:\Downloads\Nexus-Shell-2.1.1-Patch\Nexus-Shell-2.1.0-to-2.1.1.patch'
git apply --check --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch does not apply cleanly; inspect your baseline and local edits.' }
git apply --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed.' }
git diff --check
git status --short
```

Review, commit and push through the normal workflow. This delivery creates no remote branch or PR; earlier GitHub connector writes returned HTTP 403. Source delivery is not an executable update.

Build with README.md and complete [2.1 Windows acceptance](docs/START-AND-SIGNIN-2.1.1.md). Expected compiled packages are `Nexus-Shell-2.1.1-win-x64.zip` and `Nexus-Shell-2.1.1-Update-win-x64.zip`. Keep the complete current build and recovery helpers together; mixing an older Core with the new desktop leaves the settings service unavailable.
