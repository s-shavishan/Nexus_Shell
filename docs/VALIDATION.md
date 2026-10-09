# 1.4.1 validation

Baseline: all 192 source files from 1.4.0 match main commit a9f813df250f8c5b8f4a3fdadb05804fae867025. AppVeyor build 54863707 succeeded for that baseline. The user's VirtualBox VM still lags in 1.4.0 Fast after takeover; it is responsive with the Windows taskbar present.

1.4.1 changes taskbar-only takeover and native desktop placement. The added behavioral checks exercise preserved visible/hidden desktop windows, 1,000 unchanged maintenance passes without additional hide writes, reappearing/secondary taskbars, original-state recovery, durable taskbar-only journals and recovery of old journals containing WorkerW.

The authoring environment has no .NET or Windows runtime available for this update. New core tests, C# type/analyzer checks, PowerShell checks, native WinUI build and Windows execution are pending. Existing Core-checks.txt/CSharp-API-check.txt/Host-API-check.txt/Recovery-CSharp-check.txt are explicitly labeled historical 1.4.0 evidence.

The existing Python source validation checks XML/XAML resources, handlers/content order, startup/Sections ownership, assets/publish wiring, host recovery wiring and 1.4.1 artifact naming. The release patch is verified with git apply --check --whitespace=error-all, then applied to a clean baseline; every reconstructed file is compared to the full source. ZIP entries/CRC and SHA-256 hashes are checked during packaging.

CI must execute the core checks and PowerShell updater/parser tests, build both host/UI, generate native XBF/PRI resources and validate the full/small packages. Then complete SESSION-TAKEOVER-1.4.1.md and the existing Windows acceptance guides. Source checks do not establish native layer ordering or resolve the user's window-drag lag.
