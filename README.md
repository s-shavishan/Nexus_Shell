# White Dreams Nexus Shell

A native C# / WinUI 3 desktop source preview for Windows 11 x64. **Nexus Orbit** combines a macOS-inspired menu bar, floating dock, and sidebar workspace with its own pearl/violet/teal theme. It opens ordinary Windows applications.

Start with **[START-HERE.md](START-HERE.md)**. It includes AppVeyor and GitHub Actions cloud-build routes so you can obtain the Windows executable without downloading local developer tools. AppVeyor's free hosted plan requires a public, open-source project.

## Project layout

| Path | Responsibility |
|---|---|
| `src/Nexus.Shell/MainWindow.xaml` | Native desktop composition and controls |
| `src/Nexus.Shell/MainWindow.xaml.cs` | UI actions, navigation, dock, and application integration |
| `Models` | Preferences, app entries, and session records |
| `Services/AppCatalog.cs` | Known app detection, bounded Start menu discovery, launches |
| `Services/StateStore.cs` | Atomic local JSON preferences |
| `Services/UsageTracker.cs` | Opt-in foreground-time sampling |
| `Services/ResourceSampler.cs` | Refreshed process memory and elapsed CPU measurements |
| `UI/MotionController.cs` | Shared finite compositor animations, cancellation, and cleanup |
| `UI/AppAccentConverter.cs` | Reused vector-tile color brushes |
| `Services/StartupRegistration.cs` | Explicit current-user login startup |
| `Interop/NativeMethods.cs` | Win32 window enumeration, activation, and idle detection |
| `scripts` | Build, package, diagnostics, and startup cleanup |
| `appveyor.yml` | AppVeyor Windows compile/publish/package configuration |
| `.github/workflows` | Windows compile/publish workflow |

No Electron, Node.js, webview-hosted interface, cloud telemetry, or provider bundle is used by the running app. WinUI's dependency graph includes SDK support for WebView2, but the project does not instantiate a WebView2 control or run its UI in Chromium.

This prototype runs as the signed-in user. A future privileged service must be a separate process with a restricted request interface. Applications launched from the dock should retain normal user-session behavior.

## Deliberate limits

The current dock is part of the Nexus window. It is not a system taskbar replacement and will not remain visible on top of every external app. Start menu discovery covers `.lnk` shortcuts, not every Store-app registration. Window focus follows Windows' foreground restrictions. No universal third-party title-bar restyling is attempted.

Usage totals are approximate foreground time, exclude periods idle for at least 90 seconds, and stop when Nexus exits. Sampling avoids crediting long gaps across sleep or a blocked UI thread. It collects process names, not screenshots, keyboard input, or browsing history.

## Verification

See `docs/VALIDATION.md` for the checks performed while preparing this source and the Windows verification still required. There is no prebuilt executable in this source ZIP.

See `docs/CHANGELOG.md` for 0.2 changes, `docs/DESIGN-AND-PERFORMANCE.md` for motion/resource policy, and `docs/Nexus-Orbit-preview.svg` for a design reference. None of these documents establishes a measured Windows memory reduction.
