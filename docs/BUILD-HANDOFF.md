# Windows build handoff — 2.0.0

Use SOURCE-PATCH.md for the current 1.8-to-2.0 or GitHub-main-1.5-to-2.0 patch, or extract the complete Source ZIP into a fresh folder. Source deliveries contain no executable. Review and commit/push the source through the normal repository workflow.

README.md lists all build/check commands. GitHub Actions and AppVeyor publish the full `Nexus-Shell-2.0.0-win-x64.zip` and update ZIP with hashes after build/resource validation. Keep all Shell, DesktopHost, Core, Runtime and UI resources together.

Test the full package in a fresh Windows 10 VM version folder using docs/MAJOR-2.0.0.md. Do not reuse older recovery helpers: 2.0 journals a minimized-window arrangement that older helpers do not know to restore. Native Windows compilation and acceptance remain pending.
