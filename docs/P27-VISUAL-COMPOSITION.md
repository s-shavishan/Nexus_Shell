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
