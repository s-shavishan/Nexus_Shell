[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$publishDirectory = Join-Path $projectRoot 'artifacts\Nexus-Shell-1.3.0-win-x64'
if (-not (Test-Path (Join-Path $publishDirectory 'Nexus.Shell.exe'))) { throw 'Build first: scripts\build.ps1' }
$reportPath = Join-Path $publishDirectory 'Nexus.resources.json'
if (-not (Test-Path $reportPath)) { throw 'The published resources have not been verified. Run scripts\build.ps1 first.' }
$report = Get-Content $reportPath -Raw | ConvertFrom-Json
if ($report.FormatVersion -ne 1 -or $report.AppVersion -ne '1.3.0' -or @($report.ResourceFiles).Count -eq 0) { throw 'The resource report does not match this package.' }
foreach ($resource in $report.ResourceFiles) {
    $file = Join-Path $publishDirectory $resource.File
    if (-not (Test-Path $file -PathType Leaf) -or (Get-FileHash $file -Algorithm SHA256).Hash -ne $resource.SHA256) {
        throw "The compiled resource changed after verification: $($resource.File)"
    }
}
$zip = Join-Path $projectRoot 'artifacts\Nexus-Shell-1.3.0-win-x64.zip'
Compress-Archive -Path "$publishDirectory\*" -DestinationPath $zip -Force
# Verify the ZIP itself, rather than assuming every build file was compressed.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @{}
    foreach ($zipEntry in $archive.Entries) { $entries[$zipEntry.FullName.Replace('\', '/')] = $zipEntry }
    foreach ($resource in $report.ResourceFiles) {
        $entry = $entries[$resource.File]
        if ($null -eq $entry -or $entry.Length -ne $resource.Length) { throw "The ZIP omitted/changed $($resource.File)." }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actualHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($actualHash -ne $resource.SHA256) { throw "The ZIP resource bytes do not match: $($resource.File)" }
    }
    if ($null -eq $entries['Nexus.DesktopHost.exe'] -or $null -eq $entries['Nexus.DesktopHost.dll'] -or $null -eq $entries['Nexus.DesktopHost.deps.json'] -or $null -eq $entries['Nexus.DesktopHost.runtimeconfig.json'] -or $null -eq $entries['Restore-Windows-Desktop.bat'] -or $null -eq $entries['Nexus.Shell.exe'] -or $null -eq $entries['Nexus.resources.json']) { throw 'The ZIP is missing the executable or resource verification report.' }
} finally { $archive.Dispose() }
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$zip.sha256" "$hash  Nexus-Shell-1.3.0-win-x64.zip" -Encoding ASCII
Write-Host $zip
Write-Host 'Verified the compiled resource files inside the portable ZIP.' -ForegroundColor Green
& (Join-Path $PSScriptRoot 'package-update.ps1') -PublishDirectory $publishDirectory -OutputZip (Join-Path $projectRoot 'artifacts\Nexus-Shell-1.3.0-Update-win-x64.zip') -AppVersion '1.3.0'
