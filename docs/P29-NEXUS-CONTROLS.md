# P2.9 — Nexus Controls replaces Sections

User request: remove the Sections app and provide one suitable home for Nexus core controls, personalization and desktop preferences. Built on draft PR #29, including its Windows integration and P2.7 visual changes. The initial baseline for this work was `2bdf9a16f070e05dc3ae3e9563aa807335281789`. Before final validation, the desktop widget rail and Launchpad gallery changes through `26611fc0c93dc6f7abe20bec570a33d08b948e6e` were incorporated and preserved. The subsequent native Launchpad gallery gate in `d65b90c93d042e12100e98b7f69e360ea7f3a712` is also retained.

## What changes on screen

The former Sections navigation, application grids, Explore, Study, workspace and overview screens are removed from the application. Nexus Controls uses a new 1080 × 780 DIP resizable window, traffic-light window controls, a dedicated sidebar, settings search, scrollable cards and a persistent feedback footer. At content widths below 780 DIP, a page selector replaces the sidebar. It has its own original vector icon in the desktop, Launchpad and dock.

| Page | Connected behavior |
| --- | --- |
| Home | Selected desktop preview, observed Core status, current privacy choices, links to device controls and Windows tools. |
| Core & recovery | Check/reconnect services through existing recovery limits, repair dock/work area, refresh desktop, save a settings snapshot through Core, open the local data/log folder, and inspect observed service/material status. |
| Personalize | Three appearance presets with one-step undo, all existing wallpaper palettes, native glass/style/quality, motion, reduced effects and display name. |
| Desktop & dock | Floating/compact dock, hover previews, desktop icon visibility/order, focus desktop, date card, clock format and quiet notifications. |
| Privacy | Recent references, usage tracking, clear-history actions with confirmation, optional clipboard capture and explicit clear, Notes and local data access. |
| Startup & session | Existing supervised session, startup-after-sign-in, sign-in replacement and restore workflows, retaining their confirmations and backup/ownership checks. |

The desktop preview is an illustration of the selected settings, not a screenshot or a claim that Windows acrylic is active. Actual requested/effective material status appears on the Personalize and Core pages. High Contrast uses the Windows palette; motion respects existing accessibility and quality rules.

## Search, presets and ownership

- Ctrl+F focuses settings search; Escape clears the query. Fifteen named settings have bounded, literal keyword search and page/section destinations. Spotlight includes those same destinations. Search text is never used as a system command.
- Presets are **Midnight Glass**, **Quiet desktop** and **Compact desktop**. They update appearance and dock preferences immediately. Undo restores the appearance snapshot from before the last preset in the current Controls window; it is not a durable multi-step history.
- Presets and undo do not change content, usage/clipboard consent, recent-history preferences or Windows startup. Manual appearance changes made after applying a preset are included in what its undo replaces.
- Controls edits the existing shared state. Autosave remains owned by DesktopEnvironment and the revisioned Core writer. “Save settings now” awaits an ordered snapshot save and reports errors instead of claiming success before acknowledgement.
- “Check & reconnect services” uses the established health/restart budget. It does not forcibly restart a healthy Core or introduce another supervisor. Hardware controls open the existing supervised Control Center with its Core-owned device lanes.
- The view refreshes bound status while active; its timer stops when inactive/minimized/closed. It does not rescan all installed applications, sample CPU or rewrite sign-in settings during construction. Closing the window does not end Nexus, finalize the state writer or close Notes/Files.
- MainWindow.xaml and its type name retain the existing published PRI resource identity. The product title and UI are Nexus Controls; nine old Sections UI partials are deleted.

## Upgrade and retained data

The desktop/Launchpad/menu entry points now expose Controls. `nexus:sections` and `nexus:settings` remain compatibility aliases. Pinned, recent, hidden, app-collection, workspace-app and recent-app-command references to Sections migrate to `nexus:controls`; duplicates are collapsed. An unrelated app merely named “Sections” is preserved. Valid built-in workspace targets accept URI casing.

No settings schema or user data file is deleted. Existing notes are still editable in the standalone Notes window. Saved links, collections, tasks, workspace profiles and paused-focus metadata remain in the existing state and backup; the old Sections-specific editors and active focus timer UI are retired. The desktop date card opens Notes. Existing file tools, Launchpad organization, window layouts and Windows tools remain available from their own surfaces.

Privacy actions are explicit: clearing recent references does not erase saved items or pins; clearing usage/activity does not delete files or notes; clearing the Nexus clipboard shelf does not wipe the Windows clipboard. Optional capture remains off unless the user enables it. Existing Windows startup operations still ask inside the app before making a policy change.

## Validation

Performed in the Linux work environment using the same cached .NET 8 Roslyn/reference-assembly route described in P2.8:

- Portable Core checks: **36 passing groups**, including **59 Nexus Controls assertions** covering all setting destinations, case handling, invalid routes, bounded search, migration/deduplication, unrelated-app preservation, preset/undo isolation and actual settings persistence.
- Portable Runtime checks: **11 passing groups**, using the existing `--no-native-ipc` option for this environment. Windows transport/hardware/job probes remain unexecuted here; AppVeyor is unchanged and still runs native checks.
- Full shell C# API compilation against cached WinUI/Windows projections: passed with projection-version and generated-field fixture warnings. This substitutes XAML-generated declarations and is not a native XAML compilation or package build.
- Source/XML/resource/handler guards and `git diff --check`: passed. The source guard now checks Controls' bounded search/finite viewport, resource identity, native probe coverage and independent lifetime instead of requiring retired Sections grids.
- The native `--verify-ui` probe is extended to construct all six actual Controls pages at 1080 × 780 and 620 × 520 DIP, verify section destinations and responsive navigation, and close the window. It does not execute settings, hardware or sign-in actions. **That probe has not run here.**

AppVeyor, Windows 10 19045 native launch, high-DPI layout, keyboard/focus interaction and visual acceptance remain pending. This source validation does not certify the final Windows appearance or performance.

## Windows acceptance

1. Build the exact PR head with AppVeyor, including the native XAML/resource/UI probes. Start on Windows 10 19045 with the complete published folder.
2. Open Controls from the desktop, Launchpad, Nexus menu and Spotlight. Verify one window instance, correct icon, native minimize/restore/maximize and closing/reopening without stopping the desktop. Confirm Sections is absent from normal UI.
3. Search for glass, dock, clipboard and startup both inside Controls and in Spotlight. Verify each opens and scrolls to its correct setting. Test Ctrl+F, Escape, keyboard navigation and no-result queries.
4. Apply each preset, change a palette and undo. Verify dock density/placement, effects and restored appearance. Keep Notes open while doing this; its contents and privacy choices must remain intact. Close and restart Nexus to confirm persistence.
5. Resize from wide to 620 DIP and the 480-DIP minimum; test 100/150/200% scaling and High Contrast. Confirm page-selector access, scroll reachability, labels, titlebar dragging and visible status messages.
6. Change desktop sort, icon visibility, focus desktop, clock/date card, previews and quiet notifications. Verify the existing surfaces update. In Core, test Save now, service checks and repair, including a failed/unavailable Core and recovery.
7. Test history clearing with disposable records. Cancel must preserve them; confirm must clear only the named category. Capture/clear clipboard only with nonsensitive text and verify no startup consent is changed by a preset.
8. Use a copied legacy settings file containing Sections pins, hidden entries, collection members and workspace app links. Confirm migration persists, duplicates collapse, unrelated apps remain, and notes/saved content are retained.
9. On a disposable VM, exercise the moved startup workflows: cancel, supervised session, startup after sign-in, policy ownership conflict and restore. Existing backup/recovery guarantees must hold.

Keep the PR draft until the owner has assessed the Windows results. Roll back by reverting this commit. Existing stored content remains compatible; migrated Controls shortcuts may need changing back on a version that predates the Controls route.
