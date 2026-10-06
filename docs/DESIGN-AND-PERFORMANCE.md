# Nexus Orbit: design and resource behavior

The desktop aims for a calm macOS-inspired layout with a distinct Nexus identity. Pearl text, violet focus accents, teal orbital details, translucent tinted panels, generous spacing, and vector icon tiles provide the finish. The artwork is static and the running app does not load the design-reference image.

The panels have a layered glass appearance. They do **not** continuously blur the desktop behind them. This avoids adding live backdrop sampling just for decoration. Reduced effects replaces the principal translucent surfaces with opaque fills, hides ornamental artwork, and disables custom motion. Features and app launching stay available.

## Motion policy

| Interaction | Implementation |
|---|---|
| Change workspace | 180 ms opacity, 200 ms translation, at most 8 logical pixels |
| Dock/card content hover | 140 ms transition, 4-pixel lift and 1.045 scale |
| Search filtering | Update the collection without an entrance animation |
| Rapid repeated input | Replace the animation on the same property; input never waits for completion |
| Nexus loses activation | Stop custom animations and pause visual sampling |
| Reduced effects / Windows animations off | Custom motion disabled |
| High contrast | Decorative effects disabled; primary colors use system resources |

Animations run through Microsoft.UI.Composition rather than a user-authored frame loop. Hover animates the button content while its input bounds stay fixed, avoiding a moving hover boundary. No timer drives wallpaper animation, width/height interpolation, or pointer-position tracking. Native control and flyout animations remain governed by WinUI and Windows; Reduced effects disables this project's custom animation layer.

Windows animation preference is read at load/reactivation. High-contrast changes are dispatched to the UI thread. The finite animations and shared definitions aim to reduce UI-thread work; smoothness is still a Windows test result, not a guarantee from the choice of API.

## Bounds and idle behavior

The discovered catalog contains at most 500 shortcuts from each Start-menu root. Records are lightweight names/paths/glyphs. The full app library is a GridView with a finite star-row viewport and native container virtualization. It sits outside any outer ScrollViewer. When leaving the library, its ItemsSource is cleared; WinUI can retain a bounded recycling cache. Home displays at most six pin records, and the dock shows at most five. No executable-icon bitmap cache is maintained.

Settings accept at most 100 pins, 200 activity entries, 300 usage records, and a 2 MB settings file. Usage is approximate process foreground time sampled every five seconds, with idle and long-gap exclusion. The tracking timer is stopped when tracking is disabled. While minimized with tracking off, Nexus has no recurring visual or tracking timer; a pending settings write or discovery task can finish.

The on-screen resource footer samples only while the window is active and the footer is visible. It shows this process's working set and CPU normalized across available logical processors. CPU uses actual monotonic elapsed time; the first/resume sample has no CPU estimate. The tooltip adds private bytes. GPU allocations and Windows DWM memory are excluded.

Settings changes schedule a two-second coalesced save. A worker writes an immutable snapshot atomically. A final synchronous save on normal close waits for an in-flight write and prevents an older queued snapshot from overwriting it. Closing may briefly wait for storage. Abrupt power/process termination can lose changes that have not yet reached the atomic file.

## Measure on your Windows PC

Build once with the included cloud/local route, launch Nexus, wait for warm-up, then capture scenarios separately. From the source project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\measure-resources.ps1 -Scenario Home -Seconds 60
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\measure-resources.ps1 -Scenario Apps -Seconds 60
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\measure-resources.ps1 -Scenario ReducedEffects -Seconds 60
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\measure-resources.ps1 -Scenario Minimized -Seconds 60
```

Set the named scenario yourself before each command. The script labels the run; it does not click controls or modify preferences. The portable bundle also includes the script beside the executable; use `.\measure-resources.ps1` there. CSV and JSON reports are written under `%LOCALAPPDATA%\WhiteDreams\NexusShell\measurements`, so the installed app folder can remain read-only. No report is automatically shared.

Compare average/peak working set, private bytes, and normalized CPU across scenarios. Use Task Manager separately for GPU/DWM cost. Compare gaming frame rate with Nexus closed, minimized with tracking off, and minimized with tracking on. Check idle memory again after ten minutes and after repeated navigation; increasing memory across every cycle needs investigation. A nonzero cache that stabilizes is different from unbounded growth.

There are **no measured MB, FPS, or Electron comparison claims** in this source release. Lower memory usage is a design objective. .NET/WinUI runtime overhead and framework caches remain, and this must be tested on real Windows before it can be called a reliable release.

## References

- [WinUI collection virtualization](https://learn.microsoft.com/en-us/windows/apps/develop/performance/optimize-gridview-and-listview)
- [Animation performance](https://learn.microsoft.com/en-us/windows/apps/develop/performance/optimize-animations-and-media)
- [XAML / Composition interop](https://learn.microsoft.com/en-us/windows/apps/develop/composition/using-the-visual-layer-with-xaml)
- [Windows animation preference](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.animationsenabled)

`Nexus-Orbit-preview.svg` is a code-drawn design reference with sample pins, not a Windows runtime screenshot. Font rendering, native flyouts, focus outlines, and window scaling must be assessed in the Windows build.
