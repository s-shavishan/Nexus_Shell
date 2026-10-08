[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [Parameter(Mandatory=$true)][string]$OutputZip,
    [Parameter(Mandatory=$true)][string]$AppVersion
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PublishDirectory).TrimEnd([char[]]'\/')
$prefix = $root + [IO.Path]::DirectorySeparatorChar
if (-not (Test-Path (Join-Path $root 'Nexus.Shell.exe') -PathType Leaf)) { throw 'The published executable is missing.' }
if (-not (Test-Path (Join-Path $root 'Nexus.Shell.dll') -PathType Leaf)) { throw 'The published app assembly is missing.' }
foreach ($name in @('Nexus.DesktopHost.exe', 'Nexus.DesktopHost.dll', 'Nexus.DesktopHost.deps.json', 'Nexus.DesktopHost.runtimeconfig.json')) {
    if (-not (Test-Path (Join-Path $root $name) -PathType Leaf)) { throw "The published desktop host is missing $name." }
}
$report = Get-Content (Join-Path $root 'Nexus.resources.json') -Raw | ConvertFrom-Json
if ($report.FormatVersion -ne 1 -or $report.AppVersion -ne $AppVersion -or @($report.ResourceFiles).Count -eq 0) { throw 'Run the compiled-resource check before creating an update.' }
$payload = @(); $runtime = @()
$all = @(Get-ChildItem $root -Recurse -File | Sort-Object FullName)
foreach ($file in $all) {
    $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
    $record = [ordered]@{ File = $relative; Length = $file.Length; SHA256 = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    # Ship app binaries, loose compiled XAML and app assets. All other files
    # must already exist with these exact bytes in the user's full base folder.
    $owned = $relative -like 'Nexus.Shell.*' -or $relative -like 'Nexus.DesktopHost.*' -or $relative -eq 'Nexus.resources.json' -or
        $relative -eq 'resources.pri' -or $relative -like 'Assets/*' -or
        $file.Extension -eq '.xbf' -or $file.Extension -in @('.md', '.ps1', '.bat')
    if ($owned) { $payload += $record } else { $runtime += $record }
}
if ($runtime.Count -eq 0) { throw 'No runtime files were found. This is not a reusable-runtime update.' }
foreach ($resource in $report.ResourceFiles) {
    $included = @($payload | Where-Object { $_.File -eq $resource.File -and $_.SHA256 -eq $resource.SHA256 })
    if ($included.Count -ne 1) { throw "The update would omit/change the verified resource: $($resource.File)" }
}
$stage = Join-Path ([IO.Path]::GetTempPath()) ('Nexus-package-' + [Guid]::NewGuid().ToString('N'))
New-Item (Join-Path $stage 'payload') -ItemType Directory -Force | Out-Null
try {
    foreach ($record in $payload) {
        $destination = Join-Path (Join-Path $stage 'payload') $record.File
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        Copy-Item (Join-Path $root $record.File) $destination
    }
    $manifest = [ordered]@{ FormatVersion = 1; AppVersion = $AppVersion; Platform = 'win-x64'; PayloadFiles = @($payload); RuntimeFiles = @($runtime) }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $stage 'Update-Manifest.json') -Encoding UTF8
    Copy-Item (Join-Path $PSScriptRoot 'apply-update.ps1') (Join-Path $stage 'Apply-Update.ps1')
    @('@echo off', 'powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Apply-Update.ps1"', 'if errorlevel 1 pause') |
        Set-Content (Join-Path $stage 'Apply-Update.bat') -Encoding ASCII
    @'
NEXUS SMALL UPDATE

1. Extract the entire Update ZIP into a new folder.
2. Close Nexus, then double-click Apply-Update.bat.
3. Paste the path of your existing full Nexus folder containing Nexus.Shell.exe.
4. The updater checks the runtime files and creates a NEW version folder.
5. Run Nexus.Shell.exe from that new folder. Your old folder is retained.

No SDK or separate runtime installation is needed.
If runtime files are missing or different, use this release's full ZIP instead.
Do not copy only the EXE or mix UI resource files from different builds.
If desktop sign-in points to your old folder, select "Use this version at sign-in"
in the new version's Personalize page. Keep the old folder until this succeeds.
'@ | Set-Content (Join-Path $stage 'UPDATE-README.txt') -Encoding UTF8
    New-Item (Split-Path ([IO.Path]::GetFullPath($OutputZip)) -Parent) -ItemType Directory -Force | Out-Null
    Compress-Archive -Path "$stage\*" -DestinationPath $OutputZip -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($OutputZip))
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) { $entries[$entry.FullName.Replace('\', '/')] = $entry }
        foreach ($record in $payload) {
            $entry = $entries['payload/' + $record.File]
            if ($null -eq $entry -or $entry.Length -ne $record.Length) { throw "The update ZIP omitted/changed $($record.File)." }
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose(); $stream.Dispose() }
            if ($hash -ne $record.SHA256) { throw "The update ZIP bytes changed: $($record.File)" }
        }
    } finally { $archive.Dispose() }
    $hash = (Get-FileHash $OutputZip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content "$OutputZip.sha256" "$hash  $([IO.Path]::GetFileName($OutputZip))" -Encoding ASCII
    Write-Host ("Small update verified: {0:N2} MB; {1} runtime files reused after compatibility checks." -f ((Get-Item $OutputZip).Length / 1MB), $runtime.Count) -ForegroundColor Green
} finally { Remove-Item $stage -Recurse -Force }
