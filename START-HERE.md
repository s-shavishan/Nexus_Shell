# Start here — Nexus 1.3.1

1. Apply the 1.3.0→1.3.1 Git patch to the verified 1.3.0 baseline, or extract the full source into a new folder. See SOURCE-PATCH.md.
2. Commit/push the source for AppVeyor, or build on Windows using `scripts\build.ps1 -UseMSBuild`. Source ZIPs cannot run directly.
3. Extract the complete Windows artifact. Close any Nexus preview, then run **Launch-Nexus-Desktop.bat** for a temporary Nexus desktop.
4. The Windows desktop and taskbars are hidden after Nexus becomes ready. **Exit Nexus** to restore them. No sign-in policy changes are made by this mode.
5. Complete docs/TEST-DESKTOP-MODE.md on your VM, including normal exit, UI failure, host failure and a denied registry write.

Opening `Nexus.Shell.exe` is preview mode. From preview, **Sections → Personalize → Use Nexus for this session…** performs the same temporary takeover with an orderly handoff.

Persistent **Use Nexus at sign-in** is a separate option with Windows edition/build and registry permissions requirements. See docs/NEXUS-DESKTOP-MODE.md. Current-session Windows recovery is allowed to continue even if that policy setting cannot be restored.

Local core, source, host C# and application API checks pass. Native Windows compilation and 1.3.1 VM acceptance remain pending.
