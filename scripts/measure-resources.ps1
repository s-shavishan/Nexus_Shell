[CmdletBinding()]
param(
    [ValidateSet('Home','Apps','ReducedEffects','Minimized','Gaming','Navigation')][string]$Scenario = 'Home',
    [ValidateRange(15,900)][int]$Seconds = 60,
    [ValidateRange(2,30)][int]$IntervalSeconds = 5
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Run this on your Windows test PC while Nexus is open.' }
$sessionId = (Get-Process -Id $PID).SessionId
$candidates = @(Get-Process -Name 'Nexus.Shell' -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $sessionId })
if ($candidates.Count -ne 1) { throw 'Keep exactly one Nexus.Shell process running in this Windows session.' }
$nexusProcess = $candidates[0]
$startedAt = $nexusProcess.StartTime.ToUniversalTime().ToString('o')
$outputDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WhiteDreams\NexusShell\measurements'
New-Item $outputDirectory -ItemType Directory -Force | Out-Null
$basename = Join-Path $outputDirectory ($Scenario + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$samples = New-Object 'System.Collections.Generic.List[object]'
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$previousTime = $stopwatch.Elapsed.TotalSeconds
$previousCpu = $nexusProcess.TotalProcessorTime.TotalSeconds
$processors = [Environment]::ProcessorCount
try {
    Write-Host "Sampling Nexus for $Seconds seconds. Keep the requested scenario active; no settings are changed."
    while ($stopwatch.Elapsed.TotalSeconds -lt $Seconds) {
        Start-Sleep -Seconds $IntervalSeconds
        $nexusProcess.Refresh()
        if ($nexusProcess.HasExited) { break }
        $elapsed = $stopwatch.Elapsed.TotalSeconds
        $cpu = $nexusProcess.TotalProcessorTime.TotalSeconds
        $delta = $elapsed - $previousTime
        $percent = [Math]::Max(0, [Math]::Min(100, (($cpu - $previousCpu) / $delta / $processors * 100)))
        $samples.Add([pscustomobject]@{
            elapsedSeconds = [Math]::Round($elapsed, 3)
            workingSetMB = [Math]::Round($nexusProcess.WorkingSet64 / 1048576.0, 3)
            privateBytesMB = [Math]::Round($nexusProcess.PrivateMemorySize64 / 1048576.0, 3)
            cpuPercent = [Math]::Round($percent, 3)
        })
        $previousTime = $elapsed; $previousCpu = $cpu
    }
    if ($samples.Count -eq 0) { throw 'No samples collected; Nexus may have exited.' }
    $samples | Export-Csv ($basename + '.csv') -NoTypeInformation -Encoding UTF8
    $workingSet = $samples | Measure-Object -Property workingSetMB -Average -Maximum -Minimum
    $privateBytes = $samples | Measure-Object -Property privateBytesMB -Average -Maximum
    $cpuStats = $samples | Measure-Object -Property cpuPercent -Average -Maximum
    [pscustomobject]@{
        scenario = $Scenario; processId = $nexusProcess.Id; processStartedUtc = $startedAt
        sampleCount = $samples.Count; durationSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
        logicalProcessors = $processors
        workingSetAverageMB = $workingSet.Average; workingSetPeakMB = $workingSet.Maximum
        privateBytesAverageMB = $privateBytes.Average; privateBytesPeakMB = $privateBytes.Maximum
        cpuAveragePercent = $cpuStats.Average; cpuPeakPercent = $cpuStats.Maximum
        note = 'Nexus process only. Excludes GPU/DWM memory and frame time. Scenario is a manual label.'
    } | ConvertTo-Json | Set-Content ($basename + '.json') -Encoding UTF8
    Write-Host ($basename + '.csv')
    Write-Host ($basename + '.json')
} finally { $stopwatch.Stop(); $nexusProcess.Dispose() }
