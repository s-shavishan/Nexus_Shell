# Start and sign-in source validation — 2.1.1

Validation date: 10 October 2026. Linux source/API checks pass; native Windows compilation and input/sign-in acceptance remain pending.

| Check | Result |
| --- | --- |
| Runtime, Core, DesktopHost and both check projects | Direct Roslyn compilation: zero errors/warnings |
| Shell C# against restored Windows/WinUI projections | Zero errors; 19 check-setup warnings |
| Start gesture/layout checks | 66 assertions: left/right Windows keys, repeats, native combinations, keys held at connection, injected chords, secure-desktop state reset and full x64 INPUT layout |
| Startup checks | 31 assertions: existing launch/foreign-policy/elevation behavior plus case/whitespace matching, conflict inspection, original-value restoration and refusal to claim an executable without recovery ownership |
| Existing desktop checks | Pass: 1008 monitor/DPI/layout cases and 80 major desktop checks among the full managed suite |
| Runtime/settings checks | Pass: 101 runtime and 98 settings assertions, simulated Files recovery and production framing/role/stale-command tests |
| Source validation | Pass: 111 required Shell files, 140 named elements, 70 handlers, nine templates, versions/resources and ownership guards |
| PowerShell | All scripts parse; synthetic update fixtures pass |
| Whitespace | git diff --check passes |

The check compiler uses .NET SDK 8.0.419 and .NET 8.0.25 references with restored Windows/WinUI projections. Temporary XAML named-field and InitializeComponent declarations allow C# type checking only; they do not validate XBF/PRI output. The nineteen warnings are six reference-unification and thirteen unused temporary-field warnings. Roslyn checks syntax; the source validator's optional tree-sitter mode was not used.

Runtime tests use `--no-native-ipc`: actual named-pipe identity, Windows job cleanup, native device reads and foreign-HWND ownership are explicitly skipped on Linux. Windows CI runs the complete suite without that flag. Core checks do not install a keyboard hook on the CI host; physical key routing, ordered input masking, foreground focus, UAC and lock/unlock require VM acceptance.

PowerShell fixture tests use command-scoped `OS=Windows_NT` to exercise synthetic update files on Linux. No production platform guard was removed. Startup inspection is read-only; its live registry output cannot be collected from the user's VM here. Unknown sign-in policies are preserved and remain a configuration blocker until their actual value/owner is established.

The uploaded logs contain two sign-in policy refusals, acknowledged Core loads and an intentional host exit, with valid settings/backup. They contain no unhandled UI exception in this short sample and do not establish long-term stability or performance.

Follow [Windows input/sign-in acceptance](START-AND-SIGNIN-2.1.1.md) and the previous desktop/Control Center regressions. See [captured checks](START-CHECKS-2.1.1.txt). The [next major pack](NEXT-DESKTOP-PACK.md) is a proposal, not part of this implementation.
