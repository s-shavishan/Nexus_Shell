# 1.3.0 validation

Completed locally on the final prepared source:

- .NET 8 core tests execute. New checks cover Windows Pro/build eligibility, recovery data saved before policy writes, initial/upgrade rollback, preserving foreign desktop and startup values, partial recovery retry, invalid backup/path rejection, actual folder I/O/filtering/cancellation/list bounds and reserved file names.
- Shell keyboard policy checks cover Win/Win+E, key-repeat suppression, release handling, Win+L/security pass-through, modifier chords, Alt+Tab/Shift+Tab selection and Win+Tab. Restart-budget and startup/UI heartbeat timeouts execute as pure policy checks.
- Existing shared-session save/Sections detach/reopen checks, atomic ordered/final writes, Explore import/export/migration/capacity/search, task/timer recovery, workspace snapshots, accessibility palette contrast, PC counter/window-layout and source structure checks pass.
- All application C# compiles against Microsoft.WinUI 1.8.260803003, Foundation/InteractiveExperiences projections, Windows SDK NET refs 10.0.19041.56 and .NET 8 refs, with temporary XAML fields/InitializeComponent stand-ins. Reference-unification and unused stand-in warnings remain; no C# errors.
- Desktop host C# compiles separately against .NET 8 references with compiler warnings treated as errors. It is not executed as a Windows host here.
- Source checks validate XML, XAML collection ordering/resources/names/handlers, supported Thickness constructors, native vector asset/publish wiring, C# syntax (including host), independent surfaces, Explorer-free routing/pickers, desktop-mode ownership, host/recovery build/update inclusion and artifact versions.
- Updated source-derived desktop/Files+Start illustrations are rendered and visually inspected. They are sample layouts, not native screenshots.
- Git source patches are applied to all three saved baselines and compared byte-for-byte with the final complete source. Source/patch ZIP file lists, bytes and SHA-256 values are checked.

Native WinUI XAML generation/XamlReader templates, Windows launching, sign-in/registry permissions, actual low-level keyboard handling, switcher focus, work-area/fullscreen cleanup, Recycle Bin APIs, DPI, accessibility and performance are **pending**. This Linux host cannot run Windows-native dependencies; direct C# API compilation does not produce XBF/PRI resources or prove desktop operation.

Windows CI must run the real PowerShell parser/updater tests, XAML compiler and portable-package resource checks. Manual acceptance in TEST-DESKTOP-MODE.md must finish on Windows Pro. No desktop policy was written during local preparation.

Evidence files: Core-checks.txt, CSharp-API-check.txt, Host-API-check.txt and Source-checks.txt. Historical foundation/design notes describe their original versions; current scope is NEXUS-DESKTOP-MODE.md.
