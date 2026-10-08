# 1.1.0 validation

Completed locally:

- .NET 8/Roslyn compilation and execution of the linked core checks. Explore capture, import/export, independent spaces, settings recovery, save ordering, navigation, command search, timer recovery and PC layout/counter behavior pass.
- Body and secondary text contrast at least 4.5:1 in all six mood families on panels, cards, inputs, sidebars, selected segments, tinted selected rows and hero surfaces. A dark selected-segment failure was corrected before packaging. Native acrylic is not part of this calculation.
- Each mood round-trips through real settings files without losing notes or the compact-dock preference.
- Application C# compiles against Microsoft.WinUI 1.8.260803003, Windows App SDK Foundation/InteractiveExperiences projections, Microsoft.Windows.SDK.NET.Ref 10.0.19041.56 and .NET 8 reference assemblies. Temporary declarations stand in for XAML-generated fields. Native style setter property names are checked as C# assignments too. The direct compiler emits expected cross-version reference-unification warnings and unused-field warnings for generated stand-ins; there are no C# errors.
- XAML/XML structure, contiguous content, named elements, handlers, resources, Thickness constructors, interop imports, SVG integrity and publish wiring pass source validation. C# syntax is parsed independently with tree-sitter.
- Four layout references export from the source paths, shipped icons and runtime palette values. Their rendered images were inspected.

The native button press/release AddHandler and RemoveHandler wiring also compiles against the pinned WinUI APIs. Retained matching delegates clean up on unloading; mouse-only button filtering allows touch and pen contacts too. Input behavior still needs Windows testing.

Invoking the actual managed WinUI XAML compiler reaches metadata loading and fails because kernel32.dll is unavailable on Linux. This is a host limitation, not a successful XAML validation or a diagnosed application error.

The direct checks bypass Windows XAML compilation. They do not create PRI/XBF, publish or run the application. Windows-only native audio/window/desktop checks, updater PowerShell tests and resource/package verification must run through the existing Windows CI pipeline for the exact installed commit.

Native startup, font rendering, acrylic, DPI scaling, focus/input, tray/hotkeys, audio/mixer, window arrangement/Undo and performance still need Windows acceptance. See UI-POLISH.md for the visual review and TEST-WINDOWS.md for the broader workflow.
