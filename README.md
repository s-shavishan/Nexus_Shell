# NEXUS Shell 1.1.0 — Solstice

A native Windows desktop surface with a coordinated visual system for your apps, study, saved resources and PC controls.

## What's new

- **Solstice and Ember.** Warm light and dark moods inspired by the supplied macOS boards and orange controls. Each mood has matching wallpaper colors, fields, menus and selected states. Existing saved moods stay selected. Selected sidebar labels use readable text over the warm tint.
- **Native control polish.** The shared palette reaches buttons, focused inputs, dropdowns, context menus, sliders, switches and progress controls. Dialog buttons and keyboard focus use the same design.
- **Refined workspace chrome.** Compact traffic lights, history controls, search and note capture; a quiet sidebar with original colorful icons and clear selected rows.
- **A balanced dock.** Comfortable and compact modes resize all icons consistently. Active-page marks and running-window indicators make the dock readable; running windows use category artwork rather than letter tiles.
- **Different surface depths.** Windows, widgets and floating bars have separate acrylic strengths, with opaque fallbacks.
- **Quick control center.** Audio and quick tiles stay visible; session preferences use a collapsible section.
- **Consistent motion.** Restrained entrances, dock lift and press feedback follow reduced-effects and Windows animation preferences. Native handled pointer events are observed for press feedback, including touch and pen contacts. Wallpaper remains static vector artwork.

Select **Personalize → Solstice** after updating to try the new light mood, or choose **Ember** for the warm dark direction. The complete design analysis, implementation mapping and Windows review instructions are in [UI-POLISH.md](docs/UI-POLISH.md).

## Existing tools

- **A desktop canvas.** My files, Explore, Study, App Library, PC controls and up to four favorite saved cards appear as working shortcuts beside compact clock, spaces and focus widgets. A small workspace card replaces the large Home dashboard in desktop mode. Panel mode and high contrast retain the Home cards.
- **Original colorful icons.** Thirteen static vector illustrations give the dock, desktop, app categories and saved cards consistent folder, notebook, compass and app artwork. Installed apps use category illustrations rather than extracted application logos.
- **Desktop moods.** Static vector wallpaper and floating acrylic cards adapt to Solstice, Ember, Opal, Pearl, Lagoon and Graphite. Glass and motion retain reduced-effects, system-preference and high-contrast fallbacks.
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
| Ctrl+5 | PC controls |
| Space or Enter on an Explore card | Quick Look |
| Alt+Left / Alt+Right | Previous / next Nexus page |
| F11 | Full screen / windowed |
| Escape | Close search or controls, leave full screen, or minimize |

## Build and update

See [BUILD-HANDOFF.md](docs/BUILD-HANDOFF.md) for the confirmed repository baseline and current build handoff. Commit this source to the repository and let AppVeyor build it. The pipeline runs the core/native and updater checks, publishes the app, verifies its compiled XAML resources, and packages the outputs.

After a successful build, download `Nexus-Shell-1.1.0-Update-win-x64.zip`. Fully exit Nexus, extract it and run `Apply-Update.bat`, selecting your existing full app folder. It verifies the runtime files and creates a fresh version folder. If runtime checks fail, download the full `Nexus-Shell-1.1.0-win-x64.zip` once. Runtime dependencies are unchanged.

Source files require a Windows build. To build locally with the C# WinUI tools installed, run `.\scripts\build.ps1 -UseMSBuild -Run`.

## Validation and previews

Core behavior checks compile and execute locally, including six-mood contrast and actual settings-file persistence. All application C# and native style setter property names compile against the pinned WinUI/Windows references using temporary XAML field declarations. Source checks validate XAML structure, resources, handlers, asset publishing and C# syntax.

**The full Windows XAML build and native UI acceptance are still pending.** The checks above do not generate PRI/XBF or prove acrylic appearance, font metrics, input or frame pacing. See [VALIDATION.md](docs/VALIDATION.md) and [UI-POLISH.md](docs/UI-POLISH.md).

- [Solstice desktop layout](docs/Nexus-1.1.0-Solstice-desktop-reference.png)
- [Solstice Explore layout](docs/Nexus-1.1.0-Solstice-explore-reference.png)
- [Ember desktop layout](docs/Nexus-1.1.0-Ember-desktop-reference.png)
- [Ember Explore layout](docs/Nexus-1.1.0-Ember-explore-reference.png)

These are source-derived **layout illustrations with sample data**, not Windows screenshots. They read source wallpaper paths, shipped vector artwork and exported runtime palette values. Regenerate with `python scripts/solstice-preview.py` using Python and cairosvg. Those preview dependencies are not application dependencies. Historical previews remain in `docs` for reference.

See [PC-CONTROLS.md](docs/PC-CONTROLS.md) for the native audio, app mixer, metrics, window arrangement and Undo capabilities introduced in 1.0.0.
