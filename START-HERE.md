# Start here — Nexus 1.3.0

This is the source for a Nexus desktop replacement on Windows Pro. It includes the independent desktop foundation, Nexus Files/pickers, window switching, per-user sign-in setup and a desktop recovery host.

1. Extract the full source into a fresh folder, or apply the source patch matching your unchanged 1.0.0, 1.1.0 or 1.2.0 baseline.
2. Build on Windows using `scripts\build.ps1 -UseMSBuild`, or upload the source to your repository and run the included Windows CI.
3. Run the complete published folder's `Nexus.Shell.exe` to preview it. Source ZIPs are not runnable releases.
4. Complete `docs/TEST-DESKTOP-MODE.md` on a Windows Pro VM/test account before configuring your everyday sign-in.
5. Select **Sections → Personalize → Use Nexus at sign-in**. Keep the full published folder in its selected location.

Read [NEXUS-DESKTOP-MODE.md](docs/NEXUS-DESKTOP-MODE.md) for setup/recovery and [BUILD-HANDOFF.md](docs/BUILD-HANDOFF.md) for CI and artifact details.

The C# API, core and source checks pass locally. Native Windows compilation, launch and sign-in acceptance are pending. No account setting was changed here.
