[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$publishDirectory = Join-Path $projectRoot 'artifacts\Nexus-Shell-0.2.1-win-x64'
if (-not (Test-Path (Join-Path $publishDirectory 'Nexus.Shell.exe'))) { throw 'Build first: scripts\build.ps1' }
$zip = Join-Path $projectRoot 'artifacts\Nexus-Shell-0.2.1-win-x64.zip'
Compress-Archive -Path "$publishDirectory\*" -DestinationPath $zip -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$zip.sha256" "$hash  Nexus-Shell-0.2.1-win-x64.zip" -Encoding ASCII
Write-Host $zip
