# Nexus 2.0.0 Windows acceptance

Status: source delivery. Windows build, executable launch and native visual acceptance are pending. Shan reports the earlier 1.8 tests passing; the latest screenshot demonstrates additional UI/native failures addressed by this release.

## Run the new build

1. Snapshot the Windows 10 VM. Record edition/build, display resolution, scaling, GPU acceleration and Windows transparency/animation settings. Keep the old build separately.
2. Run the README build/check commands, including both managed check projects, PowerShell update fixtures, native XAML compilation and resource verification. Use the full 2.0 package for this first acceptance run.
3. Close every older Nexus session and launch `Launch-Nexus.bat` from the complete 2.0 folder. Do not copy a new EXE over a running installation.
4. Enable liquid glass in Personalize and turn off Nexus Reduced Effects for visual testing. If Windows/VM graphics use a solid fallback, record that separately from a rendering defect. Enable Windows transparency and graphics acceleration when the VM supports them.
5. Test the preview cases below, then run `Launch-Nexus-Desktop.bat` for supervised takeover. Native minimized-caption policy and top-bar work-area reservation apply during managed takeover; preview leaves the Explorer desktop settings alone and hides the top bar while an application is maximized to avoid covering its title controls.
6. Use **this 2.0 folder's** `Restore-Windows-Desktop.bat` or top Nexus menu → Return to Windows to recover. Confirm Windows work area, taskbar and original minimized arrangement return. Do not use an older restore helper against a 2.0 journal.

## Acceptance cases

| Area | Action | Expected result |
| --- | --- | --- |
| Windows Settings | Open from Launchpad, pinned Settings, top Nexus menu and Control Center; visit display, sound, network and notifications | Settings opens without the old Process.Start URI failure. Unsupported pages produce a clear Nexus alert; Control Panel remains separately available. Test both preview and managed takeover. |
| Minimize | In managed mode minimize Files, Notes, Sections, command prompt and another native app | No small caption at the lower-left desktop; the running-window dock entry remains and restores the correct window. Repeat rapid minimize/restore/maximize. |
| Corners | Resize, maximize, restore and snap Files/Notes/Calculator/Sections at 100/150/200% DPI | Native clip and XAML corners agree. Maximized content has no rounded corner or border; no white wedges or clipped controls. |
| Tooltips | Hover the former screenshot location and Files search; press Esc and Ctrl+K | No automatic bare accelerator tooltip. Escape and search shortcuts still work. Ordinary named icon tooltips remain. |
| Glass | Move supported windows over bright/dark wallpaper, change theme, disable Windows transparency, enable high contrast and Reduced Effects | Real desktop blur/tint where supported, consistent text contrast, solid fallback without crashes or transparent holes. |
| Menu bar | Maximize apps, use Nexus/File/View/Window menus, resize the VM, start a fullscreen app | Slim independent top bar; managed apps use space below it. Fullscreen hides both shell overlays. Small screens collapse optional menus. These menus belong to Nexus, not other apps. |
| Launchpad | Click Start; filter categories; search; page through more than 24 apps; navigate with keyboard; open and close while discovery runs | Start opens the grid, separate Search focuses search, only a bounded page appears, late discovery does not reopen a dismissed panel, app activation works. |
| Notifications | Generate a safe unavailable shortcut alert, click the bell/date, dismiss/clear, toggle Quiet Nexus alerts | Badge updates, read entries clear the unread count, alerts remain available while quiet, no repeat-refresh loop. Inbox holds Nexus/session alerts only, capped at 80; it does not capture Windows app toasts. |
| Control Center | Change volume/mute, reconnect audio device, drag brightness where supported, open network/Bluetooth/display, project, lock and open Task Manager | Controls reflect real device support; VM brightness can be unavailable. Links open the corresponding Windows UI. Bluetooth is a settings link, not an invented radio toggle. Quiet mode affects Nexus alerts only. |
| Dock motion | Launch, hover, minimize, restore, trigger auto-hide/reveal and move pointer back during a hide | Short, finite compositor motion without stale actions, stuck opacity or repeated bouncing. Reduced motion skips effects. Fullscreen hides immediately; external apps use their Windows animation. |
| Context menus | Right-click desktop, icons, dock and the Nexus tray icon | No Exit Nexus action. Explicit session recovery remains in the top Nexus menu. No Control Center icon remains in the dock. |
| Recovery | Kill one Files worker, kill Core, terminate the desktop, force host recovery, restart the VM and return to Explorer | Prior foundation/startup acceptance still passes. Saved state survives; original work area and owned minimized arrangement restore. A foreign arrangement change is preserved. |

Use the earlier FOUNDATION-1.7.0.md and STARTUP-1.8.0.md cases as regression checks against the 2.0 build. Include shell-less/native named-pipe identity and Windows cleanup-job cases. Run sustained use with multiple apps; capture logs and resource measurements instead of inferring performance from shorter source tests.

## Implementation boundaries

Liquid glass uses supported Windows desktop acrylic plus translucent gradients. It does not implement Apple's optical refraction or replace an external application's chrome. Nexus animates its own windows and dock; Windows owns other application transitions. This release retains Windows boot, secure sign-in and required Windows services.

The notification inbox is deliberately labelled **Nexus alerts for this session**. Permission/identity-aware Windows toast access and durable history are a separate integration. Network/Bluetooth/night-light cards open real Windows settings; volume/mute and supported brightness are direct controls.

The recovery journal stores minimized metrics before takeover, uses session-only SystemParametersInfo writes and restores the arrangement only if Nexus still owns it. Width and gap changes made during the session survive recovery. A 1.8 journal without this optional field remains readable by 2.0. Retain the journal if any restoration step fails.

## Microsoft references

- [MINIMIZEDMETRICS and ARW_HIDE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-minimizedmetrics)
- [Launching Windows Settings](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)
- [SystemBackdrop lifecycle](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.systembackdrop?view=windows-app-sdk-1.8)
- [Windows system backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops)
- [Keyboard accelerator tooltip placement](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.keyboardacceleratorplacementmode?view=windows-app-sdk-1.8)
