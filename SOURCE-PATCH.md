# Updating the source to 1.3.0

The complete Source ZIP contains the 1.3.0 source tree. Extract it into a fresh folder. The companion Source-Patch ZIP contains three Git patches; choose exactly one matching the unchanged version in `src/Nexus.Shell/Nexus.Shell.csproj`.

On 8 October 2026, repository main at `b8a5071857241ea115012d2e2bf68c8460c31f65` matched all 124 files of the saved 1.1.0 source, byte-for-byte. That commit uses `Nexus-1.1.0-to-1.3.0.patch`. Check the current repository and preserve any local edits before applying.

From the repository root, with the extracted patch saved outside the repository:

```powershell
git apply --check C:\Downloads\Nexus-1.1.0-to-1.3.0.patch
git apply C:\Downloads\Nexus-1.1.0-to-1.3.0.patch
git diff --stat
```

Use the actual patch path. Stop if the check fails. The 1.0.0 and 1.1.0 patches delete the obsolete `src/Nexus.Shell/MainWindow.Canvas.cs`; the 1.2.0 patch does not need that deletion. Each patch includes the new desktop host, native windows, services, tests, assets and build files. All three patches are checked against their saved baselines and reconstruct the complete 1.3.0 tree.

Review and commit the result, then push that commit to the repository's main branch for AppVeyor to build. Use [BUILD-HANDOFF.md](docs/BUILD-HANDOFF.md) for artifact names and Windows commands. The extracted source files must be committed; uploading the Source ZIP alone does not update the repository source.

Source ZIPs are not runnable app updates. Once a Windows build succeeds, preview the full published folder and complete [TEST-DESKTOP-MODE.md](docs/TEST-DESKTOP-MODE.md). If Nexus currently owns sign-in, use **Session → Return to Windows** before launching the new version and selecting it in Personalize. See [UPDATING.md](docs/UPDATING.md) for runtime-compatible updates.
