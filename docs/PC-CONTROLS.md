# Quick Settings — 1.4.0

The taskbar and Win+A in a managed desktop now open independent Quick Settings. More controls opens the existing Sections PC controls/mixer described below. See [QUICK-SETTINGS-AND-PERFORMANCE.md](QUICK-SETTINGS-AND-PERFORMANCE.md) for hardware capability limits and validation.

# Nexus 1.0.0 PC controls

Use Ctrl+5, the desktop shortcut, dock gear, More menu or Ctrl+K → PC controls. The quick Control center includes the default output's volume and mute; the full workspace also shows app sessions, PC status and window layouts.

## Supported operations

| Tool | Behavior |
|---|---|
| Master audio | Reads and changes the current default render output through IAudioEndpointVolume; shows its device name. |
| App mixer | Reads up to 32 sessions on that output with IAudioSessionManager2 and session notifications; changes each session through ISimpleAudioVolume. Apps on another output are outside this list. |
| System status | Uses Windows CPU/memory/power queries and local adapter/drive information. CPU needs two samples; network link/address does not establish internet access. |
| Window layouts | Select one to four visible windows for columns, stack or grid. Uses the first window's monitor work area and validates the handle's process before changing placement. |
| Undo | Restores the last successful arrangement's original placements. Closed, changed or inaccessible windows are skipped; an unsuccessful restore can be retried. |
| Lock PC | Calls the Windows workstation lock operation after you click Lock PC. |

Audio reads and retained COM objects belong to a dedicated MTA worker. Slider updates are coalesced over 120 ms. A five-second UI wait limit leaves navigation responsive if audio stops responding; an unfinished read is reused rather than filling the work queue. The shell unregisters audio notifications when the controls close or Nexus becomes inactive. It does not record audio or persist audio sessions/window handles.

App sessions can be separate even when they share a process name. Starting playback creates sessions; use Refresh if needed. Output changes invalidate pending edits to the previous device. Hardware, exclusive-mode applications and Windows permissions can affect control behavior. Advanced display, Bluetooth and network configuration still use the clearly labeled Windows Settings links.

## Windows acceptance

1. Build the exact source commit in AppVeyor and pass C#/XAML compilation, native/core/updater checks, publishing, PRI/XBF and ZIP checks. A host without an audio device cannot validate actual audio control.
2. Open Control center, compare the level with Windows, move its slider and mute/unmute. Repeat with keyboard arrows/Tab/Enter. These are intentional real volume changes; start at a comfortable level.
3. Play audio in Firefox/VLC or another app and open Ctrl+5. Change that session's level/mute and confirm another app retains its own level. Test rapid slider drags, quick repeated mute clicks, app exits, playback restarts and a newly created session.
4. Change the default output or disconnect it with controls open. Confirm the name and sessions refresh; controls disable when no output is available. Reconnect and Refresh. Test inactive/reactivated and minimized/hidden states. There must be no background polling or UI freeze.
5. Select two to four ordinary windows. Test columns, stack and grid, then Undo. Repeat with a minimized/maximized window, mixed applications, two monitors, a negative monitor origin, taskbars on different edges and different DPI scales. Windows may restrict elevated or unusual app windows; failures must leave Nexus usable.
6. Close a selected window before arranging; ensure stale targets are rejected. Choose the same window in two slots; reject duplicates. Arrange, close a target and Undo; surviving targets must restore. Verify a failed layout restores prior placements.
7. Test Lock PC, return to the session and verify the UI resumes. Test Ctrl+5/search/menu navigation, Back/Forward, remembering the workspace and using it as a profile destination.
8. Compare the actual UI at 320×480, 1024×600, 1366×768 and 1920×1080 logical sizes, with 100/125/150/200% scaling. Cards must stack, lists scroll and audio/window controls remain reachable. Check hover, pressed, disabled, Tab focus, high contrast, all moods and reduced effects.
9. Compare CPU and physical memory with Task Manager over the same interval. The first CPU reading should be pending. Measure idle/active/minimized resource use; no preview or source check is a performance result.

## API references

- [Core Audio session manager](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessionmanager2)
- [Endpoint volume](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume)
- [System CPU times](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes)
- [Window positioning](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)

The artwork previews in this repository describe the earlier 0.9.0 layout. This release's styling is implemented in WinUI; its actual rendering must be checked on Windows.
