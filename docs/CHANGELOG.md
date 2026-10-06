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

Source XML, handler/resource references, C# syntax, structural performance guards, and archive integrity are checked in the authoring workspace. Windows compilation, rendering, motion smoothness, frame timing, memory behavior, and the cloud workflow still need Windows validation. This release remains source, not a verified executable.

Providers, download/game installation, privileged services, Explorer replacement, and a persistent global dock remain outside this desktop prototype.
