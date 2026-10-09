# Updating the source to 1.4.2

The main source patch is `Nexus-1.4.1-to-1.4.2.patch`. Its verified baseline is the **194-file** GitHub tree at main commit **d98e09ff916c16ee1321bbf285a228bc785f9653** (1.4.1). Keep local edits; inspect a failing check before applying.

```powershell
git apply --check --whitespace=error-all Nexus-1.4.1-to-1.4.2.patch
git apply --whitespace=error-all Nexus-1.4.1-to-1.4.2.patch
git diff --check
```

If your local source is still the unchanged 1.4.0 tree at **a9f813df250f8c5b8f4a3fdadb05804fae867025**, use the included `Nexus-1.4.0-to-1.4.2.patch` instead. Apply exactly one patch. Neither patch should be forced onto another baseline.

Commit and push through the normal workflow. AppVeyor retains .NET core/platform checks, PowerShell updater/parser checks, native WinUI build/resource generation and package validation. Its expected artifacts are `Nexus-Shell-1.4.2-win-x64.zip` and `Nexus-Shell-1.4.2-Update-win-x64.zip`.

The Source ZIP is an alternative complete tree. Both source routes require a Windows build. Runtime dependencies remain pinned, and the host's Explorer/session takeover code remains unchanged from 1.4.1. Review docs/UI-POLISH-1.4.2.md before native acceptance.
