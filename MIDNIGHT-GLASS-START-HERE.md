# Nexus Shell — Midnight Glass Desktop (source preview)

This complete source tree is based on the uploaded Nexus Shell 1.5.0 project. The Midnight Glass design is a **native C# / WinUI 3 implementation**; it is not a web wrapper and does not replace Windows window-management code.

## What changed

- **Midnight Glass** is a new, dark blue-violet theme in the existing mood system. It is the default for *fresh* Nexus settings, while existing saved themes remain unchanged until you select Midnight Glass.
- A new **2560 × 1600 Midnight wallpaper** is included under `src/Nexus.Shell/Assets/Wallpapers/Midnight.png`. It is a procedural night-mountain graphic to complement the new glass palette.
- **Desktop top bar**: a native overlay with working Nexus launcher, Files, Explore, Windows overview, Personalize, and Control Center actions, plus a live clock. Desktop icons begin below the bar.
- **Floating dock**: refined spacing, rounded material, selection surfaces, indicators, border and an optional native AcrylicBrush with a gradient fallback. The existing dock window-discovery, minimize/restore, preview and motion code remains in place.
- **Nexus Files**: wider default window, more spacious rows, styled Places sidebar, and a new responsive Details inspector for normal browsing. File pickers remain usable without the inspector.
- **Control Center**: larger rounded cards and rebalanced type/spacing, retaining real Windows audio, brightness, and desktop-profile controls.
- **Sections and launcher**: refreshed proportions, title/search treatment, wider sidebar, and updated typography.
- **Theme mapping**: explicit Graphite/Lagoon aliases and a safe default fallback, preventing a named theme from unexpectedly using the wrong color set.

## Build on Windows

1. Extract this ZIP into a **new folder**; do not overlay older source trees containing retired files.
2. Open the folder in PowerShell on Windows 10 19045+ or Windows 11 with the required .NET 8 SDK and Windows app build dependencies installed.
3. Execute `python scripts/validate-source.py` (Python required), then `dotnet restore Nexus.Shell.sln`, then `dotnet build Nexus.Shell.sln -c Release -p:Platform=x64`.
4. Follow `START-HERE.md` and `docs/RUN-PORTABLE.md` for the project's existing supported startup and desktop-host workflows.
5. **Test in preview mode first**, not as your default sign-in shell. Keep the Windows recovery/restore mechanism available.
6. If your old settings still show Solstice/another theme, open **Sections > Personalize > Desktop moods > Midnight Glass**. Existing theme selections are intentionally preserved.

## Verification status

PASS: `python scripts/validate-source.py` (XAML/XML, resources, desktop-host dependencies, Windows shortcut ownership, packaging paths).

Not run in this Linux-based environment: native WinUI XAML compilation, `.NET` C# type checking, GPU glass rendering, and runtime behavior on Windows. The optional `--syntax` validator also requires the `tree_sitter` module, which is not installed in this environment. The Windows CI workflow in `.github/workflows/build-windows.yml` is preserved.

### Manual checks after building

1. Launch Nexus in preview mode, open Files and switch back to desktop; no XAML parse exceptions.
2. Test top bar launcher, Files, Explore, Windows overview, View, Control Center and clock update.
3. Toggle Fast / Balanced / Full visual profiles; dock and panels use correct fallbacks.
4. Minimize and restore Firefox or another app using the dock; validate indicators and hover previews.
5. Maximize an app; check existing dock auto-hide, Windows work area, and dock reveal behavior.
6. Test Win+R, Win+D and Alt+Tab under the appropriate Windows/Nexus desktop mode.
7. Test File browser inspector selection, folder navigation, narrow/resized window, and Open/Save file pickers.
8. Verify Return to Windows and recovery, especially before configuring shell replacement at sign-in.

## Scope / follow-up

This is an integrated **first native redesign pass**, not a guarantee of pixel-level parity with the earlier rendered mockup. The 3-pane file browser is functional; a future pass can further refine original app icons, window previews, menu bar behavior, live material composition and interaction motion after real Windows screenshots and logs.
