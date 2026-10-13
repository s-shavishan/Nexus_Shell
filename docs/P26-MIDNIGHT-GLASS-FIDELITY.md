# P2.6 — Midnight Glass visual fidelity
**Visual target:** user-provided `Midnight Glass Desktop Concept(2).png` (1536 × 1024 reference).  
**Base:** `ace93f09e478a3187523a122e2cd926d8884514c` — merged P2.5, PR #27.  
**Branch:** `feat/p26-midnight-glass-fidelity`  
**Status:** First multi-surface source implementation committed. **Not visual-accepted or Windows-native-build verified.** Continue refining on this same draft PR; do not merge without the project owner's explicit instruction.

## Reference composition and implementation

The reference features a 34–38 DIP dark top bar, a centered luminous floating dock, a prominent translucent search panel and icon strip, a large three-pane file manager, multiple floating utilities, an inset Control Center and nested navy cards. Values below are architectural targets; Windows desktop scaling and acrylic availability determine their final physical appearance.

| Target | Source implemented | Remaining visual acceptance |
|---|---|---|
| Blue-black glass family | Midnight-only `AuraPalette` tuned to dark navy, cooler elevated cards, blue selections, readable muted text and refined border tint. `GlassRecipe` uses less gray/washed-out native tint while preserving opaque fallback | Inspect real composition under the approved wallpaper on Win10/Win11, glass Clear/Balanced/Frosted, battery saver, High Contrast and software rendering |
| Consistent material geometry | `MidnightGlassMetrics` shared frame (24), dock (25), card (18), control (12), and padding dimensions. `DesktopPanelWindow` and `QuickSettingsWindow` match native WinUI clip radii. | Check Win32 resize shadows, native clip black-edge artifacts and scaling across monitors |
| Floating dock | 52×56 standard dock buttons, 40×44 compact fallback, 4px grouped icon gaps, 28px separator, increased breathing room, native clip matches the WinUI dock radius. The existing density probe's actual geometry assertions were updated. | Verify no icons clip, auto-hide, edge reveal, app overflow, active indicator, pin overflow, preview hover and accessible focus |
| Top menu bar | Refined horizontally spaced controls (13pt labels, 30px control height) while retaining all existing menu and window actions | Check top-bar width adaptation at 1366, 1536, 1920 and 4K, and caption keyboard focus |
| Finder-style Files | 18px content inset, 14px vertical rhythm, more deliberate sidebar width and tinted translucent sidebar; inspector card shares reference 18px corner hierarchy. Native file operations, previews and selection model are unchanged | Inspect actual three-pane composition, responsive collapse, drag-select, save dialog, long file names and large collections |
| Control Center | 20px padding, 34px icon badges, 18px cards/nav tiles, unified window clip, stable existing real Core-backed settings/service commands | Match reference information hierarchy; validate brightness/audio/network requests, compact anchor and scroll accessibility |
| Spotlight | A functional 6-entry icon launch strip beneath the search box, using existing shell actions (Launchpad, Files, Explore, Notes, Settings, Windows). Search filters/results remain intact below. Input and nested card radii now follow the reference hierarchy. | Compare panel height/position and clear text-field focus, keyboard shortcuts, catalog search and desktop monitor placement |

## Preservation and constraints

- **No fake hardware switches:** Control Center remains wired to Nexus Core; no decorative controls that pretend to change Windows state.
- No global keyboard hooks, taskbar takeover changes, process privilege changes, file-system schema changes or third-party window theming.
- High Contrast uses existing solid/native safe fallback. No persistent glass setting is changed by this phase; the existing user's selection remains authoritative.
- A native desktop surface can style the Nexus-owned windows but **cannot** restyle every third-party Win32 app.
- The reference is a visual specification; copying third-party trademarks or using the full mockup as a wallpaper is not part of this implementation.

## Required CI / visual acceptance

1. Run AppVeyor **on this exact branch SHA**: Core checks, Runtime checks, WinUI XAML compile, `--verify-ui` native preflight and packaging. Do not infer this from main's previously successful build. Verify `MidnightGlassMetrics.cs` is included in portable Core project links.
2. Launch Preview Mode in a native Windows desktop. Select **Midnight**, native glass on, Balanced and then Clear styles; verify readable opaque fallback if DesktopAcrylicController cannot attach.
3. Capture 1536 × 1024 (matching reference), plus 1366 × 768 and 1920 × 1080. Compare top bar, search strip, centered dock, Files sidebar/inspector and Control Center cards to the target at 100% and 150% DPI. Evaluate geometry, visual contrast, material depth, clipping and hit testing.
4. Verify existing native probes still pass: retained dock density and indicator layout, unchanged menu bar refresh, native template parsing and Launchpad retention. Validate native rounded region updates during resize, show/hide and High Contrast.
5. Test actual file operations, Search results selection and shortcuts, Core-backed brightness/network/sound, window launch/close and shell recovery.
6. Send actual running Nexus screenshots and any AppVeyor failures for the next visual-fidelity iteration. Do not declare exact visual match without seeing that comparison.

## Rollback

Drop the draft feature branch. All work is presentation and opt-in native material tuning, with no preferences migration, installer changes, data writes or untrusted assets.
