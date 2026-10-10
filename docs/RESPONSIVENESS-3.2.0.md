# Nexus 3.2.0 — Responsive desktop controls

This update continues from the delivered 3.1.0 source. The VM screenshots show a broad gray Launchpad and a blank native popup; the user reports general UI delay while many integrations work. A screenshot does not identify a GPU or compositor fault. Native timing and glass acceptance remain necessary.

## Implemented behavior

| Surface | Behavior |
| --- | --- |
| Menu bar | Network, Sound, Bluetooth and Display open the corresponding compact Nexus controls. The main Control Center icon opens/expands the full panel. Narrow bars retain the full Control Center entry when individual icons collapse. |
| Compact controls | A section heading and focused content replace the full navigation grid. All controls expands the same supervised worker. Escape, outside activation and close dismiss immediately. |
| Launchpad | One grid and embedded search serve all app entry points. Separate Search buttons and the smaller alternate layout are removed. Legacy app-search dispatches focus this same Launchpad. The managed desktop owns the standalone Windows key; native Windows combinations still pass through. |
| Recent apps | Up to 12 successful Nexus launch requests are stored newest first, deduplicated by target, with a Recent category. This is Nexus launch history, not whole-PC activity. The existing Remember recent apps and search items preference opts out and clears it. |
| Installed-app refresh | The refresh action reloads the existing bounded Start-menu shortcut catalogue and retries failed icon requests. Packaged-app-only discovery remains outside this catalogue. |
| App icons | Launchpad and dock pins initially use cached Nexus vectors. Eligible local shortcut/executable thumbnails load asynchronously, with four slots, a three-second request budget, a 128-entry cache and bounded decode size. Direct network paths and mapped network drives are excluded. Unsupported/failed thumbnails keep the vector. |
| Effects | Interface animations is independent of Glass surfaces. Turn animations off for immediate interaction while retaining glass. Reduced effects remains the combined solid/low-effects mode. High contrast and Windows preferences remain authoritative. |

## Responsiveness and stability

Previously, transient panels faded their entire XAML root to zero and waited for a compositor completion or timeout before hiding the native window. This could expose a plain HWND background. Launchpad, Control Center and notifications now hide immediately. Entrance motion animates the content inside the frame: the frame itself stays painted. Entrance is 110–140 ms, with small movement and near-full initial opacity. Content windows issue native minimize immediately; Windows owns the minimize animation. Other bounded content transitions keep their surrounding frame visible.

Launchpad prepares its cached grid before activation, is constructed on the UI queue at low priority after desktop startup, and begins catalogue discovery in the background. Equal page contents retain the existing tiles, so a cached catalogue result does not create another full grid. Shared vector sources avoid repeated SVG instances; completing a native icon updates only that tile. A 45 ms filter coalescing timer reduces repeated rebuilds while typing; Enter and arrow navigation flush the latest query immediately. The brief warm-up has a memory/startup tradeoff that needs measurement on Windows.

The Control Center worker used 250–750 ms timer polling for desktop state. It now waits for an authenticated Core revision notification. One wait per worker is admitted; stale revisions return immediately, cancellation/disconnect removes the wait, shutdown wakes it, and a one-second idle response preserves the UI heartbeat. This does not replay hardware writes. Desktop state changes that occur during an in-flight sync schedule the latest state immediately afterward. Device polling still occurs only while the surface is open. A reopened section can retain its last displayed controls while its fresh device read runs.

## Glass behavior

The acrylic target now ensures the Windows system dispatcher, uses explicit Nexus theme/configuration, sets the palette before attachment, checks attachment success and selects Thin acrylic. Owner palette changes update its recipe only when it changes. Configuration/detach callbacks avoid the old native default-configuration lookup. Changing the glass preference or a blocking accessibility/system setting permits another attachment attempt.

Surface gradients keep the highlight in a narrow top band instead of spreading a pale wash through the entire panel. Light themes have darker secondary text. Every native fallback uses the matching opaque palette color. Control Center → Desktop shows whether Nexus glass, reduced effects or Windows transparency is blocking the effect.

This is Windows acrylic blur/tint and Nexus paint, not an optical-refraction renderer. Windows can replace acrylic with a solid color when transparency, graphics, accessibility or power policy blocks it. See Microsoft's [system backdrop controller guidance](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/system-backdrop-controller) and [acrylic adaptation guidance](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic). Live blur and GPU cost have not been measured here.

## Validation

The portable Core suite passes, including 166 new responsive UI assertions and the earlier 32 UI lifecycle, 66 Start-key, 31 startup, 1,008 placement and 118 desktop assertions. The new checks exercise real atomic persistence of recent history, privacy opt-out, bounded icon candidates/paint values and compact panel bounds across monitors/scales.

Runtime checks pass, including 18 new revision-wait assertions, the earlier 121 Control Center checks, 98 settings checks and 101 existing runtime assertions/30 simulated Files crashes. The new cases cover a pending wait awakened by section, preference and hide changes; missed revisions; cancellation; duplicate waits; unauthorized peers; idle heartbeat; shutdown and independent motion preferences. Production stream framing/disconnect checks pass with the OS transport replaced.

Source/API compilation passes for Shell and Core against the Windows/WinUI references. Shell uses temporary XAML field stubs for this check; it reports zero errors and 19 reference/stub warnings. Runtime, DesktopHost and both check programs also compile. The static validator passes 123 required files, 140 named XAML elements and 70 handlers. The actual PowerShell parser accepts all shipped scripts. Synthetic update fixtures pass folder preservation, runtime compatibility, hash and path rejection, and new app/resource payload checks. Git whitespace checks pass.

Native Windows XAML generation/build, named pipes/peer verification, thumbnail decoding, rendered blur, hardware behavior and sustained timing/resource measurements cannot be exercised in this Linux environment. The update fixtures run against synthetic payloads with the Windows-platform environment flag; they are not a native installation test. These checks do not establish native performance parity.

## Windows 10 VM acceptance

1. Snapshot the VM and retain your previous compiled folder. Build/package the full 3.2.0 source following README.md. Source ZIPs and patches are not runnable binaries; all components must be from this release because the panel worker uses a new revision-wait operation.
2. Test both preview and managed desktop entry points. Open Launchpad repeatedly, type rapidly, press Enter immediately after a keystroke, change pages/categories, and dismiss/reopen while catalogue/icon requests are pending. Check for blank panels, stale results and focus jumps. Check that there is one Launchpad entry with an embedded search.
3. Click each menu-bar icon: Network must show Network, Sound must show Sound, and Bluetooth/Display must show their sections. Click All controls, then reopen another direct section. Test Escape, outside click, same-icon dismissal and rapid switching. The worker PID should stay the same after its first opening.
4. Maximize/snap an app and open direct controls. Menu-bar attachment and dock visibility must retain the underlying app state. Return to the desktop, then test full-screen video, scale changes, a monitor to the left/above and monitor removal.
5. Open an app from Launchpad or a dock pin. Check Recent ordering, reopening without duplicates, restart persistence and the history opt-out. Install/create a test shortcut in the Start menu, refresh and verify its presence. Remove it and refresh again.
6. Verify actual Windows icons for local apps/shortcuts. Test a missing file, a slow/removable drive and direct network targets: failed icons must leave a usable vector and a responsive grid. Observe memory/handles while paging; retry failed icons with refresh. Thumbnail correctness is unverified here.
7. With Windows transparency enabled, set Glass surfaces on and Reduced effects off. Test Midnight, Solstice and Opal while opening/dismissing panels. Confirm light captions remain readable and the highlight is restrained. Disable Windows transparency or use high contrast: the fallback must match the palette/system colors rather than becoming a black/gray blank panel. Re-enable transparency or toggle Nexus glass off/on to retry attachment.
8. Turn Interface animations off while leaving Glass surfaces on. Verify glass remains enabled, direct panels respond immediately, dock previews remain available and Files/Sections/utility motion follows the preference after propagation. Restore animations and test minimize/maximize/close without a blank native frame.
9. Repeat the existing [3.0 recovery checks](SESSION-CORE-3.0.0.md): terminate only the Control Center worker, interrupt Core, perform rapid hide/reopen and confirm unrelated apps survive. Uncertain device writes must not be replayed. Check settings, pins and recent history after restart.
10. Compare 3.1.0 and 3.2.0 on the same VM using the same wallpaper, effects, app count and graphics settings. Record cold and warm popup timing, idle/open-panel CPU, memory/handles and input response for one hour. Source improvements do not establish native-performance parity.

Sign-in policy and Windows security boundaries are unchanged; continue using [startup inspection](START-AND-SIGNIN-3.2.0.md) for the existing foreign shell-policy conflict.
