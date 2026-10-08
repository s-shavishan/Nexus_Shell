# Nexus Shell 1.1.0 — Solstice UI polish

The release is built from the saved complete 1.0.0 source. Both generated macOS blueprint boards and the sixteen orange component references were inspected, including enlarged input and menu examples. The real 0.9.0 Windows screenshot was used to identify the gap between the previous illustration and the shipped controls. The reference images remain references; their artwork is not copied into the application.

## What makes the references attractive

The effect comes from proportion and consistency as much as transparency. The boards reserve large round corners for windows and small ones for controls. Toolbars have a strong baseline, secondary actions are quieter, and related controls form small groups. Sidebars use a single selected row rather than many competing cards. Separators are thin; hierarchy comes from whitespace, tint, weight and selection.

The orange collection applies the same warm color family to backgrounds, menus, fields and selected segments. Dark menu surfaces, lighter selected controls and pale slider knobs make the layers easy to distinguish. Rounded capsules are used for short groups; larger panels keep readable interior space. That consistency matters more than copying the orange color onto every element.

The generated boards also show coherent default, hover, pressed, focused and disabled states. A single attractive normal state would leave Nexus looking inconsistent as soon as a user types, opens a dropdown, navigates with a keyboard or disables a control.

## Reference-to-implementation mapping

| Reference pattern | Previous gap | Change in 1.1.0 |
|---|---|---|
| Compact native toolbars | Window title and hamburger had little useful structure | 52-DIP chrome with traffic-light actions, history controls, search, capture and navigation |
| Consistent component family | Inputs, menus, switches and sliders used different default Windows colors | Shared palettes also override the native control state brushes |
| Warm layered surfaces | Desktop wallpaper and control surfaces did not share a clear mood | Solstice light and Ember dark themes; matching static vector wallpaper, menu tint and selected states |
| Distinct material levels | One acrylic brush was reused for windows, widgets and dock | Three material strengths with solid fallbacks |
| Finder-like sidebars | Mixed legacy glyphs and loose rows | Original 21-DIP category icons, 34-DIP rows and clear selected states |
| Selected segments | Board/list and search categories resembled unrelated buttons | Compact segments with selected fill, border and keyboard focus |
| Balanced floating dock | Built-in, pinned and running items used different sizes | One icon footprint, active-page marks and running indicators; compact layout resizes the actual child images |
| Quiet labels and hierarchy | Uppercase breadcrumbs and oversized headings competed | Sentence-case location trail, consistent Segoe UI typography and a clearer title/body scale |
| Compact quick panels | Control center required scrolling through full application preferences | Audio and quick tiles first; session preferences collapse into an expander |
| Continuous feedback | Hover scale, pressed state and entrance motion felt unrelated | Finite 90/160/240-ms compositor interactions, smaller entrance scale and restrained dock lift |

## Design values

| Element | Value |
|---|---|
| Desktop widget column | 252 DIP when there is enough room |
| Main panel | Up to 1180 DIP; 22-DIP corners |
| Window toolbar | 52 DIP |
| Sidebar | 176 DIP; 34-DIP navigation rows |
| Common controls | 13-DIP text; generally 32–34-DIP minimum height |
| Page headings | 22–24 DIP in the fixed native workspaces |
| Desktop clock | 60-DIP light text |
| Comfortable dock | 60×64-DIP buttons with 52-DIP artwork |
| Compact dock | 44×48-DIP buttons with 38-DIP artwork |
| Panel / field / segment corners | 22 / 10 / 7 DIP |

Solstice uses warm pearl panels and a burnt-orange action color. Ember uses plum-charcoal panels and pale peach actions. Opal, Pearl, Lagoon and Graphite remain available; each now has its own wallpaper colors rather than the same pastel background with a different opacity. Fresh settings use Solstice. Existing saved mood choices are preserved. Select **Personalize → Solstice** to use the new light direction after updating.

## Interaction and performance

The native WinUI editing, selection, popup and automation behavior remains in use. Custom styles change appearance without replacing text input, slider behavior or keyboard navigation. Dialog primary, secondary and close buttons share the same component family. Native high-contrast resources remain separately defined.

Wallpaper is static vector geometry drawn inside WinUI. Motion changes opacity, translation and scale; it does not animate layout sizes or wait for completion before enabling a page. Hover content moves within a stationary input button. Press and release feedback observes handled native-button events through AddHandler, keeping WinUI click and capture behavior in control. Mouse, touch and pen contacts can trigger feedback. Pointer release, cancellation and unloading reset it; matching retained delegates are removed when the target unloads. Reduced effects and the Windows animation preference still control custom motion.

Palette brushes update when the mood or high-contrast state changes; focusing the window does not rewrite every color. Glass remains optional and falls back when unavailable. These choices reduce avoidable work; actual frame rate and memory still need measurement on Windows.

## What has been verified

The linked core behavior checks compile and execute with .NET 8. All six palette families pass 4.5:1 checks for body and secondary text on representative panels, cards, inputs, sidebars, selected segments and hero surfaces. The selected dark-segment check caught and corrected a faint counter. A follow-up selected-sidebar check found that orange and blue accent labels fell below 4.5:1 in Solstice and Opal. Selected rows now use the readable selected-text color while retaining their tinted selection fill; this raises representative calculated contrast from 3.81 to 9.36 in Solstice and from 3.92 to 9.74 in Opal. These values describe the nominal palette, not rendered acrylic. Real settings-file tests confirm every mood persists without losing notes or compact-dock preferences.

All application C# compiles against the pinned WinUI 1.8.260803003, Windows App SDK projection assemblies and Windows SDK .NET reference assemblies. Temporary declarations substitute for generated XAML fields. The check also compiles the native style setter property names against those APIs. XAML structure, contiguous content, handler wiring, resources, imports, vector assets and C# syntax pass the source validator.

The pinned managed WinUI XAML compiler was also invoked directly. It stops at its Windows kernel32 metadata-file dependency on this Linux host, before validating the XAML. This result does not establish an XAML compile pass.

This is not a full Windows application build. Native XAML compilation, PRI/XBF generation, launch, acrylic appearance, font metrics, DPI scaling, mouse/touch behavior and frame pacing remain Windows acceptance work. The previews read source wallpaper paths, shipped icons and exported runtime palette values, but are **layout illustrations with sample content**, not Windows screenshots.

## Windows visual acceptance

1. Build the exact uploaded 1.1.0 commit through AppVeyor; install its Update ZIP into a fresh version folder.
2. Select Solstice, then Ember. Open Explore, Study, Apps, PC controls, Personalize, search and a dialog.
3. Inspect default, hover, pressed, focused and disabled controls. Text fields must keep readable text when focused. Dropdowns and menus must follow the selected mood.
4. Compare at 100%, 125% and 150% display scaling; resize from a small window to full screen. Check the toolbar, sidebar, sliders, dock and vertical scrolling.
5. Tab through the controls and use history, search, note capture and the existing keyboard shortcuts. Return to other Windows apps through the running dock.
6. Toggle glass, reduced effects and Windows high contrast. Check opaque fallbacks and rapid pointer movement at dock edges.
7. Confirm audio, app mixer, window arrangement/Undo, notes, focus sessions and saved preferences still work. Measure responsiveness and resource usage on the real PC.

Repository handoff and the confirmed 1.0.0 baseline are recorded in BUILD-HANDOFF.md.

Microsoft implementation references: [in-app acrylic](https://learn.microsoft.com/windows/apps/develop/ui/in-app-acrylic) and [XAML/Composition interoperability](https://learn.microsoft.com/windows/apps/develop/composition/xaml-comp-interop). Press event behavior was checked against Microsoft documentation for [PointerPressed](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.pointerpressed?view=windows-app-sdk-1.8) and [AddHandler](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.addhandler?view=windows-app-sdk-1.8). Native control resource names were checked against the Generic.xaml shipped in the pinned WinUI package.
