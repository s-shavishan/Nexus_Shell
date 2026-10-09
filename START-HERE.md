# Start here — Nexus 1.4.0

1. Apply `Nexus-1.3.1-to-1.4.0.patch` to the verified 1.3.1 baseline, then commit/push for AppVeyor. See SOURCE-PATCH.md. The Source ZIP is an alternative complete source tree.
2. Wait for the Windows build to complete. Extract the entire `Nexus-Shell-1.4.0-win-x64.zip` artifact to a new folder. Keep the old folder.
3. Exit the old Nexus instance. Launch the new `Launch-Nexus-Desktop.bat`, or use Personalize → Use Nexus for this session from preview.
4. Open the taskbar's Quick Settings button or press Win+A. Choose **Fast** in the VM. Drag the same ordinary application windows you used with 1.3.1; compare Balanced and Full afterward.
5. Exit Nexus to verify Windows returns. Complete docs/QUICK-SETTINGS-AND-PERFORMANCE.md and docs/TEST-DESKTOP-MODE.md.

Source ZIPs and patches require compilation; they are not runnable Windows updates. Local C#/core/source checks pass, but 1.4.0 native build and measured VM smoothness remain pending. Temporary session mode does not change protected sign-in policy.
