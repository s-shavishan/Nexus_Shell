# NEXUS Shell 0.8.0 — Opal & Explore

A native Windows desktop surface for your apps, study sessions, notes and resources.

## What's new

- **An open desktop.** Floating widgets and a centered dock sit on a folded iris/blue wallpaper. Explore, Study, Apps and other work areas open in a rounded panel with a sidebar. Closing the panel returns to the desktop.
- **Opal.** A new luminous palette with native in-app acrylic, softer surfaces and readable controls. Pearl, Lagoon and Graphite remain available in Personalize. Existing settings get an appearance refresh once, with a migration backup.
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

After a successful build, download `Nexus-Shell-0.8.0-Update-win-x64.zip`. Fully exit Nexus, extract it and run `Apply-Update.bat`, selecting your existing full app folder. It verifies the runtime files and creates a fresh version folder. If runtime checks fail, download the full `Nexus-Shell-0.8.0-win-x64.zip` once. Runtime dependencies are unchanged.

Source files require a Windows build. To build locally with the C# WinUI tools installed, run `.\scripts\build.ps1 -UseMSBuild -Run`.

## Validation

The core behavior checks have compiled and run locally, including Explore migration, imports, independent spaces, timer recovery, settings backups and final-save ordering. XAML structure, resources and event wiring also pass. Windows compilation, native layout, startup, input, glass rendering and performance need the Windows build and acceptance checks in [TEST-WINDOWS.md](docs/TEST-WINDOWS.md).

Design references in `docs` illustrate layout; they are not Windows screenshots.

- [Opal desktop layout](docs/Nexus-0.8.0-Desktop-reference.png)
- [Explore layout](docs/Nexus-0.8.0-Explore-reference.png)

Regenerate the SVG references with `python scripts/opal-preview.py`. The optional `--png` flag requires CairoSVG; Inkscape can also export the SVGs.
