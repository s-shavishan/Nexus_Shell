# Validation record

Prepared on 2026-10-07 for Windows 10 build 19041 or newer and Windows 11, x64. Current source version: **0.4.0**.

## Windows evidence supplied by the user

- The first AppVeyor build failed at NuGet restore with NU1605. The direct Windows SDK BuildTools pin was corrected from 10.0.26100.1 to 10.0.26100.4654, satisfying the Windows App SDK Base requirement.
- The second build restored successfully, then reported two CS7036 errors for two-argument C# `Thickness` constructors. Both were corrected to `new Thickness(14, 9, 14, 9)`.
- The subsequent **0.2.0** build succeeded with **0 warnings and 0 errors** in 00:03:00.58. AppVeyor uploaded the portable ZIP (84,444,232 bytes) and its SHA-256 file. This is evidence of compilation/publish, not successful startup.
- On **Windows 10 Pro, x64, build 19045**, three attempts reached `App.OnLaunched`, then failed in `MainWindow.InitializeComponent()` during `Application.LoadComponent` with `Microsoft.UI.Xaml.Markup.XamlParseException: XAML parsing failed.`
- The generic log omits the XAML element/resource, native restricted description, and HRESULT. It establishes the startup stage but does **not** establish the exact cause. Build 19045 meets the declared minimum (19041).
- The first **0.2.1** AppVeyor build, commit `1aebb402b2c022e62143ba68ff17a5bfd03568b5`, restored successfully but failed at `App.xaml(44,5)` during `MarkupCompilePass1` with **WMC0035**, duplicate assignment to the dictionary's implicit `_Items` collection. No 0.2.1 executable was produced by that run. The fallback brushes and styles were separated by `ThemeDictionaries`; this correction moves the brushes after that property element, joining the direct items into one block.

## Latest Windows evidence and 0.2.2 repair

- After the dictionary-ordering correction, the user confirmed a **successful 0.2.1 compile/publish** with 0 errors and two occurrences of the same CS8622 context-menu sender warning. AppVeyor uploaded the 84,447,048-byte portable ZIP and SHA-256 file.
- At 22:17:27 and 22:17:48 on 2026-10-06 (Asia/Colombo), the 0.2.1 startup log reached `Loading MainWindow.xaml`, then reported HRESULT **0x802B000A** and `RestrictedDescription: Cannot locate resource from 'ms-appx:///MainWindow.xaml'.`
- This proves the failure to resolve the compiled MainWindow resource. It does **not**, on its own, prove which PRI/XBF file is absent, whether a resource-map entry is misplaced, or whether extraction removed a file. The binary bundle has not been inspected in the authoring workspace.
- The disabled `EnableMsixTooling` and Microsoft's unpackaged publishing reports support an app-PRI publishing omission as the repair target: [WindowsAppSDK #6720](https://github.com/microsoft/WindowsAppSDK/issues/6720), [#3718](https://github.com/microsoft/WindowsAppSDK/issues/3718). The post-publish XBF preservation approach is also documented in [#6394](https://github.com/microsoft/WindowsAppSDK/issues/6394); that report's AOT configuration differs from this non-AOT project.

0.2.2 enables the resource tooling while retaining unpackaged deployment, explicitly copies built PRI/XBF resources after publish, and runs MakePri on the **published app index**. The checker requires the root Files/MainWindow XBF/XAML entry and verifies any loose-file candidates exist inside the published folder. The packager then checks resource sizes and hashes **inside the ZIP**, rather than trusting the source directory. Diagnostics preserve a small resource report and startup inventory. The CS8622 sender mismatch is corrected with a nullable sender and a type guard.

The new Windows build scripts, MakePri check, MSBuild publish target, and runtime launch have **not been executed here**. They are included to make the next AppVeyor run produce direct resource evidence before a user downloads a broken bundle. Passing these checks would establish resource presence/integrity; it would not establish successful native rendering or performance.

## 0.2.1 changes

- Added fallback Nexus brushes and explicit Light/Dark/HighContrast resource dictionaries.
- Moved Control center from a named Grid resource to its anchor's `Button.Flyout`; opening performs layout sizing before display.
- Removed the event-bearing context flyout from a shared Style setter. Realized GridView containers own and reuse their menus; recycled menus drop old item actions.
- Replaced the Windows 11 Segoe Fluent Icons dependency with Segoe MDL2 Assets in XAML and code-created icons. Missing fonts were a compatibility issue; the original exception does not prove they caused the crash.
- Installed resource tracing and error hooks before App.xaml initialization. Logs now record startup stages, HRESULTs, inner exceptions, and bounded textual Exception.Data values, including available native restricted descriptions. A native MessageBox can report failures without loading WinUI XAML.
- Bumped source/application/package version to 0.2.1 so a fresh log distinguishes the update.

The resource-loading changes are a proposed repair, **not a Windows-verified root-cause fix**. Missing-resource tracing may not report non-resource XAML failures. Further diagnosis can still be necessary.

## Checks performed for the earlier 0.2.2 source

- Parsed XAML, project, NuGet configuration, and manifest as XML.
- Checked implicit XAML content ordering across both XAML files. Exercised the new guard against the exact rejected App.xaml from the prior source ZIP; it rejects the split dictionary before accepting the corrected source. XML well-formedness and resource-key checks had missed this compiler constraint.
- Checked duplicate names and XAML event-handler references, including container realization and flyout opening.
- Checked StaticResource references, including nested bindings, and verified every used Nexus ThemeResource in Light, Dark, HighContrast, and the ordinary fallback dictionary.
- Parsed JSON and both CI YAML configurations; checked build-script and artifact references/version consistency.
- Checked the icon and source ZIP integrity.
- Parsed the revised project/publish target and both CI YAML files, checked the 0.2.2 artifact/version paths, and reviewed the PowerShell resource/ZIP validation logic. No PowerShell parser or Windows execution was available for the new scripts.
- Reviewed the changed C# code and native MessageBox signature. A Linux source review is not C# type checking or Windows execution.
- Rechecked the finite app-library viewport and absence of custom infinite/render-loop animations.

The previous 0.2.0 preparation also parsed C# and PowerShell with tree-sitter and inspected the SVG design reference. Those historical syntax/preview checks do not validate the changed 0.2.1/0.2.2 C# or native rendering.

## Remaining verification after 0.2.2

The authoring workspace has no Windows UI runtime or .NET compiler. **0.2.2 has not been compiled or launched here or in a cloud build by the authoring workspace.** The user's successful corrected 0.2.1 build and unresolved resource launch error are recorded above; they do not validate this changed 0.2.2 source. A fresh Windows compile/publish, launch, native rendering, control-center/context-menu behavior, DPI/multi-monitor behavior, accessibility, animation reliability, and memory/CPU measurements remain to be tested. The user's successful 0.2.0 build must not be reported as evidence for this changed source.

Run `python scripts/validate-source.py` for structural checks. Optional C# syntax parsing uses `--syntax` with tree-sitter and tree-sitter-c-sharp installed in an authoring environment; it is not a runtime dependency or a replacement for compilation.

Direct package versions remain pinned. NuGet creates a dependency lockfile during Windows restore; none is fabricated in this bundle. Follow `TEST-WINDOWS.md` before treating a binary as accepted or enabling actual shell replacement.

## 0.3.0 preparation

The source adds native command search, a bounded saved-item board, shared local notes, a monotonic focus-session model, desktop mood presets, and a runtime-reusing update distribution. The prior resource-publishing target and dictionary-ordering repair remain in place.

Authoring checks: XAML/project/manifest XML; contiguous implicit XAML collections; 83 unique names and 39 handler links across both MainWindow partial files; resource keys and fallback/theme dictionaries; a finite app-library viewport; finite custom motion; both CI YAML configurations and artifact/version consistency; documentation SVG rendering and visual inspection; source/patch ZIP CRC and byte checks.

The workspace has no .NET compiler, PowerShell host, or Windows UI runtime. `tests/Nexus.Core.Checks` and `scripts/test-update.ps1` are wired into Windows CI but have not run here. The former checks timing/pause/completion, query ranking/bounds, and snapshot preservation. The latter uses fixtures to run the actual packager/updater and checks compatible reuse, original-folder preservation, runtime mismatch, damaged payload, existing destinations and path bounds; it also invokes the native PowerShell parser on shipped scripts.

The new source is not yet compiled or launched on Windows. The last user-supplied native launch evidence is the 0.2.1 missing MainWindow resource. The 0.2.2 repair has no successful launch evidence yet and is retained in 0.3.0. Documentation preview images are code-drawn references, not screenshots or measurements. Update archive size, native appearance, motion reliability, accessibility, CPU and memory require the Windows build and test.

## 0.4.0 preparation

Adds a virtualized window overview, task checklist, paused focus checkpoint recovery, favorites/collections/editing, connected Home summaries, explicit startup-path repair, and a legacy settings backup attempt. The PRI/XBF publishing target and resource checks are retained.

Local checks: XML/XAML collection order and single-child content controls; 94 unique named elements and 44 event-handler references across three MainWindow partial files; theme/static resources; finite app and window card viewports and correct page ownership; native icon; project and CI version/artifact consistency; both YAML configurations; documentation SVG rendering/visual inspection and source/patch ZIP integrity. Source review is not C# type checking.

Expanded core checks cover paused recovery without time-away credit or repeated completion, older settings migration, bounded/unique task and saved-item records, JSON round-trip and independent snapshots. Expanded PowerShell fixture checks reject reused app binaries/resources, invalid hashes, path aliases and traversal, alongside prior compatibility/corruption/destination checks. These are wired into Windows CI and **have not executed in this authoring workspace**.

No successful Windows 0.2.2/0.3.0/0.4.0 launch has been supplied. .NET compilation, native PowerShell parsing/execution, WinUI load/rendering, keyboard/accessibility/DPI behavior, actual update size, animations, and memory/CPU measurements remain required on Windows.
