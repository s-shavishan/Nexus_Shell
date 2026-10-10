# Nexus Shell 1.6.0 — Midnight Glass

A native C# / WinUI desktop surface for Windows, with an independent desktop, dock, launcher, file browser, Control Center and workspace window.

This update follows the Midnight Glass reference: a night-mountain wallpaper, compact desktop menus, centered Spotlight launcher, colorful dock artwork, three-pane Files, floating Notes and Calculator, and a date/task card with your actual Study tasks.

## What changed

- **Files:** switch between grid and list, sort by name/date/size/type, navigate back and forward, filter the current folder, preview raster images, and inspect file metadata. Pickers still support single/multiple selection, folders and save destinations.
- **Notes:** a floating editor with search, pinning and deletion confirmation. Board notes are shared with Explore; Quick note is shared with Study. Existing notes are preserved.
- **Calculator:** decimal arithmetic, percent, sign, backspace, chained operations and repeated equals. Invalid calculations show a recoverable error.
- **Control Center:** connection status, real audio and brightness controls, visual profiles, dock preferences and a Focus desktop action that hides desktop icons and the date/task card.
- **Window reliability:** Files and Sections use native minimize. Own content-window notifications update the dock; utility windows participate in previews, overview, Show desktop and shutdown. Fullscreen takes priority over stale dock interaction holds.
- **Themes:** canonicalize saved Graphite/Lagoon/Pearl aliases without resetting content. Each floating window owns its material and fallback.

Select **Sections → Personalize → Midnight Glass** if an existing saved theme is active. New installations use Midnight Glass by default. App icons are original Nexus vectors; Windows system dialogs and external application windows retain their own appearance.

## Build on Windows

Requires the existing .NET 8 SDK and Windows app build tools. Dependency versions are pinned in the project and lock file.

```powershell
python scripts/validate-source.py
dotnet run --project tests/Nexus.Core.Checks/Nexus.Core.Checks.csproj -c Release
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

GitHub Actions and AppVeyor package `Nexus-Shell-1.6.0-win-x64.zip` and the smaller `Nexus-Shell-1.6.0-Update-win-x64.zip`. Follow [portable startup](docs/RUN-PORTABLE.md) and [desktop session setup](docs/NEXUS-DESKTOP-MODE.md).

## Validation

The portable core checks pass, including the new arithmetic, notes, file sorting/history, fullscreen and theme migration regressions. C# type checking against the pinned WinUI and Windows SDK assemblies passes with temporary XAML field declarations. Source/XML/resource/runtime-template checks pass.

Native Windows XAML compilation, PRI/XBF packaging, actual material rendering and Windows desktop acceptance are still required. See [validation details](docs/VALIDATION.md) and [the acceptance checklist](docs/MIDNIGHT-GLASS-1.6.0.md). The supplied concept is a design reference; pixel-level equivalence has not been measured on Windows.
