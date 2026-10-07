# Smaller Nexus downloads

The build emits a full app ZIP and a separate `Nexus-Shell-0.4.1-Update-win-x64.zip`. The full ZIP carries all self-contained runtimes; the Update ZIP omits their bytes and records the exact runtime files needed from a base folder. CI prints its actual size.

Extract the Update ZIP separately and double-click `Apply-Update.bat`. Choose your old extracted app folder. Before creating anything, the updater checks downloaded payload hashes and reused runtime hashes. It then uses local copies to assemble a fresh version folder and verifies the copied bytes. The original folder is retained. No SDK, global .NET installation, or runtime installer is added.

A runtime mismatch requires the full ZIP once. The updater compares bytes, rather than trusting app version or direct NuGet pins: a build host's .NET servicing version or transitive dependency can change. A prior broken app can supply runtime files if those files are intact and match; its old executable and app resource index are replaced by the update payload in the fresh destination.

If the default destination already exists, use `-TargetDirectory` with an unused path. Existing destinations are rejected. Your settings, pins, notes and board remain in `%LOCALAPPDATA%\WhiteDreams\NexusShell`, separate from both binary folders. If sign-in startup points to the older folder, Control center shows a **Use this version at sign-in** button. It updates the registration only when you select it.

The updater performs local copying, so the new folder still needs disk space comparable to a full app. This reduces network downloads; it is not an automatic online update service. Download archives only from your own successful CI run. Build checks establish file compatibility and integrity; Windows launch/rendering still need testing.

The updater requires Nexus.Shell.exe, Nexus.Shell.dll, and Nexus.resources.json in the new payload. App-owned binaries/assets and loose XBF files cannot be reclassified as old runtime files. The resource report must match the release and every listed app resource must match a new payload record; a root app PRI is mandatory. File records require canonical relative paths, nonnegative lengths, valid SHA-256 values and unique normalized names. These checks detect corrupt/incompatible packages; they are not a publisher signature.

Existing notes, pins, and saved items migrate to the new schema; older saved items become Personal items without favorites. The app attempts one migration backup before first saving older settings. An older binary may rewrite settings without new fields, so retain a current data copy before a rollback.
