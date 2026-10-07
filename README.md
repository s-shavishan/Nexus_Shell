# NEXUS Shell 0.9.0 — A softer desktop

A native Windows desktop surface for your apps, study sessions, notes and resources.

## What's new

- **A desktop canvas.** My files, Explore, Study, App Library and up to four favorite saved cards appear as working shortcuts beside compact clock, spaces and focus widgets. A small workspace card replaces the large Home dashboard in desktop mode. Panel mode and high contrast retain the Home cards.
- **Original colorful icons.** Thirteen static vector illustrations give the dock, desktop, app categories and saved cards consistent folder, notebook, compass and app artwork. Installed apps use category illustrations rather than extracted application logos.
- **Softer Opal.** Peach, coral, lilac and blue vector waves, floating acrylic cards, compact traffic lights, smaller controls and a 66-pixel Explore list. Orbit, Aurora and Slate remain available. Glass and custom motion retain reduced-effects, system-preference and high-contrast fallbacks.
- **Build correction.** Fixed both unsupported two-argument WinUI `Thickness` calls from the supplied 0.8.0 build log. Source validation now checks those constructors, and publishing checks require the new vector assets.
- **Explore spaces.** Create up to 12 spaces, keep links, notes, files and folders together, group them into collections, favorite cards and search titles, notes and tags.
- **Quick capture.** Paste a web address or a note, drop files or folders, or press Ctrl+N to write a note. Space/Enter opens Quick Look on a selected card.
- **Previews.** Read the first 16 KB of supported text files and inspect supported local images. Open other resources in their Windows app or default browser.
- **Continue and move.** Restore the last Explore space, view and selected card; move cards between spaces. The same link can belong to different spaces.
- **Export/import.** A space can be exported to a `.nexus-space.json` file and imported as an independent space. Import validates format, addresses and capacity before changing the board.
- **Focus on the desktop.** Start or pause your study session without opening Study.
- **Settings recovery.** Atomic saves keep a readable backup. A damaged or missing settings file can recover the previous save, with a visible recovery message. Unreadable copies are preserved.

References to local files do not move the originals. Board capacity is 100 cards across all spaces; notes allow 2,000 characters and five tags. URLs and local paths are opened only when you choose Open or confirm a configured workspace launch. File and image previews run locally.

## Shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+K | Search apps, saved cards, spaces, tasks and actions |
| Ctrl+Alt+Space | Summon search from another app while Nexus runs and the shortcut is enabled |
| Ctrl+N | Create a note in Explore |
| Ctrl+1 / 2 / 3 / 4 | Desktop / Explore / Study / Window overview |
| Space or Enter on an Explore card | Quick Look |
| Alt+Left / Alt+Right | Previous / next Nexus page |
| F11 | Full screen / windowed |
| Escape | Close search or controls, leave full screen, or minimize |

## Build and update

Commit this source to the repository and let AppVeyor build it. The pipeline runs the core/native and updater checks, publishes the app, verifies its compiled XAML resources, and packages the outputs.

After a successful build, download `Nexus-Shell-0.9.0-Update-win-x64.zip`. Fully exit Nexus, extract it and run `Apply-Update.bat`, selecting your existing full app folder. It verifies the runtime files and creates a fresh version folder. If runtime checks fail, download the full `Nexus-Shell-0.9.0-win-x64.zip` once. Runtime dependencies are unchanged.

Source files require a Windows build. To build locally with the C# WinUI tools installed, run `.\scripts\build.ps1 -UseMSBuild -Run`.

## Validation

All application C# compiles locally against the project's pinned WinUI/Windows API references, using generated field declarations in place of compiled XAML. The core behavior checks also compile and run, including Explore migration, imports, independent spaces, timer recovery, settings backups and final-save ordering. XAML structure, resources, assets and event wiring pass source checks. Native Windows XAML compilation, packaging, startup, layout, input, glass rendering and performance still need the Windows build and acceptance checks in [TEST-WINDOWS.md](docs/TEST-WINDOWS.md). See [VALIDATION.md](docs/VALIDATION.md) for the exact limits.

Design references in `docs` illustrate layout; they are not Windows screenshots.

- [Desktop layout](docs/Nexus-0.9.0-Desktop-reference.png)
- [Explore layout](docs/Nexus-0.9.0-Explore-reference.png)

Regenerate the SVG references with `python scripts/desktop-preview.py`, then export them with Inkscape or another SVG renderer. They use the source wallpaper and original icon assets with sample content. The supplied macOS component boards and Windows concept informed spacing, materials and hierarchy; their artwork is not bundled.
