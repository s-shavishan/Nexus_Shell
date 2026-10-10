# Foundation validation evidence — 10 October 2026

## Completed in the source environment

- .NET 8.0.419 Roslyn compilation of Nexus.Runtime, Nexus.Core, Nexus.DesktopHost, and both check projects; nullable warnings treated as errors for these compilations.
- All 15 existing portable core check groups pass.
- 101 new runtime assertions pass: revisions, writer exclusion, failed atomic saves, legacy settings, lost acknowledgement reconciliation, cancellation, 30 simulated picker crashes, readiness/UI-heartbeat deadlines, process bounds, and restart cooldown.
- The production stream connection handler passes framed persistence, role rejection, caller disconnect cancellation, and malformed/truncated frame checks. These tests replace only the OS transport.
- Shell C# type checking against the pinned WinUI/Foundation/InteractiveExperiences assemblies and Windows SDK projections, using temporary generated XAML field declarations.
- Source XML/resources/templates/build-reference checks.
- PowerShell 7.4.13 parses all shipped scripts. Update fixtures pass on Linux with the fixture's Windows platform guard enabled: new Core payload, runtime reuse, hashes, old-folder retention, path canonicalization, and rejected corrupt/invalid manifests.

## Not established by these checks

This environment cannot compile native Windows XAML or run WinUI. Its Linux named-pipe implementation also cannot create the required Unix socket. The runtime checks therefore used the explicit `--no-native-ipc` test option here; Windows CI runs the normal command with no skip.

Actual Windows peer identity/elevation behavior, named-pipe admission tests, job termination and external-app breakaway, native XAML/resource packaging, launch, recovery, dock interaction, sign-out, and sustained performance remain pending.

Direct C# API checking is not a native build. It reports 19 warnings: six projection-reference unification warnings (CS1701/CS1702) and 13 unused fields in the temporary XAML declarations (CS0414). No warnings originate in the application source. This does not validate PRI/XBF resources or XAML binding generation. The check output is recorded in [the source evidence log](FOUNDATION-CHECKS-1.7.0.txt).

See [FOUNDATION-1.7.0.md](FOUNDATION-1.7.0.md) for the native acceptance procedure.
