# Validation record

Prepared on 2026-10-06 for Windows 11 x64, Nexus Shell 0.2.0.

Performed in the authoring workspace:

- Parsed XAML, project, NuGet configuration, and manifest as XML.
- Checked XAML event-handler references against code-behind methods.
- Checked named-element references and duplicate XAML names.
- Parsed JSON and GitHub workflow structure.
- Parsed the added AppVeyor YAML configuration and checked its build-script references and artifact paths against the package script.
- Verified file paths used by build/package scripts and solution.
- Checked icon contents and source ZIP integrity.
- Parsed PowerShell script syntax with tree-sitter-powershell 0.26.4. This is not a Windows PowerShell execution test.
- Parsed every C# source file with tree-sitter C# grammar; no syntax errors. This does not check API availability, type resolution, XAML generation, or runtime behavior.
- Checked the finite app-library viewport, data-bound items, absence of custom infinite/render-loop animations, background visual-timer suspension, and final-save guard through source review.
- Rendered and inspected the SVG design reference. It is not native WinUI rendering.
- Reviewed native API signatures, current-user startup scope, foreground focus restrictions, asynchronous discovery, local persistence, and exit handling.

User-provided Windows build evidence on 2026-10-06:

- AppVeyor checked out `dd14303c0dfbf13e395117ef65ab1f54f04ed90b` and ran `scripts\build.ps1 -UseMSBuild`.
- NuGet downloaded the pinned Windows App SDK packages, but restore failed with NU1605. `Microsoft.WindowsAppSDK.Base` 1.8.251216001 requires `Microsoft.Windows.SDK.BuildTools >= 10.0.26100.4654`; the project directly referenced 10.0.26100.1.
- Corrected the direct BuildTools reference to 10.0.26100.4654 and verified the package version and dependency requirement against Microsoft's NuGet entries. Project XML and the standard source checks pass.
- The second AppVeyor run, commit `cf8049f7a8b5d3a06b52ddb500104524d0036b9e`, restored packages successfully and reached `XamlPreCompile` C# compilation. This confirms that the earlier NU1605 restore conflict was resolved in that run.
- C# compilation reported CS7036 at `MainWindow.xaml.cs` lines 322 and 328, both two-argument `Thickness` calls. Corrected both to `new Thickness(14, 9, 14, 9)` and reviewed every other C# `Thickness` construction against the documented one- or four-argument constructors. A subsequent Windows build of this correction is still required.

Not performed here:

- NuGet restore executed by the authoring workspace; successful user-run restore is documented above.
- Successful C# compilation, complete XAML compilation, or publish.
- Windows launch, native rendering, DPI/multi-monitor testing, or performance measurement.
- GitHub Actions or AppVeyor execution by the authoring workspace. Two user-run AppVeyor attempts are documented above.
- Windows PowerShell script execution, actual resource sampling, and measured memory/CPU/frame-time comparisons.

The Linux authoring environment has no .NET compiler or Windows UI runtime. Static checks cannot establish that the application builds or runs. A successful Windows workflow/local build and the acceptance checklist are required before treating this as a verified binary or enabling actual shell replacement.

Re-run the standard checks with `python scripts/validate-source.py`. Optional C# syntax parsing uses `--syntax` with tree-sitter 0.26.0 and tree-sitter-c-sharp 0.23.5 installed in an authoring environment. These tools are not runtime dependencies. The GitHub workflow runs the standard structural checks before its Windows MSBuild publish.

Direct package versions are pinned. The dependency lockfile is generated at the first successful Windows restore; it is not fabricated in this source bundle.
