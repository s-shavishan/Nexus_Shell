# Nexus Shell 0.2.1 — Nexus Orbit

This is a complete **C# + WinUI 3 source project**, not a compiled Windows download. Build it on Windows, or use the included AppVeyor or GitHub Actions configuration to produce the executable online.

Version **0.2.0 compiled and published successfully in your AppVeyor build** with zero warnings and errors. Its launch on Windows 10 Pro x64 build 19045 then failed in `MainWindow.InitializeComponent()` with a generic `XamlParseException`. Version **0.2.1** is a startup compatibility/diagnostic patch; it has been statically checked here, and still requires a new Windows build and launch test.

The project targets **Windows 10 build 19041 or newer and Windows 11, x64**. The earlier Windows-11-only wording was incorrect. Build 19045 meets this project's minimum.

### Update your existing AppVeyor project

1. Extract this source ZIP and upload the **contents of `Nexus-Shell-0.2.1`** to your existing GitHub repository root. Replace the matching files, including `src`, `scripts`, `appveyor.yml`, and `.github/workflows/build-windows.yml`. Do not upload the ZIP or create a nested `Nexus-Shell-0.2.1` folder in the repository.
2. Commit the update. In AppVeyor, start **New build** for the latest commit, or let its existing push trigger run.
3. Download **`Nexus-Shell-0.2.1-win-x64.zip`** from the successful build's Artifacts tab. Extract it into a **new folder**, keeping all its files together. Run `Nexus.Shell.exe`.
4. Confirm the latest log entry in `%LOCALAPPDATA%\WhiteDreams\NexusShell\nexus.log` says **Nexus Shell 0.2.1**. Successful initialization writes `MainWindow.xaml loaded; configuring window`, followed by `MainWindow activation completed`. These markers establish startup progress, not complete UI acceptance or performance.
5. If launch still fails, close the native error message and copy the latest startup section of `nexus.log`. The improved log includes the HRESULT, available `RestrictedDescription`/other textual native error data, and missing-resource messages when WinUI emits them. You can read the final lines in PowerShell:

```powershell
Get-Content "$env:LOCALAPPDATA\WhiteDreams\NexusShell\nexus.log" -Tail 100
```

No local SDK or Visual Studio installation is required for this route.

The patch supplies ordinary-resource fallback brushes and explicit Light/Dark/HighContrast dictionaries; attaches Control center directly to its anchor button; creates app context menus on realized/recycled containers rather than inside a Style setter; and uses Windows 10's Segoe MDL2 Assets icon font. The Nexus Orbit design and motion policy remain in place. **The original log does not identify an exact failing element or resource, so these loading changes are not a confirmed root-cause fix.** The diagnostic additions are intended to expose that detail if it still fails.

See `docs/CHANGELOG.md`, `docs/VALIDATION.md`, and the included design reference.

### Build fix: NU1605 package downgrade

The first reported AppVeyor run reached NuGet restore, then failed because this project's old Windows SDK BuildTools pin was below the minimum required by Windows App SDK. The corrected source references **10.0.26100.4654**.

If you already uploaded the source, edit `src/Nexus.Shell/Nexus.Shell.csproj` in GitHub's browser editor. Replace the existing BuildTools reference with:

```xml
<PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.4654" PrivateAssets="all" />
```

Commit that change, then start a **New build** for the latest `main` commit in AppVeyor. You do not need to download the source again or install local developer tools for this change. This corrected the reported restore conflict. Your subsequent 0.2.0 AppVeyor build compiled and published successfully; its separate startup failure is addressed by the 0.2.1 update instructions above.

### Build fix: CS7036 button padding

The second reported AppVeyor run restored packages successfully, then reported two C# errors in `MainWindow.xaml.cs`. Both button helpers used a two-argument `Thickness` constructor. WinUI's C# constructors accept one uniform value or four explicit side values.

The corrected source uses `new Thickness(14, 9, 14, 9)` in both `ActionButton` and `AsyncButton`, preserving the intended 14-pixel horizontal and 9-pixel vertical padding. If editing the existing GitHub source in your browser, open `src/Nexus.Shell/MainWindow.xaml.cs` and replace **both** occurrences of `new Thickness(14, 9)` with `new Thickness(14, 9, 14, 9)`. Commit and start a **New build** for the latest `main` commit. The SDK BuildTools correction above is still required.

## Test without installing developer tools on your PC

### AppVeyor: free for public, open-source projects

AppVeyor's [Open-source plan](https://www.appveyor.com/pricing/) offers free hosted builds for public projects, with one concurrent job and a 60-minute limit per job. The build uses AppVeyor's Windows server, independently of GitHub Actions. A GitHub Actions billing lock does not need to be resolved to configure this separate build service, provided your repository remains accessible.

This free route makes your source public. Use it only if that suits your project; private projects are on paid plans. Choose **Open-source / FREE**, rather than the private-project trial.

1. Put the **contents of `Nexus-Shell-0.2.1`** in your public GitHub repository root. `appveyor.yml`, `Nexus.Shell.sln`, and the `scripts` and `src` folders must be at the same level. Do not upload only the ZIP.
2. If you already uploaded the previous source, you can add just `appveyor.yml` using GitHub's **Add file > Create new file** in your browser. Copy this configuration:

```yaml
image: Visual Studio 2022

build_script:
  - ps: .\scripts\build.ps1 -UseMSBuild
  - ps: .\scripts\package.ps1

test: off
deploy: off

artifacts:
  - path: artifacts\Nexus-Shell-0.2.1-win-x64.zip
  - path: artifacts\Nexus-Shell-0.2.1-win-x64.zip.sha256
```

3. [Create a free AppVeyor account](https://ci.appveyor.com/signup/free). Add a new project, connect GitHub, and select this repository. Public-repository access is enough for this route.
4. Click **New build**. The configuration selects the **Visual Studio 2022** Windows image, then restores, compiles, publishes, and packages Nexus. The tools and NuGet downloads stay on the build server.
5. When the build succeeds, open its **Artifacts** tab. Download `Nexus-Shell-0.2.1-win-x64.zip`, extract it, and run `Nexus.Shell.exe` or `Launch-Nexus.bat`. Keep every DLL and resource in the extracted folder.

No Visual Studio or SDK download is needed on your PC. You still need to download the finished app bundle to run a native Windows application locally. Your 0.2.0 AppVeyor bundle was 84,444,232 bytes (about 84.4 MB). The next package size can differ; this source ZIP is not the runnable bundle.

If the build fails, use AppVeyor's console log to find the first build error. A successful source upload is not evidence of successful compilation. This configuration has been checked locally. Your first run failed at restore with NU1605; the second reported the two button-padding compile errors corrected above; the third compiled and published 0.2.0 successfully. Your Windows 10 launch log then showed a MainWindow XAML parsing failure. Successful launch of this 0.2.1 patch is still required. The authoring workspace has not submitted or executed a cloud build.

### GitHub Actions: if your account can run workflows

1. Create a GitHub repository for this starter.
2. Extract this ZIP. Upload the **contents of `Nexus-Shell-0.2.1`** into the repository root, including the `.github` folder. Do not put the whole project inside another folder in the repository.
3. Open the repository's **Actions** tab. Select **Build Nexus for Windows** and choose **Run workflow**. A push to `main` or `master` also triggers it.
4. When the job succeeds, open the run and download the **Nexus-Shell-0.2.1-win-x64** artifact. GitHub downloads it as a wrapper ZIP containing the runnable ZIP and its SHA-256 file.
5. Extract the wrapper, then extract `Nexus-Shell-0.2.1-win-x64.zip` to a permanent folder.
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

If `dotnet publish` fails with a XAML compiler/MSBuild task error, use either included Windows cloud-build route. If you already have Visual Studio with Windows/.NET development components, try:

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

- Runnable folder: `artifacts\Nexus-Shell-0.2.1-win-x64`.
- Optional portable ZIP: run `scripts\package.ps1` after a successful build.
- Build logs: `artifacts\logs\build.txt` and `build.binlog`.
- A NuGet dependency lockfile is created during the first restore. Commit it after a successful Windows restore if continuing development.

## Official references

- [AppVeyor pricing and free public-project plan](https://www.appveyor.com/pricing/)
- [AppVeyor project setup](https://www.appveyor.com/docs/)
- [AppVeyor Windows images and installed tools](https://www.appveyor.com/docs/windows-images-software/)
- [AppVeyor YAML reference](https://www.appveyor.com/docs/appveyor-yml/)
- [WinUI development tools](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here)
- [Self-contained Windows App SDK deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)
- [WinUI package pinned by this project](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.WinUI/1.8.260803003)
- [Windows App SDK runtime pinned by this project](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.Runtime/1.8.260921001)
- [Windows SDK build tools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/10.0.26100.4654)
- [Windows App SDK Base dependency requirements](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.Base/1.8.251216001)
- [NuGet NU1605 package downgrade](https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1605)
- [WinUI Thickness values and C# constructors](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.thickness?view=windows-app-sdk-1.8)
