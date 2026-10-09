# 1.4.2 validation

Baseline: all **194** source blobs match GitHub main commit **d98e09ff916c16ee1321bbf285a228bc785f9653** (1.4.1). The user reports much smoother dragging in a short 1.4.1 VirtualBox test with Explorer retained. That is user runtime evidence for the baseline, not a native test of 1.4.2 or a complete diagnosis of earlier lag.

The current Python source check passes: 76 required app files, 132 named XAML elements, 69 handlers, resources, assets, publish wiring, layer ownership and release metadata. Host takeover/recovery code, registry/session record logic, NuGet.Config, global.json and direct package references remain unchanged from 1.4.1.

DesktopUiChecks adds 1,008 geometry cases, settings migration and snapshot/JSON persistence checks. The authoring environment has no .NET/Windows/PowerShell runtime. New C# test execution, type/analyzer checks, PowerShell checks, WinUI XBF/PRI generation and native execution remain pending. Historical 1.4.0 API/test logs do not validate this update.

Packaging verifies both source patches with git apply --check --whitespace=error-all, applies them to isolated baseline copies, compares every reconstructed source byte, and checks ZIP entries/CRC and SHA-256. Source/packaging checks cannot establish rounded-window rendering, native resize behavior, popup focus, minimized artifacts or VM frame timing.

CI must execute core/platform checks and PowerShell updater/parser tests, build both host/UI and validate full/small packages. Complete UI-POLISH-1.4.2.md and existing desktop recovery checks on Windows.
