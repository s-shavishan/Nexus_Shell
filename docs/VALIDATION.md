# 1.0.0 validation

Completed locally:

- C# compilation and execution of the linked core behavior checks using .NET 8 and Roslyn: timer recovery, navigation, search, snapshots, Explore migration/capture/import/export, independent-space references, URL path casing, nullable imported fields and settings recovery.
- New behavior checks: correct CPU idle/kernel accounting, first/reset samples, one-to-four-window layouts within odd-sized monitor bounds and negative origins, no overlaps, full-layout coverage, stale-window rejection and PC workspace recovery.
- Settings tests use real files: atomic replacement, recovery from a damaged/missing main file, preserving unreadable copies, rejecting oversized writes before replacement, and final-save ordering.
- XML/XAML/project/manifest parsing, event/resource wiring, XAML content ordering, finite library/board/overview viewports, desktop WinRT compatibility guards and interop imports.
- Roslyn C# 12 semantic compilation of all application source files against the pinned Microsoft.WinUI, Windows SDK and Windows App SDK projection references, with .NET 8 reference assemblies. Temporary generated XAML field declarations stand in for XAML-generated fields. This checks C# types and API signatures, including the corrected Thickness calls; it does not compile XAML, generate PRI/XBF, publish the app or run WinUI.
- Source guards for the allowed Thickness overloads, required SVG assets, asset publishing entries and static-vector XML. All thirteen owned icons parse without external images, text, scripts, filters or animation.
- Text-contrast checks for all four palettes, compositing translucent colors over their defined surfaces. These do not measure Windows acrylic or certify every native control state.
- Git whitespace checks.

The supplied 0.9.0 screenshot confirms that version's UI ran on the user's Windows PC. It also shows the visual gap from the illustrative preview. It is not validation of this 1.0.0 source.

The Windows CI pipeline now also tests actual placement and Undo using its own test window, rejects a wrong-process handle, and reads system memory and audio availability. The audio check never changes levels. A build host without an audio output prints SKIP for device reads and still checks the unavailable response and worker cleanup. These Windows checks have not run in this Linux authoring environment.

Windows XAML compilation, updater/PowerShell checks, publishing, required SVG asset checks and compiled-resource/archive verification remain CI gates. Check the outcome for the exact commit being installed.

Native WinUI startup, rendering, input, high contrast, transparency, scaling, tray/hotkey behavior and performance require Windows acceptance. Follow TEST-WINDOWS.md. Design references are illustrations, not proof of native rendering.
