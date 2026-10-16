# Development

## Requirements

Build on Windows x64 with .NET 8 SDK and Visual Studio 2022's Windows application development/MSBuild tools. The application targets Windows App SDK/WinUI 3; Linux C# compilation alone does not build native XAML or prove Windows behavior.

`global.json` selects the .NET SDK family. Exact Windows package dependencies remain in `Nexus.Shell.csproj` and its lock file; do not delete these as generated clutter.

## Local workflow

1. Open `Nexus.Shell.sln` and choose Debug or Release, x64.
2. Run `build/build.ps1 -UseMSBuild` from the repository. Use `-Run` to open the published preview.
3. Run `build/package.ps1` to produce full and compatible-runtime update ZIPs with SHA-256 sidecars.
4. Keep all generated files under `artifacts/`; do not commit build output or local logs.

The build publishes Shell, DesktopHost and Core, checks shared runtime compatibility, then verifies compiled PRI/XBF resources and shipped assets. A fresh publish directory prevents old diagnostic helpers from entering a new package. AppVeyor uses the same build and packaging entry points. Its success now describes build/package validation; removed test suites and native UI experiments no longer run in CI.

## Editing source

- Find features through the source map in ARCHITECTURE.md. Services and native API wrappers are grouped by feature, and each project owns its files.
- Preserve the existing IPC/schema and native identities when moving code. Keep `App.xaml`, `MainWindow.xaml`, their generated class names and published resource paths aligned.
- Put shared logic/data in Runtime, native settings backends in Core, UI in Shell and session supervision in DesktopHost. Do not reintroduce cross-project `<Compile Include>` links or project cycles.
- Follow `.editorconfig`; use UTF-8 and four spaces in C#/XML/PowerShell. PowerShell files include a UTF-8 BOM for Windows PowerShell 5.1's Unicode decoding.
- The first source comment is `නමෝ බුද්ධාය 🤍`. Use `//`, `#`, `@REM` or XML comments as appropriate. Documentation is excluded. Strict JSON/lock files and the solution's required format header remain valid metadata, without injected comments.
- Keep actual input/device/privacy/cancellation/recovery checks. Removed test fixtures are not an excuse to weaken production guards.

## Review and running

Keep work on the feature branch and leave the PR draft until the owner requests otherwise. Native builds and actual Windows acceptance are separate: check startup/recovery, dock/window interactions, file workflows, device controls, accessibility and resource use on the target Windows system after structural changes.

Historical tests, screenshots, patch bundles and phase documents are available in Git history. The active tree intentionally contains only current source and operational tooling; do not restore old duplicate trees into it.
