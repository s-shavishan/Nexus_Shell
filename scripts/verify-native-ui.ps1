[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [Parameter(Mandatory=$true)][string]$ReportFile,
    [switch]$AllowHeadlessSkip
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'The native WinUI check must run on Windows.' }
$exe = Join-Path ([IO.Path]::GetFullPath($PublishDirectory)) 'Nexus.Shell.exe'
$report = [IO.Path]::GetFullPath($ReportFile)
if (Test-Path $report) { Remove-Item $report -Force }
New-Item (Split-Path $report -Parent) -ItemType Directory -Force | Out-Null
if (-not [Environment]::UserInteractive) {
    if (-not $AllowHeadlessSkip) { throw 'Run the native UI check in an interactive Windows desktop session.' }
    [ordered]@{ Status = 'SKIP'; Reason = 'This build worker has no interactive desktop. Run Check-Nexus-UI.bat inside the VM before acceptance.' } |
        ConvertTo-Json | Set-Content $report -Encoding UTF8
    Write-Host 'Native UI check skipped on this headless worker; the VM check remains required.'
    return
}
$probe = Start-Process -FilePath $exe -ArgumentList @('--verify-ui', '--report', ('"' + $report + '"')) -PassThru
try {
    if (-not $probe.WaitForExit(45000)) { $probe.Kill(); throw 'The native UI check exceeded its deadline. See the Nexus log.' }
    if ($probe.ExitCode -ne 0 -or -not (Test-Path $report)) { throw "The native UI check failed with exit code $($probe.ExitCode). See the build logs and Nexus log." }
    $result = Get-Content $report -Raw | ConvertFrom-Json
    if ($result.Status -ne 'PASS' -or $result.Templates -lt 1) { throw "The native UI report failed: $report" }
    Write-Host "Native WinUI template and surface checks passed: $report" -ForegroundColor Green
} finally { $probe.Dispose() }
