[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceDirectory,
    [Parameter(Mandatory=$true)][string]$OutputFile
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($SourceDirectory)
$templates = @()
foreach ($file in Get-ChildItem $root -Filter '*.cs' -Recurse -File | Sort-Object FullName) {
    $text = Get-Content $file.FullName -Raw
    foreach ($match in [regex]::Matches($text, 'XamlReader\.Load\(\s*"""([\s\S]*?)"""\s*\)')) {
        $xml = $match.Groups[1].Value.Trim()
        [xml]$parsed = $xml
        if ($parsed.DocumentElement.LocalName -notin @('DataTemplate','ItemsPanelTemplate')) { throw "Unexpected UI check template in $($file.Name)." }
        $templates += [ordered]@{ Source = $file.FullName.Substring($root.Length + 1).Replace('\','/'); Xml = $xml }
    }
    foreach ($match in [regex]::Matches($text, 'XamlReader\.Load\("(<ItemsPanelTemplate[^"\r\n]*)"\)')) {
        $xml = $match.Groups[1].Value
        [xml]$parsed = $xml
        $templates += [ordered]@{ Source = $file.FullName.Substring($root.Length + 1).Replace('\','/'); Xml = $xml }
    }
}
if ($templates.Count -eq 0) { throw 'No production UI templates were exported.' }
New-Item (Split-Path ([IO.Path]::GetFullPath($OutputFile)) -Parent) -ItemType Directory -Force | Out-Null
[ordered]@{ FormatVersion = 1; Templates = @($templates) } | ConvertTo-Json -Depth 6 | Set-Content $OutputFile -Encoding UTF8
Write-Host "Exported $($templates.Count) production templates for native WinUI validation."
