# NEXUS Shell changes

## 1.1.0 — Solstice UI polish

- Added Solstice and Ember moods with coordinated static vector wallpaper; preserved existing saved mood choices.
- Extended shared palette colors to native control states, menus, focused fields, sliders, switches and progress controls.
- Refined workspace chrome, sidebar artwork, location labels and segmented selections.
- Unified dock icon sizes, active-page marks and running-window artwork.
- Split window, widget and floating-bar material strengths; cached palette updates.
- Made quick controls more compact and grouped session preferences in an expander.
- Refined finite compositor feedback and entrance motion.
- Added six-mood settings/contrast checks, source-derived layout references and a detailed UI analysis.
- Follow-up review: selected sidebar labels meet calculated small-text contrast in light moods; press/release feedback observes native handled events and includes touch/pen contacts.
- Full Windows XAML build and native rendering remain pending.

## 1.0.0 — PC control surface

- Added an in-shell PC controls workspace with Ctrl+5, desktop/dock/menu/search entry points and optional workspace destinations.
- Added native default-output volume/mute and up to 32 app audio sessions through documented Core Audio interfaces, a dedicated MTA worker, session notifications, coalesced slider writes, device-change checks and unavailable states.
- Added live CPU, physical memory, power, local network link/address, fixed-drive free space and uptime. Polling follows the active view; audio connections release when controls are closed or inactive.
- Added one-to-four-window columns, stack and grid layouts on the first window's monitor work area, process ownership checks, rollback on failed placement, Undo and direct Lock PC.
- Added Nexus button templates with native focus behavior, original toolbar branding, fuller clock typography, larger comfortable dock icons and compact width fallbacks.
- Added CPU/layout/recovery behavior checks and Windows-only native placement, ownership, Undo and read-only audio checks. Updated all binary artifact versions without changing runtime dependencies.
- Local C# API compilation, core behavior and source checks pass. Windows XAML compilation, device operation, rendering and performance remain pending.

## 0.9.0 — A softer desktop

- Fixed the two CS7036 build failures in Explore by using four-sided WinUI Thickness constructors; added a regression guard.
- Replaced the large Home dashboard in desktop mode with a shortcut canvas, compact workspace card and smaller clock/spaces/focus widgets. Panel mode and high contrast keep the Home cards.
- Added thirteen original, cached SVG illustrations to the desktop, dock, app categories and Explore cards. Assets are included in full and small-update publishing checks.
- Added peach/coral/lilac/blue wallpaper waves, lighter Opal materials, smaller traffic lights and compact controls. Existing glass, motion, accessibility and inactive fallbacks remain.
- Made Explore list rows 66 pixels tall with a single-line note/path subtitle; retained board view, capture, search, spaces, previews and import/export.
- Updated versioned CI artifacts and added clearly labeled source-based layout illustrations.
- All application C# compiles against the pinned WinUI/Windows references with generated XAML field declarations. Core and source checks pass locally. Native Windows XAML compilation, startup, rendering and performance remain pending.

## 0.8.0 — Opal & Explore

- Added the Opal palette, folded vector wallpaper, an open Home desktop, floating focus/spaces widgets and framed work panels.
- Added Explore spaces, boards/list views, notes/tags, collections, quick capture, drop capture, local Quick Look, moves, import/export and persisted view/selection.
- References can appear in independent spaces; duplicate detection respects case-sensitive URL paths and case-insensitive Windows paths.
- Added Ctrl+N note capture and Space/Enter Quick Look. Panel close returns Home and unconfigured workspace launch opens configuration.
- Added bounded settings recovery, durable atomic replacement and a visible recovery message. A queued save cannot overwrite the final snapshot.
- Corrected four-mood selection mapping, light-theme dialogs, stale preview cleanup and single-instance window lookup.
- Core behavior and source checks pass locally. Native Windows compilation and runtime acceptance are separate checks.

## 0.7.0 — Shell Experience

- Added bounded in-session page history, back/forward buttons, Alt+Left/Right and page/profile breadcrumbs.
- Added seven search categories and current open-window entries; async refreshes preserve selected items.
- Added bounded, live-resolved recent search choices with clear/disable controls. Queries and window handles are excluded from this recent list.
- Added the Personalize page, appearance reset, widget/Home-card visibility, 12/24-hour clock and compact dock.
- Packed visible Home cards, synchronized appearance controls, themed native switch states, and fixed high-contrast workspace selection text.
- Added navigation/search/recent-reference/settings migration tests to the Windows CI harness and updated illustrative previews.
- No new runtime dependency. Windows build and native acceptance pending.


## 0.6.0 — Nexus Aura

- Introduced the shared Aura palette and Pearl/Lagoon/Graphite moods, preserving saved mood identifiers.
- Polished the floating menu/dock, desktop hero, workspace capsules, cards, search, inputs and all six ContentDialog paths.
- Added themed navigation states and compact navigation/labels for smaller windows.
- Redesigned Control center's Windows-settings shortcuts as a two-column grid.
- Added opt-in native acrylic with reduced-effects/high-contrast/focus fallbacks; default remains off.
- Added pure contrast and glass-preference checks to the core CI harness, source checks and clearly labeled design previews.
- Runtime dependencies and existing desktop/workspace behaviors retained. Windows build and runtime acceptance pending.


- Added desktop layout, responsive two-column Home, and configurable workspace presets.
- Added explicit workspace launch previews and workspace-specific app/saved-item selections.
- Added an optional global shortcut, notification-area residency, reopen/search/exit controls and Explorer-restart recovery.
- Added a bounded running-window dock that refreshes only while Nexus is active.
- Added last-page recovery, nested snapshot isolation, and settings migration backups.
- Filtered DWM-cloaked windows out of the visible-window list.
- Retained runtime package versions, Windows 10 accessibility handling, PRI/XBF checks and the small-update workflow.

Preparation checks pass. Windows CI and native acceptance are pending.

# Nexus Shell 0.4.1 — desktop startup repair

- Removes the unsupported `AccessibilitySettings.HighContrastChanged` event that aborted 0.4.0 startup on Windows 10 build 19045 after XAML had loaded.
- Replaces startup-created WinRT appearance objects with guarded desktop Win32 high-contrast and animation preference queries.
- Checks preferences on startup, activation and the existing active-only five-second UI tick; no extra background timer. Retains last known values on query failure, with custom motion initially disabled.
- Keeps the existing high-contrast resources and finite animations; unchanged settings do not rebuild pages.
- Adds a source guard for unsupported UWP event subscriptions and actual Win32 query smoke checks in Windows CI.
- Updates application, manifest, resource report and full/small artifact versions to 0.4.1. Direct package pins and the published PRI/XBF repair are retained.

The supplied 0.4.0 log confirms MainWindow XAML loading on Windows 10, followed by this event failure. The 0.4.1 source has not yet been compiled or launched in the authoring workspace; a successful AppVeyor build and a visible, interactive desktop on the user's PC are still required.

# Nexus Shell 0.4.0 — your everyday orbit

- Searchable window overview with native card virtualization, responsive widths, background enumeration, active-view refresh, and Ctrl+4.
- Local task checklist, task selection for focus, completion/reopening/removal, Home tasks and command search.
- Paused timer recovery with monotonic resume, coalesced running checkpoints and exact normal-close persistence.
- Saved-item favorites, named collections, filters, editing and Home favorite shortcuts.
- Explicit startup-folder repair in Control center and an attempted one-time legacy-settings backup.
- Stronger small-update payload/resource requirements and canonical manifest checks; expanded Windows CI behavior checks.
- 0.4.0 artifact names and application version; footer now reflects the current release.
- Reuses pinned dependencies and existing Windows minimum, resource-publishing repair, static vector assets, and finite motion.

Windows compilation, new core/PowerShell test execution, launch, rendering, and performance remain pending.

# Nexus Shell 0.3.0 — your orbit, expanded

- Native command palette for apps, saved items, workspaces and actions. Ctrl+K, arrows, Enter, Esc; lazy Start-menu discovery; at most 30 results.
- Explore board: save/open/search/remove links, files and folders. Up to 100 shortcut records, stored locally without a file index.
- Study: monotonic 25/50-minute focus timer and 5-minute break; pause/resume/reset; daily completion count; shared Home/Study notes with debounced atomic persistence.
- Home quick notes, summaries, workspace cards, extra sidebar/menu navigation, dock search, and Orbit/Aurora/Slate desktop moods. Finite compositor motion and reduced-effects support remain.
- Small update ZIPs contain app code/assets/compiled UI and fingerprints for reused runtime files. The offline updater validates both sets, assembles a fresh folder using local copies, and retains the old folder.
- CI adds behavioral checks for focus/search/snapshots and update compatibility/integrity. Both full and update archives are checked; MakePri verifies published MainWindow resources.
- Version, manifest and artifact names move to 0.3.0. Existing direct dependency pins and Windows minimum stay the same.

This is a source release. The new .NET/PowerShell behavior checks and WinUI startup still require the Windows CI run. The last user-supplied runtime log is the 0.2.1 unresolved MainWindow resource. No measured animation/frame-time/memory claim is made.

# Nexus Shell 0.2.2 — published XAML resource repair

The 0.2.1 diagnostics identified an unresolved `ms-appx:///MainWindow.xaml` resource (HRESULT 0x802B000A). This release repairs resource publishing and adds a packaging verification gate.

- Enables MSIX build tooling while retaining `WindowsPackageType=None` and self-contained folder deployment.
- Explicitly copies generated PRI/XBF resources into publish output with their paths preserved.
- Uses build-server MakePri to verify the root MainWindow index entry and its embedded/loose candidates.
- Records resource fingerprints and verifies their presence and exact bytes inside the portable ZIP.
- Adds resource index/report artifacts and startup file inventory logging.
- Corrects the CS8622 nullable event sender warning.
- Updates the application/manifest/package version to 0.2.2; dependency versions and UI/motion policy are unchanged.

The exact absent/indexed file in the user's existing bundle has not been inspected here. The repair targets the observed lookup failure and the documented unpackaged app-PRI publish omission. 0.2.2 needs a fresh Windows compile/resource/package check and launch test; it is not a verified runnable release yet.

# Nexus Shell 0.2.1 — startup compatibility and diagnostics

Build correction on 2026-10-06: the initial 0.2.1 source failed Windows XAML compilation with WMC0035 at App.xaml line 44. Moved the fallback brushes below `ResourceDictionary.ThemeDictionaries`, keeping all direct dictionary items together. Added a structural ordering check that rejects the original split collection. The app/package version stayed 0.2.1. The user subsequently confirmed successful compilation/publishing, followed by the unresolved MainWindow resource error addressed by 0.2.2.

- Declares Windows 10 build 19041 or newer and Windows 11 x64 accurately in the run instructions.
- Adds ordinary-resource fallback brushes and explicit Light/Dark/HighContrast dictionaries.
- Attaches Control center to its Button.Flyout and sizes it on Opening.
- Creates/reuses app context menus on realized containers; removes event-bearing flyouts from Style setters and clears recycled item actions.
- Uses Segoe MDL2 Assets icons on Windows 10 and Windows 11.
- Hooks resource tracing before App.xaml loads, logs available native restricted descriptions/HRESULTs, records startup stages, and shows a native error dialog for startup failures.
- Updates assembly, manifest, CI, and portable-package names to 0.2.1.

The user's 0.2.0 build succeeded, then failed at runtime with a generic MainWindow XamlParseException. **The exact original failing XAML element/resource is unknown.** These resource/flyout loading changes require a fresh Windows build and launch test; they are not presented as a confirmed fix. The existing Nexus Orbit appearance, bounded library, and animation policy are retained.

# Nexus Shell 0.2.0

The main desktop now uses the **Nexus Orbit** theme: dark pearl surfaces, violet accents, fine borders, static ribbon artwork, an orbit emblem, a floating dock, and a workspace sidebar. The layout borrows macOS proportions while preserving ordinary Windows application behavior.

## Interaction

- Short compositor entrance transitions and subtle dock/card hover lift.
- Reduced effects preference, Windows animation preference, and high-contrast fallback.
- Ctrl+K focuses app search inside Nexus; it is not a system-wide hotkey.
- Native light-dismiss Control center with keyboard dismissal.
- Red hides the workspace; yellow minimizes Nexus; green expands the workspace.
- Initial window fits the current monitor work area. Sidebar, widgets, dock capacity, and shortcut cards adapt to available space.

## Work and memory

- App library uses data-bound, virtualized GridView containers in a finite viewport, instead of constructing a button for every application.
- Start-menu discovery runs off the UI thread and starts only when Apps is first opened.
- Search uses a 160 ms debounce and does not rescan disks.
- Visual clock/resource updates pause when the Nexus window loses activation. The clock updates its text only when the displayed minute changes.
- Foreground usage sampling runs only when explicitly enabled. It can continue while Nexus is minimized, as needed for gaming-time summaries.
- Settings are saved from snapshots on a background worker, coalesced across quick changes. The final write guards against older queued writes.
- A cached process sampler measures elapsed CPU time and refreshes memory properties; its tooltip includes private bytes. No memory target is claimed as measured.
- Pins, activity, usage records, settings input size, discovered shortcuts, and realized library views are bounded.
- Added a Windows resource measurement script and comparison checklist.

## Validation status

Added `appveyor.yml` and browser setup instructions for AppVeyor's free public, open-source Windows builds. The desktop application version remains 0.2.0. The first user-run AppVeyor attempt failed during NuGet restore; the second restored packages but failed during C# compilation. A subsequent user-run 0.2.0 build compiled and published successfully; launch then failed in MainWindow XAML loading on Windows 10 build 19045.

Source XML, handler/resource references, C# syntax, structural performance guards, and archive integrity are checked in the authoring workspace. The user has confirmed 0.2.0 compilation and cloud packaging. Successful launch, rendering, motion smoothness, frame timing, and memory behavior still need Windows validation. This release remains source, not a verified executable.

Providers, download/game installation, privileged services, Explorer replacement, and a persistent global dock remain outside this desktop prototype.

## Build correction — 2026-10-06

Updated `Microsoft.Windows.SDK.BuildTools` from 10.0.26100.1 to 10.0.26100.4654 to satisfy the existing Windows App SDK Base dependency and correct the reported NU1605 package downgrade. The change keeps the existing app version and UI. `START-HERE.md` includes a one-line GitHub browser edit so existing users can retry the cloud build without downloading developer tools or the source again.

Corrected both action-button padding calls from `new Thickness(14, 9)` to `new Thickness(14, 9, 14, 9)`, resolving the reported CS7036 constructor-argument errors while retaining the intended spacing. Other C# `Thickness` constructions were reviewed. The user then confirmed a successful 0.2.0 Windows compile and publish with zero warnings/errors. The subsequent runtime XAML failure is separate.
