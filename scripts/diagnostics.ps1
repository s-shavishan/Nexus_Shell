[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WhiteDreams\NexusShell\diagnostics'))
$ErrorActionPreference = 'Stop'
New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
$os = Get-CimInstance Win32_OperatingSystem
$process = Get-Process -Name 'Nexus.Shell' -ErrorAction SilentlyContinue
$report = [ordered]@{
    Timestamp = (Get-Date).ToString('o')
    OS = $os.Caption
    Build = $os.BuildNumber
    Architecture = $os.OSArchitecture
    CpuThreads = [Environment]::ProcessorCount
    DotnetSDKs = if (Get-Command dotnet -ErrorAction SilentlyContinue) { @(& dotnet --list-sdks) } else { @() }
    NexusProcesses = @($process | Select-Object -Property @('Id','WorkingSet64','CPU','Responding'))
}
$report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'environment.json') -Encoding UTF8
$dataDirectory = Join-Path $env:LOCALAPPDATA 'WhiteDreams\NexusShell'
if (Test-Path (Join-Path $dataDirectory 'nexus.log')) { Copy-Item (Join-Path $dataDirectory 'nexus.log') $OutputDirectory -Force }
# settings.json contains personal pins and usage data, so it is deliberately excluded.
Write-Host "Diagnostics saved to $OutputDirectory"
