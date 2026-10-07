# 0.9.0 validation

Completed locally:

- C# compilation and execution of the linked core behavior checks using .NET 8 and Roslyn: timer recovery, navigation, search, snapshots, Explore migration/capture/import/export, independent-space references, URL path casing, nullable imported fields and settings recovery.
- Settings tests use real files: atomic replacement, recovery from a damaged/missing main file, preserving unreadable copies, rejecting oversized writes before replacement, and final-save ordering.
- XML/XAML/project/manifest parsing, event/resource wiring, XAML content ordering, finite library/board/overview viewports, desktop WinRT compatibility guards and interop imports.
- Roslyn C# 12 semantic compilation of all application source files against the pinned Microsoft.WinUI, Windows SDK and Windows App SDK projection references, with .NET 8 reference assemblies. Temporary generated XAML field declarations stand in for XAML-generated fields. This checks C# types and API signatures, including the corrected Thickness calls; it does not compile XAML, generate PRI/XBF, publish the app or run WinUI.
- Source guards for the allowed Thickness overloads, required SVG assets, asset publishing entries and static-vector XML. All thirteen owned icons parse without external images, text, scripts, filters or animation.
- Text-contrast checks for all four palettes, compositing translucent colors over their defined surfaces. These do not measure Windows acrylic or certify every native control state.
- Git whitespace checks.

The supplied 0.8.0 AppVeyor log shows native/core and updater checks passed before compilation stopped at the two unsupported Thickness calls. Those calls are corrected in this source. That earlier log is not validation of the 0.9.0 build.

The Windows CI pipeline runs native interop and updater checks before compilation, publishing and compiled-resource/archive verification, now including the required SVG assets. Check its outcome for the exact commit being installed. The new PowerShell publishing guard has not run locally on Windows.

Native WinUI startup, rendering, input, high contrast, transparency, scaling, tray/hotkey behavior and performance require Windows acceptance. Follow TEST-WINDOWS.md. Design references are illustrations, not proof of native rendering.
