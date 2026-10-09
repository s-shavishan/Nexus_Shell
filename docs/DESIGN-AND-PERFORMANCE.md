# Current desktop performance — 1.4.2

The user clarified that the smoother window-drag result came from **1.4.1 with Explorer still running**. Explorer-free operation was not the successful comparison. This update preserves 1.4.1's taskbar-only takeover and existing desktop-relative placement; Explorer's desktop remains active underneath the opaque Nexus desktop.

Floating taskbar geometry has two parts: a stable full-width work-area reservation and a centered clipped visible surface. App-count changes adjust the visible dock without changing global work-area geometry. Existing cached work-area writes, asynchronous notifications, validated Explorer identity checks and hidden-panel polling suspension are retained.

Rounded window regions are cached by size/shape/DPI and only apply to Nexus HWNDs. There are no new timers, mouse hooks, infinite animations, global layout loops or Explorer stop/restart loops. Entrance/hover/press effects use finite compositor animations; their completion never controls input, focus or panel visibility. Fast and accessibility preferences suppress them.

The code can target visible rough edges without claiming a measured speedup. Compare actual 1.4.2 Windows dragging to 1.4.1, with the same VM, resolution, graphics settings and performance profile. Verify render/focus/resize/recovery behavior from UI-POLISH-1.4.2.md before treating the UI update as accepted.
