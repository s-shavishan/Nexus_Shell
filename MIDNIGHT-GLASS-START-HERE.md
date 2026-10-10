# Midnight Glass 1.6.0

The redesign is implemented in the native shell source. Start with [README](README.md), then [the Windows acceptance checklist](docs/MIDNIGHT-GLASS-1.6.0.md).

Use **Sections → Personalize → Midnight Glass** to select the reference palette. Use Full for Windows-managed material where available, Balanced for cached wallpaper without glass, and Fast for the reduced-effects fallback.

Desktop menu → File opens Files, Notes and Calculator. The dock and launcher expose the same native windows. Ctrl+K on the focused desktop opens search. Files adds Alt+Left/Right, Ctrl+L, Ctrl+F and F5. Native Win+R, Win+D and Alt+Tab continue to belong to Windows.

The image reference guided geometry, colors, typography and composition. The shell does not draw a nonfunctional image over the desktop. Existing external applications keep their own window UI.

Checks completed: portable core regression suite, C# API/type checks against pinned WinUI/Windows SDK projections, source/resource/template validation and whitespace validation. Native XAML build, packaging and Windows runtime/visual acceptance remain pending.
