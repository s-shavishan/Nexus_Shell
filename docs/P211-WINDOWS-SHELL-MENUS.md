# P2.11 — Compact context menus and Windows shell integration

The owner requested a usable shell: compact context menus throughout Nexus, a correct About destination, real Windows calendar/notifications, Explorer retained in the background, and Windows 10/11 fallbacks. This pack changes command behavior and process ownership as well as menu presentation.

## Menus and destinations

- Desktop background goes from roughly twenty rows to six action rows plus a separator (one additional Undo row when available). View contains icon visibility and sorting; Tools groups Search, File Jobs, Places, Windows tools and Clipboard shelf. Personalize opens the real Controls page.
- Desktop items, Launchpad apps, menu-bar menus/status buttons, Files, dock pins/windows/background/clock and the Nexus tray menu are updated. Native WinUI flyouts share a compact presenter, icons, shortcut hints, constrained width/height and submenu/toggle styles. Platform text-edit menus keep native behavior and inherit the common styling.
- About Nexus opens its own centered window with actual assembly version, OS/architecture, session mode, project links, diagnostics access and copyable diagnostics. It no longer opens Core preferences.
- File actions follow type: folders, media, pictures, archives, applications/installers/shortcuts and ordinary/unknown documents have appropriate Open labels and Open with availability. Transfer and less frequent actions are grouped. Windows properties and registered shell options are available.
- A file/window menu retains the target selected when it opens. Dock commands revalidate native ownership before execution through the existing window command services. Arrange is a submenu; dock/background options hold auto-hide while their menus are open. Checked preferences refresh when their menus open.

## Windows panels, not cloned flyouts

| Action | Windows 11, build 22000+ | Windows 10, build 19041+ |
| --- | --- | --- |
| Menu-bar/dock clock and desktop date card | Native combined calendar/notification center via Win+N | Native date/time via Win+Alt+D |
| Menu-bar bell | Native notification center via Win+N | Native Action Center via Win+A |
| Windows quick settings action | Native Quick Settings via Win+A | Native Action Center via Win+A |
| Windows clipboard history | Win+V | Win+V |
| Windows Task View | Win+Tab | Win+Tab |

Clock and bell context menus provide matching Windows settings destinations. The clock also controls Nexus's existing 12/24-hour display. Native clipboard and Task View are available from menus/search alongside Nexus's optional shelf and saved layouts. Sound/network/Bluetooth/display right-click menus connect Nexus controls with Windows Quick Settings and advanced settings.

These use Microsoft's documented shortcuts. Nexus requests the Windows panel; successful input admission does not prove rendering or availability under a modified installation/policy. No notification contents or Windows unread counts are scraped. The former Nexus unread badge is removed from the Windows bell; Nexus's own service alerts remain under **Nexus activity** in View, Controls and the bell context menu.

The bridge checks Explorer/taskbar presence, refuses while physical modifiers are held, submits one ordered down/up batch and never retries a panel toggle. On partial admission it releases only keys from the injected prefix. No new keyboard hook or global hotkey is installed. UIPI/Windows denial is disclosed. An unavailable Windows shell opens the action's settings fallback; keyboard/denial cases preserve input and explain the shortcut/settings route. Windows owns flyout placement and appearance, including when Explorer's taskbar is hidden.

## Explorer and process reliability

Existing managed sessions hide only Windows taskbar surfaces and retain Explorer's desktop infrastructure. The host now starts Explorer if the Windows shell is absent at startup, including legacy Winlogon shell-replacement sessions. It never kills Explorer. The existing saved visibility/work-area lease and crash recovery remain in charge. Session status reports shell availability and observed native file-helper counts.

Native Windows file commands are isolated in a dedicated Core-supervised helper process with its own STA, COM lifetime, owner window and message pump. It uses `SHOpenWithDialog`, `SHBindToParent`, `IShellFolder` and `IContextMenu`/2/3. Windows options exposes registered classic shell verbs/extension actions; Windows keeps its own menu styling. A modeless native dialog keeps the helper alive while visible helper windows remain.

- Desktop and Files authenticated peers can request only menu/properties/open-with/reveal actions on literal full filesystem paths. Core checks the calling window owner; the helper checks it again before showing UI. Device namespaces and alternate streams are rejected. Show in Windows Explorer uses the documented shell selection API instead of constructing an Explorer command line.
- Source path inspection and extension calls happen in the helper, so a stalled network path or shell extension does not block the desktop/Core service lane. Request delivery is asynchronous and bounded. Three native helpers can be active at a time.
- A monotonic 15-second startup deadline applies before command admission. The helper announces readiness and waits for Core's separate admission grant before offering/invoking a command. A timeout cannot race into an already admitted command. Admitted dialogs/commands are not killed by that startup timer.
- Request IDs prevent duplicate commands after lost replies. An accepted command is never automatically replayed after failure/restart. Status is scoped to its originating component; failures reach Core health/Nexus activity.
- Core shutdown releases its owned helpers through the existing job/process cleanup. User applications launched by native commands retain the job's silent-breakaway behavior. Explorer is never assigned to that cleanup job.

Process isolation is a stability boundary, not a permissions sandbox. Registered Windows extensions run with the helper's inherited Nexus token. Windows owns native command semantics, confirmations, permissions and copy/delete behavior; those commands are separate from File Jobs' staged-copy guarantees. Shell-handler status means requested/finished/dismissed/failed, not proof that a selected native file operation completed successfully. No native command is reissued to guess an outcome.

## Validation and remaining gates

- Portable checks: 37 Core groups and 13 Runtime groups, including 244 Windows panel/input/type-policy assertions, 20 native-handler lifecycle/scope/layout assertions and the existing 45 File Jobs assertions.
- Runtime, production Core and DesktopHost C# compile with .NET 8 Roslyn warnings treated as errors. Shell C# API compilation uses cached WinUI/Windows projections and generated-XAML field fixtures; projection/fixture warnings are reported. This is not a native XAML build.
- Source/XML/resource guards cover shared menus, About, Windows routes, Explorer retention and helper ownership.
- Native preflight now constructs the compact desktop/Nexus/menu-bar/clock/notification/dock/system-control menus, checks the desktop action budget, builds About at two sizes and verifies six Files context types at wide/compact sizes using an isolated environment without Core, drives, scans, previews or commands. It has not run on this Linux host.
- AppVeyor, native WinUI build/preflight, target Windows 10/11 interaction tests, third-party shell extension behavior, packaging and screenshots remain pending. Portable tests inject no actual OS shortcuts and invoke no Windows COM dialog.

## Windows acceptance

1. Build the exact PR head and run Core/Runtime checks without the native-IPC skip plus the native UI preflight. Check the new menu/Files/About stages and retained dock/Spotlight stages.
2. On Windows 10 19045 and Windows 11, click clock/bell with Explorer's taskbar visible and hidden. Confirm the correct real flyout, keyboard activation, modifier-held refusal, settings fallbacks, 12/24-hour display and close/focus behavior.
3. Test desktop empty space/items, Launchpad apps/collections, menu-bar/status buttons, dock background/pins/running windows and Files blank-space/typed selections. Verify compact layout, submenus, checked state, keyboard context invocation, High Contrast, DPI, small work areas and stale-window/selection cases.
4. Test Open with, properties and Windows options on disposable folders, Unicode files, images, media, archives, executables, shortcuts, missing targets and slow network locations. Verify native confirmations and associations, foreground handoff, dismissal, modeless dialog lifetime and external-app survival.
5. Simulate a blocked/crashing shell handler before admission and a long native command after admission. Confirm startup-only termination, no repeated command, bounded helper count, accurate diagnostics and responsive Nexus/Core.
6. Start a managed session with Explorer already running and with it absent. Confirm Explorer persists, only taskbar surfaces hide, no reserved bottom gap appears, and exit/crash recovery restores the saved Windows surfaces. Verify sleep/wake, display changes and Explorer restart.

Official API/shortcut references: [Windows shortcuts](https://support.microsoft.com/en-au/accessibility/windows/keyboard-shortcuts-in-windows), [calendar/notification area](https://support.microsoft.com/en-us/windows/experience/personalization/customize-the-taskbar-in-windows), [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput), [SHOpenWithDialog](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenwithdialog), [IContextMenu](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-icontextmenu) and [SHBindToParent](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shbindtoparent).
