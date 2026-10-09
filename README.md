# Nexus Shell 1.5.0 — motion and window previews

The dock now responds to real window state: a finite launch lift, arrival cue for newly opened windows, activation feedback, downward minimize cue and upward restore cue. Its active underline smoothly changes length and opacity inside a fixed layout slot. Hover and press run on a separate container, so state motion cannot overwrite them. Overview cards enter in a short stagger.

Hover an open app for a window preview and restore, minimize, maximize/restore-size or close controls. The preview is an independent, non-activating window above the icon. Move into it across the gap, or press Down on a focused dock button / choose Window preview in its context menu for keyboard access. Escape dismisses it. The context menu exposes the same window commands. Close asks the app to close normally, including its own save prompts.

Balanced/Full use one native DWM thumbnail while a card is open. Fast and high contrast use the app icon, title and controls. Minimized/hidden windows also use a card. Protected apps can show blank contents. Toggle **Quick Settings → Dock hover previews** or **Sections → Personalize → Dock hover previews** to turn previews off. No screenshots are saved and closed previews keep no thumbnail. Motion respects Fast/Reduced effects, high contrast and Windows animation settings.

Ember's burgundy surfaces, orange-to-rose accents, rounded sidebar/search and real workspace/resource cards remain. Desktop, dock, Start, Quick Settings, Files, window overview, previews and Sections are independent windows sharing state. Sections opens from its desktop shortcut. Floating mode reserves no work-area strip and retains maximize auto-hide / bottom-edge reveal.

The host still hides Windows taskbars in managed session mode and restores Windows on exit. Explorer infrastructure, Windows services, recovery/startup/update implementation and dependency pins are preserved. Windows owns desktop shortcuts.

## Build and test

Apply one matching patch from [SOURCE-PATCH.md](SOURCE-PATCH.md), then commit/push for the Windows CI build. Routes cover GitHub main's 1.4.2 tree at `1cd328f83d28d606fe5e9da7250ee3d792725cd0` and prepared 1.4.3/1.4.4 source. Complete source is an alternative route. Source ZIPs contain no executable.

Local source/XML/resource/ownership and C# syntax checks, unchanged palette contrast and exact patch/ZIP reconstruction checks are recorded separately from native execution. This Linux authoring environment has no .NET, WinUI or PowerShell runtime. Native build, .NET/Win32 checks, package checks and Windows/VirtualBox acceptance remain pending; performance is not claimed as measured.

After CI succeeds, extract `Nexus-Shell-1.5.0-win-x64.zip` into a fresh folder and exit the old Nexus before launching it. Compare Fast and Balanced and complete [motion and preview acceptance](docs/MOTION-DOCK-1.5.0.md), [dock acceptance](docs/DOCK-RELIABILITY-1.4.3.md), [session recovery](docs/NEXUS-DESKTOP-MODE.md) and [validation status](docs/VALIDATION.md). Choose Personalize → Ember for the supplied reference's warm appearance.

Dock icon cues do not redirect Windows' own minimize animation into the Nexus icon. Full tray/Jump List contracts, app-native icons and additional-display shell surfaces remain future work.
