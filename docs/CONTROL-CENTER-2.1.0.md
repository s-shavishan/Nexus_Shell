# Nexus 2.1.0 — Windows VM acceptance

Build and package the complete source with README.md. This delivery contains source, not a runnable Windows build. Close the old Nexus, snapshot the VM, and extract the complete compiled 2.1 folder separately. Keep Core, Runtime, DesktopHost, Shell and UI resources from the same build together. Start in preview, then repeat the relevant checks in supervised desktop mode without Explorer providing the desktop.

## Findings from the uploaded logs

Windows 10 build 19045, Nexus 2.0.0. Five unhandled UI exceptions name the invalid backdrop target (`E_INVALIDARG`, `0x80070057`); the host records five matching `0xC000027B` exits. They occur in the default backdrop-configuration callback. Six Windows Settings URI activations fail. Core starts and loads revisions through 26; the current settings and backup parse successfully at revisions 26 and 25. Both uploaded session journals are valid format 1. These files do not establish settings corruption or prove that Explorer absence caused the external Settings failure. No uploaded file was changed.

2.1 removes the failing native target lookup/base callback from configuration updates, contains material lifecycle/cleanup errors, and maps everyday settings into Core-backed Nexus controls. It does not repair the Windows Settings installation.

## Control Center tests

| Area | Actions | Expected result |
| --- | --- | --- |
| Settings entry | Open Settings from Launchpad, network from the menu bar, and Nexus Settings from the desktop menu; repeat after restart | Nexus Control Center opens the corresponding section; supported routes do not launch Explorer or Windows Settings |
| Sound | Play audio, adjust master and application sliders, mute/unmute; drag rapidly and close before debounce | Actual levels change and the final slider intent is retained. Stopped sessions disappear on refresh. A changed output requires a fresh device snapshot |
| Wi-Fi | With a real/passed-through adapter, scan, turn software radio off/on, connect/disconnect a saved profile | State comes from Windows APIs; connection acceptance is labeled “requested” until a later snapshot confirms it |
| New Wi-Fi | Select a visible WPA2-Personal AES network, enter a password, leave the editor open for more than five seconds, then connect | Polling does not discard the password entry. A per-user manual-connection profile is created without overwriting another profile. The password is absent from Nexus settings and logs |
| Wi-Fi recovery | Try a wrong password, refresh, use the two-click Forget action on the Nexus-created connection, and reconnect | No false “connected” state. Forget only targets Nexus-created per-user profiles. New enterprise/WPA3 authentication remains an explicit advanced control |
| Ethernet | Open Network and select/copy address, gateway and DNS text | Correct current adapter information; missing Wi-Fi does not hide wired status |
| Bluetooth | With a classic Bluetooth adapter, refresh cached devices, explicitly scan, change visibility | Actual classic device/paired state; discoverability is distinct from radio power. Visibility belongs to the current Core lifetime. Pairing, power and BLE remain advanced controls |
| Display | Check resolution/refresh metadata and hardware brightness; move the desktop to another display if available | Device identity is checked before writes. Unsupported VM/laptop/DDC brightness shows a capability message and disables its slider |
| Power | Switch between existing power plans; compare the active plan and battery/AC status | The actual Windows plan changes. Missing battery and unsupported plan APIs do not invent capabilities. Restore the original plan after testing |
| Desktop | Change wallpaper/effects/dock/widgets/clock/quiet alerts; close the panel, wait for saving, restart | Preferences update across desktop surfaces and persist through Core; quiet Nexus alerts remain quiet after restart |
| Advanced controls | Open the explicit Windows link while Control Center is usable | If Windows refuses activation, Nexus reports it and its integrated sections remain usable |

An ordinary VM often exposes Ethernet only and no controllable brightness or Bluetooth/Wi-Fi radio. That is a capability result, not a failed Nexus control. Use USB passthrough or real hardware for the radio and physical-monitor cases. This update adds desktop/service capabilities, not standalone applications.

## Restart and stability tests

1. Run the complete portable/runtime suites on Windows without `--no-native-ipc`. The runtime suite includes read-only native API probes and a foreign-HWND ownership test; it reports unavailable host capabilities explicitly and performs no device writes.
2. Open and close Control Center, notifications, Files, Launchpad and Sections at least 50 times. Switch sections quickly, focus/unfocus windows, toggle glass/reduced effects and Windows high contrast. Exercise the focus/close sequence that produced the invalid-target crashes. No unhandled UI exception or host restart should occur; unavailable glass should use solid surfaces.
3. While idle with no file operation or picker open, end only Nexus.Core in Task Manager. Refresh Control Center. Core should recover with a new epoch. A control using the old epoch must ask for a refresh before writing; it must never replay an uncertain command. Preferences must reload their acknowledged revision.
4. Unplug an audio/radio/monitor device while its controls are open. Pending changes must fail clearly or reflect current state; reconnect and refresh. A slow native call has an eight-second response deadline but can continue inside its driver. Its lane remains busy until actual completion; other sections and desktop health must remain responsive.
5. Check small screens, non-default DPI, a monitor with a negative origin, keyboard navigation, screen-reader labels and reduced motion. Password entry, mute buttons and section navigation must remain reachable; no automatic accelerator popup should cover a control.
6. Complete the existing MAJOR-2.0.0.md, STARTUP-1.8.0.md and FOUNDATION-1.7.0.md regressions: minimized-caption recovery, maximized corners, dock transitions, work areas, fullscreen, Files isolation, supervised startup and independent restoration to Windows. Use the current shipped restore helpers.
7. Use the desktop for at least one hour, including a Windows restart and repeated panel interactions. Capture diagnostics/core/host logs and resource measurements. Portable checks provide no native performance or sustained stability measurement.

Windows remains responsible for drivers, authentication, security, Core Audio, Wi-Fi and Bluetooth services. Nexus Core runs per user with the desktop; it is not a new elevated Windows service and does not disable Windows services.

## API references

- [SystemBackdrop lifecycle](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.systembackdrop?view=windows-app-sdk-1.8)
- [WlanSetInterface](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlansetinterface) and [WlanSetProfile](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlansetprofile)
- [WPA2 profile schema](https://learn.microsoft.com/en-us/windows/win32/nativewifi/wpa2-personal-profile-sample)
- [BluetoothEnableDiscovery](https://learn.microsoft.com/en-us/windows/win32/api/bluetoothapis/nf-bluetoothapis-bluetoothenablediscovery)
- [Power schemes](https://learn.microsoft.com/en-us/windows/win32/power/managing-power-schemes)
