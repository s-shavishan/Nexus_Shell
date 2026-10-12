# Rendering responsiveness: retained Launchpad and opt-in timing

Baseline: main `c5bbff45504145667bda98b5c9a4aaf7946b7f0b`.
Shan reports that sticky dragging is solved on his PC and rendering is better,
but still slower than expected. That is user-reported acceptance of dragging,
not a measured frame-rate result or completion of the Windows version matrix.
The baseline AppVeyor 1.0.55 native UI check passed with a solid backdrop
fallback; it did not certify real acrylic performance or DWM pixels.

## Source-supported change

Launchpad already realizes a bounded 24-item page and retains unchanged tile
objects. However, every `Render()` still re-filtered/sorted the catalog and
cleared/recreated the page buttons, including repeat renders with the same
catalog, category and query. A warm opening renders the cached catalog before
installed-app refresh completes, then can render the same data again.

The view now owns `LaunchpadCatalogCache`: unchanged catalog snapshots and
filtered results are reused. It invalidates on session changes, changed pins,
catalog contents, category/query, culture or state identity. Snapshots are
in memory only; no installed-app index is persisted. Explicit refresh and
failed-icon retry remain available. Page controls are retained until page
count, selected page or high-contrast mode changes. Their palette brushes are
the existing shared resources. Native drag, caption, resize and material
mechanics are preserved.

## Timing events

The existing `Nexus-Shell-UI` ETW provider now emits event 3, `WorkEvent`, only
while a listener/trace enables it. It records stage, surface role, PID, managed
thread ID, item count, synchronous elapsed milliseconds and managed allocated
bytes. Cross-thread disposal reports allocation as `-1` (unknown). There are
no diagnostic timers, automatic disk writes or UI-thread file logging. Disabled
scopes use no stopwatch or managed allocations.

| Stage | Synchronous work measured |
| --- | --- |
| `menu-open` | Launchpad appearance, placement, immediate catalog work and activation request |
| `launchpad-render` | Filtering, page reconciliation, indicator/category presentation and starting icon requests |
| `dock-refresh` | Running-window/pin reconciliation and immediate dock UI updates |
| `material-apply` | Native backdrop/paint appearance updates |
| `chrome-refresh` | Native bounds/region calculation and decoration updates |

Nested durations overlap. Do not sum all stage totals, interpret an async icon
request's start as its completion, or call these values frame time/FPS. They
help distinguish application work from later layout/compositor/presentation
costs. For OS-thread correlation in WPA, use the ETW event header, not the
managed thread-ID payload.

## Collect one comparison on Windows

Use the current baseline and patched build with the same settings, DPI,
resolution, wallpaper and GPU driver. Keep normal Nexus privileges. If WPR/WPA
is already installed, run a short capture from the repository root:

```powershell
wpr -status
wpr -start GeneralProfile -start .\scripts\Nexus-UI.wprp!NexusUI -filemode
# Repeat Launchpad open/close, dock interactions and Files resize for 20 seconds.
wpr -stop .\artifacts\logs\nexus-render.etl
```

WPR may require an elevated terminal; the application and this patch do not
elevate or start tracing automatically. Preserve an existing recording instead
of cancelling it. The custom profile alone does not capture GPU presentation;
add an available GPU/DWM profile when frame pacing is the problem. Review a
broader system trace before sharing it.

In WPA, filter Generic Events to `Nexus-Shell-UI`. Compare `WorkEvent` stage
durations and allocation distributions across identical interactions. Correlate
with CPU/layout/presentation and the existing native geometry/controller
markers. A long `menu-open`/`launchpad-render` points to UI work; short markers
with late frames need compositor/layout evidence. These are investigation
rules, not a claim that the reported delay has a confirmed sole cause.

## Checks and acceptance

Core checks cover retained identity over 500 repeats, the 600-catalog/24-page
bounds, pins, hidden apps, mutable collection membership, privacy opt-out,
catalog refresh/removal, snapshot isolation and culture/state changes. ETW
checks validate the new payload, actual allocation accounting and 1000
allocation-free disabled scopes.

The native `--verify-ui` preflight includes stage `retained-launchpad-rendering`:
20 unchanged renders must retain the actual item source and page-control
identities. This tests retained UI objects, not smoothness or displayed pixels.
Run the existing Windows build, both check suites and native preflight. Then
test collection edits, pin reorder/removal, search/category/page navigation,
explicit catalog refresh, privacy, palette/high-contrast changes and shutdown.

Portable/API compilation and data-path benchmark results are provided in this
phase's delivery report. Native execution of these new checks and end-to-end
rendering speed need a fresh Windows run. Black regions and glass-tier work
remain unresolved until that evidence identifies the next rendering path.

## Rollback

Revert the retained-Launchpad change, then the timing change, or restore the
matching backed-up source files. No settings/schema, privileges, native
shortcut, dock policy, Explorer recovery or third-party adapter change is
included. This phase retains the existing design and animation durations.

## References

- [Microsoft: optimize WinUI XAML layout](https://learn.microsoft.com/en-us/windows/apps/develop/performance/optimize-xaml-layout)
- [Microsoft: optimize ListView/GridView](https://learn.microsoft.com/en-us/windows/apps/develop/performance/optimize-gridview-and-listview)
- [Microsoft: optimize animations and media](https://learn.microsoft.com/en-us/windows/apps/develop/performance/optimize-animations-and-media)
