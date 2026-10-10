# Start here — Nexus 1.7.0 foundation

1. Use the complete Source ZIP or apply the matching patch from SOURCE-PATCH.md. These are source deliveries, not executable Windows releases.
2. Review and push the source through your usual GitHub workflow. Run **Build Nexus for Windows** in GitHub Actions, or use the checked-in AppVeyor configuration. Both build routes run the portable and native runtime checks before packaging.
3. On a Windows development machine, run the commands in README.md. Extract the resulting full `Nexus-Shell-1.7.0-win-x64.zip` into a separate folder and keep all three executables plus their supporting files together.
4. Exit the old Nexus. Start `Nexus.Shell.exe` as a preview alongside Windows before trying `Launch-Nexus-Desktop.bat` for a temporary desktop session.
5. Complete docs/FOUNDATION-1.7.0.md, then the Midnight Glass and existing desktop recovery acceptance checks. Windows launch, pipe security, process cleanup, dock interaction, and sustained performance are still pending.

This milestone adds Core, isolated Files, durable settings, and bounded recovery. Boot branding, custom sign-in, Windows service reductions, and durable file-operation jobs come later.
