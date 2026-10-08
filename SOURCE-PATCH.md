# Updating the source to 1.3.1

`Nexus-1.3.0-to-1.3.1.patch` targets the complete source at repository commit **8659dbbdb1953cd18bfce4025d57e7bb08be059f**, the 1.3.0 CA1416 fix. All 172 baseline files were verified against GitHub's Git blob hashes. The patch adds temporary desktop takeover, policy-independent recovery, a saved desktop session record, launch/package integration and checks.

Extract the Source-Patch ZIP and copy **only the .patch** into your repository root. Preserve local work, then run from that root:

```powershell
git apply --check Nexus-1.3.0-to-1.3.1.patch
git apply Nexus-1.3.0-to-1.3.1.patch
git diff --stat
```

Stop if `--check` fails; do not force the patch over a different or modified baseline. The existing `.gitignore` excludes `*.patch`. Review the new and changed source files, commit them, and push to main for AppVeyor. This patch is checked by applying it to the verified baseline and comparing every resulting file with the full 1.3.1 source.

The patch and full Source ZIP are source code, not binary updates. The next successful Windows build should produce `Nexus-Shell-1.3.1-win-x64.zip` and `Nexus-Shell-1.3.1-Update-win-x64.zip`. Keep the entire runnable folder together.

Once built, exit the old Nexus version, then run **Launch-Nexus-Desktop.bat** in the new full folder. This temporary mode hides the Windows desktop/taskbars and restores them on exit without changing sign-in settings. `Nexus.Shell.exe` remains preview. Complete docs/TEST-DESKTOP-MODE.md in the VM.
