# P2.7 — Visual composition correction from native screenshots

**Reference:** user-approved Midnight Glass Desktop Concept, 1536 × 1024.  
**Observed running UI:** five Nexus Shell screenshots supplied 2026-10-13, resolution 1649 × 928 (dock, Search, Launchpad, Files and Notifications).  
**Base:** main at `cf0076791c69c74ba7e225cb71e5e4450f98af31` (merged P2.6).  
**Status:** meaningful composition edits on an isolated feature branch; **not yet compiled or visually validated on Windows**.

## Why P2.6 did not feel transformed

The real screenshot shows a 420–450px dock strip with tiny ~36px icon artwork; a top menu bar whose spacing is reasonably refined; a 790 × 700 Search dialog occupying much of the desktop; and a functional but opaque-looking three-pane Files window. Launchpad has many repeated generic placeholders. The vector wallpaper offers low microtexture for evaluating native backdrop translucency; flat-looking windows are not proof the compositor is inactive.

The approved target has a larger, more expressive floating dock, a compact horizontal Spotlight launcher near the top, rich layered materials and spacious visually distinguished application panels. This is primarily a **scale and composition deficit**, not merely a color deficit.

## Implemented in this patch

### Spotlight gets two real native sizes

- Empty query: a **760 × 260 DIP** top-centered Spotlight launcher with six actual Nexus application buttons.
- Nonempty query: expands to **760 × 630 DIP** and reveals result filters, results, status and actions. Clearing the input returns to the compact launcher.
- `DesktopPanelWindow.ResolveBounds` introduces an overridable placement policy without modifying any other existing desktop panel positions.
- `DesktopLayout.SpotlightPanelBounds` clamps both sizes to work areas, DPI scaling and negative-origin monitors; unit assertions cover 1649×928, large and small monitors at 1×/1.5×/2×/invalid DPI. It is near the top rather than centered vertically.
- Idle opening does not start an unused indexed search. In-flight work is canceled when returning to the launcher. Selection/search functionality remains available once typed.
- Windows taskbar takeover, accessibility theme, user data and shell shortcuts are unchanged.

### Dock restores visual scale

- Normal mode: 64×68 DIP app buttons with 46px icon artwork and wider icon spacing; compact mode stays 40×44 DIP.
- The floating native dock strip rises to 90 DIP to accommodate the larger controls. Its work-area reservation logic remains zero in floating mode and previous native clip/auto-hide logic is unchanged.
- Updated `TaskbarView.VerifyRetainedDensity` assertions keep the native layout preflight aligned with real UI dimensions; portable Core tests verify dock height and compact behavior.
- Actual width is driven by real items and limited by `TaskbarBounds`; no arbitrary fixed 1000px empty dock is painted.

## Remaining high-impact differences (next acceptance targets)

1. Launchpad: real icon acquisition and fallback differentiation; repeated white/pastel placeholder art is visually dominant.
2. Files: elevate sidebar, toolbar, preview card and grid images; keep all native file actions and performance-sensitive virtualization intact.
3. Control Center and Notifications: redesign internal hierarchy rather than recolor the existing long lists.
4. Glass: use the existing Control Center material status and ETW trace to distinguish active Windows acrylic from its opaque fallback before adjusting blur parameters; avoid fake glass state.
5. Wallpaper and widgets: user-selectable, more detailed reference-compatible wallpaper backgrounds and coherent desktop widget grouping.

## Verification sequence

1. AppVeyor Core, Runtime, native WinUI probes, `scripts/build.ps1 -UseMSBuild`, and packaging for the **exact branch**.
2. Preview Mode: check compact Search launch, type to expand, clear to collapse, Escape, click quick-app shortcuts, and rapid typing/cancel.
3. Confirm results, shortcuts, file search and native window focus remain correct.
4. Check dock icon alignment/overflow and auto-hide on maximized windows, and native resize/clip behavior at 100%, 150%, 200% DPI, including secondary monitor.
5. Return screenshots at native resolution with dock and Spotlight visible; compare composition and native material state to reference.
6. Keep the PR unmerged until the owner explicitly decides; AppVeyor main-only configuration may require owner-controlled test merge, and CI success is not visual approval.

This patch does **not** claim pixel-perfect reference fidelity.

## Stage 2 — Launchpad differentiation and compact control navigation

The supplied Launchpad screenshot shows many unrelated installed Win32 applications represented by the same pastel Nexus glyph. `LaunchpadTile` now retains the real asynchronous Windows thumbnail loading path, but when WinRT cannot provide one it shows a deterministic 64px accent tile with a two-character app-specific monogram instead of cloning the Nexus logo. First-party Nexus features keep their vector artwork. The tile updates in place if a genuine icon arrives; no catalog rescan is triggered. `LaunchpadArtworkPolicy` is portable and tested for stable colors and monograms.

Control Center's navigation changes from a small 3×2 matrix of centered badges to larger, icon-led **2×3 horizontal service cards**. It retains the existing live Core-backed sections and does not invent fake network, brightness or audio state. Notification cards gain more generous internal spacing and distinct unread borders/backgrounds; dismissal, search, snooze, undo and paging remain wired to their existing services.

**Validation:** Check app icons under actual Win10 shell links and packaged/system utilities, as well as changes to loaded thumbnails. Check the Control Center at 100/125/150% DPI and the Notifications panel with mixed read/unread items. The new UI code has not been compiled in this session. All changes remain on unmerged PR #29.

## Stage 3 — Rebuild Files pane framing and preview composition

The running P2.6 screenshot exposed a flat, indistinguishable middle file area and small unframed inspector artwork. The P2.7 Files update introduces:
- An inset **center content card**, with a real folder name in its header, view/sort actions retained, and larger 68-DIP file illustrations. GridView virtualization, selection, rename, undo, open and drag operations continue to use the original controls.
- A **200-DIP preview stage** in the inspector that switches between the existing 172-DIP asynchronous image preview and a larger 86-DIP category icon. The original cancellation/version checks remain.
- Responsive sidebar/inspector width policy. Above 860 DIP in browse mode the inspector is available; below that it collapses so the main file list remains usable. File open/save/folder dialogs never show a browse-only inspector. High Contrast falls back to the existing solid materials.
- More consistent 50-DIP Files titlebar, accessible traffic-light controls and a 24-DIP shell frame radius (removed while maximized or High Contrast).
- Portable Core regression assertions for narrow, screenshot-sized and large layouts, plus invalid widths and dialog mode.

**Important:** Source changes are committed, but no native Windows build or comparison screenshot has run for this update. In Windows preview validate 800/940/1200-DIP widths, mixed files, image previews, 1000+ item scrolling, file operations, dialogs, maximize/restore and native glass status before merging.

## Stage 4 — Launchpad paging density

The native Launchpad uses a 760 × 740 DIP window. The previous 24-app page needed four 124-DIP rows plus search, category/collection controls, heading, footer and padding, causing a fourth row to require scrolling at reference size. The page now contains 18 apps (three six-column rows), preserving the existing category, collection, keyboard and icon-loading pipelines. Core regression assertions cover page boundaries, nonoverlap and the last partial page of the 600-app catalog.

Native screenshot and AppVeyor validation remain pending; this is not a claim of visual parity.

## Stage 4 — Control Center hierarchy and retained theme correctness (2026-10-14)

The user-approved reference places strong emphasis on connectivity/media modules. Rather than six equally dominant tall service blocks, Nexus now presents four primary 68-DIP actions (Sound, Network, Bluetooth, Display) in a 2×2 card grid with clearer 36-DIP icon badges; Power and Desktop remain fully functional but appear in a secondary 52-DIP row with smaller 28-DIP badges. The existing section commands, scanning, device reads and accessibility labels are preserved. `ControlCenterTilePolicy` supplies the hierarchy and reusable geometry, with portable checks.

A discovered application's colored Launchpad fallback previously captured High Contrast only at tile creation. Switching theme while the same page remained realized left the old saturated color in place. The existing `SolidColorBrush` now changes color in place during `StartMenuView.ApplyAppearance`, preserving the already-loaded native thumbnail and the retained page identity. Portable checks verify transparent High Contrast versus deterministic normal-theme colors.

**Validation:** Core regression assertions are committed but not executed here. Native WinUI validation should verify Control Center card spacing, keyboard accessibility/selection, normal vs compact mode, live Sound/Network/Bluetooth/Display and Power/Desktop functionality. Launchpad must switch to/from Windows High Contrast while remaining open and must not flash/restart icon loading. No native AppVeyor or screenshot acceptance has run on this branch.
