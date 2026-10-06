# Run Nexus Shell

Extract the entire package, then run **Nexus.Shell.exe** or **Launch-Nexus.bat**. Keep all DLL, PRI, XBF, and asset files together. This x64 preview targets Windows 10 build 19041 or newer, including Windows 10 22H2 build 19045, and Windows 11.

You do not need developer tools to run this compiled folder. .NET and Windows App SDK runtime files are included. If Windows reports a missing `VCRUNTIME`/`MSVCP` component, install the official x64 Visual C++ Redistributable: https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist

Nexus opens alongside Explorer. Use the full-screen icon or **F11** to explore the OS-surface view. **Escape** closes Control center first, otherwise leaves full screen or minimizes Nexus. **Exit to Windows** and **Alt+F4** close it. **Ctrl+K** focuses app search inside Nexus.

Control center includes **Reduced effects** and **Focus view**. Reduced effects simplifies the main surfaces and disables custom entrance/hover motion; Windows animation settings are also respected. Red hides the workspace, yellow minimizes Nexus, and green expands the workspace. The dock N button reopens Home.

This build does not change Windows' shell or stop system services. It is unsigned and has not been notarized by a publisher. Keep it in a permanent folder if you opt into startup after sign-in.

Settings and logs are stored in `%LOCALAPPDATA%\WhiteDreams\NexusShell`. `diagnostics.ps1` writes a small environment report and the log under that folder’s `diagnostics` subfolder, excluding the personal settings/usage file. `disable-startup.ps1` removes only this project's current-user login entry.

If startup fails after the runtime has loaded, version 0.2.1 displays a native error message and writes the startup stage, HRESULT, inner exceptions, and available native restricted descriptions to `nexus.log`. Missing-resource tracing is enabled before App.xaml loads; it reports resource failures when WinUI provides them. A generic XAML exception can still require further diagnosis. The dialog includes the log path. Close it, then run `diagnostics.ps1` and share the latest startup section of the log.

Extract a rebuilt package into a new folder; do not mix old application/resource files with a new build. Confirm the new log entry says **Nexus Shell 0.2.1**.

See **TEST-WINDOWS.md** for the acceptance checklist.

Use `measure-resources.ps1` to collect your own process-memory/CPU samples. No benchmark results are bundled or automatically shared.
