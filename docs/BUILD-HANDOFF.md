# Nexus Shell 1.3.1 — Windows build handoff

The verified repository baseline is **8659dbbdb1953cd18bfce4025d57e7bb08be059f**, the 1.3.0 CA1416 fix. Its [AppVeyor build 54859892](https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54859892) has a successful commit status; the user reports that build works in a Windows VM. This evidence applies to 1.3.0, not the prepared 1.3.1 source.

This connection has no available GitHub write installation; an earlier branch write was rejected with HTTP 403, “Resource not accessible by integration.” The 1.3.1 source has not been pushed or built on Windows here. Use [SOURCE-PATCH.md](../SOURCE-PATCH.md) to apply the small patch, then commit/push it for AppVeyor.

Both included CI configurations run core checks, the actual PowerShell parser/updater checks, WinUI publishing and compiled-resource/package verification. Versions and artifact names are 1.3.1. The host is published with the same SDK/runtime; differing shared runtime files fail the build.

```powershell
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

A successful build produces full `Nexus-Shell-1.3.1-win-x64.zip` and compatible-runtime `Nexus-Shell-1.3.1-Update-win-x64.zip`, with SHA-256 files. Packages include the host, **Launch-Nexus-Desktop.bat**, **Restore-Windows-Desktop.bat** and the independent recovery PowerShell script. The updater creates a separate version folder and reuses only hash-matching runtime files.

Run the new desktop launcher for a temporary session; exit restores Windows. From preview the Personalize session button performs the handoff. Test the real visibility, work-area, keyboard and host-loss paths in TEST-DESKTOP-MODE.md before using persistent sign-in. The sign-in setting still needs Windows policy permission.

Local core tests run with .NET 8 SDK analyzers and warnings as errors. Host code passes the same analyzer checks. Application code resolves against real pinned WinUI/Windows API references with temporary XAML stand-ins. Native XBF/PRI generation and Windows desktop execution remain pending for 1.3.1; see VALIDATION.md.
