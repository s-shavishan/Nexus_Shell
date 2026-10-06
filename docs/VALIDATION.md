# Validation record

Prepared on 2026-10-06 for Windows 11 x64, Nexus Shell 0.2.0.

Performed in the authoring workspace:

- Parsed XAML, project, NuGet configuration, and manifest as XML.
- Checked XAML event-handler references against code-behind methods.
- Checked named-element references and duplicate XAML names.
- Parsed JSON and GitHub workflow structure.
- Verified file paths used by build/package scripts and solution.
- Checked icon contents and source ZIP integrity.
- Parsed PowerShell script syntax with tree-sitter-powershell 0.26.4. This is not a Windows PowerShell execution test.
- Parsed every C# source file with tree-sitter C# grammar; no syntax errors. This does not check API availability, type resolution, XAML generation, or runtime behavior.
- Checked the finite app-library viewport, data-bound items, absence of custom infinite/render-loop animations, background visual-timer suspension, and final-save guard through source review.
- Rendered and inspected the SVG design reference. It is not native WinUI rendering.
- Reviewed native API signatures, current-user startup scope, foreground focus restrictions, asynchronous discovery, local persistence, and exit handling.

Not performed here:

- NuGet restore and resolution of transitive dependencies.
- C# compilation or XAML compilation.
- Windows launch, native rendering, DPI/multi-monitor testing, or performance measurement.
- GitHub Actions execution. The workflow is provided, not run.
- Windows PowerShell script execution, actual resource sampling, and measured memory/CPU/frame-time comparisons.

The Linux authoring environment has no .NET compiler or Windows UI runtime. Static checks cannot establish that the application builds or runs. A successful Windows workflow/local build and the acceptance checklist are required before treating this as a verified binary or enabling actual shell replacement.

Re-run the standard checks with `python scripts/validate-source.py`. Optional C# syntax parsing uses `--syntax` with tree-sitter 0.26.0 and tree-sitter-c-sharp 0.23.5 installed in an authoring environment. These tools are not runtime dependencies. The GitHub workflow runs the standard structural checks before its Windows MSBuild publish.

Direct package versions are pinned. The dependency lockfile is generated at the first successful Windows restore; it is not fabricated in this source bundle.
