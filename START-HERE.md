# Start here — Nexus 2.0.0

1. Extract the complete Source ZIP into a fresh folder, or apply exactly one matching patch from SOURCE-PATCH.md.
2. Build on Windows with the commands in README.md, or commit/push the reviewed source and retrieve the CI build artifacts.
3. Extract the full `Nexus-Shell-2.0.0-win-x64.zip` into a separate version folder. Close the older Nexus before launching the new one.
4. Snapshot the VM, start `Launch-Nexus.bat` in preview, then follow `MAJOR-2.0.0.md` in the compiled folder (or docs/MAJOR-2.0.0.md in source).
5. Test supervised takeover using `Launch-Nexus-Desktop.bat` and the **2.0** restore helper before enabling automatic startup or shell replacement.

All visuals and native recovery need new Windows acceptance. The earlier 1.8 test report does not certify this 2.0 build. Keep the old build and VM snapshot available.
