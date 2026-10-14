# P3.0 — Nexus Shell reliability-first acceptance tracker

**Baseline:** `main` commit `5cd41d1259fe158eb7e227989f8b28b1371ea62c` (P2.9 squash merge). User reported P2.9 "implemented quite well but needs more attention"; **no blanket P2.9 acceptance** is implied. The latest observed AppVeyor **branch check succeeded** for this main commit (build 54897165); manual Windows 10/11 acceptance and artifact installation remain open.

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
| P0 | Restore AppVeyor main build to green and verify the test archive is deployable. | **CI green** at build 54897165; artifact deployment and manual VM run still pending. |
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


## P3.0 continuation — pinned app previews and multiple documents

**Additional source implementation (not VM-certified):**

- Confirmed a usability gap in P2.9: the dock hid the representative running-window icon once claimed by a pin, but hover previews were attached only to the visible running-window icon buttons. The pin had no equivalent hover or keyboard handler, so a successfully paired app lost easy preview access.
- Pin icons now participate in the **existing** delayed dock preview pipeline. Hovering an active pin resolves its representative HWND at the time the timer fires, checks native process ownership, uses the same screen-pixel anchor and pointer corridor as an ordinary running icon, and opens the existing DWM preview/fallback. Down-arrow on a focused pin opens the same preview with keyboard focus.
- To avoid lost secondary documents, the pin context menu now populates a live **Open windows** submenu for up to twelve owned, matching HWNDs. Entries activate a specific window; the representative remains merged into the pin while additional document windows retain separate dock buttons. The menu never launches an arbitrary process.
- The bounded identity policy is portable and verified with explicit checks for duplicate (HWND, PID), other-process spoofing, caps, and invalid capacities.
- Dock hover requests are cancelled when pins are rebuilt or the dock is disposed; retained WinUI hover state does not reference an unloaded pin.
- No new DWM bitmap capture or polling is introduced, and the pinned hover uses the already implemented live DWM thumbnail with an accessibility/driver fallback.

**Acceptance additions:** With pinned Firefox/Files/Calculator, hover a running pin and confirm the correct preview after a short delay; press Down on focused pin, then Escape to dismiss; check Alt+Tab/focus, minimized-window fallback, context-menu Open windows activation, 15+ windows, and remove/re-pin during pending hover. Repeat in Compact Dock, 125%/150% DPI and Windows 10/11. Live DWM previews can still be unavailable under VirtualBox—validate fallback instead of claiming hardware support.

**CI status:** The existing main-branch build has not been independently verified green as of this edit. GitHub's status API reported a pending main build after an earlier error, and AppVeyor's console was not externally readable. Use GitHub's latest CI status and the first failing AppVeyor console line before calling P3.0 testable; this document does not claim compile success.


## CI checkpoint and pinned hover stabilization

**Updated baseline:** GitHub's AppVeyor branch check reports **success** for `main` commit `5cd41d1259fe158eb7e227989f8b28b1371ea62c`, linked to build [54897165](https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54897165). This clears the previously listed baseline CI uncertainty, **not** the user's manual VM acceptance requirements.

The follow-up also now:
- Suppresses pinned-app WinUI tooltips **while native preview cards are enabled**, matching the existing running-icon policy. Screen-reader automation names always stay available and indicate **Open / Switch to / Minimize / Restore** based on the observed native state.
- Uses native ownership checks before drawing a pinned running indicator or opening a preview, so stale HWND/PID pairs do not masquerade as active.
- Populates the **Open windows** submenu in `MenuFlyout.Opening`, before the flyout is measured; the opened event only manages preview dismissal and visibility retention.
- Keeps a single pending hover request per dock: moving from pin to running icon (or vice versa) cancels the previous request. Pin rebuild and release cancel outstanding requests.
- Adds portable policy tests for action labels, 18-window menu input, deduplication, 12-entry cap, process spoofing and invalid limits.

**Remaining quality gate:** P3.0 is still source-only until branch AppVeyor, Windows 10 VirtualBox and Windows 11 interactive verification. In particular validate no hovering popup remains after moving between pins and running icons, and no preview is attempted for an invalid or closed HWND.


## P3.0 continuation — functional, compact Network control panel

The old Network page constructed one full card for every Windows network interface (including disconnected virtual adapters) and an unbounded vertical stack of Wi-Fi scan results. On systems with multiple virtual NICs, this made the active connection difficult to locate. The new source:

- Sorts native adapters deterministically with **all connected interfaces first**; keeps those and up to two adapter rows visible. Remaining down/offline adapters are available through an explicit **Show other adapters** disclosure, not discarded.
- Orders nearby Wi-Fi entries by connected state then signal strength; shows six first and provides **More networks** to disclose the remaining rows. Existing Windows-native Connect, Disconnect, WPA2 join, Forget with confirmation, radio toggle and scan requests remain wired to Core.
- Offers single-row adapter tools for native Windows Network settings and the explicitly allowlisted Network Adapters applet. Address/gateway/DNS details and Copy IP remain available in each adapter card without consuming default vertical space.
- Uses pure `NetworkPresentationPolicy` linked into `Nexus.Core.Checks`; tests active-first order, all connected interfaces staying visible under 8+ active adapters, connected-first Wi-Fi ordering, and six-row threshold.
- Does not create a fake Wi-Fi implementation when no radio exists. The existing native WLAN unavailable message and wired adapter tools continue to work.

**Windows acceptance**: Test Ethernet-only Windows 10 VM, real Wi-Fi with saved/unsaved WPA2 profiles, 10+ virtual/disabled NICs and 10+ nearby SSIDs. Open **More networks**; ensure a refresh doesn't corrupt the panel or submit a command unexpectedly. Verify failed native connection requests show errors, and password text clears after submission or cancel. Do not claim secure Wi-Fi pairing or hardware support without real tests.

**Current test status**: source and portable policy checks added; complete .NET build / Windows UI acceptance for this revision is pending.
