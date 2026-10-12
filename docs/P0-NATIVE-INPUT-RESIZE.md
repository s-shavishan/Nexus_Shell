# P0 native input and resize patch — Windows acceptance

Baseline: `s-shavishan/Nexus_Shell`, `main`, `324c9595b8d5b7fe39f6021dd13708bb78816603` (3.6.0).
Local branch: `codex/p0-native-drag-resize`. No settings schema, privilege, shell-registration or external adapter changes.

## What this patch changes

1. Files, utility windows and Sections register physical caption rectangles with Windows App SDK 1.8 `InputNonClientPointerSource`. Windows handles dragging, button release and caption double-click. Buttons and the Sections search control remain client input. Rectangles update with XAML layout and DPI, and clear on unload, fullscreen and disposal. Registration failure restores the Windows frame. The old pointer-press `ReleaseCapture` / synthesized `WM_NCLBUTTONDOWN` path is removed.
2. Custom `WM_NCCALCSIZE` returns zero, retaining previous client pixels instead of forcing `WVR_REDRAW` for every resize. The menu bar commits its painted frame and native clip together, with a short content-only fade. It no longer expands its clip over unpainted margins while awaiting an animation callback. Acrylic paint stays opaque while the controller is pending; supported Active acrylic still uses the existing translucent design.
3. Opt-in ETW provider `Nexus-Shell-UI` records native size, clipping, caption registrations, move-size entry/exit, capture cancellation and backdrop lifetime. No background diagnostic polling or UI-thread file logging is introduced.

The reported sticky drag and black regions are **not reproduced on Windows in this delivery environment**. These are source corrections and instrumented candidates. They are not a hardware-validation claim or completion of the handoff's P0 release gate.

## Build and automated checks on Windows

Run from the repository root, using the existing .NET 8/Windows build environment:

```powershell
python .\scripts\validate-source.py
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
dotnet run --project .\tests\Nexus.Runtime.Checks\Nexus.Runtime.Checks.csproj --configuration Release
.\scripts\build.ps1 -UseMSBuild
```

The Core suite includes a test-owned HWND check of production non-client calculation and Windows-frame fallback. The native UI resource check also verifies the production caption adapter at two sizes: real registration, control exclusion, disable/re-enable and disposal. Headless UI skips remain explicit. Neither test synthesizes mouse input or certifies drag release.

After publishing, run the existing `Check-Nexus-UI.bat` in an interactive Windows desktop, or:

```powershell
.\scripts\verify-native-ui.ps1 -PublishDirectory .\artifacts\Nexus-Shell-3.6.0-win-x64 -ReportFile .\artifacts\logs\native-ui-interactive.json
```

Do not pass `-AllowHeadlessSkip` for acceptance. The executable's actual probe switch is `--verify-ui`.

## Reproduce and compare

Use the current baseline and patched build in separate directories with the same Windows build, GPU driver, wallpaper, settings, resolution and DPI. Start in normal preview mode through the existing launcher before testing managed desktop mode. Keep the previous build for rollback.

| Scenario | Procedure | Pass condition |
|---|---|---|
| Click and release | Files, Notes/Calculator, Sections: click blank title space, release, move cursor | No movement after release |
| Drag | Hold primary button, move across the window edge, release outside | Movement ends on release; a second click is unnecessary |
| Cancel | During drag, Alt+Tab or use Escape; release and return | No stale capture or cursor-follow |
| Controls | Traffic lights; Sections search; back/forward; title double-click | One correct action, normal text input and native maximize/restore |
| Snap/DPI | Win+Arrow and drag to screen edges; move between 100/125/150/200% monitors | Native snap; caption rectangles track current scaling |
| Native shortcuts | Win+R, Win+D, then type plain R/D in another app | Correct OS command; plain letters remain normal input |
| Window resize | Drag every edge; maximize/restore; repeated programmatic placement | No black patch, clipped title or multi-second residue |
| Menu/dock | Maximize/snap/restore apps; rapidly add/remove dock entries | Painted bounds match the visible/native region; correct final state |
| Recovery | Normal exit, then the existing controlled recovery drill | Explorer/taskbar/work-area restored by existing recovery paths |

Record failures/attempts, cursor-visible video and exact environment. Perform 500 mixed interaction cycles after initial smoke checks. The 500 portable geometry cycles are **not** that Windows input/visual stress test.

## Capture diagnostic markers

When WPR/WPA is already available, use a short local trace. This profile's XML is checked here; execution against the Windows WPR schema remains pending. WPR may require an elevated terminal; this patch does not change Nexus privileges or run tracing automatically.

```powershell
wpr -status
wpr -start GeneralProfile -start .\scripts\Nexus-UI.wprp!NexusUI -filemode
# Reproduce one problem for about 20 seconds.
wpr -stop .\artifacts\logs\nexus-ui.etl
```

If another recording is active, preserve it; do not cancel someone else's trace. For GPU presentation analysis use an available GPU/WPR profile alongside the markers. The custom profile alone does not measure frame pacing or GPU presents.

In WPA Generic Events, filter provider `Nexus-Shell-UI` and group by PID/HWND and role. Compare:

- `MoveSizeEvent` enter/exit: left-button state, elapsed milliseconds, size-event and region-update counts. Keyboard-initiated moves can legitimately enter with no mouse button held. `capture-changed` / `cancel-mode` aid correlation; their presence alone does not prove a fault.
- `native-size`, `region-applied`, `menu-position`, `dock-position`: correlate geometry with video and UI/CPU/GPU stalls.
- `controller-created`, `Active`/`Fallback`/`Unavailable`, `controller-disposed`: each instance has a stable resource ID in `value`. Pure resizing should retain the same controller, with no new creation/disposal burst.

Marker fields contain stable surface roles, owned HWND/PID, numeric geometry and counts. They do not contain titles, app document paths, SSIDs or Bluetooth device names. A broader system trace can contain sensitive data; review it before sharing.

## Rollback and next gate

Revert the individual logical commits in reverse order, or run the previous build. No user-data conversion is involved. Avoid overlaying this patch on an unrelated 3.7.x source tree; apply against the recorded base or cherry-pick with reviewed conflicts.

First next milestone: obtain a native Windows build, run both probes, then complete drag/menu/dock/resize smoke tests and one short before/after trace. Preserve the existing keyboard router, window registry, dock visibility and Explorer recovery while investigating any failures.

P0 must pass on Windows 10 19045 and the supported Windows 11 matrix before enabling the later roadmap work: retained optical resources, four quality tiers, further animations, 280–340 DIP system flyouts and capability-based app menus. Existing Balanced/Clear/Frosted styles are not the proposed Ultra/Balanced/Essential/Solid quality tiers.

## References checked for this implementation

- [Microsoft title-bar customization](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar?tabs=winui3)
- [Windows App SDK 1.8 non-client input source](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.input.inputnonclientpointersource?view=windows-app-sdk-1.8)
- [Microsoft WinUI issue 9661: borderless caption-region support fixed in 1.6](https://github.com/microsoft/microsoft-ui-xaml/issues/9661)
- [WM_NCCALCSIZE pixel preservation](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-nccalcsize)
- [WPR profile definitions](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/2-system-and-event-provider-definitions)
