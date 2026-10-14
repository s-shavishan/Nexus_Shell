# P2.9 — File-type-aware Nexus context menus

**Status:** Source implemented on draft PR #32; not merged; native Windows build and VM tests remain pending.

## Purpose

Replace the fixed, increasingly long file right-click menus with a compact
profile-driven surface that works in **Nexus Files** and on **desktop shortcuts**
without replacing Explorer's own file-association system or the existing Core
file-operation workers.

The large external AI-generated reference catalog was intentionally *not*
loaded verbatim. It contains 37 proposed profiles, some non-existent handlers,
and 16 extensions with conflicting categories (including `.ts`, `.mdf`,
`.h5`, `.rom`, `.rm` and `.ptx`). Its mappings and universal action list
are design inputs rather than authoritative Windows associations.

## Implemented boundary

- `src/Nexus.Shell/Assets/Configuration/file_context_profiles.json`:
  15 conservative, unique-extension profiles, about 190 selected extensions,
  plus a safe fallback. Examples: text, documents, raster images, design,
  audio, video, source, structured data, archives, programs, certificates,
  models, libraries, game assets and Windows shortcuts.
- `FileTypeContextCatalog`: C# strongly typed profile parser and immutable
  lookup cache. Exact case-insensitive compound suffix matching; a copied
  `bundle.TAR.GZ` resolves before the shorter `.gz`. Unknown/ambiguous
  extension => `fallback.unknown`. Folder is a separate explicit profile.
- The JSON contains **no executable commands** and **no handler DLL names**.
  Only five audited action IDs are accepted (open, play, run, edit,
  open_with). Invalid schema, duplicate profile IDs, duplicate extension
  ownership, unknown action IDs, excessive size, or unsupported extensions
  cause rejection; keep the previous valid loaded catalog. FileSystemWatcher
  validates updates off the UI thread. No disk scanning during a popup.
- `TypedFileMenu` is a shared renderer with typed handler IDs. Maximum seven
  **actionable top-level entries**; Organize, Transfer and Advanced group
  working operations. The first single-item action is the profile default.
  No dead/decorative entries for missing capabilities.
- **Nexus Files:** Open/Play/Run through the user's registered Windows
  application; native Edit verb where appropriate; existing Windows
  Open With/Properties/Reveal/Windows Options worker; Core Copy-to and
  Move-to transfers; Rename through existing Files edits; Copy path(s)
  through the clipboard. Existing Ctrl/Shift selection is retained. Mixed
  selections have no mass-open/run; available batch transfers use existing
  Core validation. More than 100 items returns a clear limit instead of
  attempting unbounded popups/transfers.
- **Desktop:** Open, Rename, Copy path, Properties and Explorer/Windows
  options retain existing handlers, as do Open With on supported types
  and Pin folder to Places. Nothing changes Windows-native application's
  own right-click menus.
- **Fallbacks:** no file type ever has an empty default profile; native
  Windows Options remains available for native extensions/actions that
  Nexus does not implement itself.

## Intentional exclusions

The reference actions Convert, Compress, Encrypt, Sign, Extract, Mount,
Threat Scan, Hash and mass Delete are *not* mapped to shell commands.
No Nexus Core capability is currently registered for all of those
operations. Do not introduce arbitrary CLI handlers, unsandboxed new
processes, undocumented shell verbs, or silently bypass Core governance.
A future capability must have a typed contract, permission/risk checks,
progress/cancellation, undo/recovery where relevant, and a native acceptance
suite **before** it becomes visible.

Universal copy/cut/delete on the clipboard or Recycle Bin are also not
claimed as finished Nexus-native actions. Nexus Files currently offers
Core Copy-to/Move-to and Copy Path(s). The Windows Options submenu remains
the compatibility escape hatch for extensions handled by Explorer.

Hot-reload is bounded, best-effort and applies to the next popup. The
configuration file may be read-only in a system-wide installed release.
The common menu path performs no file I/O, but the reference's strict
16 ms latency target has not been measured on real hardware.

## Validation requirements

1. AppVeyor warnings-as-errors Core/Runtime, WinUI x64 compilation,
   native UI probes and publish/package. The JSON must be present at
   `Assets/Configuration/file_context_profiles.json` in the release.
2. Portable Core checks: real config loads, correct profile choice for
   `.MP4`, `.TAR.GZ`, `.TS`, shortcut and unknown `.MDF`; no overlap
   in shipped catalog, safe mixed-action intersection and no mass-run.
3. Native Files preflight at two window sizes: folder, video, audio,
   image, application, archive, unknown and mixed files, max seven
   actionable top-level items, correct first action and optional Open With.
4. Interactive Windows 10 and 11: right-click selected items in Files list
   and grid without losing selection; Copy-to/Move-to produces one Core
   transaction for the captured paths; Rename/Properties/Open With work;
   no dead submenu items or pointer focus loss.
5. Desktop shortcut contexts preserve native action routing and do not
   modify Explorer's right-click menus in third-party applications.
6. Changing the JSON safely hot-reloads the menu on next opening.
   Replacing it with invalid JSON must not crash Nexus or erase the
   previous valid profile map. Check the fallback if the catalog is missing.
7. Test 100+ selections, an app with no Edit verb, inaccessible/locked files,
   network paths, NTFS permission failures and a file that disappears
   between menu invocation and action. The original target must remain
   captured throughout.
8. Measure cold and warm popup latency in the VM at 100/150/200% DPI,
   High Contrast and normal Midnight glass. Do not advertise <16 ms until
   a repeatable trace confirms it.

**Release gate:** keep PR #32 a draft until user-directed build and Windows
acceptance. No automatic merge into `main`.
