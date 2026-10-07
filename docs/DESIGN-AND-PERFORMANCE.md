# Shell experience and Aura design — 0.7.0

The default desktop layout removes the central panel's chrome/sidebar while keeping a top navigation bar, desktop widgets, Home cards and a dock. A Control center switch restores the panel layout. F11 provides fullscreen.

The desktop remains a native WinUI surface over Windows. Explorer keeps running; presets do not create Windows virtual desktops. No web renderer or external process is added.

## Navigation and search

`NavigationTrail` stores at most 24 page names in memory. Returning Back/Forward does not create a new visit; visiting a new page after Back removes the forward branch. Profile selections and third-party launches are not replayed. Last-page persistence accepts Personalize.

`ShellExperience` categorizes command entries and resolves up to eight persisted recent references against the current command catalog. Recent-first ordering applies only to an empty All search; typed queries retain the existing title-prefix ranking. Actions, tasks and ephemeral window handles never enter this list. Disabling recent items clears the references and prevents new recording. Activity/usage remain separate existing data stores.

Search includes current window titles/process names from the existing Windows enumeration. Its results stay limited to 30, and the list remains bounded. Opening search requests an asynchronous window refresh; changed windows and app-discovery completion refresh results while preserving selection when possible. Execution resolves the selected handle against the current in-memory list and reports unavailable windows.

## Personalization

The Personalize page uses generated native controls and the same Aura resources. It exposes appearance/layout/widget/card choices, compact dock, clock format and recent-item controls. Control center and the page synchronize under a reentrancy guard.

Hidden Home cards preserve their underlying notes/pins; remaining cards are packed into one or two columns. The dock changes button/content sizes and spacing without replacing its launch behavior or timers. A 12-hour desktop clock places the localized period in a separate small label to avoid compressing the main numerals.

Appearance reset restores the mood, material/effects, desktop layout/widgets/cards, clock and dock defaults. It leaves notes, tasks, pins, profiles, tracking/startup and recent-item settings intact.

Mood cards and settings columns respond to page width. Controls release their synchronization delegates and references when the page is left. Navigation/filter/setting changes add no rendering loop or polling timer.

## Aura visual system

`AuraPalette` is the source for shared ARGB tokens. App.xaml provides fallback, Light/Dark and high-contrast dictionaries; palette changes mutate the existing non-high-contrast brushes so active controls follow the chosen mood. System high-contrast colors remain intact.

Pearl uses iris and teal on charcoal. Lagoon shifts the surfaces toward teal. Graphite uses cool blue with quieter surfaces. Existing serialized mood values are retained for compatibility.

Menu, dock, Control center and search share a material. Native acrylic is off by default and only connects while enabled, active and outside high contrast/reduced effects. Construction/connection errors select solid surfaces; Windows can also provide the brush's configured fallback. The design references show the default pearl surfaces, not measured blur or transparency behavior.

Typography, radii and spacing are shared across static XAML and generated pages. Dialog buttons use the native AccentButtonStyle with Aura resources; native input/focus behavior remains. The body/secondary/primary-action text checks use the actual palette values and composite translucent cards over their backgrounds. They do not certify every native state or the whole UI.

A compact menu exposes every page at narrow widths. Widgets/sidebar, pinned/running dock capacity, labels, profile columns, card columns, hero typography/actions and bounded search/control scrolling respond to available dimensions. Verify physical sizes and text scaling on Windows.

No extra package, wallpaper bitmap, backdrop service, browser renderer or continuous rendering loop was added. Optional acrylic should be measured on the target PC; no memory/CPU performance claim is based on the previews.

## Workspaces

Each preset owns a name, description, destination, up to eight app selections and eight saved-item references. Selected apps replace the global pin list on Home and in the pinned dock; empty app selections use global pins. Selected saved items replace global favorites on Home; empty saved selections use favorites.

Notes, tasks, focus sessions and activity stay shared. Enter changes the desktop without launching anything. Open shows a bounded launch preview and requests each distinct target after confirmation. It does not reposition third-party windows or promise those applications started successfully; failed requests are reported.

Presets copy the selected app metadata so they survive lazy Start-menu discovery. Missing saved-item references are pruned. Snapshots copy nested selection lists before persistence runs off the UI thread.

## Desktop presence

Ctrl+Alt+Space uses RegisterHotKey and native window messages; no global keyboard hook is used. Shortcut registration can fail independently of window startup. The local Ctrl+K palette remains available.

Notification-area residency is opt-in. Hiding stops visible-UI refreshes and custom motion. An active focus timer and user-enabled usage tracking continue, using the existing timers. Exit saves a paused focus checkpoint and removes native registrations.

The running-window dock is limited to three entries and appears only on wide layouts. Enumeration refreshes every five seconds while the surface is active; it releases dock entries when hidden. It excludes own/tool/DWM-cloaked windows. Full window overview remains available at all sizes. Window order follows Windows enumeration, not a claimed recency ranking.

Existing finite composition animations and reduced-effects/high-contrast handling are retained. There is no continuous wallpaper-render loop or window-thumbnail capture.

## Rendering evidence

`Nexus-Experience-preview.png` and `Nexus-Experience-search.png` is a code-drawn design reference with illustrative selected apps/tasks/links. The shipped app uses actual local settings. Windows launch/layout and memory/CPU measurements remain necessary.
