# Updating the source to 1.6.0

The patch targets `s-shavishan/Nexus_Shell` main at `0184d98d5a36b433995fbf8d1b9e6e982f434ac2` (1.5.0). Extract the patch ZIP outside your repository and keep its delivery files there. Preserve local edits before applying; do not force an applicability error.

From the repository root in PowerShell, substitute the actual path to the extracted patch:

```powershell
$nexusPatch = 'C:\Downloads\Nexus-Shell-1.6.0-Patch\Nexus-Shell-1.5.0-to-1.6.0.patch'
git apply --check --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch does not apply cleanly; inspect your source version and local edits.' }
git apply --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed.' }
git diff --check
git status --short
```

The complete Source ZIP is an alternative: extract it into a fresh directory. Both downloads contain source, including the new wallpaper, rather than compiled Windows executables. Review the changes and commit/push through your usual workflow.

Run `python scripts/validate-source.py`, then the checked-in Windows build pipeline or `scripts/build.ps1` and `scripts/package.ps1`. Expected compiled artifacts are `Nexus-Shell-1.6.0-win-x64.zip` and `Nexus-Shell-1.6.0-Update-win-x64.zip`.

Read [the 1.6.0 changes and acceptance sequence](docs/MIDNIGHT-GLASS-1.6.0.md) and [validation evidence](docs/VALIDATION.md). Windows XAML compilation, packaging and native visual acceptance still need to be completed.
