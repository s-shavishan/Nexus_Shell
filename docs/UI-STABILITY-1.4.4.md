# 1.4.4 — UI improvement and stability

## Reference adaptation

The supplied dashboard has a clear visual hierarchy: dark burgundy surrounding surfaces, vivid warm accents for selected controls, generous rounded panels, restrained outlines and compact rows for detail. Its consistent spacing separates navigation, summary, content and floating actions.

Nexus applies that language to real shell features:

| Reference | Nexus adaptation |
| --- | --- |
| Orange-to-rose actions and selected navigation | Ember gradient, selected page/workspace, primary buttons and active dock entry |
| Tall rounded sidebar | Separate navigation panel with outlined Windows glyphs and actual workspace shortcuts |
| Search capsule | Local Nexus search with Ctrl+K hint; Windows shortcuts remain native |
| Greeting and metrics | Time-aware greeting; actual workspace, saved-resource, open-task and focus counts |
| Recent Files table | Five saved-resource rows with All/Files/Links/Notes filters and collection/type metadata |
| Project spotlight | Current workspace description, configured item counts and existing wallpaper art; opens/configures that workspace |
| Floating dock | Warm shared surface, outlined shell actions, retained running entries and active/minimized indicators |

Saved rows prioritize the current workspace and favorites. No file timestamps, progress percentages, people or services are invented. Existing Apps, Explore, Study, Activity and PC control routes remain available. Smaller windows collapse summary columns, stack cards and hide the large search capsule while keeping compact search/navigation available. Workspace shortcuts remain available through the Workspaces route if the sidebar collapses.

Choose **Personalize → Ember** for this appearance. Other moods use the revised layout with their palettes. Existing preferences are preserved.

## Stability and performance

- Shared surface gradients are cached per palette. Accent resource brushes update color stops in place. Window events and clock ticks do not allocate new surface gradients.
- Windows animation preference changes are recognized by the theme. Motion remains finite and respects accessibility/reduced-effects settings.
- Dock context-menu membership uses a set, so repeated notifications cannot imbalance a counter. Removing an app dismisses its menu and removes its interaction hold.
- Removed dock buttons explicitly detach motion handlers. Newly loaded buttons reattach once.
- Saved rows reuse their view while displayed records and palette remain unchanged. Responsive grid definitions rebuild only when row/column counts change.
- Workspace creation checks the eight-workspace limit before editing, avoiding a saved-success message for an item discarded by normalization.

The 1.4.3 window observer, fixed coalescing interval, periodic reconciliation, HWND/process validation, stable order, auto-hide and zero floating reservation remain. Host takeover, registry backup, recovery and dependency pins are preserved. No Explorer/service shutdown is added. Desktop, dock and menus remain independent native windows.

## Verification

Local checks cover XML/XAML content order, resources/assets, event handlers, layer ownership, versions, C# syntax and numerical palette/gradient contrast. Syntax parsing does not resolve WinUI types/APIs. Existing .NET checks are extended to sample text contrast across action gradients; those checks are not executable in this Linux editing environment.

Both patches must apply to exact isolated baselines and reconstruct every filename/byte of the full source tree. Archive entries/CRC and SHA-256 are checked. Native WinUI/host compilation, .NET/Win32 tests, PowerShell packaging and Windows/VirtualBox acceptance remain pending.

## Windows acceptance after CI

1. Choose Ember and then every other mood. Check selected page/workspace, filters, buttons, Start, Quick Settings, Files and dock together. Enable high contrast and disable Windows animations. Repeat in Fast and Balanced.
2. Test 1920×1080 and 1024×768 at 100%, 150% and 200% scale. Resize Sections and scroll the sidebar; verify single-column cards, wrapped labels and reachable compact navigation. Inspect the full desktop for non-client outlines.
3. Save a file, folder, link and note in Explore. Verify filters, empty states, counts and row opening. Create/configure/cancel workspaces, including the eight-item limit. Restart and confirm preferences/content survive.
4. Minimize/restore external apps and Nexus Sections/Files. Close an app while its dock context menu is open; verify dismissal, button removal and auto-hide. Repeat many launch/close cycles and inspect memory growth.
5. Maximize an app, reveal the floating dock at the bottom edge and open a menu. Verify dock visibility through interaction and hiding after dismissal. Check fullscreen, overflow, drag responsiveness and native Win+R/Win+D.
6. Exit Nexus and verify the Windows desktop/taskbar return. Retain NEXUS-DESKTOP-MODE.md recovery and DOCK-RELIABILITY-1.4.3.md dock checks.

OS minimize animation still uses its native destination. Primary-display scope and incomplete tray/Jump List/extension replacement remain documented limitations.
