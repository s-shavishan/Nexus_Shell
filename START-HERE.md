# Start here — Nexus 3.2.0

1. The Source ZIP contains the complete source, not a compiled executable. Build on Windows with the README commands or use the full compiled Windows CI artifact.
2. Keep your previous build in its own folder. Use the complete Source ZIP, or apply exactly one patch matching your existing 3.1.0, 3.0.0, 2.1.1 or main 1.5.0 source; see SOURCE-PATCH.md. Build/package it before the next step.
3. Extract `Nexus-Shell-3.2.0-win-x64.zip` into a separate build folder and snapshot the VM.
4. Start `Launch-Nexus.bat` in preview, then `Launch-Nexus-Desktop.bat` for the managed session. Follow RESPONSIVENESS-3.2.0.md in the compiled folder (docs/RESPONSIVENESS-3.2.0.md in source).
5. Test direct menu controls, unified Launchpad, recent apps, glass/animation preferences and panel hide/reopen races. Then repeat the existing recovery and work-area checks.
6. Sign-in replacement is separate. Use START-AND-SIGNIN-3.2.0.md and Inspect-Nexus-Startup.bat; unidentified foreign shell policy remains untouched. Keep the matching Restore helpers available.

This is a UI and reliability update on the Session Core foundation. Native Windows acceptance is required before treating it as a stable release.
