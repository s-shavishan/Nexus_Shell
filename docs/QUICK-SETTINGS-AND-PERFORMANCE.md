# Quick Settings and desktop performance — 1.4.0

## Independent panel

Use the taskbar Quick Settings button, desktop right-click → Quick settings, or Win+A in a managed session. Start and Quick Settings dismiss each other. Escape, Close and deactivation dismiss the panel without closing the desktop. The panel is created on demand, owns a native tool window, stays above the primary taskbar and scrolls on small displays. More controls opens Sections explicitly.

| Control | Behavior |
|---|---|
| Sound | Real default-output volume and mute; More controls opens the existing per-app mixer. No audio is recorded. |
| Brightness | Real hardware brightness only when a single physical monitor exposes the DDC/CI brightness capability. VM displays and unsupported monitors disable the slider with a message. This release does not implement the ACPI/WMI laptop brightness route. |
| Fast | Simple gradient background, no custom motion or glass. Recommended starting point for this VM. |
| Balanced | Cached ribbon wallpaper and permitted motion; glass off. |
| Full | Cached wallpaper, permitted motion and the existing glass preference. Windows may supply solid material fallbacks. |
| Compact taskbar | Changes the Nexus reservation once for the new height; an open panel follows its updated bounds. |
| Status | Power and active network-link information. A link is not an Internet connectivity test. |
| Actions | Windows Settings, Control Panel, Task Manager, more Nexus PC controls and Return to Windows/Exit Nexus. |

Audio stays on its existing MTA worker. Monitor/status operations run on background tasks; no driver call is awaited synchronously by the UI. Slider writes are coalesced at 120 ms with one operation in flight per controller. Device changes are checked before writes. Five-second audio/status polling occurs only while the panel is visible; display brightness is read on opening/Refresh and after changes. Slow operations are retained across timeouts to avoid building a request backlog. Physical monitor handles are released after each operation.

## What changed in the lag path

The former taskbar timer compared the OS working area against the Nexus area every half-second, then rewrote and synchronously broadcast settings changes when different. Explorer's hidden appbar manager remained alive in session mode. This creates a plausible repeated layout conflict during dragging; it is a source-level diagnosis, not a measured Windows trace.

The timer now reads fullscreen stacking only. A successful intended working area is cached; actual geometry changes and explicit display/DPI notifications update it. `SPI_SETWORKAREA` uses flags 0, followed by asynchronous notifications to other app windows while excluding Explorer and Nexus. Idle OS-area differences are not continuously repaired. If Explorer restarts or an external appbar changes the area, check maximized bounds; use Windows return/relaunch if the reservation needs re-establishing. Display/DPI changes invalidate the cache. This tradeoff requires the native tests below.

The host retains validated Explorer process handles, detects exits and validates a new process on restart. It still checks surface handle/process/class before hiding or restoring, but no longer rereads the module path every maintenance pass. The six desktop wallpaper images preserve the original ribbon paths/colors at 2560×1600; only the selected image is used. Fast mode hides that image.

Reference API behavior: [SystemParametersInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow), [SendNotifyMessageW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendnotifymessagew), [monitor brightness](https://learn.microsoft.com/en-us/windows/win32/api/highlevelmonitorconfigurationapi/nf-highlevelmonitorconfigurationapi-getmonitorbrightness).

## VM comparison and acceptance

Keep both complete builds. Use the same VM resolution, display scale, virtual GPU/3D settings and application windows for 1.3.1 and 1.4.0. Return fully to Windows between runs. Do not change graphics settings during the initial comparison.

1. Record dragging in plain Windows, then 1.3.1 session mode, then 1.4.0 Fast. Use the same Notepad/browser/Settings window, drag for 20–30 seconds and resize/maximize/restore it. Record whether only Nexus windows or all applications lag.
2. Compare Balanced and Full. Observe Nexus.Shell, Nexus.DesktopHost, Explorer and Desktop Window Manager CPU/GPU in Task Manager. Record the VM guest OS, vCPU/RAM, graphics adapter/driver, resolution and scale. No performance target is claimed before measurement.
3. Leave the desktop idle for a minute, then drag again with Quick Settings closed. `nexus.log` in the Nexus data folder should show one “Desktop work area reserved” entry at initial placement, with extra entries only for actual density/display changes. There should be no half-second reservation stream.
4. Repeatedly toggle compact size, maximize an external app, fullscreen it, leave fullscreen, and change resolution/DPI. Nexus must remain correctly stacked and maximized apps must stay above its taskbar. Restart Explorer through Windows tooling and check the reservation; report any overwritten area.
5. Open Quick Settings with Sections closed. Test keyboard focus, Escape, deactivation, Close, Alt+F4/reopen, taskbar/context/Win+A entry, scrolling at 1366×768 and 150–200% scale, high contrast and Windows animation preferences.
6. Drag volume repeatedly and click Mute/Unmute quickly. Change the default audio device and disconnect/reconnect it. No writes may reach a different device using stale state. Opening/refreshing the panel alone must not change audio or brightness.
7. An unsupported VM display must show unavailable brightness. On a known compatible DDC/CI monitor, test its reported range and readback. Keep laptop/ACPI and virtual displays marked unsupported for this release.
8. Change appearance from Quick Settings while Sections is open; confirm both agree and notes/pins remain. Close the panel and verify status/audio polling stops. Reopen repeatedly without growing workers, request queues or retained monitor handles.
9. Exit via Quick Settings → Return to Windows. Check original Windows taskbars, auto-hide behavior, desktop visibility and maximized working area. Repeat the existing host/UI failure recovery checks.

Local checks cover reservation repetition/retry, profile persistence, brightness numeric ranges and panel geometry. Native Windows build, smoothness, drivers, focus and lifecycle require this VM evidence. If dragging is still severe in Fast with the panel closed, retain logs and a short comparison recording; evaluate the VM graphics path using those observations.
