# Nexus Shell P2 — Desktop Experience & Rendering Reliability

Development branch: `feat/p2-desktop-experience-batch`
Starting baseline: `487c73f71a834c34f01783a8736d6c39aee427e1` (main, includes merged PR #16).
Status: **IN PROGRESS**. No P2 pull request has been created. These commits are not a released or native-validated build.

## Stage A — Native resizing and desktop recovery (implemented; needs CI)

- `WindowChrome`: cancelled interactive sizing now releases temporary unclipped mode on `WM_CANCELMODE` and `WM_CAPTURECHANGED`, as well as normal `WM_EXITSIZEMOVE`. Otherwise the rounded-region update can remain suppressed after Windows interrupts a mouse sizing gesture.
- Production `LiveResizeRegionPolicy` and portable native-caption checks cover normal completion, capture loss, cancel mode, intermediate size notifications, and custom-clip boundaries.
- `DesktopWindow`: replace the old stuck-on-dispatch-refusal boolean with the existing, production-tested `CoalescedUiDispatch`. One monitor reposition runs per queued callback; a rejected native WinUI enqueue no longer poisons future requests.

## Stage B — Dock preview and window lifecycle polish (implemented; needs CI)

- `TaskbarWindow`: retain a `DockPlacementSnapshot` across positions. Repeated appbar and position notifications with identical visible dock bounds, monitor, reservation and work area no longer dismiss an otherwise valid hover preview.
- `DockPreviewWindow`: an open preview can regain its DWM thumbnail after the app goes minimized → restored or cloaked → visible without being reopened. Never attach while source identity is invalid, minimized, cloaked, reduced-effects/high-contrast or card is closed.
- Extended preview state identity to include accessibility contrast so fallback/native changes are noticed.
- Preserve current native ownership verification, one-thumbnail lifetime per preview, pointer interaction corridor, and static-card fallback.
- `WindowTransition`: native restore notifications cannot cancel an explicit user close that is already animating toward the native close action. Minimize/restore remains reversible.
- Production `DockPlacementPolicy`, `DockPreviewPolicy`, `WindowExitPolicy` and Core checks cover lifecycle and spatial transitions.

## Guardrails

- Windows shell takeover, Explorer coexistence, keyboard shortcuts, appbar registration, process privileges, native HWND ownership checks, settings schema, and file operations are unchanged.
- No injected hooks, screenshot polling or foreign-window mutation.
- Small, bounded changes are committed on this one batch branch; do not open or merge a PR until additional P2 milestones, integration checks and full AppVeyor validation are ready.
- A passing portable policy suite is not a substitute for checking WinUI XAML, native GDI regions or DWM surface pixels. A canceled/pending CI job is not a pass.

## Remaining batch targets

1. Eliminate confirmed redundant chrome/layout work without changing taskbar geometry, DPI or drag handling.
2. Instrument/compare retained dock and window visual costs using opt-in ETW timing on Windows.
3. Review Liquid Glass 2.1 appearance/fallback and remaining black-edge resize artifacts against a real native trace.
4. Complete integration audit and regression gates before opening one substantial P2 PR.

## Consolidated Windows acceptance (later)

1. **Win32 resize recovery:** drag a resizable Nexus Files/Utility window then interrupt with Escape, capture loss, application switch and DPI/monitor migration. Corners should restore at end; no persistent black clipping; moving alone must keep rounded geometry.
2. **Desktop monitor topology:** attach/detach/rotate a display, change DPI and force repeated reposition notifications. A rejected dispatcher request must not strand the Nexus desktop on old bounds.
3. **Preview stability:** hover an app and trigger no-op appbar notifications; preview should remain open. Move the dock or change reservation/monitor: stale preview must close.
4. **Live thumbnail transition:** open preview, minimize/restore the app externally, switch virtual desktop, toggle High Contrast and Reduced Effects. DWM thumbnail may be absent on unsupported sessions, but a readable static fallback must remain.
5. **Native close semantics:** close a Files/Utility window while Windows emits restore/focus/size messages; the close must still run once after its bounded animation.
6. **Build gates:** Core checks, Runtime checks, native diagnostics, XAML compile, WinUI publish, packaging and rollback. Compare ETW and compositor evidence before claiming a measured performance improvement.

## Rollback

This branch is unmerged. Individual stage commits can be reverted on the branch, or the whole branch abandoned while production `main` remains unchanged. No migration or persisted-state rollback is required.
