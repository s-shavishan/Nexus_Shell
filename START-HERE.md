# Nexus Shell 0.2.0 — Nexus Orbit

This is a complete **C# + WinUI 3 source project**, not a compiled Windows download. Build it on Windows, or let the included GitHub Actions workflow produce the executable for you.

The source was authored and statically checked in a Linux workspace. It has **not been compiled, launched, or performance-tested on Windows here**. The first successful Windows build is the next validation gate.

This update improves the main desktop, introduces restrained compositor motion and Reduced effects, and changes the full app library to a finite, virtualized view. See `docs/CHANGELOG.md` and the included design reference.

## Test without installing developer tools on your PC

1. Create a GitHub repository for this starter.
2. Extract this ZIP. Upload the **contents of `Nexus-Shell-0.2.0`** into the repository root, including the `.github` folder. Do not put the whole project inside another folder in the repository.
3. Open the repository's **Actions** tab. Select **Build Nexus for Windows** and choose **Run workflow**. A push to `main` or `master` also triggers it.
4. When the job succeeds, open the run and download the **Nexus-Shell-0.2.0-win-x64** artifact. GitHub downloads it as a wrapper ZIP containing the runnable ZIP and its SHA-256 file.
5. Extract the wrapper, then extract `Nexus-Shell-0.2.0-win-x64.zip` to a permanent folder.
6. Run `Nexus.Shell.exe`, or `Launch-Nexus.bat`. Keep every DLL and resource beside the executable.

The build runs on a GitHub Windows runner. You download the finished runtime bundle instead of SDKs and build dependencies. Workflow availability and minutes depend on your GitHub account. This project includes the workflow; no repository has been created or build submitted for you.

A compiled portable package includes .NET and the Windows App SDK runtime. It does not require Visual Studio, the .NET SDK, or Node.js on the test PC. Some PCs may still need Microsoft's [Visual C++ Redistributable (x64)](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist). This project is unsigned.

## Build on your own Windows PC

You can try the smaller command-line route first, without installing the entire Visual Studio IDE:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --exact --source winget
```

Open a fresh PowerShell window in the extracted project folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Run
```

Or double-click **Build-and-Run.bat**. It checks prerequisites and runs the same script. It does not silently install development tools.

The first build restores WinUI, Windows SDK build tools, .NET targeting/runtime packs, and their transitive dependencies from NuGet. Download size depends on your existing caches and installed SDKs. **Do not assume a tiny download or a fixed total.** A full Visual Studio + WinUI development installation is typically a multi-GB route; it is unnecessary just to run the compiled package.

If `dotnet publish` fails with a XAML compiler/MSBuild task error, use the included GitHub workflow. If you already have Visual Studio with Windows/.NET development components, try:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -UseMSBuild -Run
```

The project pins .NET 8 and Windows App SDK **1.8** components for this prototype. The current Microsoft quickstart also describes a newer **.NET 10 + winapp CLI** route. Do not substitute its commands into this project's pinned build without updating the project as a whole. A long-lived release should migrate to a supported current .NET baseline after the prototype is validated.

## What you can test

- Native menu bar, desktop clock, floating dock, home window, and control center.
- Nexus Orbit theme, sidebar, short entrance/hover motion, and Reduced effects.
- **Ctrl+K** opens and focuses app search while Nexus is active.
- Windowed/full-screen mode; **F11** toggles it. **Escape** leaves full screen, or minimizes Nexus when windowed.
- Real pinned-app launching. Firefox, VS Code, and Steam are detected when installed in recognized locations.
- Start-menu shortcut discovery and app search.
- Add `.exe` or `.lnk` files; right-click app tiles to pin/unpin or add to Gaming.
- Enumerate visible windows in your Windows session and request focus switching.
- Links to real sound, network, Bluetooth, and display settings.
- Saved name, pins, focus view, and optional foreground-time summaries.
- Optional sign-in startup for the current user.
- Live **Nexus process** working-set memory and sampled CPU usage.
- Clear activity with a confirmation dialog, and collect diagnostic logs.
- Measure your actual process memory/CPU with `scripts\measure-resources.ps1`.

This is a desktop-surface preview. It does not replace Explorer, disable Windows services, install a Windows service, change your default shell, modify games, or include provider bundles. Existing Nexus downloads, game installation, a persistent dock above other applications, tray-host compatibility, and recovery as the actual Windows shell are later milestones.

## Exit and reset

- Click **Exit to Windows**, or press **Alt+F4**.
- If startup was enabled, turn it off in Control center before moving/deleting the executable. `scripts\disable-startup.ps1` can also remove this project's startup entry.
- Settings and logs: `%LOCALAPPDATA%\WhiteDreams\NexusShell`.
- To reset, close Nexus and rename `settings.json` in that folder. It affects only this prototype's preferences.
- Follow [docs/TEST-WINDOWS.md](docs/TEST-WINDOWS.md) before judging performance or choosing full shell replacement.

## Build outputs

- Runnable folder: `artifacts\Nexus-Shell-0.2.0-win-x64`.
- Optional portable ZIP: run `scripts\package.ps1` after a successful build.
- Build logs: `artifacts\logs\build.txt` and `build.binlog`.
- A NuGet dependency lockfile is created during the first restore. Commit it after a successful Windows restore if continuing development.

## Official references

- [WinUI development tools](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here)
- [Self-contained Windows App SDK deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)
- [WinUI package pinned by this project](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.WinUI/1.8.260803003)
- [Windows App SDK runtime pinned by this project](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.Runtime/1.8.260921001)
- [Windows SDK build tools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/10.0.26100.1)
