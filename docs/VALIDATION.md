# Validation record

Prepared on 2026-10-06 for Windows 10 build 19041 or newer and Windows 11, x64. Current source version: **0.2.1**.

## Windows evidence supplied by the user

- The first AppVeyor build failed at NuGet restore with NU1605. The direct Windows SDK BuildTools pin was corrected from 10.0.26100.1 to 10.0.26100.4654, satisfying the Windows App SDK Base requirement.
- The second build restored successfully, then reported two CS7036 errors for two-argument C# `Thickness` constructors. Both were corrected to `new Thickness(14, 9, 14, 9)`.
- The subsequent **0.2.0** build succeeded with **0 warnings and 0 errors** in 00:03:00.58. AppVeyor uploaded the portable ZIP (84,444,232 bytes) and its SHA-256 file. This is evidence of compilation/publish, not successful startup.
- On **Windows 10 Pro, x64, build 19045**, three attempts reached `App.OnLaunched`, then failed in `MainWindow.InitializeComponent()` during `Application.LoadComponent` with `Microsoft.UI.Xaml.Markup.XamlParseException: XAML parsing failed.`
- The generic log omits the XAML element/resource, native restricted description, and HRESULT. It establishes the startup stage but does **not** establish the exact cause. Build 19045 meets the declared minimum (19041).

## 0.2.1 changes

- Added fallback Nexus brushes and explicit Light/Dark/HighContrast resource dictionaries.
- Moved Control center from a named Grid resource to its anchor's `Button.Flyout`; opening performs layout sizing before display.
- Removed the event-bearing context flyout from a shared Style setter. Realized GridView containers own and reuse their menus; recycled menus drop old item actions.
- Replaced the Windows 11 Segoe Fluent Icons dependency with Segoe MDL2 Assets in XAML and code-created icons. Missing fonts were a compatibility issue; the original exception does not prove they caused the crash.
- Installed resource tracing and error hooks before App.xaml initialization. Logs now record startup stages, HRESULTs, inner exceptions, and bounded textual Exception.Data values, including available native restricted descriptions. A native MessageBox can report failures without loading WinUI XAML.
- Bumped source/application/package version to 0.2.1 so a fresh log distinguishes the update.

The resource-loading changes are a proposed repair, **not a Windows-verified root-cause fix**. Missing-resource tracing may not report non-resource XAML failures. Further diagnosis can still be necessary.

## Checks performed on the updated source

- Parsed XAML, project, NuGet configuration, and manifest as XML.
- Checked duplicate names and XAML event-handler references, including container realization and flyout opening.
- Checked StaticResource references, including nested bindings, and verified every used Nexus ThemeResource in Light, Dark, HighContrast, and the ordinary fallback dictionary.
- Parsed JSON and both CI YAML configurations; checked build-script and artifact references/version consistency.
- Checked the icon and source ZIP integrity.
- Reviewed the changed C# code and native MessageBox signature. A Linux source review is not C# type checking or Windows execution.
- Rechecked the finite app-library viewport and absence of custom infinite/render-loop animations.

The previous 0.2.0 preparation also parsed C# and PowerShell with tree-sitter and inspected the SVG design reference. Those historical syntax/preview checks do not validate the changed 0.2.1 C# or native rendering.

## Remaining verification

The authoring workspace has no Windows UI runtime or .NET compiler. **0.2.1 has not been compiled or launched here or in a cloud build by the authoring workspace.** Its new Windows compile/publish, launch, native rendering, control-center/context-menu behavior, DPI/multi-monitor behavior, accessibility, animation reliability, and memory/CPU measurements remain to be tested. The user's successful 0.2.0 build must not be reported as evidence for this changed source.

Run `python scripts/validate-source.py` for structural checks. Optional C# syntax parsing uses `--syntax` with tree-sitter and tree-sitter-c-sharp installed in an authoring environment; it is not a runtime dependency or a replacement for compilation.

Direct package versions remain pinned. NuGet creates a dependency lockfile during Windows restore; none is fabricated in this bundle. Follow `TEST-WINDOWS.md` before treating a binary as accepted or enabling actual shell replacement.
