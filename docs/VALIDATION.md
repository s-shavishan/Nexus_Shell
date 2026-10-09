# 1.4.2 validation

Baseline: all **194** source blobs match GitHub main commit **d98e09ff916c16ee1321bbf285a228bc785f9653** (1.4.1). The user reports much smoother dragging in a short 1.4.1 VirtualBox test with Explorer retained. That is user runtime evidence for the baseline, not a native test of 1.4.2 or a complete diagnosis of earlier lag.

The current Python source check passes: 76 required app files, 132 named XAML elements, 69 handlers, resources, assets, publish wiring, layer ownership and release metadata. Host takeover/recovery code, registry/session record logic, NuGet.Config, global.json and direct package references remain unchanged from 1.4.1.

The user-supplied AppVeyor log for **af26198808fbc31f92275847cabfdf461093e35f** confirms that .NET core checks passed, including the 1,008 DesktopUiChecks geometry cases, settings migration, snapshot/JSON persistence and native test-window checks. PowerShell updater checks also passed. Audio-device acceptance was skipped because the CI host had no output device. The UI compile then failed with **CS0509: TaskbarView cannot derive from sealed type Border**, so native publishing and package validation did not finish.

The build fix composes the taskbar's Border inside a Grid and adds a source guard against direct WinUI Border inheritance. All **199** failed-build baseline blobs match the current GitHub tree. The guard rejects the failed source and accepts the corrected source; complete source/resource checks pass locally. The authoring environment has no .NET/Windows/PowerShell runtime, so the corrected UI has not been compiled here.

Packaging verifies the hotfix with git apply --check --whitespace=error-all, applies it to an isolated baseline copy, compares every reconstructed source byte, and checks ZIP entries/CRC and SHA-256. Source/packaging checks cannot establish rounded-window rendering, native resize behavior, popup focus, minimized artifacts or VM frame timing.

CI must execute core/platform checks and PowerShell updater/parser tests, build both host/UI and validate full/small packages. Complete UI-POLISH-1.4.2.md and existing desktop recovery checks on Windows.
