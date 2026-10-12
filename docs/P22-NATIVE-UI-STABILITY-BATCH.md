# Nexus Shell P2.2 — Native UI Stability & Retained Rendering

Development branch: `feat/p22-native-ui-stability-batch`  
Baseline: `bd0c53a305920dc3854da3c6bedb270b3ce694d2` (merged P2.1 PR #19).  
Status: **integration candidate; do not merge or release until a green exact-revision AppVeyor Windows build**.

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

### E. Guard native window-chrome settings and invalid radii

`WindowChrome.SetCorners`, `SetFullscreen` and `SetClip` formerly entered the Win32 region-refresh path for identical appearance values. This is especially wasteful during repeated palette and accessibility application. `ChromeAppearancePolicy` now normalizes NaN, infinity, negative and excessive corner radii before they reach GDI and recognizes no-op setter requests.

- Retain the same native `WM_SIZE`, `WM_DPICHANGED` and interactive resize reconciliation paths, including clipping recovery after `WM_CANCELMODE` and capture loss. No XAML layout or focus change.
- Skip identical explicit frame settings but refresh immediately when the radius, fullscreen mode or clip rectangle changes. High Contrast uses square corners.
- Add opt-in `RegionRefreshRequests` diagnostic counter on the native chrome instance. A test-owned Win32 fixture asserts that repeated settings do not invoke a redundant refresh, while true changes do.
- Extend portable Core tests for fractional/invalid corner inputs, clip equality, and accessibility transitions.

### F. Retain desktop bounds without unnecessary AppWindow moves

The native desktop layer previously sent `AppWindow.MoveAndResize` on every monitor/shell-position event, including when the HWND was already exactly the target monitor size and position. Repeated native position messages can cascade into WinUI layout and subclass callbacks.

- `DesktopWindow.Position()` now reads the actual native HWND rectangle and resizes only if it differs from the current monitor bounds (or cannot be read).
- `ShellLayerInterop.AnchorDesktop` still runs **on every event**, even if geometry is unchanged, because Windows WorkerW/Progman ancestry and desktop Z-order can change independently of dimensions. No desktop ownership or shell takeover logic is bypassed.
- New `DesktopSurfacePositionPolicy` is compiled into portable Core checks. Windows `DesktopNativeChecks` also verifies the no-op behavior against its own real Win32 test HWND.
- Opt-in `desktop-native-resize` ETW work timing records actual native desktop resize operations; no background polling or new disk writes.

### G. Retain foreground selection across frequent dock visibility polls

The visibility timer can run every 80 ms while the dock is being interacted with. `DesktopEnvironment.ObserveForeground` previously rebuilt a new running-window candidate list and searched that list on **every tick**, even when the OS foreground HWND and underlying `_windows` enumeration had not changed.

- New `ForegroundLookupCache<T>` memoizes the selected foreground identity only for an unchanged `_windows` snapshot and HWND, and only while the selected external application's native HWND/process ownership remains valid.
- When a native enumeration publishes a new list, or the foreground HWND changes, full discovery runs again. Nexus-owned utility windows bypass positive caching because their internal registry can change independently of the external window snapshot.
- Unrecognized application HWNDs are **not** negatively cached, so an app which registers after the first poll is discovered promptly. Only known empty/desktop foreground states may use a safe empty cache.
- Cached selection does not suppress reading `IsZoomed`; existing maximized state changes and `ActiveWindowChanged` continue to propagate.
- Portable checks use 1,500 stable polls, new snapshot publications, HWND/process reuse, negative-cache and thrown-discovery recovery. This measures a policy outcome, not a claimed FPS improvement.

### H. Remove UI-thread disk logging from successful maximized-window repair

The existing `MaximizedWindowReconciler` emitted a synchronous `Log.Write` for every accepted native `SetWindowPos` repair. Since the logger opens/rotates a file on the caller thread, this could add unrelated disk latency during focus, maximize and work-area transitions.

- Preserve the two-attempt native `WindowRepairBudget`, HWND ownership checks and asynchronous Win32 positioning flags.
- Publish accepted/refused repair outcomes through the already-established opt-in `Nexus-Shell-UI` ETW Surface event. An accepted outcome no longer triggers a disk write.
- Preserve one diagnostic file message when Windows **refuses** the correction. The ETW payload includes the target HWND and intended dimensions, but no window title or document path.
- No claim of measured compositor FPS improvement without Windows frame-time traces.

### I. Isolate native Win32 chrome observers

A throwing `Restored` subscriber after `DefSubclassProc` could propagate out of the hook; a throwing maximize-state listener previously propagated into `WindowChrome.Refresh` and permanently disabled rounded-region updates.

- Restore and attached-state event consumers are now called individually within exception boundaries. A failed subscriber does not skip later subscribers, and diagnostics are logged at most once per listener group per chrome lifetime.
- Preserve the original `WM_SIZE`, Nexus restore message and `WindowChrome` subclass lifecycle.
- The actual Windows `NativeChromeChecks` fixture now registers one throwing and one healthy state/restore listener and verifies the healthy listeners still execute without disabling GDI decoration.

### J. Recover queued dock/desktop and native WinEvent failures

An accepted `DispatcherQueue` work item can execute after a WinUI window has begun closing. Both `CoalescedUiDispatch` (dock/desktop position) and `WindowEventDispatchGate` (native task-list events) previously released their queue gate but allowed subsequent consumer exceptions to escape the asynchronous UI callback.

- Add optional error reporters to both production dispatch gates, preserving existing exception propagation for callers without a reporter.
- The native dock, desktop and WinEvent call sites supply diagnostics to contain late UI exceptions. Refusal and throwing dispatcher admission release the pending flag, and native foreground priority is still retained after a rejected queue request.
- New portable `NativeUiDispatchChecks` covers coalescing, reentrant dispatch, logging failures and **500** failed callbacks. Expanded `WindowEventPipelineChecks` covers rejected foreground events and 500 failing deliveries.
- The 80 ms dock timer now passes its sampled foreground HWND to `TaskbarView.RefreshWindowStates`, avoiding an extra native foreground read on a changed-foreground tick.

### Integration with merged Liquid Glass quality work

PR #20 (`codex/glass-quality-policy`) merged into `main` as `016bfeb95dff378f385f9746c36915f08716e93f`. It is now integrated into this development branch by merge commit `4b175bd739eb83f9d1abe6de28b89e382d465bd0` with both parents preserved. The merge retained all 27 PR #20 files while combining the two overlapping sources:

- `DesktopEnvironment.cs`: current glass-quality theme application, quality tracking and appearance state coexist with P2.2's safe native WinEvent dispatch error reporter.
- `UiResourceProbe.cs`: native proof of all four requested glass-quality tiers and solid fallback **plus** the pinned-icon refresh probe and 20 unchanged dock polls.

The full PR #20 Glass Quality runtime/persistence/accessibility work remains untouched. A source-level merge is **not a Windows build result**: run both Core and Runtime check suites and the native probe on the exact integration revision. Do not merge into `main` on the basis of GitHub's conflict-free comparison alone.

## Merge authorization

Only mark this batch READY TO MERGE if all of the following are documented for the **same final commit SHA**:

1. AppVeyor passes Core checks, Runtime checks, native diagnostic fixture, complete Windows MSBuild/XAML compile and package generation (not merely `pending` or a build for older `main`).
2. The WinUI native `UiResourceProbe` passes including retained dock icon identities, caption/transition checks, Glass Quality tiers and opaque fallback. If hosted AppVeyor is noninteractive and skips a portion, record the skipped coverage and do **not** call it passed.
3. No merge conflicts or lost functionality from PR #20; check the glass-quality runtime protocol/setting and native `WindowEventDispatchGate` protection and both `UiResourceProbe` sections.
4. Restore/minimize, dock auto-hide, appbar reservation, mixed DPI, monitor changes, High Contrast and acrylic fallback have an interactive native validation plan; complete the essential smoke test before assigning the final READY TO MERGE verdict.
5. The complete source is reviewed for build warnings/errors and any failed/canceled CI jobs have been resolved. No auto-merge.

Until these are demonstrated, status remains **HOLD — DO NOT MERGE**.

## Safety boundaries

- Do **not** modify Windows Explorer integration, shell installation/registration, work-area ownership, input hooks, native privileges or elevation, application execution, data persistence or file-provider behavior.
- Explicit user refresh may rebuild dock pin surfaces. Regular dock polling, state changes and unchanged foreground snapshots must not.
- Frame fast-path must not suppress resizing, DPI changes, palette compatibility/High Contrast or native positioning. The original `WindowChrome` WM_SIZE/WM_DPICHANGED callbacks continue to update regions.
- No real FPS/latency claim without ETW/WPA/DWM evidence. These are targeted source-level optimizations and policy checks.

## Verification gates before a PR

1. Run source validation and portable Core/Runtime checks against the **final branch SHA** (including `ChromeAppearancePolicy` and `NativeChromeChecks`).
2. Windows AppVeyor compile/XAML generation, native `UiResourceProbe`, native desktop diagnostics, package/publish and rollback paths must succeed on that exact SHA; an older green main or a pending/cancelled status does not establish a pass.
3. In a Windows 10 test session and Windows 11 where available, exercise 20 minimize/restore cycles, appbar notifications, keyboard and dock preview actions, maximized/browser-borderless fullscreen switching.
4. Toggle compact vs normal dock, floating vs attached, High Contrast, transparency and Reduced Effects; move across negative-origin and fractional-DPI monitors. Compare actual desktop bounds and icon geometry.
5. Change a pinned app shortcut icon (same path), explicitly Refresh installed apps, and verify both Launchpad and the dock show the updated image. Ordinary polling should not recreate pins.
6. Rename window titles while their preview is open, including title-dependent Nexus windows; process label, icon and action state should update without a broken DWM relationship.
7. Collect opt-in `dock-frame-layout`, `dock-desktop-relayout`, `dock-refresh`, `dock-enumerate`, `maximized-repair-queued/refused` and compositor frame-time data before claiming responsiveness gains.
8. During native tests, exercise a thrown/deferred window-state observer and a failed dispatcher callback; subsequent restore/dock updates must still work, including after multiple sequential failures.

## Rollback

There are no schema or migration changes. Keep all P2.2 edits on the development branch until integration CI is proven. If visual behavior regresses, revert the corresponding stage commits or abandon the branch; `main` is unaffected.
