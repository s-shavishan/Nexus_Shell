# P0 AppVeyor native preflight failure

The supplied log is for main `f85777c436f050e020aa07361990d126fb5b98e3`, AppVeyor build [1.0.35 / 54877755](https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54877755).

## What the evidence establishes

- Both Windows check executables passed, including the production NCCALCSIZE/native-frame fixture, native HWND/minimize/restore/event tests, named-pipe transport and job cleanup.
- The Shell XAML/MSBuild publish succeeded with **0 warnings and 0 errors**. DesktopHost and Core also published. Eight production templates were exported.
- `verify-native-ui.ps1` subsequently reported an exit-code-1 WinUI preflight failure. The pasted log contains no exception or failure stage.
- The failed AppVeyor job published no artifacts. Its native UI JSON/log cannot be recovered through the public artifact list.

This is a runtime construction/layout failure after compilation. It is not evidence of a C# compilation failure, or successful physical drag/black-region acceptance. The exact native failure remains unknown until the JSON/log is obtained.

## This correction

The verifier prints the actual native report and the last 80 lines of a **probe-specific** log before preserving its failure. AppVeyor uploads those files in `on_failure`, even if packaging was never reached. Native reports include the failing stage, HRESULT and exception stack. The ordinary user's `nexus.log` and settings are not copied to CI artifacts; `--verify-ui`/`--verify-input` route their own logging next to the requested report.

The caption fixture also waits up to five seconds for observed native/XAML layout convergence instead of assuming that a fixed 80 ms sleep proves a completed resize. A timing race is a source-supported risk, not a confirmed cause of this failed build. The probe still fails if geometry does not converge or native caption registration/control exclusion is wrong. No headless bypass or disabled preflight is introduced.

## Rerun and evidence

Rebuild this branch in AppVeyor. If preflight fails, its console now includes the native failure, and the artifacts include:

```text
native-ui.json
native-ui.json.log
```

The files originate under `artifacts/logs/`. Use `FailureDetails`, `LastStage` and the log's native restricted error/HRESULT to select the smallest production fix. Do not declare the underlying runtime error fixed merely because this diagnostic patch compiles.

For an existing published build in a Windows VM:

```powershell
.\scripts\verify-native-ui.ps1 -PublishDirectory .\artifacts\Nexus-Shell-3.6.0-win-x64 -ReportFile .\artifacts\logs\native-ui-interactive.json
```

The native input experiment is a separate explicit opt-in command and is not executed by this preflight or automatically by CI.

## Validation and rollback

`scripts/test-native-ui-diagnostics.ps1` runs the production verifier with a mock failing process and real fixture files. It checks that the failure is preserved, stage/HRESULT/log are printed, and artifacts remain. This does not execute WinUI. Portable source/API compilation, existing regressions and source structure checks are separate checks. A fresh native preflight pass remains pending.

Revert the diagnostic/layout commit to restore the previous verifier and fixed-delay caption test. No user-data conversion, privilege, shell policy, dock-state or native-shortcut change is involved.
