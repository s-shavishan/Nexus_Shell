# Start here — Nexus 1.8.0 startup

1. Use the complete Source ZIP or the matching patch in SOURCE-PATCH.md. These are source deliveries and require a Windows build.
2. Run the Windows CI pipeline. It checks startup policy, Core persistence, Files recovery, real IPC/job behavior, updates, and native publishing.
3. Extract the full `Nexus-Shell-1.8.0-win-x64.zip` into a separate folder. Exit the older Nexus and start `Nexus.Shell.exe` in preview.
4. Follow docs/STARTUP-1.8.0.md. Test the readiness screen and temporary desktop before enabling automatic startup.
5. If an older Nexus shell is selected, use Personalize → Restore Windows desktop at sign-in or Restore-Nexus-SignIn.bat. Windows may request permission. Then use Personalize → Start Nexus desktop after Windows sign-in.

The 1.7 VM report is recorded in docs/VM-EVIDENCE-2026-10-10.md. Native 1.8 startup, shell-less compatibility, and elevation behavior remain pending.
