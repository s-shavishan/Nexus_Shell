# Nexus 3.1.0 — UI changes and Windows acceptance

This update modifies the native C# / WinUI desktop environment. It continues from 3.0.0. The [preview](UI-3.1.0-preview.png) is a design projection with example content, using the shipped vector icons and new wallpaper. Windows acrylic rendering, native control spacing and animation timing need to be checked on the VM.

## Implemented surfaces

| Surface | Change |
| --- | --- |
| Shared material | Separate frame/card/input layers, a subtle vertical highlight and a fine gradient edge; cached paint, opaque disabled-glass/reduced-effects fallback and system high-contrast colors. |
| Midnight desktop | A static 3.6 KB mountain/lake SVG replaces the flat Midnight image in the desktop view. The older cached PNG remains the decode fallback; the solid canvas remains available if that image also fails. |
| Menu bar | Small Nexus icon, cleaner type/spacing, floating 4 DIP top gap and existing animated attachment. Opening a Nexus overlay preserves the underlying app's attachment/full-screen state. Compact time respects the selected 12/24-hour format; unread counts use a bounded badge. |
| Dock | 40 DIP app icons in normal density, 30 DIP in compact density, consistent frame edge and direct pin removal. Pin context menus keep the dock available until dismissed. |
| Launchpad | Header identity, integrated search with a focus outline, larger icons, Pinned category, pin/unpin context action, clickable page indicators and a helpful empty state. The app catalogue remains bounded and paged. |
| Control Center | Six navigation tiles, clearer device cards, quieter preference switches, inline error messages and a fixed activity slot. Its indeterminate animation stops when idle or hidden. |
| Notification Center | Date groups, All / Warnings & errors filters, severity icons, accessible dismiss controls and an empty state. History, dismissals and read state keep their existing atomic persistence. |
| Files | Consistent traffic-light colors, a separated title strip and finer sidebar/inspector frames. Removing a redundant full-window paint layer allows the owned window material to show through. |
| Date widget | Refined spacing/type and an actual button, so keyboard users can open Study as well as mouse users. It clamps to the viewport and hides below 300 DIP width. |

Glass means Windows acrylic blur/tint plus the Nexus paint hierarchy. The preview's blur is an approximation. No optical-refraction effect, new standalone application, external-app frame patch or credential UI is added.

## Stability behavior

The Control Center view now has an opening/section generation. Device reads and preference failures from an earlier opening are ignored by the current view. Accepted device commands may still finish; the UI guard does not replay or silently cancel a hardware write. A fresh read follows pending work when needed.

Unchanged network, Bluetooth and power snapshots retain the existing controls instead of recreating them at every poll. This preserves keyboard focus and confirmation state for identical snapshots. A genuinely changed device snapshot can still rebuild those controls. Sound and brightness retain their existing in-place updates and intent coalescing.

Notification refresh requests are coalesced. Equal history/appearance does not rebuild the list, and no-op clear/dismiss actions do not cause another paint/save. Dock state changes reuse their transparent paint. Cached gradients are retained between clock and window events.

## Validation obtained here

The portable Core suite passes, including 66 Start-key, 31 startup, 1,008 layout/work-area, 118 major desktop and 32 new UI reliability assertions. New cases exercise late asynchronous success/failure after hide/reopen, current-error visibility, bounded pinning, target casing, local date grouping, compact 12-hour time and inbox change counts.

The Runtime suite passes: 98 settings checks, 121 Control Center checks and 101 existing runtime assertions, including 30 simulated Files worker crashes. Actual named pipes, Windows job cleanup and hardware tests are explicitly skipped in this Linux environment.

Shell source/API compilation succeeds against restored Windows/WinUI references. This uses temporary XAML field stubs; its 19 reference/stub warnings do not establish a native Windows XAML build. Source validation passes 119 required files, 140 named elements, 70 handlers and nine runtime templates, including static SVG and publish/fallback wiring. All PowerShell scripts parse. The synthetic update fixtures pass, including installing the actual new SVG and rejecting altered wallpaper bytes before creating a destination. Those fixtures simulate a Windows environment; they do not install Nexus in a real Windows session. Whitespace, version metadata and guide links pass. The exported patches are applied to isolated Git indexes and checked against the exact delivery tree.

## Windows 10 VM acceptance

1. Snapshot the VM. Keep the previous compiled folder. Build/package the full 3.1.0 source using README.md, or use the corresponding compiled Windows CI artifact. The Source ZIP and `.patch` files are not runnable binaries.
2. Start `Launch-Nexus.bat` in preview. Confirm the new Midnight scene, panel hierarchy, dock icon sizes and readable Files title strip. Open Files and test maximize/restore/left-right snap. Preview hides the top bar above maximized/snapped windows to protect their title controls.
3. Start `Launch-Nexus-Desktop.bat` for the managed session. Check the floating top gap, animated bar attachment, square maximized/snapped Nexus frames, dock auto-hide/reveal and full-screen behavior. Open Launchpad, notifications, Control Center and dock context menus above a maximized/snapped app: the bar must retain that app's attachment state. Return to the desktop and verify floating placement. Repeat in preview; an overlay must not re-expose the bar above protected title controls. Native external apps keep Windows' animations.
4. In Launchpad search, use Tab, arrows, Enter and Escape. Right-click an app or use the keyboard context menu to pin/unpin it; verify the dock changes. Test the Pinned category, empty results, page dots and pin removal from the dock. Reopen and restart to check persistence.
5. In Control Center open Sound, navigate away while a read is pending, hide/reopen quickly and change a desktop preference. A previous result/error must not replace the new opening. Test slow/failed device reads and Refresh. Do not repeat an uncertain hardware write until its state has been read.
6. Leave a network/power/Bluetooth control focused while an unchanged poll completes. Focus/confirmation must remain. Verify volume/mute, app mixer and supported brightness. The loading line must stop animating when idle, without changing the panel's layout height on each poll.
7. Generate Nexus alerts, filter Warnings & errors, dismiss one, clear all and restart after the save debounce. Test long titles/messages and keyboard dismissal. Reopening an unchanged list must not repeatedly jump the scroll position due to duplicate queued paints.
8. Change glass, reduced effects, all wallpaper palettes and high contrast while panels are open. Keyboard focus outlines must remain visible. In the test build, temporarily rename `Assets\Wallpapers\MidnightGlass.svg`: Midnight must use its old PNG or solid canvas. Restore the asset before package verification; incomplete published builds must be rejected.
9. Test 100%, 150% and 200% scale, a display to the left/above, small resolutions, monitor removal and VM graphics acceleration disabled. Check panel scroll access, reachable close controls and date-widget keyboard access.
10. Repeat the [3.0 recovery acceptance](SESSION-CORE-3.0.0.md), especially Control Center-only worker termination, Core restart, external-app survival and one hour of idle/active CPU, memory/handle and input observation. These native measurements have not been obtained here.

The attached 2.1.0 logs show a foreign sign-in policy conflict and orderly Core shutdown, rather than evidence of a new Core crash. Keep that policy preserved and use [startup inspection](START-AND-SIGNIN-3.1.0.md) to identify its owner before changing it.
