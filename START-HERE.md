# Start here — Nexus 3.6.0

1. Delivery ZIPs contain **source, not an executable**. Use the full Source ZIP or exactly one patch matching your clean 3.5.1, 3.5.0, 3.4.0, 3.3.0, 3.2.0, 3.1.0, 3.0.0, 2.1.1 or main 1.5.0 source. See SOURCE-PATCH.md.
2. Build/package on Windows using README.md, or the Windows CI build. Keep the previous compiled folder. Interactive builds run native UI checks; headless runs explicitly record SKIP.
3. Extract `Nexus-Shell-3.6.0-win-x64.zip` into its own folder. Keep all launch/recovery helpers, published components and UI resources together.
4. Run Check-Nexus-UI.bat, then Launch-Nexus.bat for preview or Launch-Nexus-Desktop.bat for the managed desktop. Follow SEARCH-PHASE-02-3.6.0.md in the compiled folder for one acceptance pass.
5. Start the default Everything client in the same Windows session, then click the menu-bar Search icon or press Ctrl+Alt+Space. Search a known file, change category/scope and try Open folder and Copy path. Exit Everything and verify desktop commands/windows still work. Follow the Phase 02 guide for one complete acceptance pass.
6. Sign-in configuration remains separate. Follow START-AND-SIGNIN-3.6.0.md and Inspect-Nexus-Startup.bat; retain the Restore helpers.

Portable checks do not certify native Windows rendering, clipboard/window/session integration, redirected filesystems or sustained desktop performance.
