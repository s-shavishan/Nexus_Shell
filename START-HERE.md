# Start here — Nexus 2.1.1

1. Extract the complete Source ZIP into a fresh folder, or apply exactly one matching patch from SOURCE-PATCH.md.
2. Build on Windows with the commands in README.md, or commit/push the reviewed source and retrieve the CI build artifacts.
3. Extract the full `Nexus-Shell-2.1.1-win-x64.zip` into a separate version folder. Close the older Nexus before launching the new one.
4. Snapshot the VM, start `Launch-Nexus.bat` in preview, then follow `START-AND-SIGNIN-2.1.1.md` in the compiled folder (or docs/START-AND-SIGNIN-2.1.1.md in source).
5. Test supervised takeover using `Launch-Nexus-Desktop.bat`, the current restore helper, and the existing MAJOR-2.0.0.md regressions before enabling automatic startup or shell replacement.

The Source and Patch ZIPs contain no executable. The reported 2.0 test pass and the portable 2.1 checks do not certify this Windows build. Wi-Fi/Bluetooth controls need real or passed-through adapters; unsupported VM brightness and absent radios should produce capability messages.
