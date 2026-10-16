# Nexus Shell

A native C# / WinUI 3 desktop shell for Windows 10 and Windows 11. Windows provides drivers, security and application compatibility; Nexus provides the desktop, dock, Launchpad, Files, Search, Controls and System Monitor.

## Repository

| Path | Purpose |
| --- | --- |
| `src/Nexus.Shell/` | WinUI application, desktop surfaces, input and Windows integration |
| `src/Nexus.Core/` | Supervised settings, file/search workers and Windows backends |
| `src/Nexus.Runtime/` | Shared contracts, persistence, session policies and feature engines |
| `src/Nexus.DesktopHost/` | Session supervision, startup and independent Windows recovery |
| `build/` | Windows build, compiled-resource verification, packaging and updates |
| `tools/` | Operational diagnostics, startup inspection and recovery scripts |
| `docs/` | Current architecture, development and running instructions |

## Build

Use Windows x64, .NET 8 SDK, Visual Studio 2022 with Windows application development tools, and the Windows SDK. Dependencies restore from NuGet.

```powershell
.\build\build.ps1 -UseMSBuild
.\build\package.ps1
```

Open `Nexus.Shell.sln` to develop in Visual Studio. `Build-and-Run.bat` builds and opens the preview. AppVeyor runs the native build and packaging workflow.

The full portable output is `artifacts/Nexus-Shell-3.6.0-win-x64/`. Keep its executables, dependencies, resources, assets and recovery helpers together. Source files are not a runnable release.

## Files workspace

Browse several folders in one Files window with up to 12 independent tabs. Use Ctrl+T, Ctrl+W, Ctrl+Tab and Ctrl+Shift+T; right-click a folder to open it in a new tab. Each open tab retains its history, filter, selection, sorting and view. External folder requests reuse a matching tab or open another. File pickers keep a single location.

See [Files tabs and manual checks](docs/FILES-WORKSPACE.md).

## Storage Analyzer

Find large folders and files from Launchpad, Search, Nexus Tools or Controls. Scan a chosen folder/drive, drill into folder totals, inspect file types and skipped paths, open results in Files, and export a CSV. Scans show real logical sizes and explicit partial results; they never delete data or follow links/cloud-recall items.

See [Storage Analyzer and manual checks](docs/STORAGE-ANALYZER.md).

## Development guides

- [Architecture and source ownership](docs/ARCHITECTURE.md)
- [Build and contribution guide](docs/DEVELOPMENT.md)
- [Running, updates and recovery](docs/RUNNING.md)

This tree contains production source and operational build/recovery tooling. Test projects, embedded test fixtures, duplicate patch trees, generated previews and historical build reports are excluded. Git history retains earlier development records.

Native Windows usability and hardware acceptance remain separate from a successful build. Current work remains on its feature branch until the owner authorizes merging.
