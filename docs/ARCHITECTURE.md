# Architecture

Nexus Shell runs on Windows 10 build 19041 or newer; the primary Windows 10 target is 19045. It uses C#/.NET 8 and WinUI 3. This repository is independent of the Electron Nexus application and the separate universal-compatibility OS concept.

## Processes and dependencies

| Project | Responsibility | References |
| --- | --- | --- |
| Nexus.Runtime | Typed IPC contracts, sole-writer persistence, session policies, feature coordinators and transfer/search/monitor logic | .NET libraries |
| Nexus.Core | Owns settings/persistence and supervises native file/search/settings workers | Nexus.Runtime |
| Nexus.Shell | Desktop UI; also supplies isolated Files, Control Center and native file-dialog workers | Nexus.Runtime |
| Nexus.DesktopHost | Supervises the shell session and restores Windows independently when needed | Nexus.Runtime |

No project compiles source files from a sibling project's directory. Shared state and session policies live physically in Runtime; device backends live in Core. Some shared types retain their established `Nexus.Shell.Models` and `Nexus.Shell.Services` namespaces for compatibility. Directory ownership, project references and assembly identity are authoritative.

## Source map

Bulk rename keeps rules/preflight, immutable receipts/Undo and admission in Runtime. Shell's Windows adapter verifies opened file/parent identities and performs no-replacement sibling renames; its native Files dialog owns presentation and confirmation. Receipts are session-only unless explicitly exported, and no mutation is automatically replayed. Existing settings/RPC contracts and single-edit Undo remain separate.

Startup inventory uses the Shell's Windows adapter and Runtime's typed coordinator/journal. Only confirmed supported current-user Run edits are mutable; original registrations live in a separate account-bound journal beside settings. This feature does not change the Core settings writer, desktop sign-in policy or IPC schema.

| Location | Content |
| --- | --- |
| Shell/App.xaml.cs | Process role selection and desktop startup |
| Shell/MainWindow* | Nexus Controls XAML window and partial implementations |
| Shell/Desktop/Session | Desktop environment, isolated worker environments and feature entry points |
| Shell/Desktop/Windows | Window hosts for the dock, menu bar, panels and utilities |
| Shell/UI | Views, controls, menus, themes, motion and native materials |
| Shell/Services | Policies and presentation models grouped by subsystem |
| Shell/Interop | Native APIs grouped into desktop, windowing, files, input and monitoring |
| Core/Settings | Windows sound, network, display, Bluetooth and power backends |
| Core/Windows | Isolated native file menus, dialogs and recycling |
| Core/Transfers, Core/Search | Windows transfer-source handles and Everything transport |
| Runtime/Models, State, Appearance | Shared data, settings persistence and appearance rules |
| Runtime/Session | Startup/sign-in ownership and Windows desktop restoration policies |
| Runtime/Protocol, ControlCenter, Files, Settings | IPC and supervised feature coordination |
| Runtime/Transfers, Search, Monitoring | File jobs/ZIP engines, search policy and monitor logic |

UI and native interoperability belong to Shell/Core. Keep Runtime free of WinUI references. UI requests go through existing typed coordinators rather than performing privileged work directly. Core keeps settings as the sole authoritative writer; views must not create competing profile writers.

## Boundaries to preserve

- Explorer remains running behind the Nexus desktop. Windows owns native shortcuts, calendar, notifications and application compatibility.
- The host owns managed startup, recovery journals, policy ownership and restoring the original Windows desktop. Closing Controls or a utility does not end that session.
- Files and native shell handlers retain their separate supervised process roles. UI cancellation must not silently replay an admitted native operation.
- File jobs retain immutable receipts, bounded single-worker execution, collision handling, staging, verification and restart review. Cleaning source does not remove data-integrity checks.
- Process controls retain PID/creation identity, token/session and critical-process checks. Unknown or protected targets remain refused.
- Settings formats, enum values, mutex/pipe identities, native window classes and resource URIs remain stable across the directory reorganization.

The source tree has no test-mode UI constructors or test command-line entry points. Runtime validation, capability checks, logging, cancellation and recovery are production behavior and remain included.
