# P3.0 — Nexus Shell reliability-first acceptance tracker

**Baseline:** `main` commit `5cd41d1259fe158eb7e227989f8b28b1371ea62c` (P2.9 squash merge). User reported P2.9 "implemented quite well but needs more attention"; **no blanket P2.9 acceptance** is implied. Main AppVeyor commit status was **error** at inspection; the build's first error message is not available through the linked GitHub status. Do not mark main clean without the actual successful main build.

## Prior progress must be preserved

The user's earlier Windows 10 VM test confirmed these specific improvements, **on the older build only**: Windows Tools dialog became clickable/closable, first-session floating dock bottom gap disappeared, menu bar stopped retaining closed-window titles, visible dock scrollbar disappeared, and Files/Desktop Ctrl/Shift/marquee selection worked. Do not regress these fixes; do not generalize the old VM result to new builds, Windows 11, or all hardware.

## Current user feedback

- P2.9 desktop UI is materially better, **but not finished or accepted**.
- In an earlier P2.9 build, **Drag → Snap → Stage → Apply → Undo did not work**; a fix was merged, but subsequent main-branch behavioral acceptance has not been reported.
- The user explicitly reserves confirmation of other new features until full **main-branch testing**.
- Priorities remain reliability and real Windows integration—not visual-only gadgets or artificial controls.

## Priority queue, in order

| Priority | User-visible acceptance gate | Status |
|---|---|---|
| P0 | Restore AppVeyor main build to green, identify actual first failing stage and verify release package. | **Open:** GitHub main status reports error; need the AppVeyor console failure lines. |
| P0 | Native overview: drag preview, all snap zones, stage before movement, Done moves original HWND, reopen/Undo restores, Escape/Cancel never apply. | **Unverified:** prior VM failure, P2.9 source corrections merged. |
| P0 | Dock pin identity: built-in Files and Windows Calculator, no duplicate running icon; foreground indicator updates immediately; second app window remains accessible. | **Source fix in P3.0 draft**, native test pending. |
| P1 | Dock DWM previews: correct windows/minimize and reliable fallback; avoid spurious "DWM Notification Window". | **Unverified**, VM/real Windows checks needed. |
| P1 | Explorer remains running behind Nexus; native Windows notifications/calendar/date-time reachable; Win10 and Win11 have suitable fallbacks. | **Source routes exist, hardware acceptance pending.** Do not kill Explorer or capture system shortcuts. |
| P1 | Control Center/network/display/Bluetooth: genuinely operable; compact; hide unsupported hardware brightness/Bluetooth; native display preview/rollback safe. | **Partly implemented, unverified on target hardware.** |
| P1 | Dock placement: floating dock reserves no unwanted work area; maximized windows don't leave a bottom gap after cold login/Explorer restart. | Older VM pass, **new release retest required**. |
| P1 | Right-click menus on desktop, Files, apps, menu bar and dock remain compact, correct for content type and responsive; undo and file operations must be real. | Source partially implemented; universal handlers and advanced batch operations **not complete**. |
| P2 | Liquid Glass, traffic-light chrome, native resize, odd key-hint/tooltip popups, frame hitching, dark stacked bevels. | Visual tuning and diagnostics still **unaccepted**. |
| P2 | Complete dual Windows 10 19045 / Windows 11 acceptance, scaling/high-contrast, multi-monitor layouts and Explorer recovery. | **Open**. |

## P3.0 dock identity source changes

1. **Files pin:** `nexus:files` now recognizes the supervised `Nexus.Shell` browsing worker and its initial `My files · Nexus` caption or updated `<folder> · Nexus Files` caption. It deliberately does not merge a foreign `explorer.exe` window or a file-picker's unrelated initial caption.
2. **Calculator:** a pinned Windows `System32/calc.exe` launcher can claim its real packaged `CalculatorApp` window. No fuzzy title-only association, user-directory `calc.exe`, or unrelated process merge.
3. **Pinned indicator:** the fast foreground-only event path now updates pin active state using the observed HWND instead of waiting for a full dock scan; it avoids one duplicate `ForegroundTaskWindow` query on normal state updates.
4. **Regression:** extended portable `DockChecks` with case-sensitive boundaries for those identities, including negative cases.

These source changes were checked by 10 targeted cross-file assertions. **Those are NOT a .NET compile or live Windows test.**

## Required Windows acceptance for the dock follow-up

1. Run the successful P3.0 AppVeyor branch package in a fresh Windows 10 VM snapshot; open built-in **My files**. Verify the pinned Files icon receives the running indicator without a new duplicate icon. Navigate into several folders and back.
2. Open multiple Files windows if supported. Preserve a separate reachable additional window rather than hiding legitimate documents.
3. Pin the native Windows Calculator app / System32 calc.exe, launch it, verify the pinned icon claims its running window; try an unrelated app named Calculator to check false matching.
4. Switch foreground rapidly between Files, Notepad, browser and desktop; pinned running and active indicators must update on each switch without an extra 50-ms structural scan.
5. Minimize/reopen pinned apps; test close/relaunch; inspect dock tooltip and accessibility title for stale states.
6. Retest fullscreen overview, native notification/calendar, Bluetooth absent/present, window resizing, contextual file menus, glass and first-login dock/work-area behavior on **main** before release acceptance.

**Merge gate:** do not merge this draft unless a real AppVeyor Windows build passes and the user explicitly directs the merge. P2.9 remains the main test baseline until then.
