[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WhiteDreams\NexusShell\diagnostics'))
$ErrorActionPreference = 'Stop'
New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
$os = Get-CimInstance Win32_OperatingSystem
$sessionId = (Get-Process -Id $PID).SessionId
$process = Get-Process -Name @('Nexus.Shell','Nexus.Core','Nexus.DesktopHost') -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $sessionId }
$appDirectory = if (Test-Path (Join-Path $PSScriptRoot 'Nexus.Shell.exe')) { $PSScriptRoot } else { $null }
$report = [ordered]@{
    Timestamp = (Get-Date).ToString('o')
    OS = $os.Caption
    Build = $os.BuildNumber
    Architecture = $os.OSArchitecture
    CpuThreads = [Environment]::ProcessorCount
    DotnetSDKs = if (Get-Command dotnet -ErrorAction SilentlyContinue) { @(& dotnet --list-sdks) } else { @() }
    NexusProcesses = @($process | Select-Object -Property @('ProcessName','Id','WorkingSet64','PrivateMemorySize64','HandleCount','CPU','Responding'))
    PublishedResourceFiles = if ($appDirectory) { @(Get-ChildItem $appDirectory -Recurse -File | Where-Object { $_.Extension -in @('.pri','.xbf') } | Select-Object -Property @('Name','Length')) } else { @() }
}
$report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'environment.json') -Encoding UTF8
$dataDirectory = Join-Path $env:LOCALAPPDATA 'WhiteDreams\NexusShell'
foreach ($name in @('nexus.log','core.log','desktop-host.log')) {
    if (Test-Path (Join-Path $dataDirectory $name)) { Copy-Item (Join-Path $dataDirectory $name) $OutputDirectory -Force }
}
if ($appDirectory -and (Test-Path (Join-Path $appDirectory 'Nexus.resources.json'))) { Copy-Item (Join-Path $appDirectory 'Nexus.resources.json') $OutputDirectory -Force }
# settings.json contains personal pins and usage data, so it is deliberately excluded.
Write-Host "Diagnostics saved to $OutputDirectory"
