# Updating source to 3.1.0

The complete Source ZIP is a fresh checkout of the delivery commit. The Patch ZIP contains alternative patch routes; choose exactly one matching your current source baseline.

| Patch | Required source baseline |
| --- | --- |
| `Nexus-Shell-3.0.0-to-3.1.0.patch` | Delivered 3.0.0, commit `6ebc2fedd5c431d645269d40d659d92a965ba943` |
| `Nexus-Shell-2.1.1-to-3.1.0.patch` | Delivered 2.1.1, commit `b40510296b7781c2d0e021c6bf89ecc004c298e1` |
| `Nexus-Shell-main-1.5.0-to-3.1.0.patch` | Retrieved main 1.5.0, commit `0184d98d5a36b433995fbf8d1b9e6e982f434ac2` |

Check the worktree and patch before applying. Preserve your own edits first.

```powershell
git status --short
git rev-parse HEAD
$nexusPatch = 'C:\Downloads\Nexus-Shell-3.0.0-to-3.1.0.patch'
git apply --check $nexusPatch
git apply $nexusPatch
python scripts/validate-source.py
```

Both exported routes are applied to a temporary Git index and verified against the delivery tree. They do not require overlaying old folders or manually deleting retired files.

Build with README.md and complete [Windows acceptance](docs/UI-3.1.0.md). Expected compiled packages are `Nexus-Shell-3.1.0-win-x64.zip` and `Nexus-Shell-3.1.0-Update-win-x64.zip`. All four project versions and the Runtime project lock are 3.1.0. Keep the compiled components and resources together.
