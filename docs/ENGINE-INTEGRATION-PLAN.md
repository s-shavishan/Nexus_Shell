# Nexus engine integration direction

Requested on 2026-10-11 during Stability Phase 01. This is a researched implementation direction, not a claim that these integrations already ship. The goal is desktop capability: fast retrieval, complete file workflows and recoverable transfers behind Nexus interfaces.

Phase 02 update: 3.6.0 implements the first Everything-backed Search slice, with a Core-side IPC adapter, integrated panel, categories/scopes, desktop commands/windows and failure states. See [Search setup and acceptance](SEARCH-PHASE-02-3.6.0.md). No third-party binaries are bundled, and native Windows/Everything acceptance is still required. Explorer++ and transfer engines remain prototypes to pursue next.

## Candidate decisions

| Capability | Candidate and approach | Constraints |
| --- | --- | --- |
| Nexus Search | Use Everything's documented SDK/IPC for indexed file/folder results. Nexus renders and categorizes results; internal providers supply existing desktop commands, settings and windows. Launchpad remains the app launcher. | Everything must run in the background. Its published license allows redistribution with notices, but inspect the exact release and SDK licenses before bundling. The SDK is an IPC interface, not an independent indexer library. |
| Nexus Files | Prototype a separately built Explorer++ fork with Nexus colors, layout, window controls and desktop entry points. Assess its existing tabs, drag/drop and file operations before committing to a replacement. | Explorer++ is C++/Win32, while Nexus is C#/WinUI: a complete visual port is substantial work. GPLv3 distribution requires notices and corresponding source for the modified covered work. Keep the integration design and distribution licensing explicit; a process boundary alone is not a blanket licensing exemption. |
| Nexus Transfers | Evaluate Ultracopier's open-source engine/plugin design for queue, pause/resume, collision handling and unfinished jobs. A Nexus-owned worker exposes progress and supported actions. Retain Windows Robocopy as a baseline/optional backend where its behavior fits. | Ultracopier is GPLv3 and uses Qt. Validate the precise edition, dependencies and Windows build. Robocopy already supports restartable transfers and multithreading; different engines require different capability flags and result handling. |
| FastCopy | Consider only a separately licensed optional external backend if needed. | Current official EULA requires Pro for workplace/non-domestic use and prohibits reverse engineering. Do not assume free home use grants a source fork or unrestricted product bundling. |
| Text search | Evaluate ripgrep for opt-in searches inside selected text/source folders, with result limits and cancellation. | This does not provide general PDF/Office document extraction. Retain component notices for the exact shipped build. |
| Archives | Evaluate 7-Zip behind Nexus extract/create actions. | Its license combines LGPL/BSD components and an unRAR restriction. Review the exact components used and retain the required notices/source arrangements. |

The repository currently has no root LICENSE file. Decide Nexus distribution licensing before importing GPL source into Nexus-owned projects or distributing a combined modified build. This plan does not change project licensing.

## Integration shape

Nexus owns the search/file/transfer interfaces, categories, shortcuts, themes, accessibility and user preferences. Each engine adapter reports capabilities, readiness, errors and version. Blocking indexing or transfer work runs outside the shell UI process. A missing indexer or a crashed file worker must leave the desktop usable.

Use traffic-light close/minimize/maximize controls on Nexus-owned windows and maintained forks, preserving native window semantics, keyboard access, focus, snap/maximize behavior and accessible names. Avoid foreign-window code injection. A transient search popup may only need Close; don't expose inert minimize/maximize controls.

Search results carry provider identity, stable result identity and a bounded list of explicit actions such as Open, Reveal in Files and Copy path. Cancel superseded queries. Show index readiness and a useful fallback rather than blocking startup on the indexer. Keep results restricted to the intended user-visible scope; use current permissions when opening results and test multiple Windows accounts.

Transfer jobs need persisted identities, explicit conflict decisions, bounded retries, cancellation, progress and an honest terminal state. Retrying after a worker crash must reconcile existing destination files before moving/deleting any source. Never replay a destructive job merely because an acknowledgement was lost. Resume and verification are capability-specific, not universal promises.

## Delivery order and gates

1. Finish the 3.5.1 native acceptance pass and preserve its rollback build.
2. Deliver one end-to-end Everything-backed Nexus Search slice, including missing-engine/index-building behavior, categories, keyboard operation and results opening in Files. Keep third-party packaging/version checks reproducible.
3. Build an Explorer++ proof of concept as an independently supervised Files replacement. Compare tabs, drag/drop, rename, shell namespaces, DPI/accessibility and recovery against current Nexus Files before changing defaults.
4. Integrate a transfer queue behind existing desktop file operations. Exercise real copies, collisions, full disks, disconnects, cancellation and restart recovery on disposable data before enabling moves or unattended jobs.
5. Add scoped text search and archives after their data-integrity and permission cases pass.

Record exact upstream tags/commits, hashes, licenses and local patches for every bundled dependency. Retain a small upstream delta and test updates before advancing it. Reuse provides capability; reliability still depends on the Nexus integration and Windows acceptance results.

## Primary references checked

- [Everything SDK](https://www.voidtools.com/support/everything/sdk/)
- [Everything published license](https://www.voidtools.com/License.txt)
- [Explorer++ source and features](https://github.com/derceg/explorerplusplus)
- [Explorer++ GPLv3 license](https://github.com/derceg/explorerplusplus/blob/master/LICENSE)
- [Ultracopier source, license and features](https://github.com/alphaonex86/Ultracopier)
- [FastCopy current EULA and command line](https://fastcopy.jp/help/fastcopy_eng.htm)
- [Microsoft Robocopy options](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy)
- [ripgrep source](https://github.com/BurntSushi/ripgrep)
- [7-Zip license](https://www.7-zip.org/license.txt)
