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


## P3.0 continuation — retain device controls after failed mutations

Previously any unsuccessful non-Display `QuickSettingsView.ExecuteAsync` request cleared `_state` and rebuilt an error-only page. That meant an invalid Wi-Fi password, transient WLAN API failure, denied Power plan switch or Bluetooth radio error removed all connection controls, forcing the user to navigate or refresh.

- A new `ControlCenterTilePolicy.RetainPanelAfterFailure` holds on to the last known-good state **only for an actual mutation error** (Network, Bluetooth, Sound, Power) or for Display, where native Keep/Revert must survive a read error.
- Existing controls remain visible beside a truthful device error and a manual Refresh action. No failed native command is automatically replayed, and the queued pending commands are cleared.
- A failed **initial read** still shows the error-only placeholder; Nexus never fabricates device capability state from a missing snapshot.
- Display preserves its prior independent auto-rollback and explicit Keep/Revert handling.
- Portable Core regression tests cover normal mutation failure preservation, Display read failure, initial-read failure, and false preservation for non-Display reads.

**Windows acceptance:** In a Windows 10 VM, attempt a deliberately invalid Wi-Fi connection, unavailable adapter action or denied Power change. Check that the error is visible, the device controls remain interactive, Refresh works, and there is no automatic retry storm. Confirm failed Display preview still keeps its Keep/Revert surface and automatic rollback timer independent from UI.

**Source gate:** 18/18 targeted consistency assertions passed for native Wi-Fi actions, compact/network policies, error recovery policy, portable tests and preserved pinned hover. **This is not a .NET build**.


## P3.0 — Self-healing pinned shortcut identity after Windows recovery

A native dock defect was found in `TaskbarView.QueueShortcutIdentity`: an installed application's Start Menu `.lnk` was resolved on an STA thread **exactly once**. An empty result—whether from Explorer startup, a transient COM failure, an incomplete or temporarily unavailable shortcut—was cached for the rest of the Nexus session, so its running app could remain a duplicate unpinned icon until restart.

- New `DockShortcutRetryState` is a portable, timer-free monotonic backoff policy: retry 2 seconds after the first failure, then 10, 30, 60 and 120 seconds (capped at two minutes). Persistently unavailable shortcuts are retried at a low frequency instead of causing a per-frame COM storm; tick overflow saturates safely.
- Shortcut resolution stays on background STA threads. The existing `_pendingShortcuts` lockout ensures **one in-flight resolution per shortcut**, including simultaneous dock/foreground refreshes.
- The existing retained dock refresh lane checks only unresolved pinned `.lnk` targets against the retry deadline. It never reads shortcut content or performs COM on the foreground/UI thread and introduces no new native event or polling timer.
- Success stores a valid executable path, clears failure backoff, immediately reconciles pin artwork and active markers. Failure schedules another bounded attempt **without storing an empty identity**.
- Removal of a pin prunes its retry record, and an async resolution completing after unpin does not recreate state or attach an unrelated app to that pin. Dock release clears all retry/pending/cached state.
- Tests validate first-attempt admission, 2/10/30/60/120-second delays, a bounded failure counter, eventual retry, and no clock overflow. Tests do not simulate a live COM failure.

**VM acceptance:** Pin a `.lnk` app (e.g. browser), launch it while Explorer/Start Menu shortcuts are still settling, verify no lasting duplicate icon, then retry after a transient shortcut lookup failure or Explorer restart. Remove/re-pin during an in-flight lookup, and verify a closed or removed pin never reappears. Repeat on Windows 10 and 11 and keep hover smooth under 130+ running windows.

**Status:** Source change committed on draft PR #33; native compile/VM acceptance is not yet established for this follow-up.


## P3.0 — DWM dock preview first-layout recovery

**Root cause identified from production code**: `DockPreviewWindow.Show` can call `WindowThumbnail.TryCreate` immediately after `AppWindow.Show`, *before* WinUI measures the viewport. Previously `RefreshState` accepted non-null DWM registration as an active thumbnail, reset its retry state, and could leave an unconfigured thumbnail attached. With no new `SizeChanged` callback on subsequent openings at an unchanged card size, the user could observe a blank/incorrect preview even when `DwmRegisterThumbnail` succeeded.

**Implementation**:
- `WindowThumbnail.HasDestination` now reflects actual successful DWM destination-property configuration, rather than successful registration alone. It resets on Dispose.
- `DockPreviewWindow.RefreshState` revisits attached-but-unconfigured thumbnails on existing pointer/health refresh ticks, using the same thumbnail instead of registering another one. Initial `Loaded` now re-enters the refresh path.
- `DockPreviewPolicy.ShowFallback` keeps the accessible static card visible until the DWM relationship **and its destination** are ready. A registration with unknown viewport dimensions is not advertised as active.
- If setting the native thumbnail rectangle fails, existing disposal and bounded retry behavior remains available; minimized, cloaked, high-contrast and reduced-effects cases still use the accessible fallback.
- Portable DockChecks verify all four combinations of registration/destination readiness. Existing Windows-native `DesktopNativeChecks` now verify registered → configured → disposed states on owned test windows.

**Acceptance on Windows 10 VM / Windows 11**: hover an ordinary running icon and a pinned running icon immediately after opening Nexus, before any manual resize. Open/close the preview 20 times without changing its size. Confirm DWM renders content where supported, otherwise retains an accessible static card; no invisible blank panel. With VirtualBox DWM restrictions, accept the fallback—not a fabricated screenshot. Repeat at 100%/150% DPI, while rapidly minimizing/restoring the target, and after Explorer restart.

**Status**: committed to draft PR #33; actual WinUI interactive rendering remains a VM test, distinct from successful native DWM registration assertions. Merge requires a new green AppVeyor head and user approval.


## P3.0 — Overview reopen and DWM recovery (2026-10-15)

Source review found two independent interaction defects:

1. **Reopen with unchanged HWNDs:** `SwitcherWindow.Show` clears selections and resets `_selectionInitialized`. A subsequent `RefreshAsync` compared against the previous snapshot and returned early when unchanged, before reconstructing selection and cards. The same two apps therefore appeared to work on the first Overview opening but Grid/Columns/Stack could become disabled on the next opening. `WindowBoardPolicy.ShouldRenderSnapshot` now treats an uninitialized presentation as needing a rebuild regardless of unchanged HWNDs. Portable tests cover all four changed/unchanged × fresh/initialized combinations.
2. **One-shot Overview DWM previews:** Preview cards previously recorded `Attempted=true` after the first `DwmRegisterThumbnail` attempt. A temporarily unavailable compositor or app, or a late WinUI viewport layout, could leave Overview showing a static card indefinitely. Preview cards now use the already-tested `DockThumbnailRetry` backoff policy; DWM is retried on existing layout updates and a 700-ms heartbeat **only while Overview is visible**. Actual process ownership, minimization, visibility, cloaking, reduced-effects and high-contrast safeguards are preserved. The fallback is hidden only when the DWM relationship is configured with valid destination geometry, not when registration merely succeeded. The Save Layout flyout suppresses DWM reattachment while editing, and OnHidden/OnClosed stops the health timer and disposes native thumbnails.
3. Tests now assert retry timing/reset plus the deterministic reopen policy; no simulated WinUI pointer input is mislabeled as a successful manual test.

**Windows acceptance**: Open Overview with Notepad and Files; confirm Grid/Columns/Stack available. Close without changing any windows and reopen at least five times; the initial selection and controls must still work. Drag a preview, snap near an edge, confirm only staging, apply, then reopen and Undo the real HWND placement. Test Save Layout flyout while opening/closing live DWM previews. Minimize/restore window, toggle fast/reduced effects and high contrast, and retry under a VirtualBox DWM-unavailable compositor: accessible fallback must always remain usable. Repeat at 100/150% DPI.

**Release gate**: 15/15 targeted source assertions matched production and portable tests after this edit. The precise AppVeyor head and actual Windows interaction remain separate gates; do not merge PR #33 automatically.


## P3.0 — Bounded shortcut completion and rescan recovery (2026-10-15)

A second shortcut-identity failure existed after the retry work: `TaskbarView` removed a target from its pending set only inside `DispatcherQueue.TryEnqueue`. A rejected callback discarded both successful and failed results while retaining the pending marker forever. Successful targets were also retained after unpin and explicit installed-app rescan, so editing or replacing a shortcut could keep its previous executable identity.

- `DockShortcutIdentityCache` owns admission, retry and successful targets on the desktop dispatcher. Background STA workers only publish to a completion mailbox. The ordinary retained `RefreshWindowStates` lane drains it; an immediate dispatcher wake-up is optional, so a rejected wake-up cannot strand admission.
- At most **two** shortcut workers may be admitted per dock, with one worker per case-insensitive shortcut path. Completed workers keep their slots until drained. Unpinning, re-pinning and rescanning never pretend to cancel an in-flight COM call or release its reservation early.
- Completion ownership uses the exact admitted request instance. Removed or invalidated pin generations cannot publish into a replacement pin, and duplicate late results cannot free a newer request's slot.
- Ordinary pin refresh retains valid identities. Unpin evicts successes and failures. Explicit installed-app rescan invalidates successful targets as well as artwork and rereads the current shortcut after any old worker actually finishes.
- Existing 2/10/30/60/120-second retry backoff, local `.lnk` restriction and background STA resolution remain. No new timer, UI-thread COM call or process launch is introduced.
- Shutdown closes the completion mailbox and discards late results. A stuck native COM call cannot safely be aborted; it retains one of the two slots until it returns, without expanding into unbounded threads.

**Validation performed on Linux:** 39 portable Core groups and 13 portable Runtime groups passed. The new production-cache suite includes **1,220 assertions** and 200 recovery generations with real background completion publication. Runtime and portable checks compiled with warnings as errors. Full Shell C# API compilation passed against cached Windows/WinUI projections with generated-XAML field fixtures and their documented projection/fixture warnings; this is not native XAML compilation. Source/resource guards and whitespace checks passed. Runtime used the existing `--no-native-ipc` option; native transport, hardware and Windows-only checks were skipped.

**Windows acceptance additions:** Pin several local Start Menu `.lnk` apps and launch them during Explorer recovery. Repeat unpin/re-pin while a lookup is pending. Change a shortcut's executable, perform the explicit Launchpad installed-app rescan, and confirm the old app is no longer claimed by that pin and the new app is matched. Exercise more than two pins together, permanently broken shortcuts, normal pin metadata edits and shutdown during lookup. Confirm hover and keyboard previews, extra document windows and running indicators remain correct. A manual test cannot reliably force a dispatcher rejection; the portable completion test covers recovery without invoking any wake-up callback.

**Status:** implementation and portable validation on draft PR #33. Exact-head AppVeyor, native WinUI/XAML, packaged Windows 10/11 behavior and owner acceptance remain pending. No merge or Windows VM result is claimed.


## P3.0 — Recoverable Apply/Undo transactions (2026-10-15)

A source audit found that the previous Overview follow-up confused `ShowWindowAsync` with `ShowWindow`: it discarded a rejected maximize request. [Microsoft documents nonzero as successful operation start](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindowasync), not previous visibility. This section supersedes earlier notes making that claim; old commits remain historical evidence, not the current API contract.

- The production `WindowLayouts` transaction now uses an injectable native adapter. Native code checks maximize admission; the portable suite exercises the same transaction with rejected native operations, without issuing Windows commands on this host.
- All requested windows pass ownership, resize capability and placement-capture preflight before any window changes. Requests are snapshotted; duplicate HWNDs, excessive counts, zero/negative dimensions and overflowing target rectangles are rejected.
- An interrupted Apply requests restoration **only for attempted windows**, in reverse order. It no longer sends SetWindowPlacement to unrelated later entries in the batch. HWND/PID identity is checked again between operations and before restoration.
- A failed restoration retains its exact previous placement for **Retry previous positions**. Further staging/Apply is blocked until those entries are retried or their original windows are observed unavailable. Recovery after a failed new arrangement preserves the preceding successful Undo history.
- Rejected ordinary Undo operations are also promoted to recovery, preventing a later Apply from overwriting their only snapshot. Closed/reused HWNDs are counted and removed without intentionally commanding their replacement owner; accepted restorations are not replayed during the next retry.
- Overview distinguishes restoration being requested, failures needing retry and windows that closed/changed. Cancel discards staged edits and closes the panel; it does not claim to reverse an already partially processed native operation. Recovery snapshots remain in the live Overview instance across hiding/reopening, not across process restart.

`WPF_ASYNCWINDOWPLACEMENT`, asynchronous SetWindowPos and ShowWindowAsync continue to avoid waiting for another app's window thread. Accepted requests do not certify completed geometry, and native multi-window movement is not atomic. Normal placement snapshots stay in Windows workspace coordinates; SetWindowPos targets remain in screen coordinates. No force-focus input, thread attachment or synchronous waiting was added.

**Validation:** 40 portable Core groups and 13 portable Runtime groups pass, including **1,199 new placement transaction assertions** covering rejection at every native mutation stage and every batch position for one through twelve windows. Tests also cover partial rollback, retries, retained earlier Undo, ordinary Undo exceptions, closed/reused HWNDs, preflight, native struct size, mutable request isolation and invalid bounds. Runtime/Core-check compilation uses warnings as errors. Shell C# API compilation passes with the cached projection/generated-XAML fixture limitations. Native WinUI/XAML/AppVeyor and Windows interaction remain separate acceptance gates.

**Authored Windows fixture (not executed locally):** its own native test HWND is minimized, staged/maximized through production Apply, then undone; the fixture checks actual maximized/minimized states. It never moves a user's app. Existing real Apply/Undo geometry and ownership fixtures remain.

**Manual acceptance:** On Windows 10 19045 and Windows 11, stage normal and minimized/maximized apps across monitors at 100/150% DPI; apply and reopen/Undo. Close an app after staging, test a denied/elevated or unresponsive app, and verify errors retain retry controls without touching later untouched apps. Reopen Overview after Cancel during a pending restoration and verify Retry remains available. Check Undo failures do not destroy prior history, no replacement HWND is deliberately commanded, UI remains responsive and actual geometry is checked after asynchronous completion.
