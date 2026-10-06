# Nexus Shell 0.2.0

The main desktop now uses the **Nexus Orbit** theme: dark pearl surfaces, violet accents, fine borders, static ribbon artwork, an orbit emblem, a floating dock, and a workspace sidebar. The layout borrows macOS proportions while preserving ordinary Windows application behavior.

## Interaction

- Short compositor entrance transitions and subtle dock/card hover lift.
- Reduced effects preference, Windows animation preference, and high-contrast fallback.
- Ctrl+K focuses app search inside Nexus; it is not a system-wide hotkey.
- Native light-dismiss Control center with keyboard dismissal.
- Red hides the workspace; yellow minimizes Nexus; green expands the workspace.
- Initial window fits the current monitor work area. Sidebar, widgets, dock capacity, and shortcut cards adapt to available space.

## Work and memory

- App library uses data-bound, virtualized GridView containers in a finite viewport, instead of constructing a button for every application.
- Start-menu discovery runs off the UI thread and starts only when Apps is first opened.
- Search uses a 160 ms debounce and does not rescan disks.
- Visual clock/resource updates pause when the Nexus window loses activation. The clock updates its text only when the displayed minute changes.
- Foreground usage sampling runs only when explicitly enabled. It can continue while Nexus is minimized, as needed for gaming-time summaries.
- Settings are saved from snapshots on a background worker, coalesced across quick changes. The final write guards against older queued writes.
- A cached process sampler measures elapsed CPU time and refreshes memory properties; its tooltip includes private bytes. No memory target is claimed as measured.
- Pins, activity, usage records, settings input size, discovered shortcuts, and realized library views are bounded.
- Added a Windows resource measurement script and comparison checklist.

## Validation status

Added `appveyor.yml` and browser setup instructions for AppVeyor's free public, open-source Windows builds. The desktop application version remains 0.2.0. The first user-run AppVeyor attempt failed during NuGet restore; the second restored packages but failed during C# compilation. No successful cloud publish is verified yet.

Source XML, handler/resource references, C# syntax, structural performance guards, and archive integrity are checked in the authoring workspace. Windows compilation, rendering, motion smoothness, frame timing, memory behavior, and the cloud workflow still need Windows validation. This release remains source, not a verified executable.

Providers, download/game installation, privileged services, Explorer replacement, and a persistent global dock remain outside this desktop prototype.

## Build correction — 2026-10-06

Updated `Microsoft.Windows.SDK.BuildTools` from 10.0.26100.1 to 10.0.26100.4654 to satisfy the existing Windows App SDK Base dependency and correct the reported NU1605 package downgrade. The change keeps the existing app version and UI. `START-HERE.md` includes a one-line GitHub browser edit so existing users can retry the cloud build without downloading developer tools or the source again.

Corrected both action-button padding calls from `new Thickness(14, 9)` to `new Thickness(14, 9, 14, 9)`, resolving the reported CS7036 constructor-argument errors while retaining the intended spacing. Other C# `Thickness` constructions were reviewed. The corrected source still awaits successful Windows compilation and publish.
