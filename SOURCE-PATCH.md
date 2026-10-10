# Updating the source to 1.8.0

The patch delivery contains two alternatives. Use exactly one that matches your clean source baseline:

| Patch | Baseline |
| --- | --- |
| `Nexus-Shell-1.7.0-to-1.8.0.patch` | Previous delivered 1.7.0 source, commit `72b96e9803c6ca57cb2cb1b05634c735fbcf94e7` |
| `Nexus-Shell-main-1.5.0-to-1.8.0.patch` | Retrieved `s-shavishan/Nexus_Shell` main, commit `0184d98d5a36b433995fbf8d1b9e6e982f434ac2` |

Extract the patch ZIP outside the repository. Preserve local edits before applying and do not force an applicability error. In PowerShell from the repository root, substitute the actual path to the matching patch:

```powershell
$nexusPatch = 'C:\Downloads\Nexus-Shell-1.8.0-Patch\Nexus-Shell-1.7.0-to-1.8.0.patch'
git apply --check --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch does not apply cleanly; inspect your source version and local edits.' }
git apply --whitespace=error-all $nexusPatch
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed.' }
git diff --check
git status --short
```

The complete Source ZIP is an alternative: extract it into a fresh directory. Review the changes and commit/push through your usual workflow. No remote branch or pull request was created by this delivery; the GitHub integration previously rejected repository writes with HTTP 403.

Run the commands in README.md and complete [startup acceptance](docs/STARTUP-1.8.0.md). Expected compiled artifacts are `Nexus-Shell-1.8.0-win-x64.zip` and `Nexus-Shell-1.8.0-Update-win-x64.zip`. Native Windows build and execution remain pending.
