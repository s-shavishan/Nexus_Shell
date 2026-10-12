# P0 resize and transition correction

Files, utility windows and Sections subscribed their opening/restore animation
to `WindowChrome.Resized`. The chrome emitted that notification for ordinary
noninteractive size changes. `WindowTransition.Restore()` first calls
`Cancel()`, invalidating a pending close, then starts three content animations.
The same routine therefore replayed restore motion during resize and could
suppress a requested close. This coupling is confirmed in source. It is not
proof of the cause of reported black regions or the AppVeyor runtime failure.

All three windows now use `WindowTransition.BindRestore()`. Only native
minimize-to-restore and explicit Nexus restore messages trigger that binding.
The unused resize notification and its extra bounds query are removed. Disposal
detaches the restore handler. Opening, close, dock and actual restore motion
remain available; no settings, privilege, desktop policy or input hook changes
are included.

## Regression coverage

The Windows branch of `Nexus.Core.Checks` sends normal/maximized/minimized size
and explicit Nexus restore messages through production chrome on its own HWND.
It asserts that normal/maximized sizes do not request restore, a minimized
window requests restore once, and explicit restore remains available. These
messages are a native fixture test, not a physical minimize or drag experiment.

`--verify-ui` now includes stage `native-resize-motion-and-close`. Its test-owned
WinUI window uses the production chrome/transition binding, changes native size
three times, then resizes while a real composition close batch is pending. The
counted close callback must complete exactly once within two seconds. It fails
if no pending composition batch was started. The fixture injects no input and
closes no existing user window. It measures callback and layout behavior, not
black pixels, animation smoothness or physical mouse release.

## Windows acceptance

Run the existing build and both Windows suites, then the native preflight:

```powershell
dotnet run --project .\tests\Nexus.Core.Checks\Nexus.Core.Checks.csproj --configuration Release
dotnet run --project .\tests\Nexus.Runtime.Checks\Nexus.Runtime.Checks.csproj --configuration Release
.\scripts\build.ps1 -UseMSBuild
.\scripts\verify-native-ui.ps1 -PublishDirectory .\artifacts\Nexus-Shell-3.6.0-win-x64 -ReportFile .\artifacts\logs\native-ui-interactive.json
```

The build script publishes and runs its native/resource preflight; the two
check executables are separate commands, as in AppVeyor. Use the explicit verifier
for a published build or a controlled interactive VM rerun. If it fails, retain
`native-ui.json`, its adjacent `.log`, and `LastStage`/`FailureDetails`. A compile
pass does not establish that the previous AppVeyor exit-code-1 failure is fixed.

Manually resize Files, a utility window and Sections, including snap/maximize,
restore from the native taskbar and the Nexus dock, resize while closing, and
test animations disabled/reduced effects. Confirm Win+R, Win+D, dock state and
Explorer recovery on the controlled Windows test system. Capture the reported
black regions with the existing ETW/WPR profile before choosing another
material or lifecycle change.

## Validation limits and rollback

The Linux validation environment can compile the C# APIs, run the portable
suites and validate source structure. It cannot execute WinUI/XAML compilation,
native chrome tests, composition batches or DWM pixels. New Windows regression
coverage is included but unexecuted there. Revert this isolated commit to
restore the previous resize/restore binding; user data requires no migration.
