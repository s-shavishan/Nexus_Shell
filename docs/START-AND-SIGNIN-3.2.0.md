# Start and sign-in in Nexus 3.2.0

Build/package with README.md, then begin with [UI and Windows acceptance](RESPONSIVENESS-3.2.0.md). Preview uses `Launch-Nexus.bat`; the managed session uses `Launch-Nexus-Desktop.bat`.

The standalone Windows key toggles Nexus Launchpad in managed sessions. Windows key combinations keep their existing routing. Preview deliberately coexists with Explorer; it does not own the standalone Windows key or Explorer's work area.

Startup alongside Windows and replacement of the sign-in shell are separate choices. A Run startup entry can show Windows before Nexus. For shell replacement, first run `Inspect-Nexus-Startup.bat`, check the selected command and recovery owner in Personalize, and keep the matching `Restore-Nexus-SignIn.bat` and `Restore-Windows-Desktop.bat` helpers available.

The latest attached VM log reports “Another custom desktop is configured.” This update preserves that foreign policy; it does not identify or overwrite its owner. Windows still owns boot, authentication and protected services.
