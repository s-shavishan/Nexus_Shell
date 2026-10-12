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
        [IO.File]::WriteAllText(($diagnosticReport + '.log'), 'probe-only restricted error details')
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
    Write-Host 'PASS: production verifier preserves failure, prints stage/HRESULT and probe-only log, and retains both artifacts (mock process; native WinUI not executed).'
} finally {
    $env:OS = $savedOS
    Remove-Item $temporary -Recurse -Force
}
