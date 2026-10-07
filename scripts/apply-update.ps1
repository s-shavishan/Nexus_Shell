[CmdletBinding()]
param([string]$BaseDirectory, [string]$TargetDirectory, [switch]$NoOpenFolder)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This update is for Windows x64.' }
if (-not $BaseDirectory) {
    Add-Type -AssemblyName System.Windows.Forms
    $picker = New-Object System.Windows.Forms.FolderBrowserDialog
    $picker.Description = 'Choose your existing full Nexus folder containing Nexus.Shell.exe'
    $picker.ShowNewFolderButton = $false
    try {
        if ($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
        $BaseDirectory = $picker.SelectedPath
    } finally { $picker.Dispose() }
}
$base = [IO.Path]::GetFullPath($BaseDirectory).TrimEnd([char[]]'\/')
if (-not (Test-Path (Join-Path $base 'Nexus.Shell.exe') -PathType Leaf)) { throw 'Choose the extracted app folder, not a ZIP or source folder.' }
$manifest = Get-Content (Join-Path $PSScriptRoot 'Update-Manifest.json') -Raw | ConvertFrom-Json
if ($manifest.FormatVersion -ne 1 -or $manifest.Platform -ne 'win-x64' -or $manifest.AppVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'This update manifest is invalid.' }
if (@($manifest.PayloadFiles).Count -eq 0 -or @($manifest.RuntimeFiles).Count -eq 0) { throw 'The update manifest is incomplete.' }
function Resolve-UpdatePath([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or $Relative.Contains(':')) { throw 'Invalid update file path.' }
    # Reject aliases such as ./file and folder/../file before duplicate checks.
    foreach ($part in ($Relative -split '[\\/]')) {
        if (-not $part -or $part -in @('.', '..') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or
            $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'An update file path is not canonical.' }
    }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    $path = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'An update file points outside its folder.' }
    return $path
}
function Assert-UpdateFile([string]$Root, $Record) {
    $file = Resolve-UpdatePath $Root $Record.File
    if (-not (Test-Path $file -PathType Leaf) -or (Get-Item $file).Length -ne $Record.Length -or
        (Get-FileHash $file -Algorithm SHA256).Hash -ne $Record.SHA256) {
        throw "Missing or different file: $($Record.File). Use the full release ZIP if this is a runtime file."
    }
}
$payloadRoot = Join-Path $PSScriptRoot 'payload'
$seen = @{}; $payloadFiles = @{}
foreach ($record in @($manifest.PayloadFiles) + @($manifest.RuntimeFiles)) {
    [long]$length = 0
    if (-not [long]::TryParse([string]$record.Length, [ref]$length) -or $length -lt 0 -or
        [string]$record.SHA256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid update length or hash.' }
    [void](Resolve-UpdatePath $payloadRoot ([string]$record.File))
    $key = ([string]$record.File).Replace('\', '/')
    if ($seen.ContainsKey($key)) { throw 'Duplicate file in the update manifest.' }
    $seen[$key] = $true
}
foreach ($record in $manifest.PayloadFiles) { $payloadFiles[([string]$record.File).Replace('\', '/')] = $record }
foreach ($required in @('Nexus.Shell.exe', 'Nexus.Shell.dll', 'Nexus.resources.json')) {
    if (-not $payloadFiles.ContainsKey($required)) { throw "The update payload is missing $required." }
}
foreach ($record in $manifest.RuntimeFiles) {
    $relative = ([string]$record.File).Replace('\', '/')
    if ($relative -like 'Nexus.Shell.*' -or $relative -eq 'Nexus.resources.json' -or $relative -eq 'resources.pri' -or
        $relative -like 'Assets/*' -or [IO.Path]::GetExtension($relative) -eq '.xbf') {
        throw "App-owned files must come from the new payload: $relative"
    }
}
Write-Host 'Checking update files and your existing runtime. The old app folder is retained.'
foreach ($record in $manifest.PayloadFiles) { Assert-UpdateFile $payloadRoot $record }
foreach ($record in $manifest.RuntimeFiles) { Assert-UpdateFile $base $record }
# Bind the resource report to this release and require every compiled app
# resource to come from the new payload, never from an older base application.
$report = Get-Content (Join-Path $payloadRoot 'Nexus.resources.json') -Raw | ConvertFrom-Json
if ($report.FormatVersion -ne 1 -or $report.AppVersion -ne $manifest.AppVersion -or @($report.ResourceFiles).Count -eq 0) {
    throw 'The compiled-resource report does not match this update.'
}
$hasAppIndex = $false
foreach ($resource in $report.ResourceFiles) {
    $key = ([string]$resource.File).Replace('\', '/')
    $record = $payloadFiles[$key]
    if ($null -eq $record -or $record.Length -ne $resource.Length -or $record.SHA256 -ne $resource.SHA256) {
        throw "The new payload is missing a verified app resource: $key"
    }
    if ($key -in @('resources.pri', 'Nexus.Shell.pri')) { $hasAppIndex = $true }
}
if (-not $hasAppIndex) { throw 'The new app resource index is missing.' }
if (-not $TargetDirectory) { $TargetDirectory = Join-Path (Split-Path $base -Parent) ('Nexus-Shell-' + $manifest.AppVersion + '-win-x64') }
$target = [IO.Path]::GetFullPath($TargetDirectory).TrimEnd([char[]]'\/')
if (Test-Path $target) { throw "The destination already exists: $target. Choose a new folder with -TargetDirectory." }
$basePrefix = $base + [IO.Path]::DirectorySeparatorChar
if ($target -eq $base -or $target.StartsWith($basePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Choose a destination outside the old app folder.' }
$parent = Split-Path $target -Parent
if (-not (Test-Path $parent -PathType Container)) { throw 'The destination parent folder does not exist.' }
$stage = Join-Path $parent ('Nexus-update-' + [Guid]::NewGuid().ToString('N'))
New-Item $stage -ItemType Directory | Out-Null
try {
    foreach ($record in $manifest.RuntimeFiles) {
        $destination = Resolve-UpdatePath $stage $record.File
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        Copy-Item (Resolve-UpdatePath $base $record.File) $destination
        Assert-UpdateFile $stage $record
    }
    foreach ($record in $manifest.PayloadFiles) {
        $destination = Resolve-UpdatePath $stage $record.File
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        Copy-Item (Resolve-UpdatePath $payloadRoot $record.File) $destination
        Assert-UpdateFile $stage $record
    }
    Move-Item $stage $target
    Write-Host "Nexus $($manifest.AppVersion) is ready in: $target" -ForegroundColor Green
    if (-not $NoOpenFolder) { Invoke-Item $target }
} finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
}
