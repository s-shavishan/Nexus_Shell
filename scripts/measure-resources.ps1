[CmdletBinding()]
param(
    [ValidateSet('Home','Apps','ReducedEffects','Minimized','Gaming','Navigation','Files')][string]$Scenario = 'Home',
    [ValidateRange(15,900)][int]$Seconds = 60,
    [ValidateRange(2,30)][int]$IntervalSeconds = 5,
    [string]$InstallDirectory
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Run this on your Windows test PC while Nexus is open.' }
$sessionId = (Get-Process -Id $PID).SessionId
if (-not $InstallDirectory) {
    if (Test-Path (Join-Path $PSScriptRoot 'Nexus.Shell.exe')) { $InstallDirectory = $PSScriptRoot }
    else {
        $folders = @(Get-Process -Name 'Nexus.Shell' -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $sessionId } |
            ForEach-Object { try { [IO.Path]::GetDirectoryName($_.Path) } catch { } } | Sort-Object -Unique)
        if ($folders.Count -ne 1) { throw 'Specify -InstallDirectory for the Nexus build to measure.' }
        $InstallDirectory = $folders[0]
    }
}
$installRoot = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd([char[]]'\/')
function Read-NexusProcesses {
    foreach ($item in Get-Process -Name @('Nexus.Shell','Nexus.Core','Nexus.DesktopHost') -ErrorAction SilentlyContinue) {
        try {
            if ($item.SessionId -ne $sessionId -or [IO.Path]::GetDirectoryName($item.Path) -ine $installRoot) { continue }
            [pscustomobject]@{ Key = "$($item.Id)@$($item.StartTime.ToUniversalTime().Ticks)"; Id = $item.Id; Name = $item.ProcessName
                Cpu = $item.TotalProcessorTime.TotalSeconds; Working = $item.WorkingSet64; Private = $item.PrivateMemorySize64; Handles = $item.HandleCount }
        } catch { } finally { $item.Dispose() }
    }
}
$initial = @(Read-NexusProcesses)
if ($initial.Count -eq 0) { throw 'No Nexus processes from this installation are running in this session.' }
$outputDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WhiteDreams\NexusShell\measurements'
New-Item $outputDirectory -ItemType Directory -Force | Out-Null
$basename = Join-Path $outputDirectory ($Scenario + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$samples = New-Object 'System.Collections.Generic.List[object]'
$details = New-Object 'System.Collections.Generic.List[object]'
$previous = @{}; foreach ($item in $initial) { $previous[$item.Key] = $item.Cpu }
$stopwatch = [Diagnostics.Stopwatch]::StartNew(); $previousTime = 0.0
$processors = [Environment]::ProcessorCount
try {
    while ($stopwatch.Elapsed.TotalSeconds -lt $Seconds) {
        Start-Sleep -Seconds $IntervalSeconds
        $elapsed = $stopwatch.Elapsed.TotalSeconds; $delta = $elapsed - $previousTime
        $current = @(Read-NexusProcesses); if ($current.Count -eq 0) { break }
        $cpuDelta = 0.0; $working = 0L; $private = 0L; $handles = 0; $next = @{}
        foreach ($item in $current) {
            $used = if ($previous.ContainsKey($item.Key)) { [Math]::Max(0, $item.Cpu - $previous[$item.Key]) } else { $item.Cpu }
            $cpuDelta += $used; $working += $item.Working; $private += $item.Private; $handles += $item.Handles; $next[$item.Key] = $item.Cpu
            $details.Add([pscustomobject]@{ elapsedSeconds = [Math]::Round($elapsed,3); processId = $item.Id; component = $item.Name
                workingSetMB = [Math]::Round($item.Working / 1048576.0,3); privateBytesMB = [Math]::Round($item.Private / 1048576.0,3)
                handles = $item.Handles; cpuPercent = [Math]::Round($used / $delta / $processors * 100,3) })
        }
        $samples.Add([pscustomobject]@{ elapsedSeconds = [Math]::Round($elapsed,3); processCount = $current.Count
            workingSetMB = [Math]::Round($working / 1048576.0,3); privateBytesMB = [Math]::Round($private / 1048576.0,3)
            handles = $handles; cpuPercent = [Math]::Round($cpuDelta / $delta / $processors * 100,3) })
        $previous = $next; $previousTime = $elapsed
    }
    if ($samples.Count -eq 0) { throw 'No samples collected; Nexus may have exited.' }
    $samples | Export-Csv ($basename + '.csv') -NoTypeInformation -Encoding UTF8
    $details | Export-Csv ($basename + '-processes.csv') -NoTypeInformation -Encoding UTF8
    $workingSet = $samples | Measure-Object -Property workingSetMB -Average -Maximum
    $privateBytes = $samples | Measure-Object -Property privateBytesMB -Average -Maximum
    $cpuStats = $samples | Measure-Object -Property cpuPercent -Average -Maximum
    [pscustomobject]@{ scenario = $Scenario; sampleCount = $samples.Count; logicalProcessors = $processors
        durationSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds,3)
        workingSetAverageMB = $workingSet.Average; workingSetPeakMB = $workingSet.Maximum
        privateBytesAverageMB = $privateBytes.Average; privateBytesPeakMB = $privateBytes.Maximum
        cpuAveragePercent = $cpuStats.Average; cpuPeakPercent = $cpuStats.Maximum
        note = 'All Nexus components in this installation/session. Working-set sums count shared pages more than once. Excludes GPU/DWM and frame time; short-lived processes between samples may be missed.'
    } | ConvertTo-Json | Set-Content ($basename + '.json') -Encoding UTF8
    Write-Host ($basename + '.csv'); Write-Host ($basename + '-processes.csv'); Write-Host ($basename + '.json')
} finally { $stopwatch.Stop() }
