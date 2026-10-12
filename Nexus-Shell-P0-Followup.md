# Nexus Shell — P0 follow-up and AppVeyor preflight evidence

12 October 2026. Current repository baseline: `f85777c436f050e020aa07361990d126fb5b98e3` (main, Nexus Shell 3.6.0).

**Outcome:** two follow-up changes are committed locally and portable-tested. The supplied AppVeyor log confirms that the previous P0 source compiled successfully on Windows and passed both Windows test suites. Its subsequent WinUI preflight failed without exposing the exception. This delivery improves that preflight's diagnostics/layout wait and adds a separate opt-in native-input acceptance runner. The exact WinUI failure and physical drag/black-region behavior remain unvalidated.

## Source continuity

Remote main `f85777c` has the exact same Git source tree as the earlier P0 final `154d11b`: `7d9769df470a0667ed02cc8699c67bbfe7d1fd90`. The prior patch is already applied; this delivery starts from that updated main, not from the older 3.6.0 base and not from Nexus V5.

Local branch: `codex/p0-interaction-validation`.

| Logical change | Local commit | Diff |
|---|---|---|
| Preflight diagnostics and observed layout wait | `f30e20d` | 8 files, 161 insertions, 5 deletions |
| Explicit native-input acceptance runner | `0647e4c` | 10 files, 473 insertions, 1 deletion |
| Combined | `0647e4c14d31eb396cf655e7bc333524f5dedbc4` | 18 files, 634 insertions, 6 deletions |

Git push could not authenticate. The connected GitHub branch-create operation returned HTTP 403, `Resource not accessible by integration`. No remote branch, PR, merge or release was created. There was no automatic approval-review rejection; this is an integration-access failure. Downloads are the delivery path.

## AppVeyor error diagnosis

Evidence: the attached `Pasted text(1).txt`, verified against [AppVeyor build 54877755 / 1.0.35](https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54877755), job `wnnjo9w7185yutn4`, SHA `f85777c`.

```text
PASS: production NCCALCSIZE pixel preservation and Windows-frame fallback (own native fixture).
PASS: actual named-pipe requests, role rejection, protocol mismatch, frame limits, disconnect cancellation, and 40 concurrent health requests.
PASS: actual Windows job cleanup kills the assigned worker and preserves its external child.
Build succeeded.
    0 Warning(s)
    0 Error(s)
Exported 8 production templates for native WinUI validation.
The native UI check failed with exit code 1. See the build logs and Nexus log.
```

The full log also reports Windows HWND/minimize/restore/event/thumbnail checks and both check executables passing. Audio was explicitly unavailable on that build host; Network had an explicit unsupported-device response. These are supplied CI observations, not tests performed on Shan's PC by this assistant.

The failure occurs **after** Shell XAML/MSBuild publish, DesktopHost/Core publish and template export, when `scripts/verify-native-ui.ps1` runs the published executable's `--verify-ui` mode. No exception, HRESULT or failed phase appears in the supplied log. The public AppVeyor artifact list is empty, so the missing native report could not be retrieved. The underlying runtime cause remains unknown.

Separately, [GitHub Actions run 38190991408](https://github.com/s-shavishan/Nexus_Shell/actions/runs/38190991408) failed before starting its job. Its annotation states that the account is locked due to a billing issue. That is separate from the AppVeyor WinUI failure and is not fixed by source changes.

## Change 1: expose the native failure and remove a brittle test wait

| Modified file | Reason |
|---|---|
| `appveyor.yml` | Collect native UI JSON and its isolated log in `on_failure`, even when packaging is never reached |
| `scripts/verify-native-ui.ps1` | Print the failed report and last 80 probe-log lines while preserving failure status |
| `scripts/test-native-ui-diagnostics.ps1` (new) | Execute the production verifier with a mock failing process; check output and retained artifacts |
| `src/Nexus.Shell/Services/Log.cs` | Route only explicit probe modes to `<report>.log`; normal logger behavior remains the same |
| `src/Nexus.Shell/UI/UiResourceProbe.cs` | Report failed stage, HRESULT and exception stack; log native restricted error details |
| `src/Nexus.Shell/UI/NativeProbeLayout.cs` (new) | Wait up to five seconds for measured native/XAML size convergence |
| `src/Nexus.Shell/UI/NativeCaptionProbe.cs` | Replace its fixed 80 ms assumption with that observed-layout wait |
| `docs/APPVEYOR-P0-PREFLIGHT.md` (new) | Record the exact build boundary, missing evidence, rerun and rollback |

The 80 ms timing assumption is a confirmed source weakness, not a proven cause of this failed build. A geometry timeout still fails the check with actual dimensions and DPI. Caption registration, control exclusion and fallback checks remain active. This change does not disable the preflight, force a headless skip or turn failed native execution into a pass.

Validation of the isolated first commit: six actual C# source/API compilations, both portable check executables and source validation passed. The PowerShell fixture preserved exit-code-1 failure, printed stage/HRESULT/log and retained evidence. A separate actual `Nexus.Runtime` logger smoke test verified both probe routes, HRESULT output, preservation of an existing normal log/settings and unchanged normal logging.

Risks and unresolved work: native layout can still fail on a particular Windows session; routing captures probe-only exceptions in CI artifacts and requires a writable report directory. A fresh native preflight run has not occurred. Next smallest milestone: rerun AppVeyor, read `FailureDetails`/`LastStage` and the probe log, then fix the specific implicated production path. If the bounded wait itself resolves the failure, retain the native report as evidence rather than assuming the original cause.

## Change 2: real Windows input fixture, explicitly opt-in

| Modified file | Reason |
|---|---|
| `src/Nexus.Shell/Services/InputProbeOptions.cs` (new) | Require explicit consent, reject mixed/unknown/duplicate options, bound cycles 1–500; normalize physical virtual-screen points |
| `src/Nexus.Shell/Interop/InputProbeDriver.cs` (new) | Inject Windows input only for the foreground-owned fixture; observe capture/move state and post-release rectangles; cleanup inserted input |
| `src/Nexus.Shell/UI/UiInteractionProbe.cs` (new) | Own the test window, production caption adapter, click counters, move-size listener and structured report |
| `src/Nexus.Shell/App.xaml.cs` | Dispatch `--verify-input` before normal startup; isolate its unhandled failures |
| `scripts/verify-native-input.ps1` (new) | Require `-AllowTestInput`, refuse headless execution/existing reports, apply a bounded process deadline |
| `scripts/build.ps1` | Include the optional wrapper in publish output; do not execute it |
| `tests/Nexus.Core.Checks/InputProbeChecks.cs` (new) | 350 portable assertions for consent, options, bounds, negative monitor origins and real x64 interop layouts |
| `tests/Nexus.Core.Checks/Nexus.Core.Checks.csproj` | Compile actual production options/driver into the suite |
| `tests/Nexus.Core.Checks/Program.cs` | Invoke the portable assertions; they do not inject input |
| `docs/NATIVE-INPUT-PROBE.md` (new) | Explain explicit invocation, observations, safeguards and limits |

Cycles rotate through caption click, held drag, diagonal drag, Escape cancellation, client control click and search-control click. Windows move-loop release is driven on a worker so an occupied UI thread cannot prevent the test from releasing its inserted button. The fixture checks actual GUI capture/move state within 250 ms, unchanged final bounds after additional cursor movement, balanced production move-size events and correct client click delivery. Cleanup cancels only its own foreground move loop. No global hooks, other-app attachment, privilege changes or OS shell operations are added.

Run only after native publish/passive preflight succeeds, in a controlled Windows VM with idle mouse/keyboard:

```powershell
.\scripts\verify-native-input.ps1 -PublishDirectory .\artifacts\Nexus-Shell-3.6.0-win-x64 -ReportFile .\artifacts\logs\native-input-20.json -Cycles 20 -AllowTestInput
# Later, the full automated fixture:
.\scripts\verify-native-input.ps1 -PublishDirectory .\artifacts\Nexus-Shell-3.6.0-win-x64 -ReportFile .\artifacts\logs\native-input-500.json -Cycles 500 -AllowTestInput
```

Validation: all six C# compilations and both portable suites pass; 350 new assertions pass. PowerShell syntax checks pass for the verifier, input wrapper, diagnostic fixture and publish script. Production input-wrapper guards were exercised without any process launch: missing consent was refused and an existing report stayed unchanged. AppVeyor YAML parses with the failure-artifact configuration intact. Git whitespace/source checks pass.

Risks and unresolved work: the native runner has **not** executed here. SendInput interacts with the real input stream; foreground checks cannot eliminate races or user interference. The fixture refuses unsupported sessions/button settings and insufficient monitor space. Its capture deadline may reveal slow/interfered sessions; recorded polling duration is not hardware input latency or frame pacing. A fixture pass would still require physical devices, actual Files/Sections controls, Alt+Tab, Win+R/Win+D, snap, DPI transfer and black-region visual checks. Next smallest milestone: a 20-cycle native smoke, then 500 injected cycles and the manual matrix.

## Validation distinction

| Evidence | Status |
|---|---|
| Previous P0 native Windows compile and both suites | Passed in supplied AppVeyor log at `f85777c` |
| Previous P0 WinUI preflight | Failed; cause unavailable |
| Follow-up six C# compilations / two portable suites | Passed locally; Shell API check uses temporary XAML fields and 19 existing reference/stub warnings |
| Follow-up source checks / PowerShell parse / YAML | Passed locally |
| Verifier failure-output fixture / logger isolation / input-wrapper guards | Passed locally, no native WinUI/input execution |
| New Windows XAML/MSBuild publish and preflight | Pending |
| New 20/500-cycle native input experiment | Pending |
| Actual mouse/snap/DPI/resize video and WPR trace | Pending |

Portable Runtime checks explicitly use `--no-native-ipc`; Windows transport, job and hardware paths remain skipped in the local authoring environment. The supplied AppVeyor log provides separate native evidence for its previous baseline; it is not silently transferred to the new commits. Raw per-stage logs and script-fixture results are included in the archive.

## Apply, rerun and rollback

Apply against current main `f85777c`, using either both individual `git format-patch` files or the combined `Nexus-Shell-P0-Followup.patch`. The archive also includes `Nexus-Shell-AppVeyor-Preflight.patch` if only the first correction is wanted. Do not apply both that first-only patch and the combined patch. Changed source snapshots are for review, not a complete repository.

The package verifies patch application on a separate exact-baseline checkout and checks its resulting Git tree against final HEAD. The README supplies exact commands and checksums. Nothing overlays user settings or a published installation.

Rerun AppVeyor after applying. On a failure, retrieve `native-ui.json` and `native-ui.json.log` from its artifacts or copy the newly printed native failure. Those determine the next source fix. No source-only claim can establish that the unexplained WinUI runtime failure is fixed.

Revert the input-runner commit, then the diagnostic/layout commit, or continue using the previous published build. No dependency/version bump, settings migration, shell registration, Explorer/taskbar replacement, privilege or third-party adapter change is included. M3 glass tiers, M4 compact genuine controls and M5 contextual providers remain gated on P0 stability.
