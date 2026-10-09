# Updating the source to 1.4.2

The build-fix patch is `Nexus-1.4.2-Build-Fix.patch`. Its verified baseline is the **199-file** GitHub tree at main commit **af26198808fbc31f92275847cabfdf461093e35f**, the original 1.4.2 source that failed with CS0509. Keep local edits; inspect a failing check before applying.

```powershell
git apply --check --whitespace=error-all Nexus-1.4.2-Build-Fix.patch
git apply --whitespace=error-all Nexus-1.4.2-Build-Fix.patch
git diff --check
```

Apply this patch once to the failed 1.4.2 source. It is separate from the earlier 1.4.0/1.4.1-to-1.4.2 upgrade patches. If your source is still older, use the corrected complete Source ZIP or upgrade to the original 1.4.2 tree before applying the fix.

Commit and push through the normal workflow. AppVeyor retains .NET core/platform checks, PowerShell updater/parser checks, native WinUI build/resource generation and package validation. Its expected artifacts are `Nexus-Shell-1.4.2-win-x64.zip` and `Nexus-Shell-1.4.2-Update-win-x64.zip`.

The Source ZIP is an alternative complete tree. Both source routes require a Windows build. Runtime dependencies remain pinned, and the host's Explorer/session takeover code remains unchanged from 1.4.1. Review docs/UI-POLISH-1.4.2.md before native acceptance.
