# NEXUS Shell 0.7.0 — Shell Experience

A native Windows desktop surface for your apps, study sessions, saved items and daily work, built on the Nexus Aura visual system.

## New in this update

- Back/forward buttons and **Alt+Left / Alt+Right** for page navigation. History stays in the current session and is bounded to 24 pages; page refreshes add no duplicate entry.
- A page/profile breadcrumb beneath the floating menu bar.
- Search categories: **All, Apps, Saved, Workspaces, Actions, Tasks, Windows**.
- Current open-window titles in search. Choosing a window asks Windows to activate it.
- Up to eight recent app/saved-item/workspace choices from search. Current catalog entries resolve those references; stale items are omitted. Search text and ephemeral window handles are not stored in this list.
- A **Personalize** page with Pearl/Lagoon/Graphite mood cards, desktop/panel layout, widgets, Home card visibility, glass/effects, compact dock and clock format.
- Clear/disable recent-item shortcuts, and restore appearance defaults.
- Compact dock controls, packed Home cards when some are hidden, native switch palette resources and high-contrast selection fixes.

Notes and pins remain saved when their Home cards are hidden. Existing focus/tasks, workspace presets, global shortcut, notification-area residency, app library, usage tracking and binary updater remain available.

## Use the update

Open **More → Personalize**, the workspace sidebar, or Control center → **Personalize desktop**. Try compact dock and clock format; hide the Home cards you do not need. Personalization is saved on this PC. Appearance reset leaves notes, tasks, pins, workspaces and recent-item preferences intact.

Press Ctrl+K, choose a category and type. Recent items appear first only in the empty All view. After choosing a category, input returns to the search field for typing and arrow/Enter selection. Recent-item clearing applies to these search shortcuts; Activity and optional usage data have their separate controls.

| Shortcut | Action |
|---|---|
| Alt+Left / Alt+Right | Previous / next Nexus page |
| Ctrl+K | Search your orbit |
| Ctrl+Alt+Space | Summon search from other apps while Nexus runs and the shortcut is enabled |
| Ctrl+1 / 2 / 3 | Home / Explore / Study |
| Ctrl+4 | Window overview |
| F11 | Fullscreen / windowed |
| Escape | Close search or controls, leave fullscreen, or minimize |

Navigation history changes pages; it does not restore prior workspace selections or execute app launches. Nexus continues to use Windows Explorer.

## Build through AppVeyor

Merge the small source patch into your **0.6.0 source repository**, commit and run AppVeyor. For earlier source versions, start from the complete 0.7.0 source ZIP. `START-HERE.md` explains the steps.

After core/native checks, publishing and packaging succeed, use `Nexus-Shell-0.7.0-Update-win-x64.zip`. Fully exit Nexus, extract it and run `Apply-Update.bat` against your full binary folder. Runtime package references are unchanged from 0.6.0; the updater still checks actual runtime bytes. Use the full binary ZIP if compatibility checks fail.

Source ZIPs need a Windows build and are not runnable application updates. The AppVeyor route needs no local developer downloads.

## Validation status

Source XML, resource/event wiring, C# syntax parsing, linked core-source checks, version/dependency consistency and source-patch reconstruction passed here. Behavior checks were added to the CI harness. This environment has no Windows toolchain: compilation, test execution, native startup/input/material/scaling checks and performance measurements remain pending.

`docs/Nexus-Experience-preview.png` and `docs/Nexus-Experience-search.png` are illustrative design references, not native Windows screenshots. Actual labels, fonts, toggle states and data follow the Windows runtime and local settings.

Local Windows build: `.\scripts\build.ps1 -UseMSBuild -Run`. See `docs/TEST-WINDOWS.md`, `docs/VALIDATION.md`, `docs/DESIGN-AND-PERFORMANCE.md` and `docs/UPDATING.md`.
