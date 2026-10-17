# Files workspace

Use [Quick Look](QUICK-LOOK.md) with Space to preview selected files or captured visible neighbours. Its dialog keeps the owning tab stable while active and releases reads/playback on close.

Nexus Files supports up to 12 folders in one supervised browser window. Each open tab owns its own navigation trail, listing, filter, selection, sort, grid/list choice, inspector and edit Undo history. Switching tabs preserves that state. File operations continue through the existing Core workers and Windows clipboard; tabs do not introduce another mutation engine.

| Action | Control |
| --- | --- |
| New tab at the current location | `+` or Ctrl+T |
| Open a selected folder in a new tab | Context menu or Ctrl+Enter |
| Next/previous tab | Ctrl+Tab / Ctrl+Shift+Tab |
| Close a tab | Its close button or Ctrl+W |
| Reopen last closed location | Reopen button or Ctrl+Shift+T |
| Back/forward within the selected tab | Alt+Left / Alt+Right |

Closing the last tab closes the Files window. Escape retains the existing window-close behavior. Reopen remembers the last 12 closed locations in this Files session; it starts a fresh view of that location. Open tabs retain their full view state. Tab locations are not written to disk or restored across process restarts.

Desktop, Search and Places requests activate an existing tab at the requested folder, or open a new tab. Matching locations are refreshed. If all 12 slots are occupied, a visible message asks you to close one. Finish/cancel a dialog or file mutation before changing tabs; a visible message explains a refused switch. Existing Open, Save and folder pickers have no tab controls and keep their selection contract.

## Errors and resources

Each tab reads through its own existing bounded navigation/preview queues. A folder read that completes while its tab is hidden only updates that tab. Closed tabs cancel their reads and release previews, timers and views; no completion can populate another tab. Only the selected view is attached to the visual tree, and hidden clipboard polling stops. Each listing retains the existing 1,000-entry limit. There is no automatic drive scan or permission elevation.

Missing/offline/denied locations report their error within the requested tab. Previously opened tabs remain usable. Filesystem access uses the signed-in Windows account and existing filtering/selection/transfer validation.

## Acceptance and manual demo

Use the AppVeyor package on Windows 10 19045 and Windows 11. The portable model checks and C# API compilation do not establish native UI acceptance.

1. Open Documents. Navigate into a child and back. Open Downloads with Ctrl+T plus the address field; apply a filter, choose List, sort by Size and select items. Switch repeatedly: each tab must retain its own history, filter, sort, selection and view.
2. Right-click a folder and choose **Open in new tab**; repeat with Ctrl+Enter. Only folders open in tabs. A normal double-click still navigates the current tab; file launching remains unchanged.
3. Copy in one tab, paste in another and observe File Jobs. Repeat Cut/Paste and create/extract ZIP on disposable data. Finish/cancel a rename/confirmation dialog before switching; targets must stay those originally captured.
4. Close the first/middle/last tab, cycle in both directions, then reopen closed locations in reverse order. Close the final tab: the browser worker must exit normally. Opening Files again creates a fresh session.
5. Fill 12 tabs. Attempt a 13th through desktop/Search and folder context actions. Display a capacity message without replacing any tab. Close one and retry successfully.
6. Open a slow, removed or inaccessible folder and rapidly switch/close tabs. Late results must remain in their originating tab or be discarded after close. A denied folder must not clear another tab.
7. Open the same folder through Places/Search: activate the matching tab without creating a duplicate. Test independent Open/Save/Folder pickers while the browser has tabs; selections and cancellations must remain isolated.
8. Resize to 500 DIP, test horizontal tab scrolling, keyboard-only focus, 100–200% DPI, High Contrast and reduced motion. Tab titles/close actions need accessible names and selected-state contrast.

Suggested native recording: Documents and Downloads with different filters → Ctrl+Tab → cross-tab Paste/File Jobs → close/reopen → separate Save picker. Capture the real window on Windows; source-derived mockups are not execution evidence.
