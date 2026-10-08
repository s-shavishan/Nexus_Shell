# 1.2.0 Windows acceptance

Run this on the exact compiled 1.2.0 artifact. The Linux source/API/core checks cannot verify these native behaviors. Keep the resource verification report and record Windows build, display scale and monitors used.

| Check | Expected result |
|---|---|
| Start fresh | Desktop icons and Nexus taskbar appear; Sections is absent. Existing applications remain usable above the desktop. |
| Desktop shortcut | Double-click Sections or select it and press Enter; one normal Sections window opens. Opening again returns to the same window. |
| Close/reopen | Edit a note, change a mood and pause/start focus; close Sections. Desktop, taskbar, Start and opt-in tracking continue. Reopen and verify saved content and paused checkpoint. |
| Taskbar appbar | Maximize another app; it respects the Nexus reservation and the existing Windows taskbar. Compact taskbar changes height without cumulative work-area loss. |
| Fullscreen / exit | A fullscreen application can cover the Nexus bar; leaving fullscreen restores it. Exit Nexus returns the original work area and leaves Explorer running. |
| Desktop z-order | Activate ordinary windows, use Show desktop twice, minimize/restore Sections, and use Alt+Tab. Nexus wallpaper never covers normal apps; icon focus remains usable. |
| Independent menu | Open Start while Sections is closed and while it is active. Search, Down, Enter, Escape and deactivation work; clicking Start again dismisses it. Alt+F4 closes only Start and it can reopen. |
| Context menus | Right-click desktop and an icon, then use Open, Refresh, Personalize or Exit. Dismissal does not close other layers. |
| Native shortcuts | Desktop files/folders/.lnk/.url items open in their real Windows applications. Hidden/system items remain filtered; Refresh updates the visible list. |
| Running windows | External windows appear and return to the correct app. Closed/stale handles do not activate another process. Sections has a separate running button. |
| Explorer restart | Restart Explorer through Windows tooling. Nexus tray/appbar recover and do not accumulate reserved space. |
| DPI/display changes | Check 100/125/150/200%, negative monitor origins, 1366×768 and a high-DPI display. Start stays on the taskbar's monitor and above it; desktop icons stay clear of both bars. Hot-plug and rotation remain usable. |
| Accessibility | Keyboard focus is visible. High contrast and reduced effects work on every layer; touch/pen press/cancel and mouse activation behave correctly. |
| Shutdown/single instance | Launch twice; the existing environment is summoned. Exit from desktop/Start/tray closes all Nexus windows, releases hotkey/tray/subclass/appbar, and preserves the latest settings. |
| Existing tools | Exercise Explore import/export/Quick Look, Study, app library refresh, workspace launches, native audio/mixer and PC window arrangement/Undo. |
| Smoothness/resources | Measure idle memory/CPU and interaction while repeatedly opening/closing Sections/Start, changing pins and switching moods. Check for retained windows/handlers or timer growth. |

If native placement or an Explorer variant fails, record the exact reproduction before changing the z-order strategy. The foundation currently coexists with Explorer, uses one monitor for its layers and retains Windows' taskbar/Windows-key behavior. Those are current scope limits, not failed tests.
