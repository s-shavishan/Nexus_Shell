# 0.8.0 validation

Completed locally:

- C# compilation and execution of the linked core behavior checks using .NET 8 and Roslyn: timer recovery, navigation, search, snapshots, Explore migration/capture/import/export, independent-space references, URL path casing, nullable imported fields and settings recovery.
- Settings tests use real files: atomic replacement, recovery from a damaged/missing main file, preserving unreadable copies, rejecting oversized writes before replacement, and final-save ordering.
- XML/XAML/project/manifest parsing, event/resource wiring, XAML content ordering, finite library/board/overview viewports, desktop WinRT compatibility guards and interop imports.
- C# 12 syntax parsing of every application source file with Roslyn, with no syntax errors. This does not validate WinUI API usage or replace the Windows build.
- Text-contrast checks for all four palettes, compositing translucent colors over their defined surfaces. These do not measure Windows acrylic or certify every native control state.
- Git whitespace checks.

The Windows CI pipeline runs native interop and updater checks before compilation, publishing and compiled-resource/archive verification. Check its outcome for the exact commit being installed.

Native WinUI startup, rendering, input, high contrast, transparency, scaling, tray/hotkey behavior and performance require Windows acceptance. Follow TEST-WINDOWS.md. Design references are illustrations, not proof of native rendering.
