# Updating the source to 1.4.0

`Nexus-1.3.1-to-1.4.0.patch` targets complete source at repository commit **b493314f5b9118c19077a5ea3be6c1996ba191d0**. All 176 baseline files matched GitHub's Git blob hashes. It adds desktop performance changes, independent Quick Settings, six cached wallpapers, focused checks and versioned package integration.

Extract the Source-Patch ZIP and copy only the `.patch` into your repository root. Preserve local work. Confirm the baseline with `git rev-parse HEAD`, then run:

```powershell
git apply --check --whitespace=error-all Nexus-1.3.1-to-1.4.0.patch
git apply --whitespace=error-all Nexus-1.3.1-to-1.4.0.patch
git diff --check
git diff --stat
```

If the check fails, stop and reconcile your baseline/local changes. The patch contains Git binary additions for PNG assets and must be applied with Git. It is verified by applying to a clean baseline and comparing every result byte-for-byte with the full 1.4.0 source. No added trailing-whitespace errors are present.

Review the source, then commit and push for AppVeyor. The `.gitignore` excludes patch/archive files. Existing package pins remain unchanged. A successful Windows build produces `Nexus-Shell-1.4.0-win-x64.zip` and compatible-runtime `Nexus-Shell-1.4.0-Update-win-x64.zip`.

Use the complete compiled folder. Open Quick Settings → Desktop performance → Fast for the initial VM comparison. Local checks do not establish native frame timing; follow docs/QUICK-SETTINGS-AND-PERFORMANCE.md.
