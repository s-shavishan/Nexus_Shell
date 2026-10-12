# Opt-in native input experiment

This is the next P0 validation increment. It uses the production `NativeCaptionRegions`, `WindowChrome` and ETW move-size events in one isolated WinUI window. The existing `--verify-ui` check remains passive; ordinary app startup, build and CI never invoke this driver.

## Run on a Windows VM

After a successful publish and passive UI check, from the repository root:

```powershell
.\scripts\verify-native-input.ps1 -PublishDirectory .\artifacts\Nexus-Shell-3.6.0-win-x64 -ReportFile .\artifacts\logs\native-input-20.json -Cycles 20 -AllowTestInput
```

For the full 500-cycle automated acceptance fixture:

```powershell
.\scripts\verify-native-input.ps1 -PublishDirectory .\artifacts\Nexus-Shell-3.6.0-win-x64 -ReportFile .\artifacts\logs\native-input-500.json -Cycles 500 -AllowTestInput
```

The wrapper is also copied into the published folder. It requires an explicit `-AllowTestInput`: the experiment moves the cursor, clicks/drags and sends Escape. Keep the mouse and keyboard idle during its five-second countdown and the run. Close its window to stop. Use a normal interactive desktop with the primary button unswapped and enough space on the current monitor; the probe refuses unsupported conditions. It checks foreground/process identity before actions and checks idle click locations against its own root HWND. Foreground races and real user input cannot be made impossible; this is a controlled test, not a general automation service.

Use a new report filename. The runner never overwrites an existing report. Probe logs are written alongside the report as `<report>.log`, without reading or exporting the regular user log/settings.

## What it measures

Cycles rotate through caption click, held drag, diagonal drag, Escape cancellation, traffic-control fixture click and search-control fixture click. The worker releases its inserted input independently of the UI's native modal move loop. Cleanup sends Escape only when this fixture owns the foreground move loop; it does not activate or cancel other applications. Buttons/keys it successfully inserted are released in `finally`.

Each cycle checks:

- Movement occurred while held for drag scenarios; click scenarios did not move.
- Primary button is up and the native GUI thread has no mouse capture or move-size loop after release, within a bounded 250 ms observation deadline.
- Additional cursor movement after release leaves the final window rectangle unchanged.
- Production move-size entries/exits balance; normal release exits report the button up. Escape can exit while the button is still held.
- Client title controls receive exactly one click instead of becoming draggable regions.

Absolute input coordinates account for negative virtual-desktop origins. Repeated presses wait past the Windows double-click interval. The recorded `ReleaseMilliseconds` is a coarse polling observation of native state clearance, not input latency, frame pacing or GPU performance.

The JSON records requested/completed cycles, individual observed rectangles, click and move-size counts, Windows version/architecture, DPI scale and failures. It makes no visual black-region judgment.

## What remains unvalidated

The driver has been C# API compiled and its portable consent/coordinate/x64 structure checks run. It has **not** injected Windows input in this authoring environment. A generated source fixture is not a hardware-validation result.

Even a native fixture PASS does not certify physical mouse/touch/pen, Alt+Tab, Win+R/Win+D, native snap, multiple-DPI monitor transfer, actual Files/Sections controls, rendered resize artifacts or sustained performance. Complete the manual matrix in `P0-NATIVE-INPUT-RESIZE.md` and capture a before/after resize trace. Four quality tiers and later flyout/menu work remain gated on P0 stability.

## Primary API references

- [SendInput and its integrity/input-state restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
- [GUI thread capture/move-state fields](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-guithreadinfo)
- [MOUSEINPUT absolute/virtual-screen flags](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput)
