[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [Parameter(Mandatory=$true)][string]$MakePri,
    [Parameter(Mandatory=$true)][string]$DumpFile,
    [Parameter(Mandatory=$true)][string]$AppVersion
)
$ErrorActionPreference = 'Stop'
$publishRoot = [IO.Path]::GetFullPath($PublishDirectory).TrimEnd([char[]]'\/')
$rootPrefix = $publishRoot + [IO.Path]::DirectorySeparatorChar
$pri = Join-Path $publishRoot 'resources.pri'
if (-not (Test-Path $pri -PathType Leaf)) { $pri = Join-Path $publishRoot 'Nexus.Shell.pri' }
if (-not (Test-Path $pri -PathType Leaf)) {
    throw 'The app PRI is missing. Native runtime PRI files alone cannot supply MainWindow.xaml.'
}
if (-not (Test-Path $MakePri -PathType Leaf)) { throw 'MakePri.exe is required on the build server to verify the resource index.' }
New-Item (Split-Path $DumpFile -Parent) -ItemType Directory -Force | Out-Null
& $MakePri dump /if $pri /of $DumpFile /dt Basic /o
if ($LASTEXITCODE -ne 0) { throw "MakePri could not read the published app PRI (exit $LASTEXITCODE)." }
[xml]$index = Get-Content $DumpFile -Raw
# The URI must be at Files/MainWindow, matching ms-appx:///MainWindow.xaml.
# A similarly named resource under a different component/folder is insufficient.
$windows = @($index.SelectNodes("//*[local-name()='NamedResource']") | Where-Object {
    $_.GetAttribute('uri') -match '/Files/MainWindow\.(xbf|xaml)$'
})
if ($windows.Count -eq 0) { throw 'The published app PRI does not index the root MainWindow XAML/XBF resource. See the PRI dump in artifacts\logs.' }
$candidateTypes = @()
foreach ($window in $windows) {
    $candidates = @($window.SelectNodes("./*[local-name()='Candidate']"))
    if ($candidates.Count -eq 0) { throw 'MainWindow has no resource candidates.' }
    foreach ($candidate in $candidates) {
        $kind = $candidate.GetAttribute('type')
        $candidateTypes += $kind
        if ($kind -eq 'EmbeddedData') { continue }
        if ($kind -ne 'Path') { throw "Unexpected MainWindow candidate type: $kind" }
        $value = $candidate.SelectSingleNode("./*[local-name()='Value']")
        if ($null -eq $value -or [string]::IsNullOrWhiteSpace($value.InnerText)) { throw 'MainWindow has an empty resource path.' }
        $relative = $value.InnerText.Replace('/', '\')
        if ([IO.Path]::IsPathRooted($relative)) { throw "MainWindow still references a build-machine path: $relative" }
        $file = [IO.Path]::GetFullPath((Join-Path $publishRoot $relative))
        if (-not $file.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path $file -PathType Leaf)) {
            throw "MainWindow references a missing/outside publish file: $relative"
        }
    }
}
# Keep fingerprints for the actual app index and every loose compiled XAML file.
# The package script verifies these bytes again inside the finished ZIP.
$files = @(Get-Item $pri) + @(Get-ChildItem $publishRoot -Recurse -File -Filter '*.xbf')
$records = @($files | ForEach-Object {
    [ordered]@{
        File = $_.FullName.Substring($rootPrefix.Length).Replace('\', '/')
        Length = $_.Length
        SHA256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$report = [ordered]@{
    FormatVersion = 1
    AppVersion = $AppVersion
    MainWindowUri = @($windows | ForEach-Object { $_.GetAttribute('uri') })
    CandidateTypes = @($candidateTypes | Select-Object -Unique)
    ResourceFiles = $records
}
$reportPath = Join-Path $publishRoot 'Nexus.resources.json'
$report | ConvertTo-Json -Depth 6 | Set-Content $reportPath -Encoding UTF8
Copy-Item $reportPath (Join-Path (Split-Path $DumpFile -Parent) 'resources-check.json') -Force
Write-Host "Verified MainWindow compiled resource: $($report.MainWindowUri -join ', ')" -ForegroundColor Green
