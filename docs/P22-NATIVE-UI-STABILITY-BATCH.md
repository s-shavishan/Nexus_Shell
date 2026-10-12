# Nexus Shell P2.2 — Native UI Stability & Retained Rendering

Development branch: `feat/p22-native-ui-stability-batch`  
Baseline: `bd0c53a305920dc3854da3c6bedb270b3ce694d2` (merged P2.1 PR #19).  
Status: **development checkpoint; do not merge or release yet**.

## Source findings and implemented changes

### A. Avoid redundant dock frame layout and native clipping

`TaskbarWindow.Position()` previously rewrote `View.Width`, `View.Height`, alignment, `Margin`, available width and `WindowChrome.SetClip` even when repeated appbar notifications left the visible dock frame unchanged. The earlier P2.1 improvement skipped unrelated desktop relayout but did not avoid these frame writes.

- New `DockFrameGeometry`/`DockFrameLayoutPolicy` tracks dock pixel bounds, reservation, DPI scale, floating/attached mode and High Contrast.
- On a changed frame, apply **all** original XAML and GDI clip updates; on an unchanged frame, skip identical writes. Visibility logic and native taskbar registration **always** continue.
- Opt-in ETW `dock-frame-layout` synchronous work duration is recorded only for an actual frame update.
- Portable Core tests cover no-op calls, negative-origin monitor offsets, pin-width change, reservation moves, 125%→150% DPI, dock attachment and High Contrast.

### B. Reuse one native foreground geometry snapshot

The 80 ms active dock visibility timer previously fetched the foreground HWND once for state icons and again for fullscreen behavior, queried the dock monitor multiple times, and fetched the same foreground HWND rectangle twice for fullscreen and attached-menu classifications.

- The visibility timer forwards its observed foreground HWND into `RefreshVisibility`.
- Each visibility evaluation reads dock monitor/work-area and DPI scale once, and shares a single foreground rectangle for both classifications.
- New `DockForegroundGeometryPolicy` captures the existing `DockFullscreenPolicy` and `MenuBarLayout.Attached` semantics. It does not change maximize, monitor selection, edge-reveal or keyboard behavior.
- Portable checks preserve normal-window visibility, native-caption maximized apps, borderless fullscreen games, invalid/cross-monitor rectangles, minimized state and top-aligned attached menus.

### C. Explicit installed-app refresh also updates pinned dock icons

An explicit Launchpad catalog refresh already invalidated completed `AppIconCache` tasks and rebound Launchpad tiles, but did not recreate existing dock pin Images. The dock could retain an outdated icon at the same target path until a separate pin change.

- Following a **successful forced** installed-app rescan, request `TaskbarView.RefreshPinnedAppIcons()`.
- Rebuild pin UI using existing pin creation/lifecycle methods, preserving window-item identity and avoiding any rebuild during ordinary dock polling.
- The native `UiResourceProbe` now exercises one forced pin-surface rebuild and verifies that 20 subsequent unchanged refreshes retain the new button identity.
- There are no new background filesystem scans, network paths, persistent caches or modified pin commands.

### D. Keep a live dock-preview card synchronized after window metadata changes

`DockPreviewWindow.RefreshWindows()` updated the title and accessibility label but could leave stale process details, fallback icon or button state. It now updates the process detail, replaces the fallback icon if its icon identity truly changed, and reconciles valid/minimized/window-controls after metadata changes. The DWM thumbnail itself retains its existing independently managed lifetime.

## Safety boundaries

- Do **not** modify Windows Explorer integration, shell installation/registration, work-area ownership, input hooks, native privileges or elevation, application execution, data persistence or file-provider behavior.
- Explicit user refresh may rebuild dock pin surfaces. Regular dock polling, state changes and unchanged foreground snapshots must not.
- Frame fast-path must not suppress resizing, DPI changes, palette compatibility/High Contrast or native positioning. The original `WindowChrome` WM_SIZE/WM_DPICHANGED callbacks continue to update regions.
- No real FPS/latency claim without ETW/WPA/DWM evidence. These are targeted source-level optimizations and policy checks.

## Verification gates before a PR

1. Run source validation and portable Core/Runtime checks against the **final branch SHA**.
2. Windows AppVeyor compile/XAML generation, native `UiResourceProbe`, native desktop diagnostics, package/publish and rollback paths must succeed on that exact SHA; an older green main or a pending/cancelled status does not establish a pass.
3. In a Windows 10 test session and Windows 11 where available, exercise 20 minimize/restore cycles, appbar notifications, keyboard and dock preview actions, maximized/browser-borderless fullscreen switching.
4. Toggle compact vs normal dock, floating vs attached, High Contrast, transparency and Reduced Effects; move across negative-origin and fractional-DPI monitors. Compare actual desktop bounds and icon geometry.
5. Change a pinned app shortcut icon (same path), explicitly Refresh installed apps, and verify both Launchpad and the dock show the updated image. Ordinary polling should not recreate pins.
6. Rename window titles while their preview is open, including title-dependent Nexus windows; process label, icon and action state should update without a broken DWM relationship.
7. Collect opt-in `dock-frame-layout`, `dock-desktop-relayout`, `dock-refresh`, `dock-enumerate` and compositor frame-time data before claiming responsiveness gains.

## Rollback

There are no schema or migration changes. Keep all P2.2 edits on the development branch until integration CI is proven. If visual behavior regresses, revert the corresponding stage commits or abandon the branch; `main` is unaffected.
