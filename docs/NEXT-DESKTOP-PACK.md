# Nexus 3.x Session Core roadmap

The 3.0.0 candidate implements the first recovery boundary (Control Center), floating/attached menu-bar and frame motion, snap/title-control protection, and persistent Nexus alert history. The 3.4.0 candidate adds a DWM window board, conservative named layout restore, session-only clipboard shelf and observed session status; their native acceptance is documented in [Desktop Command Pack](DESKTOP-COMMANDS-3.4.0.md). The 3.5.0 candidate adds Launchpad collections, Places, reversible local rename/new-folder edits, desktop folder observation and searchable/snoozable Nexus alerts; see [Everyday Desktop Pack](EVERYDAY-DESKTOP-3.5.0.md). Remaining work is listed below. See [delivered scope and Windows gates](SESSION-CORE-3.0.0.md).

| Priority | Desktop system | User-visible result | Main acceptance gate |
| --- | --- | --- | --- |
| 1 | Session supervision and independent dock/panel processes | A failed Control Center or Launchpad restarts without taking down the desktop; live status and an independent recovery path | Kill each surface process repeatedly; no lost preferences, stuck input, orphan processes or forced Explorer restart |
| 2 | Multi-monitor window management | Dock per monitor, drag/snap zones and automatic unplug/reconnect recovery; the window board and named layouts are implemented in 3.4.0 | Negative monitor origins, mixed DPI, full screen, reused HWNDs and changed app instances |
| 3 | Real desktop/file interaction | File drag/drop, rectangle selection, recycle/delete/restore and a persistent copy/move queue through the Files service; local rename/new-folder undo and desktop folder observation are implemented in 3.5.0 | Errors/conflicts/cancel are explicit; interrupted jobs reconcile actual file state before resuming |
| 4 | Deeper device controls | Supported microphone controls, Bluetooth pairing flows, display-mode changes and richer power options within Control Center | Actual hardware capability and documented API support; unsupported operations remain explicit |
| 5 | Windows notification bridge | Opt-in Windows app notifications alongside Nexus alerts, groups, dismissal synchronization and persistent Nexus history | Manifest capability/identity and user permission first; revocation, clear semantics and per-notification failures are tested |
| 6 | Everyday shell compatibility | Notification-area compatibility research, safe device removal, keyboard/input indicators and screenshot controls; the text clipboard shelf is implemented in 3.4.0 | Supported API boundaries, original application menus, explicit capture/history controls and no secure-desktop capture |
| 7 | Coherent glass and motion | One material/spacing system; linked dock/window/panel transitions, clear contrast and reduced motion; measured frame and memory budgets | Real Windows rendering, VM GPU fallback, high contrast, mixed DPI and one-hour resource measurement |

The first implemented milestone is **independent Control Center recovery**, with the Core protocol extended to supervise its identity/window/heartbeat. Complete Windows restart/focus/input/settings acceptance, then extend the pattern to Launchpad and dock. The current desktop tools still share the shell process; independent Launchpad/dock recovery remains unfinished.

Use supported DWM thumbnails for live overview when available. Named Nexus layout workspaces should not be presented as a complete replacement for Windows virtual desktops: the public IVirtualDesktopManager API covers window membership/location, not a complete create/switch/enumerate controller. Keep any further integration behind a separately validated compatibility boundary.

The current notification inbox saves bounded Nexus alert history through Core. Reading other applications' notifications needs Windows' User Notification Listener capability and access permission; the current unpackaged delivery needs identity/capability work before that is promised. A taskbar notification-area compatibility layer also needs separate research; drawing icons alone does not implement the Shell_NotifyIcon contract.

Windows remains the driver, security, authentication and service platform. Nexus should own its desktop lifecycle and integrations rather than remove required Windows services. No standalone application is part of this proposal.

References: [notification listener](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener), [public virtual-desktop API](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ivirtualdesktopmanager), [DWM thumbnails](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail).

## Engine reuse direction

The next capability work follows the [engine integration plan](ENGINE-INTEGRATION-PLAN.md): Everything-backed Nexus Search first, then an Explorer++ prototype and a recoverable transfer queue. These are planned integrations; 3.5.1 contains the Phase 01 stability fixes.
