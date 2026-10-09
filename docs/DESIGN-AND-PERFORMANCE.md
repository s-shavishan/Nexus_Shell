# Current desktop performance — 1.4.1

The user's VirtualBox report establishes a useful comparison: 1.4.0 works smoothly with the Windows taskbar present, while session takeover lags even in Fast. Preview and takeover also differ in desktop visibility, layering, work-area registration and keyboard handling, so the report does not isolate a single API as the cause.

1.4.1 removes two differences: session takeover no longer hides Progman/WorkerW windows, and the Nexus desktop now uses the same above-Explorer/below-app placement path as preview. Only Windows taskbars receive hide requests. The Nexus desktop is opaque and covers the retained Windows desktop on the primary display.

This change introduces no animation, extra timer, Explorer injection, process termination or desktop reparenting. It retains the 1.4.0 cached work-area reservation, asynchronous notifications, retained Explorer ownership checks, Fast/Balanced/Full profiles and hidden-panel polling suspension.

CPU/GPU usage, presented frame time and window movement in the real VM must be measured after a successful Windows build. Use SESSION-TAKEOVER-1.4.1.md. If lag remains, compare Nexus preview/session while keeping VM graphics settings unchanged; investigate the remaining work-area and keyboard differences with evidence.
