# Nexus Shell P0 follow-up

Apply to current Nexus Shell main `f85777c436f050e020aa07361990d126fb5b98e3`. The previous P0 patch is already in this baseline. Final source: `0647e4c14d31eb396cf655e7bc333524f5dedbc4`. No remote push/PR succeeded.

Read `Nexus-Shell-P0-Followup.md` for the diagnosis, every changed file, tests, risks and next milestones. The AppVeyor build compiled; its subsequent native UI preflight failed without the exception. The new diagnostic code preserves that failure and exposes its stage/HRESULT. The 80 ms caption-test wait becomes an observed-layout wait. The input experiment is separate and opt-in.

## Recommended: apply both logical commits

From a clean clone at the baseline:

```powershell
git switch -c review/nexus-p0-followup f85777c436f050e020aa07361990d126fb5b98e3
git am C:\path\Nexus-Shell-P0-Followup\patches\0001-test-winui-expose-preflight-failures-and-wait-for-na.patch
git am C:\path\Nexus-Shell-P0-Followup\patches\0002-test-input-add-explicit-native-drag-release-acceptan.patch
```

Or use `git apply --check` then `git apply` with `Nexus-Shell-P0-Followup.patch`. That leaves changes uncommitted. Use only one method.

If only the immediate AppVeyor diagnostic/layout correction is wanted, use `Nexus-Shell-AppVeyor-Preflight.patch` against the baseline instead. Do not then apply the combined patch over it. The individual second `git am` patch can be used after the first logical change.

`changed-source/` contains the 18 final changed files for review or a carefully reviewed overlay onto the matching baseline. It is not a complete repository. Source metadata/checksums are in `manifest.json`. The packager applied the combined patch on an isolated baseline worktree and proved its tree equals final HEAD.

## Rebuild

Use the existing AppVeyor build. If preflight still fails, copy the printed failure or download `native-ui.json` and `native-ui.json.log`. This evidence is required to identify the underlying runtime error; it has not been confirmed fixed here. No failed preflight is bypassed.

The native-input wrapper requires explicit `-AllowTestInput` and is never run automatically by build/CI/startup. First run 20 cycles on a controlled interactive Windows VM, then 500 only after smoke checks. See `changed-source/docs/NATIVE-INPUT-PROBE.md`.

## Validation limits

Both portable suites, all six C# compilations, 350 new input-probe assertions, logger isolation, production PowerShell diagnostic failure handling, wrapper guards and source checks pass. Shell API compilation uses temporary XAML fields and existing warnings. New Windows XAML build, WinUI preflight, injected/physical input and black-region visuals have not executed here. Baseline native passes are supplied AppVeyor evidence, not acceptance of these new commits.

## Rollback

Revert your input-runner commit, then your diagnostic/layout commit. If the combined patch was applied uncommitted and no further changes were made, use `git apply --reverse --check` then `git apply --reverse` with that same patch. No settings migration, privilege or forced OS shell change is included.
