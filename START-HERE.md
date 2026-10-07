# Nexus Shell 0.4.1 — desktop startup repair

This release addresses the reported Windows 10 build 19045 failure after MainWindow.xaml loads. The application used AccessibilitySettings.HighContrastChanged, an event unsupported in desktop apps. 0.4.1 removes that event and reads desktop accessibility/animation preferences through guarded Win32 calls. All 0.4.0 workspaces and the existing resource-publishing repair are retained.

## Rebuild with your existing AppVeyor project

1. If your repository already contains Nexus Shell 0.4.0, extract the **Source-Patch ZIP**. Upload the **contents** of `Nexus-Shell-0.4.1-Patch` into your repository root, replacing the matching files. This focused patch assumes the complete 0.4.0 source is already present.
2. To start from a complete source tree, use the full Source ZIP and upload the contents of `Nexus-Shell-0.4.1` instead. Do not create an extra nested project directory.
3. Commit all patched files, including appveyor.yml, scripts, tests and src. Run a new AppVeyor build for that commit.
4. CI runs the existing core checks plus actual Win32 accessibility queries, the PowerShell updater fixtures, the WinUI build, and published/archived resource checks. Use artifacts from a successful run.

The source ZIPs contain no compiled EXE. You do not need local development tools to build through AppVeyor or to run the resulting self-contained app. The source fix has only been checked in the authoring workspace; the changed Windows binary still needs compilation and a launch test.

## Download the smaller app update

Keep the full 0.4.0 app folder that produced your latest log. A startup failure at the old event subscription does not prevent the updater from checking and reusing intact runtime files.

1. Download **Nexus-Shell-0.4.1-Update-win-x64.zip** from your successful AppVeyor build, and extract it into a separate folder.
2. Close Nexus. Double-click **Apply-Update.bat** and choose your full extracted 0.4.0 app folder containing Nexus.Shell.exe.
3. The updater checks the downloaded app payload and the exact runtime bytes in the old folder, then creates a fresh **Nexus-Shell-0.4.1-win-x64** folder beside it. The old folder is retained.
4. Run Nexus.Shell.exe from the newly created folder.

If runtime compatibility checks fail, use **Nexus-Shell-0.4.1-win-x64.zip** once and extract it into a fresh folder. The CI log prints the actual Update ZIP size. Do not replace only the EXE or mix app files from separate builds.

If the default destination already exists, select an unused destination:

```powershell
.\Apply-Update.ps1 -BaseDirectory "C:\Apps\Nexus-Shell-0.4.0-win-x64" -TargetDirectory "C:\Apps\Nexus-Test-0.4.1"
```

If sign-in startup points to the old folder, select **Use this version at sign-in** in Control center after the new build opens.

## Confirm the launch

The latest startup section should contain:

```text
Nexus Shell 0.4.1 started
MainWindow.xaml loaded; configuring window
Reading desktop accessibility settings
Desktop settings ready
MainWindow activation completed
```

The actual log includes timestamps and preference values. Confirm Home is visible and interactive as well; markers alone do not establish successful rendering. If it fails, collect only the newest section:

```powershell
Get-Content "$env:LOCALAPPDATA\WhiteDreams\NexusShell\nexus.log" -Tail 60
```

The old 0.2.x exceptions remain in this appended log and do not describe the current run. The supplied 0.4.0 log already confirms that MainWindow.xaml loaded, before its high-contrast event subscription failed.

## Your data and accessibility

Settings remain in `%LOCALAPPDATA%\WhiteDreams\NexusShell`. This fix introduces no new settings schema. Notes, tasks, pins and saved items use the existing 0.4.0 persistence path. The legacy migration backup remains named `settings.before-0.4.0.json`.

Desktop preferences refresh on startup, activation, and the existing five-second UI tick while Nexus is active. Optional query failures retain last known values; custom motion starts disabled until its preference is available. Unchanged settings do not rebuild Study/Explore/Activity pages. No new timer or runtime package is added.

See docs/TEST-WINDOWS.md for high-contrast, animation preference, minimize/restore and normal-close checks. See docs/VALIDATION.md for observed evidence and checks still pending on Windows.
