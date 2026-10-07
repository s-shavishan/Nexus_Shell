# Apply the 0.4.0 source patch

Extract the ZIP and upload the **contents** of Nexus-Shell-0.4.0-Patch into the existing Nexus repository root, replacing matching files. Include all source, scripts, tests and CI/configuration files. This patch can update the earlier 0.2.1, 0.2.2, or 0.3.0 tree. It omits documentation preview PNG/SVG assets, which the app does not load. It is source for the build server, not an executable update for your PC.

Commit and run New build in your existing AppVeyor project. After success, choose Nexus-Shell-0.4.0-Update-win-x64.zip for the smaller PC download. Extract it separately, close Nexus, run Apply-Update.bat and select the old full app folder. Runtime mismatch requires the full binary ZIP. Read START-HERE.md for the complete steps and validation limits.
