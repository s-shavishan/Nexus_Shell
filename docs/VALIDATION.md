# Validation — 0.7.0

## Completed in this preparation environment

- App/project XML, contiguous XAML collections, unique resource/control names, declared event handlers and finite app/window viewports checked.
- All application/test C# syntax parsed with tree-sitter. This does not check API/types or compile WinUI.
- Every core-project linked source path checked, including the new navigation and shell-experience services.
- Version references and runtime package consistency checked against 0.6.0; dependencies unchanged.
- Both updated SVG design references rasterized and visually inspected. They do not prove native appearance or behavior.
- Both source ZIPs checked, manifest hashes verified, and the patch reconstructed against the complete 0.6.0 source.
- The retained palette contrast values remain unchanged: Pearl 5.49:1, Lagoon 5.62:1, Graphite 6.33:1 minimum among the tested text/background combinations. Native states and complete UI accessibility still need acceptance testing.

## Added to the Windows CI test harness; not executed here

- Navigation bounds, duplicate page refreshes, back/forward endpoints and branch replacement.
- Category isolation, page/profile grouping, window-title search and no-result behavior.
- Recent promotion/deduplication/bounds, current-catalog resolution, stale-target omission and invalid reference rejection.
- Actions/window handles excluded from recent persistence; clear/disable behavior.
- Snapshot isolation, JSON round-trip, earlier-settings defaults and Personalize page resume.

Existing focus/search/workspace/persistence/native/update tests remain wired into CI.

## Still pending

This Linux environment has no Windows toolchain. AppVeyor must compile and run the tests. Native launch, keyboard/focus/material behavior, display/text scaling, high contrast, hotkey/tray behavior and measured CPU/memory also require the Windows checklist.

## Primary implementation references

- [Keyboard accelerators](https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-accelerators)
- [VirtualKeyModifiers](https://learn.microsoft.com/en-us/uwp/api/windows.system.virtualkeymodifiers)
- [AcrylicBrush](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.acrylicbrush?view=windows-app-sdk-1.8)
- [WinUI native switch resource discussion](https://github.com/microsoft/microsoft-ui-xaml/issues/7225)
