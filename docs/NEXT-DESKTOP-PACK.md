# Proposed next major update — Nexus 3.0 Session Core

This is a proposal, not implemented functionality. The 2.1.1 update handles the current Start-key report and improves sign-in diagnosis. The next major version should change how the desktop survives failure and handles everyday PC work.

| Priority | Desktop system | User-visible result | Main acceptance gate |
| --- | --- | --- | --- |
| 1 | Session supervision and independent dock/panel processes | A failed Control Center or Launchpad restarts without taking down the desktop; live status and an independent recovery path | Kill each surface process repeatedly; no lost preferences, stuck input, orphan processes or forced Explorer restart |
| 2 | Multi-monitor window management | Dock per monitor, visual window overview, drag/snap zones, named layout restore and unplug/reconnect recovery | Negative monitor origins, mixed DPI, full screen, reused HWNDs and changed app instances |
| 3 | Real desktop/file interaction | File drag/drop, rectangle selection, rename/delete/undo feedback and a persistent copy/move queue through the Files service | Errors/conflicts/cancel are explicit; interrupted jobs reconcile actual file state before resuming |
| 4 | Deeper device controls | Supported microphone controls, Bluetooth pairing flows, display-mode changes and richer power options within Control Center | Actual hardware capability and documented API support; unsupported operations remain explicit |
| 5 | Windows notification bridge | Opt-in Windows app notifications alongside Nexus alerts, groups, dismissal synchronization and persistent Nexus history | Manifest capability/identity and user permission first; revocation, clear semantics and per-notification failures are tested |
| 6 | Everyday shell compatibility | Notification-area compatibility research, safe device removal, keyboard/input indicators, clipboard history and screenshot controls | Supported API boundaries, original application menus, explicit capture/history controls and no secure-desktop capture |
| 7 | Coherent glass and motion | One material/spacing system; linked dock/window/panel transitions, clear contrast and reduced motion; measured frame and memory budgets | Real Windows rendering, VM GPU fallback, high contrast, mixed DPI and one-hour resource measurement |

The first milestone should be **independent panel recovery**, with the existing Core protocol extended to supervise surface identities and health. Split one surface first, validate restart/focus/input/settings behavior, then extend the pattern to Launchpad and dock. Expanding features before establishing those recovery boundaries repeats the current shared-UI failure problem.

Use supported DWM thumbnails for live overview when available. Named Nexus layout workspaces should not be presented as a complete replacement for Windows virtual desktops: the public IVirtualDesktopManager API covers window membership/location, not a complete create/switch/enumerate controller. Keep any further integration behind a separately validated compatibility boundary.

The current notification inbox contains Nexus session alerts. Reading other applications' notifications needs Windows' User Notification Listener capability and access permission; the current unpackaged delivery needs identity/capability work before that is promised. A taskbar notification-area compatibility layer also needs separate research; drawing icons alone does not implement the Shell_NotifyIcon contract.

Windows remains the driver, security, authentication and service platform. Nexus should own its desktop lifecycle and integrations rather than remove required Windows services. No standalone application is part of this proposal.

References: [notification listener](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener), [public virtual-desktop API](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ivirtualdesktopmanager), [DWM thumbnails](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail).
