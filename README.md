# Nexus Shell 3.6.0 — Nexus Search / Phase 02

Nexus is a native C# / WinUI desktop environment on Windows. This release adds an integrated Search panel backed by Everything's local index, plus desktop controls and open-window results. Launchpad remains the app launcher.

## Windows shell and compact menus (pending Windows validation)

- Compact desktop, app, Files, dock and menu-bar context menus with grouped actions, file-type-aware commands and checked preferences.
- A real **About Nexus** window with version/system information, project links and copyable diagnostics.
- Clock/date buttons open Windows calendar; the bell opens Windows notifications. Windows 11 uses its combined panel; Windows 10 uses separate date/time and Action Center routes. Native clipboard history and Task View are also connected.
- Explorer stays running. Managed startup starts it if absent and hides its taskbar through the existing recoverable surface lease.
- Windows **Open with**, properties and registered shell options run in separate supervised helper processes with startup admission deadlines, bounded concurrency and no automatic command replay.

Nexus service alerts remain under **Nexus activity**. See [shell behavior, limits and Windows acceptance](docs/P211-WINDOWS-SHELL-MENUS.md).

### Windows connection and recovery

**Nexus Controls → Core & recovery → Windows connection** now shows observed shell availability, starts Explorer when both shell surfaces are absent, exposes native calendar/notifications/quick settings and their Windows settings fallbacks, and copies connection/work-area diagnostics. Clock and bell context menus link here directly.

Supervised desktops accept Explorer at sign-in and attempt bounded recovery when it disappears. Taskbar recreation reanchors Nexus and schedules a one-shot work-area check; old Explorer recovery records and process handles are released. Preview mode uses explicit recovery only. See [behavior and Windows acceptance](docs/P212-WINDOWS-CONNECTION-RECOVERY.md).

## Nexus File Jobs (pending Windows validation)

Nexus Workspaces is removed. Its profile model, resume state, launch planner and workspace commands are retired. **File Jobs** replaces that direction with a practical file-operation queue owned by Nexus Core.

- Add files or folders, choose a destination, and track bytes, committed files and skipped items.
- Keep both names or skip collisions; existing destination items are never overwritten. Copied contents are verified with SHA-256 by default.
- Pause/resume the active transfer, independently hold later jobs, cancel work, open its destination, or load finished/interrupted jobs into the form for review.
- View measured copy, compression or extraction speed and estimated active-phase time. Paused and verification phases clear the estimate; verification time is additional.
- Open File Jobs from the Nexus menu, Launchpad, Spotlight or Controls. Nexus Files gains **Copy to…** and **Move to…** context actions.
- Closing the utility window leaves accepted jobs running. Closing Nexus stops them; a restart marks unfinished jobs interrupted for manual review.

Folder moves require the same drive; choose Copy across drives. Notes and independent saved content remain. See [behavior, recovery and Windows acceptance](docs/P210-FILE-JOBS.md).

## Everyday file operations (pending Windows validation)

- **Cut, Copy and Paste** files and folders between Nexus Files and Windows Explorer, using the Windows file clipboard. Toolbar buttons, folder/selection menus and **Ctrl+X/C/V** support up to 24 items.
- Paste into the current folder through Core's File Jobs queue, with progress, pause, cancellation, default verification and keep-both collisions. A lost response keeps the original job ID; the same Cut cannot start a second move to another folder in that Core session.
- **Move to Recycle Bin…** handles the selected files/folders through Windows, including its progress and cancellation dialogs. **Delete** opens confirmation; the Recycle Bin page opens Windows' **Restore** controls. Fixed local volumes are supported, with Windows warnings when an item cannot be recycled.
- **Ctrl+Shift+N** creates a folder while the file list has focus. Text fields retain their normal editing shortcuts.

See [file clipboard/recycle behavior and Windows acceptance](docs/P213-EVERYDAY-FILE-OPERATIONS.md).

## System Monitor (pending Windows validation)

- Open **System Monitor** from Launchpad, Search, desktop/Nexus menu → Tools or Nexus Controls → Home.
- Observe CPU, physical memory and uptime; filter processes by name, PID or executable path, sort by CPU/memory/name/PID and inspect working set, private committed memory and executable location.
- Ask an app's windows to close or explicitly confirm **End process**. Each action checks the original process identity again; Windows/Nexus infrastructure and processes outside your user/session remain protected here.
- Review local/removable/optical drive capacity and space available to your account, then open a drive in Files. Network drive capacity is not probed. Updates pause when the monitor is inactive or paused.

See [behavior, validation and Windows acceptance](docs/P215-SYSTEM-MONITOR.md).

## ZIP archives (pending Windows validation)

- Select files/folders in Nexus Files and choose **Archive… → Create ZIP…**, or **Extract ZIP…** for one selected ZIP. Choose an output name in the current folder, or use File Jobs to choose another destination.
- Archive jobs share progress, pause/resume, cancellation, queue holds and recovery. They publish one complete ZIP or extracted folder, keep both colliding names or skip the whole result, and retain sources.
- Validate archive paths and limits before extraction; check ZIP CRC integrity and enable SHA-256 readback by default. Preserve empty folders, Unicode names, ZIP modification times and existing Windows origin-zone marks.

See [supported formats, limits and acceptance](docs/P214-ZIP-ARCHIVE-JOBS.md).

## Nexus Controls (pending Windows validation)

**Sections is replaced by Nexus Controls**, available from its desktop shortcut, Launchpad, Spotlight and the Nexus menu. A new resizable window brings together Home, Core & recovery, Personalize, Desktop & dock, Privacy, and Startup & session.

- Search settings inside Controls with **Ctrl+F**, or find individual settings through Spotlight.
- Apply **Midnight Glass, Quiet desktop or Compact desktop** presets with an undo action; choose palettes, glass quality and motion individually.
- Check/reconnect Nexus services, request a settings save, repair desktop layout and open local data/logs.
- Manage dock behavior, desktop icons, time format, notifications, local history and clipboard capture. Review existing supervised startup, sign-in and Windows recovery choices in one place.

Old Sections shortcuts migrate to Controls. Existing saved content remains in settings; notes stay available in Notes. The former Sections Explore/Study/workspace screens are retired. Native Windows/AppVeyor acceptance is still pending. See [Controls behavior, migration and validation](docs/P29-NEXUS-CONTROLS.md).

## Windows integration pack (pending Windows validation)

- **Nexus menu → Windows tools** brings 14 Windows utilities/settings pages into a searchable desktop panel. Spotlight also finds individual tools, including screen snipping, Task Manager, display projection, volume mixer, startup apps and storage settings.
- **Keep awake** requests 15, 30, 60 or 120 minutes without changing your power plan, with an optional display-on request and a menu-bar indicator. It ends on lock, disconnect, sleep, expiry or Nexus shutdown.
- **Input language** in the menu bar shows the observed app language and requests an installed layout for that app. Windows retains its own input switching and permission rules.
- **Windows lifecycle recovery** responds to wake, display/device changes and clock/appearance notifications. Session status reports the integration; usage sampling pauses while asleep, locked or disconnected.

The pack includes the pending P2.7 UI work. Portable checks pass; native Windows/AppVeyor and visual acceptance are still required. See [behavior, limits and Windows tests](docs/P28-WINDOWS-INTEGRATION.md).

## Changes in 3.6.0

- Search files by category and scope in a glass desktop panel. Use My files by default or explicitly include indexed local drives; open results, open their folder in Nexus Files, or copy a path.
- Search 16 desktop controls, including Windows connection recovery, and switch open windows even when Everything is unavailable.
- Open Search from the menu bar, desktop/Nexus/File menus, Ctrl+Alt+Space across apps when available, or Ctrl+Shift+F on the desktop. Arrow keys, Enter, Ctrl+Enter, Ctrl+Shift+C, Ctrl+L and Escape keep it usable from the keyboard.
- Bound native IPC inside Core, cancel obsolete queries, reject malformed/out-of-scope results, preserve selection as results arrive and show clear dependency/readiness/timeout states.
- Keep queries in memory only while the panel is open. No duplicate index, SDK DLL or third-party binary is bundled; Session status reports the engine's last observed status.

Read [Search setup, validation and the Windows acceptance pass](docs/SEARCH-PHASE-02-3.6.0.md). File search requires the default Everything client running in your Windows session; the indexing service alone is insufficient. Native Everything and rendering acceptance remain pending until tested on Windows.

## Included Phase 01 fixes

- Bound folder reads and image previews to one active operation each; discard superseded work and show slow-location feedback.
- Keep file actions and Back/Forward states accurate during navigation, dialogs and Recycle Bin use. Ignore double-clicks on empty list space.
- Recover from rejected settings saves; capture queued state before waiting. Preserve uncertain-acknowledgement and revision-conflict protections.
- Normalize notification text before deduplication, retain severity changes, and handle backwards clock adjustments.
- Reject alternate device/UNC path spellings and discard invalid Places individually during settings recovery.
- Reposition open desktop panels when dock/work-area layout is refreshed.

The [Phase 01 fix details](docs/STABILITY-PHASE-01-3.5.1.md) remain available. The [engine integration plan](docs/ENGINE-INTEGRATION-PLAN.md) records the next file-manager and transfer work; Explorer++ and transfer engines are not substituted in this release.

## Included 3.5.0 features

- **Launchpad collections:** create, rename and remove named collections; assign apps from their context menus. Add A–Z/catalog sorting, hide/restore controls and a dedicated Hidden tab. Collections do not alter installed apps or dock pins.
- **Dock ordering:** move pins left/right from the dock or Launchpad context menu. Recent and Pinned views retain their meaningful order.
- **Places:** an integrated favorite-folder panel with common locations, labels, ordering, a folder picker and a bounded quick preview. Continue in Nexus Files with one action. Folder reads run off the UI thread, with one active worker and stale-result rejection.
- **Desktop file controls:** rename, new folder, copy path and open containing folder; sort by Name/Kind/Newest or hide icons. Desktop and Nexus Files gain one-step undo for local rename/new-folder edits, with collision and changed-item refusal.
- **Automatic icon updates:** observe user/common desktop folders, coalesce notifications and retry refresh requests that arrive during an existing read. Manual refresh remains available; Session status reports observer health.
- **Useful alert history:** search, unread/issues/snoozed filters, explicit mark-read, 15-minute snooze, bring-back and undo dismiss/clear. Rendering is limited to 20 cards per page; snooze deadlines persist through the existing Core writer.
- **Desktop shortcuts:** the menu-bar Help button and desktop F1 open an integrated guide with direct routes to existing desktop tools. New controls use the current glass, high-contrast and reduced-motion system.

Open **File → Places**, organize apps inside **Launchpad**, right-click desktop items or the desktop background, and use **Help** for shortcuts. The [Everyday Desktop guide](docs/EVERYDAY-DESKTOP-3.5.0.md) describes exact behavior, limits and one consolidated Windows VM acceptance pass. All [3.4 window-board, layout and clipboard features](docs/DESKTOP-COMMANDS-3.4.0.md) remain included.

The source/API compilation and portable checks do not certify a native Windows build, compositor output or sustained performance. Delivery ZIPs contain source; build on Windows before VM acceptance.

## Integrated controls

Core-backed sound, display, network, Bluetooth, power and Nexus preferences remain included, with separate device lanes, fresh service epochs, bounded requests and explicit unsupported capabilities.

| Section | Integrated controls | Remaining advanced controls |
| --- | --- | --- |
| Sound | Master and per-session volume/mute; current output state | Selecting the default output, input/microphone and enhancements |
| Network | Ethernet/IP/gateway/DNS status; Wi-Fi radio, scan, saved connections, new open/WPA2-Personal AES connections; forget Nexus-created user profiles | New WPA3/enterprise profiles, VPN, proxy and IP configuration |
| Bluetooth | Classic radios, cached/scanned devices, connection/paired state, discoverability | Radio power, pairing, BLE management |
| Display | Active resolutions/refresh rates; hardware brightness when exposed by the monitor | Resolution/scaling changes, night light and projection |
| Power | Battery/AC status, existing power-plan selection, lock | Sleep timers and plan editing |
| Desktop | Nexus wallpaper, dock, effects, widgets, clock and quiet-alert preferences | Windows-wide personalization and app-toast capture |

The earlier Launchpad, dock/window overview, supervised startup, independent Files/picker process, Start-key routing, sign-in policy inspection and recovery helpers remain included. Glass uses supported Windows acrylic blur/tint with a solid fallback. The existing foreign sign-in policy needs diagnostic output before ownership can be reconciled; it is preserved.

## Build on Windows

Use .NET 8 and the Windows app build tools. Source ZIPs contain no compiled executable.

```powershell
python scripts/validate-source.py
dotnet run --project tests/Nexus.Core.Checks/Nexus.Core.Checks.csproj -c Release
dotnet run --project tests/Nexus.Runtime.Checks/Nexus.Runtime.Checks.csproj -c Release
.\scripts\test-update.ps1
.\scripts\build.ps1 -UseMSBuild
.\scripts\package.ps1
```

CI publishes `Nexus-Shell-3.6.0-win-x64.zip` and the smaller update ZIP. Keep the complete published folder together, including Nexus.Shell, DesktopHost, Core, Nexus.Runtime and compiled UI resources. See [source patch routes](SOURCE-PATCH.md) and [start here](START-HERE.md).

## Next milestones

The Control Center process is the first panel boundary. Launchpad/dock separation, dock per monitor, expanded file jobs/device controls, Windows app notifications and shell compatibility work remain in [the Session Core roadmap](docs/NEXT-DESKTOP-PACK.md), each with native acceptance gates. Nexus Controls is an independently closable window in the desktop process.
