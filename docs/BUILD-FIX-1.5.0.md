# Nexus 1.5.0 — remove the retired shortcut hook

AppVeyor build 54866264 checked out `dab35cd55915a6be0df22fd091dbc768e67ec83d`. Core checks, owned-window native/DWM checks and the PowerShell updater checks passed. Native WinUI publishing then stopped with three CS0246 errors: Interop/ShellKeyboardHook.cs referenced the removed ShellKeyAction and ShellKeyboardState types.

Every Git blob in that commit was compared with the delivered 1.5.0 source. The only difference is the retained 2,839-byte Interop/ShellKeyboardHook.cs. The delivered source and cumulative patch already remove this file. Uploading new files or extracting a ZIP over an older directory does not remove obsolete files from that directory/repository.

The fix deletes the retired file. AppVeyor now checks for it before running tests/restoring the UI's packages. An MSBuild guard also reports the cleanup instruction before XamlPreCompile/CoreCompile, and the source validator gives the same explicit instruction. Version/artifact names remain 1.5.0; UI/motion, dependencies, host/recovery and native Windows shortcut ownership are preserved.

Apply Nexus-1.5.0-Build-Fix.patch to the failing GitHub main source, then commit/push including the file deletion and wait for AppVeyor. The small delivery ZIP contains that patch and an application guide. If editing through GitHub's browser, delete src/Nexus.Shell/Interop/ShellKeyboardHook.cs in the repository and commit the deletion; uploading replacement files alone will leave it present.

This corrects the reported stale-file compile failure. Source/XML/C# syntax and exact patch reconstruction are checked locally. Native WinUI/host compilation and runnable packaging need a new Windows CI run; the earlier native core checks do not establish UI rendering or VM performance. No executable is included.

Evidence: [AppVeyor build](https://ci.appveyor.com/project/s-shavishan/nexus-shell/builds/54866264), supplied Pasted markdown(1).md, and GitHub commit/tree blob comparison.
