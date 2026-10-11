# Updating source to 3.6.0

The complete Source ZIP is a fresh checkout of the delivery commit. The Patch ZIP contains alternative patch routes; choose exactly one matching your current source baseline.

| Patch | Required source baseline |
| --- | --- |
| `Nexus-Shell-3.5.1-to-3.6.0.patch` | Delivered 3.5.1, commit `f90fac3fd59b6ed6ea0742699a96a500c7e5077f` |
| `Nexus-Shell-3.5.0-to-3.6.0.patch` | Delivered 3.5.0, commit `29b9c859c133c1619bd2ef31aabe472379ecce57` |
| `Nexus-Shell-3.4.0-to-3.6.0.patch` | Delivered 3.4.0, commit `dc4a66ab089f440876b04fb3bfcdb41a73025283` |
| `Nexus-Shell-3.3.0-to-3.6.0.patch` | Delivered 3.3.0, commit `b71133477b4735c80acf9fbb39d79c44b8bf87ba` |
| `Nexus-Shell-3.2.0-to-3.6.0.patch` | Delivered 3.2.0, commit `d9896c27f99d320dacd4df24ad9200ba76da276b` |
| `Nexus-Shell-3.1.0-to-3.6.0.patch` | Delivered 3.1.0, commit `b4e86f37df14daa65e9b96dbe6b0c13e6ab519b3` |
| `Nexus-Shell-3.0.0-to-3.6.0.patch` | Delivered 3.0.0, commit `6ebc2fedd5c431d645269d40d659d92a965ba943` |
| `Nexus-Shell-2.1.1-to-3.6.0.patch` | Delivered 2.1.1, commit `b40510296b7781c2d0e021c6bf89ecc004c298e1` |
| `Nexus-Shell-main-1.5.0-to-3.6.0.patch` | Retrieved main 1.5.0, commit `0184d98d5a36b433995fbf8d1b9e6e982f434ac2` |

Check the worktree and patch before applying. Preserve your own edits first.

```powershell
git status --short
git rev-parse HEAD
$nexusPatch = 'C:\Downloads\Nexus-Shell-3.5.1-to-3.6.0.patch'
git apply --check $nexusPatch
git apply $nexusPatch
python scripts/validate-source.py
```

All nine exported routes are applied to a temporary Git index and verified against the delivery tree. They do not require overlaying old folders or manually deleting retired files.

Build with README.md and complete [Windows acceptance](docs/SEARCH-PHASE-02-3.6.0.md). Expected compiled packages are `Nexus-Shell-3.6.0-win-x64.zip` and `Nexus-Shell-3.6.0-Update-win-x64.zip`. All four project versions and the Runtime project lock are 3.6.0. Keep the compiled components and resources together.
