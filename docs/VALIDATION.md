# Current validation — 2.0.0

Read [major update source evidence](MAJOR-CHECKS-2.0.0.md) and [the Windows acceptance sequence](MAJOR-2.0.0.md). Shan reports the earlier 1.8 tests passing, while the supplied screenshot demonstrates separate Settings, minimized-caption, corner and tooltip defects. New 2.0 UI/native behavior requires fresh Windows acceptance. Earlier sections below are historical evidence.

Read [startup source evidence](STARTUP-CHECKS-1.8.0.md), [VM evidence for 1.7](VM-EVIDENCE-2026-10-10.md), and [the 1.8 acceptance sequence](STARTUP-1.8.0.md). The new 1.8 paths are source-tested; Windows runtime acceptance remains pending. Earlier sections are historical evidence.

# 1.7.0 source validation

Read [foundation validation evidence](FOUNDATION-CHECKS-1.7.0.md) and [the native acceptance gates](FOUNDATION-1.7.0.md). Portable runtime checks, existing core checks, C# API checks, source validation, and PowerShell update fixtures pass. Native Windows build and execution remain pending.

The following section records the previous 1.6.0 source snapshot; it is historical evidence.

# 1.6.0 validation

Baseline: `0184d98d5a36b433995fbf8d1b9e6e982f434ac2` from `s-shavishan/Nexus_Shell` main.

## Completed in the Linux authoring environment

- All portable `Nexus.Core.Checks` tests compiled with .NET 8 Roslyn and executed on the .NET 8 runtime. Tests cover existing settings/recovery/layout/dock behavior plus decimal arithmetic/error recovery, shared notes and snapshots, stale-note edits, metadata file sorting, history commit, fullscreen interaction priority, Spotlight bounds and persisted theme aliases.
- All app C# source type-checked against the project's restored Windows App SDK 1.8 and Windows SDK .NET projections. Temporary XAML field declarations supplied the named controls and InitializeComponent methods, so this checks C# API and type resolution but does not compile XAML or run WinUI. Reference-version unification and unused temporary-field warnings were emitted; no C# errors remained.
- `python scripts/validate-source.py`: XML, native runtime template XML, custom resources, assets, publish wiring, desktop-host recovery ownership, independent window lifetime and Windows shortcut ownership.
- `git diff --check`.

The .NET CLI entry point could not run normally in this environment because Process.StartTime was unavailable. Roslyn and the test executable ran directly through the same installed .NET runtime; these are actual C# tests, not reimplementations.

## Pending

Windows XAML compilation, PRI/XBF publishing, PowerShell updater/package checks, native UI startup, DWM/backdrop behavior, native minimize/restore, event timing, DPI/resizing and pixel-level reference comparison. No measured frame-rate, memory reduction or pixel-perfect rendering is claimed.

Run the checked-in Windows pipeline and complete [the acceptance sequence](MIDNIGHT-GLASS-1.6.0.md) before treating a packaged build as validated. Earlier verification JSON files describe previous source snapshots and are historical evidence only.

## Delivery

The GitHub connection permitted repository reads but rejected blob uploads and branch creation with HTTP 403, `Resource not accessible by integration`. No remote branch or pull request was created and main was not changed. Complete source and a binary-capable Git patch are provided instead. The patch is checked against the baseline above and includes the wallpaper.
