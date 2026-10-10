# VM evidence supplied on 10 October 2026

Shan reported that every requested 1.7 manual VM test worked. The supplied logs record Windows 10 build 19045 x64. They include historical entries for older Nexus versions, so this report distinguishes the latest successful session from earlier failed sign-in attempts.

## Corroborated by supplied files

- The latest Nexus 1.7 desktop/taskbar start completed, Sections opened, and Files processes launched.
- Core reconnected for the same desktop process after a restart, then launched further Files workers.
- The current settings file records revision 34 with a commit ID; its backup records revision 33. Both are readable JSON.
- The host recorded temporary taskbar takeover for the latest session. Session journals retain original Windows taskbar visibility and work area for recovery.

## Earlier defects retained in the evidence

- Shell-replacement launches repeatedly failed with E_NOTIMPL from AppWindow.IsShownInSwitchers in DesktopWindow construction.
- Subsequent recovery attempted policy restoration but Windows denied registry write access. Current-session Windows recovery and future sign-in restoration are separate outcomes.
- Several Core startups logged EndOfStreamException when a connection closed before sending a frame. This is consistent with short readiness probes; the 1.8 fix accepts only completely empty disconnects. Partial frames still fail.

The 1.8 source update addresses these paths. The uploaded settings, logs, and journals were read only; their contents were not changed or bundled into the release.

The user report establishes manual acceptance for the tested session. It does not independently establish a recorded eight-hour soak, measured performance, every Windows edition/elevation case, or the new 1.8 startup paths. Those remain separate gates.
