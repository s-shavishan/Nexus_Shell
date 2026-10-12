[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (-not [Environment]::UserInteractive) { Write-Host 'SKIP: the production verifier refuses this headless session; run the diagnostic fixture in an interactive session.'; return }
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('Nexus-probe-output-' + [Guid]::NewGuid().ToString('N'))
New-Item $temporary -ItemType Directory | Out-Null
$savedOS = $env:OS
try {
    # Only process-launch behavior is replaced. The production verifier executes
    # its actual report/log failure handling. No WinUI or input is simulated.
    $env:OS = 'Windows_NT'
    $script:fixtureReport = Join-Path $temporary 'native-ui.json'
    $capture = Join-Path $temporary 'captured.txt'
    function Start-Process {
        param($FilePath, $ArgumentList, [switch]$PassThru)
        $diagnosticReport = $ArgumentList[-1].Trim('"')
        [IO.File]::WriteAllText($diagnosticReport, '{"Status":"FAIL","Templates":8,"FailureDetails":[{"Stage":"native-caption-registration-and-layout","HResult":"0x80004005","Message":"fixture failure"}]}')
        if (-not $script:suppressProbeLog) { [IO.File]::WriteAllText(($diagnosticReport + '.log'), 'probe-only restricted error details') }
        $process = [pscustomobject]@{ ExitCode = 1 }
        $process | Add-Member ScriptMethod WaitForExit { param($timeout) return $true }
        $process | Add-Member ScriptMethod Dispose { }
        return $process
    }
    $failure = $null
    try { & (Join-Path $PSScriptRoot 'verify-native-ui.ps1') -PublishDirectory $temporary -ReportFile $script:fixtureReport *> $capture }
    catch { $failure = $_.Exception.Message }
    if (-not $failure -or $failure -notlike '*exit code 1*') { throw "The native verifier must keep its failing exit result. Observed: $failure" }
    $output = Get-Content $capture -Raw
    foreach ($expected in @('native-caption-registration-and-layout', '0x80004005', 'probe-only restricted error details')) {
        if (-not $output.Contains($expected)) { throw "The failure output omitted: $expected" }
    }
    if (-not (Test-Path $script:fixtureReport) -or -not (Test-Path ($script:fixtureReport + '.log'))) { throw 'Probe evidence must remain available for artifact collection.' }
    # A second failing run must not reuse a log left by the first invocation.
    $script:suppressProbeLog = $true
    $staleMarker = 'STALE-PROBE-LOG-MUST-NOT-APPEAR'
    [IO.File]::WriteAllText(($script:fixtureReport + '.log'), $staleMarker)
    $secondCapture = Join-Path $temporary 'second-captured.txt'
    $secondFailure = $null
    try { & (Join-Path $PSScriptRoot 'verify-native-ui.ps1') -PublishDirectory $temporary -ReportFile $script:fixtureReport *> $secondCapture }
    catch { $secondFailure = $_.Exception.Message }
    if (-not $secondFailure -or $secondFailure -notlike '*exit code 1*') { throw 'A subsequent native UI failure must remain a failure.' }
    if ((Get-Content $secondCapture -Raw).Contains($staleMarker)) { throw 'The verifier printed diagnostics from a previous run.' }
    if (Test-Path ($script:fixtureReport + '.log')) { throw 'The verifier retained the previous run''s stale log.' }
    if (-not (Test-Path $script:fixtureReport)) { throw 'The current failure report was not retained.' }
    Write-Host 'PASS: production verifier preserves failures, prints current stage/HRESULT and probe-only logs, retains evidence, and clears stale logs on rerun (mock process; native WinUI not executed).'
} finally {
    $env:OS = $savedOS
    Remove-Item $temporary -Recurse -Force
}
