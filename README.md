# White Dreams Nexus Shell

A native C# / WinUI 3 desktop source preview for Windows 10 (build 19041 or newer) and Windows 11 x64. **Nexus Orbit** combines a macOS-inspired menu bar, floating dock, and sidebar workspace with its own pearl/violet/teal theme. It opens ordinary Windows applications.

Start with **[START-HERE.md](START-HERE.md)**. It includes AppVeyor and GitHub Actions cloud-build routes so you can obtain the Windows executable without downloading local developer tools. AppVeyor's free hosted plan requires a public, open-source project.

## Project layout

| Path | Responsibility |
|---|---|
| `src/Nexus.Shell/MainWindow.xaml` | Native desktop composition and controls |
| `src/Nexus.Shell/MainWindow.xaml.cs` | Window lifecycle, app library, dock, and Windows integration |
| `src/Nexus.Shell/MainWindow.Workspaces.cs` | Command palette, Explore board, Study timer/notes, and desktop moods |
| `src/Nexus.Shell/MainWindow.Orbit.cs` | Tasks, favorites/collections, paused recovery, and virtualized window overview |
| `Services/WorkspaceState.cs` | Bounded workspace migration and validation |
| `tests/Nexus.Core.Checks` | Timing, recovery, search, migration, JSON, and snapshot behavioral checks |
| `Models` | Preferences, app entries, and session records |
| `Services/AppCatalog.cs` | Known app detection, bounded Start menu discovery, launches |
| `Services/StateStore.cs` | Atomic local JSON preferences |
| `Services/UsageTracker.cs` | Opt-in foreground-time sampling |
| `Services/ResourceSampler.cs` | Refreshed process memory and elapsed CPU measurements |
| `UI/MotionController.cs` | Shared finite compositor animations, cancellation, and cleanup |
| `UI/AppAccentConverter.cs` | Reused vector-tile color brushes |
| `Services/StartupRegistration.cs` | Explicit current-user login startup |
| `Interop/NativeMethods.cs` | Win32 accessibility/animation preferences, window enumeration, activation, and idle detection |
| `scripts` | Build, compiled-resource checks, full/small packages, update checks, diagnostics, and startup cleanup |
| `appveyor.yml` | AppVeyor Windows compile/publish/package configuration |
| `.github/workflows` | Windows compile/publish workflow |

No Electron, Node.js, webview-hosted interface, cloud telemetry, or provider bundle is used by the running app. WinUI's dependency graph includes SDK support for WebView2, but the project does not instantiate a WebView2 control or run its UI in Chromium.

This prototype runs as the signed-in user. A future privileged service must be a separate process with a restricted request interface. Applications launched from the dock should retain normal user-session behavior.

## Deliberate limits

The current dock is part of the Nexus window. It is not a system taskbar replacement and will not remain visible on top of every external app. Start menu discovery covers `.lnk` shortcuts, not every Store-app registration. Window focus follows Windows' foreground restrictions. No universal third-party title-bar restyling is attempted.

Usage totals are approximate foreground time, exclude periods idle for at least 90 seconds, and stop when Nexus exits. Sampling avoids crediting long gaps across sleep or a blocked UI thread. It collects process names, not screenshots, keyboard input, or browsing history.

## Verification

0.4.1 replaces the unsupported UWP high-contrast event with guarded Win32 preference queries, using the existing active-window timer and an activation refresh. The user-supplied 0.4.0 log confirms the app XAML loads on Windows 10 build 19045, then startup fails at that event subscription. The fix still needs a Windows build and launch test.

0.4.0 connects Home to a task checklist and favorite items, adds collections and editing to Explore, restores focus timers paused, and introduces a searchable virtualized window overview. Small update downloads reuse compatible runtimes and require the new app resource set. The published PRI/XBF repair is retained. Windows build and launch checks remain required.

See `docs/VALIDATION.md` for the checks performed while preparing this source and the Windows verification still required. There is no prebuilt executable in this source ZIP.

See `docs/CHANGELOG.md` for release changes, `docs/DESIGN-AND-PERFORMANCE.md` for motion/resource policy, and `docs/Nexus-Orbit-preview.svg` for a design reference. None of these documents establishes a measured Windows memory reduction.
