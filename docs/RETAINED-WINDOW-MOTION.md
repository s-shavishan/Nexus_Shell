# Retained window and dock motion

Baseline: `99c51824f3e0e4a98f26a67eacb1d6b1fd43a443` (native C#/WinUI 3 Nexus Shell).

## Source evidence and change

`UI/WindowTransition.cs` previously created a cubic easing function and three
keyframe animations inside every animated restore, close or dock hide. These
four native objects were disposed after each operation. Native completion and
generation checks already kept window actions independent from resize.

The transition now lazily retains a separate immutable animation set for
restore, minimize and close. Each set belongs to one transition and its original
content visual; no compositor or animation is shared across windows. Center
points still follow the current geometry. Timing, easing, scale, translation and
opacity values remain the same.

Scoped completion batches remain local to each exit operation. Restore still
invalidates a pending hide, resize still does not cancel close, and native
actions retain their 220 ms fallback deadline. A failed current operation drops
its cached resources so a later action can rebuild them. An obsolete operation
cannot release a newer operation's resources. Partial construction and repeated
disposal release owned resources safely.

Resources are lazy: disabled motion and unloaded surfaces do not allocate an
animation set. A transition holds at most three sets (twelve native animation/
easing objects); ordinary windows use restore/close and the dock uses
restore/minimize. All are released when the owning transition is disposed.
This trades bounded idle retention for fewer native-object constructions during
repeated interactions. It does not establish a frame-rate improvement or a
confirmed cause of black resize regions.

## Modified files

| File | Reason |
|---|---|
| `src/Nexus.Shell/UI/WindowTransition.cs` | Retain per-surface animations; clean up on failure/disposal; expose native preflight creation/ownership counts. |
| `src/Nexus.Shell/UI/NativeWindowMotionProbe.cs` | Exercise production resources on an actual test-owned WinUI visual; preserve the resize/close regression. |
| `docs/RETAINED-WINDOW-MOTION.md` | Record evidence, validation, risks and rollback. |
| `scripts/validate-source.py` | Validate the active AppVeyor route after main removed the optional GitHub Actions workflow; retain native-check and artifact-version guards. |

Main removed `.github/workflows/build-windows.yml` in `99c5182`. The integrated
source check initially failed with `FileNotFoundError` because its CI checks
still required that deleted file. AppVeyor now remains mandatory; a GitHub
workflow, if present, still receives its original build, packaging, permissions,
runner, artifact-version and native-test checks.

## Validation

The existing native UI preflight now checks:

- Three native resizes emit no restore request; resize during close preserves
  its single completion.
- Disabled motion creates no animation sets.
- Twenty window restores reuse one actual native animation set; two close
  operations reuse a separate set.
- Twenty dock restores and repeated hides use only restore/minimize sets.
- Five restore interruptions suppress pending hide actions; three completed
  hides execute exactly three actions.
- Disposal during a pending hide prevents a late action and clears retained
  resources. Repeated disposal does not recreate resources.

Fixture actions only increment counters. The fixture never starts a desktop,
replaces Explorer, injects input or changes user settings.

Local validation uses the six-project C# reference compilation harness, Core
and Runtime suites and `scripts/validate-source.py`. The Shell reference check
uses temporary XAML fields; it is not a Windows XAML build. Native HWND/WinUI,
named-pipe and hardware-dependent checks require Windows and must be reported
separately. Run the standard AppVeyor build/package/native UI preflight before
marking this change validated on Windows.

Opt-in ETW event 3 adds stage `motion-resources-create` with role `Window` or
`Dock`. It times construction only, using the existing `Nexus-Shell-UI` provider
and recording no titles or paths. Disabled tracing adds no timers or disk logs.
Use the WPR commands in [render diagnostics](RENDER-RESPONSIVENESS.md).

## Remaining acceptance and risks

Check repeated floating-dock hide/reveal, ordinary window minimize/restore,
close during resize, reduced motion/high contrast, shutdown during animation,
sleep/resume and display/DPI changes on Windows. Compare ETW counts/allocations
and real frame pacing on identical workloads. Headless acrylic fallback and
native construction checks do not certify GPU performance or visible pixels.

The main regression risks are retaining resources beyond window close,
replaying cancelled native actions, and a composition failure affecting a later
action; the new native fixture covers ownership/cancellation and the failure
path clears resources for a later retry. Device failure and recovery still need
real Windows acceptance.

Next smallest milestone: measure live resize/dock/menu frame pacing, then
implement effective glass quality and fallback policy from the handoff using
that evidence. Existing Balanced/Clear/Frosted looks are not the four proposed
quality tiers.

Rollback: revert this change as one unit. No settings schema, policy, privileges,
native caption registration, appbar/work-area ownership or Explorer recovery is
changed.
