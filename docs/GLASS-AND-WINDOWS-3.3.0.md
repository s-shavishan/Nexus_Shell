# Nexus 3.3.0: glass, window layout and useful desktop controls

## What this update addresses

The supplied 3.2 screenshot shows a maximized Command Prompt with its title/first line hidden behind the menu bar, an unused strip beneath the window, and a flat gray menu bar. An old maximized work-area rectangle is a likely explanation for the two geometry defects; it cannot be confirmed from pixels alone. This update adds a measured, bounded correction path and a report of the actual native bounds.

| Change | Result |
| --- | --- |
| Balanced / Clear / Frosted materials | Adjust the Windows acrylic tint and luminosity plus the Nexus overlay live, without making another build. Frosted uses base acrylic; Balanced/Clear use thin acrylic. |
| Restrained surface edge | A thin, non-interactive bevel replaces the pale band across content; rounded corners follow the owning frame. The edge disappears in high contrast and reduced effects. |
| Real material state | Control Center > Desktop reports whether Windows says acrylic is active or using a fallback. Unsupported attachment is not presented as successful glass. |
| Maximized caption repair | Managed desktop mode aligns a captioned, eligible app's visible frame to its monitor's work area, retaining the invisible native resize borders and caption. |
| Active app menu | Shows the current window title. Minimize, maximize/restore and close are available on wide menu bars and in its menu. |
| Placement presets and undo | Left half, right half and centered placement use the monitor work area. Undo restores the captured native placement while the same window/process identity remains valid. |
| Icon anchored controls | The compact Sound, Network, Bluetooth and Display panels open below their button and stay within the monitor. Invalid/stale anchors fall back to safe placement. |
| Menu attachment | The existing floating-to-attached movement uses a short translation without stretching text. Attached sides remain square. |
| Desktop diagnostics | A five-second CPU sample, monitor/work rectangles, eligible maximized bounds and latest logged glass states are collected in one command. No app titles or settings.json are collected. |

The menu bar's File and View menus are Nexus actions. This release does not extract an arbitrary Windows application's menu. Existing dock motion, one Launchpad, notification history, settings services and supervised workers remain included; no new standalone app is added.

## Window reliability boundaries

The correction only runs in the managed desktop. It excludes minimized, hidden, cloaked, borderless/fullscreen, other-monitor and Nexus-owned windows. It preserves native styles and focus, uses asynchronous positioning, and permits at most two corrections per window/process/work area, with a delayed retry. A large live window set cannot evict and renew resistant-window budgets. Closed windows release budget capacity. Window > Repair desktop layout is an explicit manual retry.

GetWindowRect includes invisible resize borders; DWM extended frame bounds supply the visible rectangle. The manifest already requests per-monitor DPI awareness. Get/SetWindowPlacement handles undo using its own workspace coordinates; SetWindowPos receives screen coordinates. Native behavior still needs the VM check below.

API references: [GetWindowRect](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowrect), [SetWindowPos](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos), [SetWindowPlacement](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowplacement), [Windows SDK WINDOWPLACEMENT declaration](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/WinUser.h), [SystemBackdropState](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.composition.systembackdrops.systembackdropstate).

## Windows build checks

scripts/build.ps1 exports all nine literal production templates, parses them with the real WinUI XamlReader, instantiates DataTemplates, lays out the production menu bar/Launchpad, switches three palettes through all three material profiles and tests native backdrop detach. The probe uses temporary in-memory state and never starts the desktop, Core, hardware settings, input hooks, tray or a settings writer. It can briefly show a test window.

An interactive build must pass the native check. A headless worker records **SKIP**, not PASS; generated C#/XAML compilation and compiled-resource verification still run. Build logs contain native-ui.json, and the published folder contains Nexus.UI-Check.json. Run **Check-Nexus-UI.bat** in the Windows VM if the build was headless. Its report is saved under `%LOCALAPPDATA%\WhiteDreams\NexusShell\diagnostics\native-ui.json` and the launcher enforces a deadline.

This probe checks construction/layout, not pixel appearance, all Control Center hardware views or animation frame timing. Native fallback is an allowed outcome and must still be inspected for the intended appearance.

## Short Windows 10 VM check

Use the whole new compiled folder and keep the old folder for comparison. Snapshot the VM before changing sign-in configuration.

1. Run Check-Nexus-UI.bat once. Confirm PASS; if it fails, use native-ui.json and nexus.log rather than rebuilding repeatedly without evidence.
2. Launch preview to confirm the surfaces open, then use Launch-Nexus-Desktop.bat for managed mode. Maximize Command Prompt and another standard captioned app. Confirm the full native title and first line remain below the menu bar and the visible bottom reaches the correct work area. Test restore/maximize again; floating dock mode should not reserve a permanent bottom strip.
3. Switch app focus. Confirm the title follows it. Try minimize, maximize/restore, left/right half and centered placement, then Undo placement. Confirm the correct app is targeted and undo returns its previous geometry/state. Fullscreen apps should hide the desktop bars.
4. Click Sound, Network, Bluetooth and Display separately. Confirm the compact panel follows the clicked icon and can expand to Control Center. Rapidly dismiss/reopen and switch sections; old responses must not repaint the new section.
5. In Control Center > Desktop, keep Glass surfaces on and Reduced effects off, then switch Balanced, Clear and Frosted. Inspect the menu bar over wallpaper and over a dark app. Confirm there is no bright painted strip, corner halos, clipped controls or unreadable text. Interface animations can be disabled independently. Restart Nexus once and confirm the material profile persists.
6. While a captioned app is maximized, run Check-Nexus-Desktop.bat. The timestamped diagnostics folder includes desktop-health.json and normal logs. A logged Active state confirms the compositor's reported state, not the appearance of every pixel. A geometry observation can also reflect an app's own size constraints. This helper reads state and does not move windows or change settings.

Then use the existing session/worker recovery acceptance for release readiness. The five-second CPU sample is a quick diagnostic, not proof of lower latency. Use the existing measure-resources.ps1 for sustained comparisons against the previous build. VM graphics, remote sessions, Windows transparency, power policy and high contrast can affect acrylic. These profiles use supported Windows blur/tint; optical refraction is not implemented.

## Validation performed in the implementation environment

The portable Core and Runtime suites pass, including 260 new window/glass assertions and 21 material/protocol assertions, plus the existing state, settings and recovery suites. Native C# source/API compilation uses the Windows/WinUI reference assemblies with temporary generated-field substitutes; it is not the Windows XAML generator or a runnable Windows publish. Source/resource checks, PowerShell parsing, template export and synthetic update integrity checks are also performed before delivery. Actual native UI check execution, Windows hardware behavior, pixel appearance and sustained performance remain pending Windows acceptance.
