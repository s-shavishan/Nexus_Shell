# Updating the source to 1.4.1

`Nexus-1.4.0-to-1.4.1.patch` targets main commit **a9f813df250f8c5b8f4a3fdadb05804fae867025**, the 192-file 1.4.0 source. It narrows takeover to Windows taskbars and uses the existing desktop-relative placement path in session mode. It retains recovery for old session journals.

From your existing Nexus_Shell source folder:

```powershell
git apply --check --whitespace=error-all Nexus-1.4.0-to-1.4.1.patch
git apply --whitespace=error-all Nexus-1.4.0-to-1.4.1.patch
git diff --check
```

Then commit/push through your usual workflow. AppVeyor runs core checks, the PowerShell updater/parser checks, the Windows build and resource/package verification. A successful build produces `Nexus-Shell-1.4.1-win-x64.zip` and `Nexus-Shell-1.4.1-Update-win-x64.zip`.

Use this patch once on 1.4.0. A failing check can mean it was already applied or your source differs; reconcile the diff before applying it. The patch archive and full-source archive are alternative source delivery routes, not executable Windows packages.

The patch is checked by reconstructing the update from the baseline and comparing all source bytes. No added trailing-whitespace errors are permitted. Runtime dependencies remain pinned.
