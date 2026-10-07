# Nexus Shell 0.4.0 — your everyday orbit

A complete native C# + WinUI 3 source release. The project targets Windows 10 x64 build 19041 or newer, including your build 19045, and Windows 11. Build with your existing AppVeyor project; no local development SDK is needed to use the published app.

## What changed

- Window overview replaces the old running-app list with searchable, responsive native cards. Ctrl+4 opens it; select a card to switch windows. It refreshes every five seconds while Nexus and this view are active, and releases its item source when you leave. Up to 80 visible windows in the current session; Windows controls whether focus switching is allowed.
- Study now has a local checklist: add tasks, choose a focus task from its options menu, complete/reopen tasks, remove tasks, and clear completed tasks. Open tasks also appear in Ctrl+K search. A timer finishing does not automatically check off your task.
- Focus sessions save their remaining time. Normal close preserves it exactly; reopening restores it paused. While running, checkpoints use the existing coalesced save about every 30 seconds. Time spent away from a closed app is not credited as focus. Crash recovery can lose time since the last completed save.
- Explore gains favorites, named collections, collection/favorite filters, and an edit dialog for title, collection, favorite status, and link address. Favorites appear first and up to four appear on Home. Existing saved items migrate to Personal.
- Home brings together your current task, three open tasks, and favorite items, alongside pins, shared notes, and the dock.
- Control center can repair sign-in startup when it points to an older folder. The small updater now requires new app binaries and verified PRI/XBF resources in the downloaded payload.
- Existing command search, desktop moods, finite motion, reduced effects, usage settings, and the resource-publishing repair are retained. Ctrl+1 returns Home; Ctrl+2 opens Explore; Ctrl+3 opens Study.

## Build in AppVeyor

1. Extract the Source-Patch ZIP and upload the **contents** of `Nexus-Shell-0.4.0-Patch` into your existing repository root, replacing matching files. Include `src`, `scripts`, `tests`, `appveyor.yml`, `.github`, and root configuration files. Do not create a nested project folder.
2. The full Source ZIP can also replace the root using the contents of `Nexus-Shell-0.4.0`. It additionally includes the documentation preview assets.
3. Commit, then start **New build** for that commit in your existing AppVeyor project.
4. Windows CI runs the core behavior checks and actual PowerShell packager/updater fixture checks, builds WinUI, inspects the published MainWindow resource with MakePri, and checks archived resource hashes. Only use artifacts from a successful run.

## Download less

Choose **`Nexus-Shell-0.4.0-Update-win-x64.zip`** from AppVeyor when you still have an extracted full Nexus app folder.

1. Extract the entire Update ZIP into a separate folder.
2. Close Nexus and double-click **Apply-Update.bat**.
3. Select your old full app folder containing Nexus.Shell.exe.
4. The updater verifies downloaded app files and the exact runtime bytes already on your PC, then assembles a fresh `Nexus-Shell-0.4.0-win-x64` folder beside the old folder.
5. Run Nexus.Shell.exe from that new folder. If sign-in startup points to the old folder, choose **Use this version at sign-in** in Control center.

If runtime compatibility checks fail, download **`Nexus-Shell-0.4.0-win-x64.zip`** once and extract it into a fresh folder. CI prints the actual small-update size; it is not estimated here. This saves network transfer but still needs local disk space for a full new app folder.

If the destination already exists, use an unused path:

```powershell
.\Apply-Update.ps1 -BaseDirectory "C:\Apps\MyOldNexus" -TargetDirectory "C:\Apps\Nexus-Test-0.4"
```

## Your data and first launch

Settings remain in `%LOCALAPPDATA%\WhiteDreams\NexusShell`. Pins, notes and saved items are preserved by migration. Before upgrading older settings, the app attempts a one-time `settings.before-0.4.0.json` backup in that folder. Keep Nexus closed before manually restoring a settings backup. Older Nexus versions do not understand tasks and collections and can drop those fields when they save; preserve a current settings copy before rolling back.

Check the latest startup section:

```powershell
Get-Content "$env:LOCALAPPDATA\WhiteDreams\NexusShell\nexus.log" -Tail 100
```

It should say **Nexus Shell 0.4.0**, then `MainWindow.xaml loaded; configuring window`, then `MainWindow activation completed`. If it fails, share that latest section and Nexus.resources.json. The last supplied Windows launch evidence is still the 0.2.1 missing MainWindow resource; the publishing repair is retained and needs confirmation on your PC.

Source structural checks pass in the authoring workspace. No Windows runtime, .NET compiler, or PowerShell host is available here. The 0.4.0 build, new behavioral checks, native appearance, animation reliability, and CPU/memory results need Windows verification. The image is a code-drawn design reference, not a native screenshot.
