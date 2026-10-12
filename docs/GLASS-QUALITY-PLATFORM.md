# Reversible glass quality and native fallback

Nexus now exposes Ultra, Balanced, Essential and Solid in Sections > Personalize
and Control Center > Desktop. The requested quality travels through Core-owned
state, Control Center preferences and isolated Files appearance reads. A
temporary system override changes the effective material without saving over
the user's request. The surface status reports both values and the reason.

Base: `bd0c53a305920dc3854da3c6bedb270b3ce694d2` (merged PR #19).
This is the first quality-platform milestone from the engineering handoff.
It uses the existing native acrylic, cached paint and retained rim. Shader
refraction, ripple, new shadows, sustained-jank detection and a GPU performance
budget remain later milestones; selecting Ultra does not imply those exist.

## Repository verification

This is Nexus Shell 3.6.0, C#/WinUI 3, with separate Core, Shell, DesktopHost,
Files and Control Center ownership. `global.json` requests .NET SDK 8.0.100
with `latestFeature` roll-forward. Shell targets
`net8.0-windows10.0.19041.0`, x64/win-x64, unpackaged and self-contained, using
Windows App SDK 1.8.260803003. The active Windows build route is `appveyor.yml`;
the GitHub Actions workflow removed from main was not restored.

PR #17's merged tree matches the previously tested retained-motion source.
PR #18 is merged. PR #19 adds bounded DWM thumbnail retries, dock-only relayout
gating and unchanged clock/motion guards; this branch rebases onto those changes
without modifying them. Native caption/non-client input, Start-key routing,
appbar reservations, Explorer recovery and the `asInvoker` manifest are retained.

## Implemented tiers

| Selected tier on a healthy system | Material in this milestone |
|---|---|
| Ultra | Existing native acrylic and cached material look, retained one-pixel rim at 0.85 opacity; existing shell motion remains eligible. |
| Balanced (default) | Existing native acrylic and cached material look, retained rim at 0.65 opacity; existing shell motion remains eligible. |
| Essential | Opaque palette base with inexpensive cached layers; no native backdrop or rim; quality disables custom shell motion. |
| Solid | Opaque adaptive base, simpler palette paint, no native backdrop or rim; quality disables custom shell motion. |

Balanced/Clear/Frosted remain three independent **material looks**. Quality
edits preserve that choice. Windows animation settings and the existing
Interface animations preference still gate motion independently.

High contrast, Reduced effects, Windows transparency off and explicit Nexus
glass-off select Solid material. Unsupported native backdrop, attachment failure
or a reported Windows backdrop fallback also produce Solid. Remote sessions and
energy saver select Essential when native acrylic is otherwise supported.
The quality policy is pure and does not mutate state.

System policy is sampled by existing appearance refreshes using
`DesktopAcrylicController.IsSupported`, `GetSystemMetrics(SM_REMOTESESSION)` and
`GetSystemPowerStatus`. No new timer, screen capture, hardware command or
privilege change is introduced. Native capability-query failure chooses Solid;
unavailable power/session reads retain false for those two hints. Windows'
own backdrop state remains an additional per-window fallback signal.

Each window keeps its existing controller/attachment ownership. Balanced-to-
Ultra changes reuse the controller and rim. Essential/Solid detach the backdrop
and paint an opaque base. A Windows-reported fallback collapses the rim and
keeps a readable base while leaving the controller attached so an Active event
can recover. Attachment failure uses the existing explicit glass-toggle retry.
That per-window fallback changes optical material; the shared theme's motion
gate follows selected quality and system policy.

Old JSON and worker records missing `GlassQuality` default to Balanced.
Unknown persisted values normalize to Balanced without deleting other state.
Typed preference edits and panel-sync values reject invalid quality. The new
enum value is appended, and optional record fields are appended so existing
numeric preference values and older payload defaults remain stable.

## Modified files and reasons

| File | Reason |
|---|---|
| `src/Nexus.Shell/Services/GlassQuality.cs` | Define four choices, normalization and pure requested/effective policy. |
| `src/Nexus.Shell/Models/ShellState.cs` | Persist quality and retain it in independent snapshots. |
| `src/Nexus.Shell/Services/StateStore.cs` | Normalize old/invalid settings without changing unrelated data. |
| `src/Nexus.Runtime/Nexus.Runtime.csproj` | Share production policy types with Core and workers. |
| `src/Nexus.Shell/Nexus.Shell.csproj` | Remove the duplicate compile of the Runtime-linked policy source. |
| `src/Nexus.Runtime/ControlCenterContract.cs` | Add a strictly validated preference and backward-defaulted snapshot field. |
| `src/Nexus.Runtime/ControlCenterCoordinator.cs` | Reject malformed worker-sync quality. |
| `src/Nexus.Runtime/RuntimeProtocol.cs` | Include quality in isolated Files appearance records. |
| `src/Nexus.Runtime/CoreStateRepository.cs` | Publish quality only with the existing ordered durable state commit. |
| `src/Nexus.Shell/Interop/GlassSystemPolicy.cs` | Read native backdrop support, remote-session and energy-saver hints. |
| `src/Nexus.Shell/UI/ShellTheme.cs` | Resolve effective quality, reuse paint, select opaque tiers and gate custom motion. |
| `src/Nexus.Shell/UI/GlassDecorations.cs` | Adjust the retained rim for Ultra; collapse it on palette/fallback tiers. |
| `src/Nexus.Shell/UI/WindowMaterial.cs` | Apply per-window quality/fallback, retain attachment gates and report effective status. |
| `src/Nexus.Shell/Desktop/DesktopEnvironment.cs` | Detect requested/system quality changes in the existing appearance refresh. |
| `src/Nexus.Shell/Desktop/ControlCenterEnvironment.cs` | Apply Core-synchronized quality to the worker's theme. |
| `src/Nexus.Shell/Desktop/FilesEnvironment.cs` | Read and apply quality without changing Files ownership. |
| `src/Nexus.Shell/MainWindow.Appearance.cs` | Apply quality to Sections' existing backdrop and status pipeline. |
| `src/Nexus.Shell/MainWindow.Experience.cs` | Add the Sections personalization selector and preserve synchronization. |
| `src/Nexus.Shell/MainWindow.xaml.cs` | Prevent raw Windows-animation reads from repeatedly re-enabling quality-disabled motion on UI ticks. |
| `src/Nexus.Shell/UI/Controls/QuickSettingsView.cs` | Add the guarded asynchronous Control Center selector and disclose actual material status under accessibility overrides. |
| `src/Nexus.Shell/Services/UiTrace.cs` | Add opt-in ETW event 4 for requested/effective quality, reason and owner PID. |
| `src/Nexus.Shell/UI/UiResourceProbe.cs` | Extend the actual WinUI preflight with four selections, opaque palette bases and backdrop detach checks. |
| `tests/Nexus.Runtime.Checks/GlassQualityChecks.cs` | Exercise the production policy matrix, JSON defaults, malformed edits and real durable save/restart. |
| `tests/Nexus.Runtime.Checks/Program.cs` | Run the quality checks in the existing suite. |
| `tests/Nexus.Runtime.Checks/ControlCenterChecks.cs` | Cover rejection of invalid quality in worker sync. |
| `tests/Nexus.Core.Checks/UiTraceChecks.cs` | Check the new ETW payload and disabled-event path. |
| `docs/GLASS-QUALITY-PLATFORM.md` | Record implemented scope, verification evidence and remaining acceptance. |

ETW event 4 contains role, requested quality, effective quality, reason and PID.
Normal status changes add no synchronous file log. Existing event IDs 1–3 and
their payloads remain unchanged. The Sections pipeline retains its pre-existing
state-change logging; it has not been refactored into WindowMaterial here.

## Validation and limits

After rebasing onto PR #19, the six-project C# reference harness compiled the
actual Runtime, Core, DesktopHost, Core Checks, Runtime Checks and Shell sources.
It used .NET 8.0.419 reference tooling and real WinUI references, with temporary
named-field XAML stubs for Shell. All six compilation exits were zero. Shell's
19 existing reference/stub warnings remain; this is not Windows XAML generation.

The integrated results were:

```text
Nexus.Core.Checks run: exit=0
Nexus.Runtime.Checks run: exit=0
PASS: 4000 glass quality checks; policy matrix, requested/effective separation,
      accessibility, power/RDP fallback, legacy contracts, strict edits and durable state.
PASS: UI ETW schema, owner/geometry/release payload, synchronous work timing/allocations
      and 1000 allocation-free disabled scopes.
Source checks: exit=0
PASS: 151 project files, 141 named elements, 69 handlers, resources, icon, JSON, and build references.
```

The quality matrix covers four choices and every combination of nine policy
flags. Durable tests use a temporary real StateStore/CoreStateRepository and
restart it; they check material-look, note and pin preservation. Existing dock,
caption geometry, attachment/dispatch, state-recovery and worker-lifecycle
regressions also pass. Linux uses `--no-native-ipc`; Everything transport,
actual named pipes, Windows jobs/settings and HWND ownership are explicitly
skipped. The new four-tier WinUI preflight is compiled but has not run locally.

Windows baseline: [AppVeyor 1.0.141](https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54880078)
passed at `8ba177e065c883029c40ab7358e51983b93b477f`, with a passing native UI
report and seven published artifacts. That report used a solid native-backdrop
fallback. It validates the earlier retained-motion/dock changes, not this new
quality source or PR #19. Windows compilation, generated XAML, publish/package
and the extended native preflight must still run on this branch's exact SHA.

Run the unchanged Windows route:

```powershell
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
dotnet run --project .\tests\Nexus.Runtime.Checks\Nexus.Runtime.Checks.csproj --configuration Release
.\scripts\test-update.ps1
.\scripts\test-native-ui-diagnostics.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

No physical mouse-release, native snap/Win+R/Win+D, GPU frame pacing, visible
resize pixels, hardware toggle or sleep/RDP/driver-reset validation was performed
for this change. The user's earlier drag success and rendering improvement are
user observations; no measured GPU improvement is claimed here.

## Risks, acceptance and next milestone

The main regression risks are policy transitions, stale asynchronous preference
replies and worker version mismatch during a partial update. Existing session
epochs, disabled controls, attachment gates and Core's sole writer remain in
place. Use a coherent package and restart workers after upgrading. Switching
Essential/Solid intentionally removes custom shell motion even when Interface
animations remains selected; the stored preference is preserved for recovery.

On Windows, select all four qualities with each of the three material looks,
then change high contrast, transparency and energy saver. Verify that requested
quality stays selected, effective status explains the override and Essential/
Solid detach native backdrops without black frames. Repeat menu/dock/Files/
Sections open, close and resize, mixed-DPI movement, minimize/restore and RDP
transitions. Check native snap and Win+R/Win+D before accepting the phase.

Next smallest milestone: run this branch's Windows preflight and gather a
repeatable menu/dock/live-resize ETW and compositor frame-time baseline on a
real PC. Use that evidence to choose the next rendering change before adding
new optical layers. Sustained-jank downgrading, device-loss acceptance, bounded
shader/shadow/ripple work and compact system flyouts remain unresolved.

Rollback: revert this milestone as a unit. An older reader may ignore/drop the
new optional quality field on a later save; unrelated settings/data remain in
their existing schema. No migration, policy replacement or OS takeover is
performed by this change.
