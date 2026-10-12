[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [Parameter(Mandatory=$true)][string]$ReportFile,
    [ValidateRange(1, 500)][int]$Cycles = 20,
    [switch]$AllowTestInput
)
$ErrorActionPreference = 'Stop'
if (-not $AllowTestInput) { throw 'This experiment moves the cursor, clicks, drags and presses Escape in its own window. Pass -AllowTestInput explicitly to run it.' }
if ($env:OS -ne 'Windows_NT' -or -not [Environment]::UserInteractive) { throw 'Run the input experiment in an interactive Windows desktop. Headless skips are not supported.' }
$exe = Join-Path ([IO.Path]::GetFullPath($PublishDirectory)) 'Nexus.Shell.exe'
$report = [IO.Path]::GetFullPath($ReportFile)
if (-not (Test-Path $exe -PathType Leaf)) { throw "Published Nexus executable not found: $exe" }
if (Test-Path $report) { throw 'Choose an unused report filename; existing reports are preserved.' }
New-Item (Split-Path $report -Parent) -ItemType Directory -Force | Out-Null
Write-Host "Running $Cycles isolated input cycles. Keep the mouse/keyboard idle; close the experiment window to stop."
$probe = Start-Process -FilePath $exe -ArgumentList @('--verify-input', '--allow-test-input', '--cycles', $Cycles.ToString(), '--report', ('"' + $report + '"')) -PassThru
try {
    $deadline = [Math]::Min([int]::MaxValue, ($Cycles * 8000 + 60000))
    if (-not $probe.WaitForExit($deadline)) {
        $null = $probe.CloseMainWindow()
        if (-not $probe.WaitForExit(5000)) { throw 'The probe did not stop after its deadline. Close its window and release any held input; no other process is terminated.' }
        throw 'The input experiment exceeded its deadline.'
    }
    if ($probe.ExitCode -ne 0 -or -not (Test-Path $report)) { throw "The input experiment failed (exit $($probe.ExitCode)); inspect $report and the Nexus log." }
    $result = Get-Content $report -Raw | ConvertFrom-Json
    if ($result.Status -ne 'PASS' -or $result.CompletedCycles -ne $Cycles) { throw "Input checks failed or were incomplete: $report" }
    Write-Host "Isolated Windows input checks passed: $report" -ForegroundColor Green
    Write-Host 'Physical-device, native shortcut/snap/DPI, real app-control and black-region visual acceptance remain separate checks.'
} finally { $probe.Dispose() }
