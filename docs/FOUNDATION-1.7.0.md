# Nexus 1.7.0 runtime foundation

Status: prepared source update. Native Windows acceptance is pending.

## Process ownership

| Process | Ownership |
| --- | --- |
| Nexus.DesktopHost | Existing managed-desktop heartbeat supervision and Windows recovery |
| Nexus.Shell desktop | Desktop, dock, Sections, Notes, Calculator, and UI state |
| Nexus.Core | Per-session coordination, sole profile writer, Files process supervision |
| Nexus.Shell Files worker | One browser or picker window; read-only appearance settings |

The browser process is reused for navigation. Pickers use separate processes. No machine service, boot policy, authentication provider, or service-disable profile is installed.

Files startup has a 15-second readiness deadline. A ready tool stops after 20 seconds without its UI heartbeat. Up to eight Files processes are admitted. A terminated tool is reopened by a new user request; failed picker selections are not silently retried. Core allows three starts in one minute, then pauses automatic restarts.

Workers are assigned to an unnamed Windows job configured to terminate them when Core's job handle closes. Silent breakaway lets external applications launched by Files live independently. Actual Windows job behavior is a required gate.

## State and local protocol

Core holds an exclusive profile writer lease. Existing settings migrate with revision zero; notes and layout are preserved. Every acknowledged commit records its revision and commit ID in the atomic settings file.

The desktop submits immutable snapshots in order. A stale expected revision is rejected. If a reply is lost, the next save reads the current commit metadata, or replays the original snapshot and ID, before sending newer edits. Files cannot call the state writer.

The versioned, length-framed local protocol limits messages to 4 MiB, settings to 2 MiB, and concurrent connections to 24. Endpoints use a per-session random name, CurrentUserOnly pipes, and Windows peer-process identity checks. Each role has an explicit operation allowlist. A picker request observes its caller's disconnect.

Appearance changes reach workers through their two-second UI pulse after settings have been committed. Core publishes appearance without holding the disk writer lock.

An exit save has an eight-second wait limit. If it cannot be confirmed, a unique `settings.unsaved-*.json` copy is attempted and its location reported. Recovery copies are announced on startup and never automatically overwrite newer settings.

## Windows release gates

Run these checks on a recorded Windows edition/build in preview before enabling desktop sign-in replacement.

1. Run both check projects, including real named-pipe tests and Windows job cleanup/breakaway. Run source validation, update fixtures, native build, and package verification.
2. Start the complete extracted package in preview. Confirm Core starts, desktop/dock open, and Files workers never create another desktop.
3. Open Files repeatedly: one browser process is reused. Open single-file, multi-file, folder, and save pickers; select and cancel each. Close Sections while its picker is open and verify that worker closes.
4. Force 30 Files exits and a Files UI hang on a test PC. Desktop/dock must remain responsive; other pickers must stay usable. Reopening must launch a fresh worker without an automatic restart loop.
5. Minimize, restore, switch, preview, and close isolated Files windows through the Nexus dock, overview, and Show desktop. Repeat 100 open/close cycles and inspect handles and private bytes.
6. Stop Core while Files is open. Verify worker cleanup, retained desktop edits, bounded Core recovery, and a subsequent successful save. Check that external applications opened from Files remain running.
7. Edit Notes, layout, and appearance; exit/restart and inspect acknowledged state. Exercise an interrupted save and an unwritable settings directory. Confirm any unsaved recovery copy is visible and does not overwrite the authoritative file.
8. Test session handoff, returning to Windows, host failure, lock/unlock, sleep/resume, sign-out, and display changes. Keep the existing Windows recovery helper accessible.
9. Measure the complete Nexus process set using `measure-resources.ps1 -Scenario Files`, then run an eight-hour session. Check for unrecovered hangs, lost acknowledged edits, and unexplained handle/private-memory growth.

The first source milestone does not pass these native gates by itself. Multi-session shared-profile support, additional tool isolation, durable file jobs, boot branding, sign-in customization, and optional Windows service tuning remain open.
