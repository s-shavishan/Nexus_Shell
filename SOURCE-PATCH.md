# Updating the source to 1.5.0

Extract the delivery ZIP outside the repository. Apply **one** matching patch. Copy only that patch into your source folder; keep APPLY-1.5.0.md and PATCH-VERIFICATION.json outside the repository.

| Your current source | Patch |
| --- | --- |
| GitHub main's working 1.4.2 hotfix at `1cd328f83d28d606fe5e9da7250ee3d792725cd0` | `Nexus-1.4.2-to-1.5.0.patch` |
| Prepared 1.4.3 source | `Nexus-1.4.3-to-1.5.0.patch` |
| Prepared 1.4.4 source | `Nexus-1.4.4-to-1.5.0.patch` |

For current main:

```powershell
git apply --check --whitespace=error-all Nexus-1.4.2-to-1.5.0.patch
git apply --whitespace=error-all Nexus-1.4.2-to-1.5.0.patch
git diff --check
```

For 1.4.3/1.4.4, substitute the matching patch in both commands. Preserve local edits and inspect any applicability error. Do not combine these patches or apply an older upgrade first. The cumulative patch already contains previous UI/dock changes. The complete Source ZIP is an alternative source tree.

Commit/push through the normal workflow. AppVeyor retains .NET core/Win32, PowerShell updater, native WinUI/host, compiled-resource and packaging checks. Expected compiled artifacts are `Nexus-Shell-1.5.0-win-x64.zip` and `Nexus-Shell-1.5.0-Update-win-x64.zip`, with hashes. Neither source ZIP contains an executable.

Read docs/MOTION-DOCK-1.5.0.md before native acceptance. Host/recovery, startup/updater implementation and dependency pins are preserved; native compilation and VM performance remain unverified here.
