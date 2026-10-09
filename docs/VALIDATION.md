# 1.5.0 validation

The cumulative baseline is the 199-file 1.4.2 GitHub tree at `1cd328f83d28d606fe5e9da7250ee3d792725cd0`. Incremental baselines are the 203-file prepared 1.4.3 and 206-file prepared 1.4.4 sources, verified against their delivered complete Source ZIPs. Earlier working-build reports do not establish acceptance of the new motion/preview features.

Local checks pass for source/XML/XAML content order, resources/assets, publish wiring, event handlers, independent-layer ownership and CI versions. Tree-sitter parses C# source/tests without syntax errors; it does not resolve types or APIs. No global keyboard hook/registration is introduced. Sealed-Border, Thickness and bounded-motion guards remain.

The palette/App.xaml/ShellTheme values are unchanged from 1.4.4. The retained numeric contrast report covers body/muted and action/selection surfaces. Host C#, session/registry recovery, startup/updater implementation, NuGet.Config, global.json and dependency pins remain byte-identical. UI/host and artifact versions advance to 1.5.0.

Each patch is checked with git apply --check --whitespace=error-all, applied in an isolated baseline and compared by every filename/byte to the complete source. ZIP entries/CRC and SHA-256 are checked. PATCH-VERIFICATION.json records delivery evidence separately from repository VERIFICATION.json.

New .NET tests are added for actual pure policy transitions/geometry/preferences and owned-window Win32 maximize/close/DWM behavior; they have not executed in this Linux environment. There is no .NET, Windows or PowerShell runtime here. Native WinUI/host compilation, CA1416/type checks, .NET/Win32 tests, PowerShell and resource/package checks remain pending.

Windows 10/11 and VirtualBox still need the manual motion/preview/focus/frame/memory/drag checks in MOTION-DOCK-1.5.0.md. Live DWM content over the WinUI viewport is specifically unverified. API success cannot establish that a protected app supplies visible contents; some apps can show a blank preview. Static cards and dock context actions remain available. No frame-rate, memory or lag reduction is claimed as measured.
