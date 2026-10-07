[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# Parse all shipped scripts with the actual PowerShell parser on the build host.
$scripts = @(Get-ChildItem $PSScriptRoot -Filter '*.ps1')
foreach ($script in $scripts) {
    $tokens = $null; $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) { throw "PowerShell syntax failure in $($script.Name): $($errors -join '; ')" }
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('Nexus-update-check-' + [Guid]::NewGuid().ToString('N'))
$pub = Join-Path $fixture 'publish'
$base = Join-Path $fixture 'base'
$unpacked = Join-Path $fixture 'update'
New-Item $pub, $base -ItemType Directory -Force | Out-Null
function Assert-Check([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Reject-Update([string]$Target) {
    $failed = $false
    try { & (Join-Path $unpacked 'Apply-Update.ps1') -BaseDirectory $base -TargetDirectory $Target -NoOpenFolder }
    catch { $failed = $true }
    Assert-Check $failed 'An invalid update was accepted.'
}
try {
    Set-Content (Join-Path $pub 'Nexus.Shell.exe') 'new app' -Encoding ASCII
    Set-Content (Join-Path $pub 'Nexus.Shell.dll') 'new app assembly' -Encoding ASCII
    Set-Content (Join-Path $pub 'resources.pri') 'app index' -Encoding ASCII
    Set-Content (Join-Path $pub 'MainWindow.xbf') 'compiled UI' -Encoding ASCII
    Set-Content (Join-Path $pub 'coreclr.dll') 'same runtime' -Encoding ASCII
    Set-Content (Join-Path $pub 'Microsoft.UI.Xaml.dll') 'same WinUI' -Encoding ASCII
    $resources = @('resources.pri', 'MainWindow.xbf') | ForEach-Object {
        $file = Join-Path $pub $_
        [ordered]@{ File = $_; Length = (Get-Item $file).Length; SHA256 = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    [ordered]@{ FormatVersion = 1; AppVersion = '0.4.1'; ResourceFiles = @($resources) } |
        ConvertTo-Json -Depth 6 | Set-Content (Join-Path $pub 'Nexus.resources.json') -Encoding UTF8
    $zip = Join-Path $fixture 'Update.zip'
    & (Join-Path $PSScriptRoot 'package-update.ps1') -PublishDirectory $pub -OutputZip $zip -AppVersion '0.4.1'
    Expand-Archive $zip $unpacked
    Assert-Check (-not (Test-Path (Join-Path $unpacked 'payload\coreclr.dll'))) 'Runtime bytes must be omitted from the update.'
    Copy-Item (Join-Path $pub 'coreclr.dll'), (Join-Path $pub 'Microsoft.UI.Xaml.dll') $base
    Set-Content (Join-Path $base 'Nexus.Shell.exe') 'old app' -Encoding ASCII
    Set-Content (Join-Path $base 'resources.pri') 'old index' -Encoding ASCII
    $target = Join-Path $fixture 'new'
    & (Join-Path $unpacked 'Apply-Update.ps1') -BaseDirectory $base -TargetDirectory $target -NoOpenFolder
    Assert-Check ((Get-Content (Join-Path $target 'Nexus.Shell.exe') -Raw).Trim() -eq 'new app') 'New app was not installed.'
    Assert-Check ((Get-Content (Join-Path $base 'Nexus.Shell.exe') -Raw).Trim() -eq 'old app') 'The old folder must remain intact.'
    Assert-Check ((Get-Content (Join-Path $target 'resources.pri') -Raw).Trim() -eq 'app index') 'The new app resource index is required.'
    Assert-Check (Test-Path (Join-Path $target 'MainWindow.xbf')) 'Loose compiled UI was omitted.'
    Reject-Update $target
    Set-Content (Join-Path $base 'coreclr.dll') 'different runtime' -Encoding ASCII
    $bad = Join-Path $fixture 'bad-runtime'
    Reject-Update $bad
    Assert-Check (-not (Test-Path $bad)) 'A runtime mismatch must fail before creating an app folder.'
    Copy-Item (Join-Path $pub 'coreclr.dll') $base -Force
    Set-Content (Join-Path $unpacked 'payload\Nexus.Shell.exe') 'damaged' -Encoding ASCII
    $bad = Join-Path $fixture 'bad-payload'
    Reject-Update $bad
    Assert-Check (-not (Test-Path $bad)) 'A damaged update must fail before creating an app folder.'
    Copy-Item (Join-Path $pub 'Nexus.Shell.exe') (Join-Path $unpacked 'payload') -Force
    $manifestPath = Join-Path $unpacked 'Update-Manifest.json'
    $originalManifest = Get-Content $manifestPath -Raw
    # An app binary/resource must not be silently reused as a runtime file.
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $appRecord = @($manifest.PayloadFiles | Where-Object { $_.File -eq 'Nexus.Shell.exe' })[0]
    $manifest.PayloadFiles = @($manifest.PayloadFiles | Where-Object { $_.File -ne 'Nexus.Shell.exe' })
    $manifest.RuntimeFiles = @($manifest.RuntimeFiles) + @($appRecord)
    Copy-Item (Join-Path $pub 'Nexus.Shell.exe') $base -Force
    $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding UTF8
    Reject-Update (Join-Path $fixture 'reused-app')
    Set-Content $manifestPath $originalManifest -Encoding UTF8
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $priRecord = @($manifest.PayloadFiles | Where-Object { $_.File -eq 'resources.pri' })[0]
    $manifest.PayloadFiles = @($manifest.PayloadFiles | Where-Object { $_.File -ne 'resources.pri' })
    $manifest.RuntimeFiles = @($manifest.RuntimeFiles) + @($priRecord)
    Copy-Item (Join-Path $pub 'resources.pri') $base -Force
    $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding UTF8
    Reject-Update (Join-Path $fixture 'reused-pri')
    Set-Content $manifestPath $originalManifest -Encoding UTF8
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest.PayloadFiles[0].File = './' + $manifest.PayloadFiles[0].File
    $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding UTF8
    Reject-Update (Join-Path $fixture 'alias-path')
    Set-Content $manifestPath $originalManifest -Encoding UTF8
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest.PayloadFiles[0].SHA256 = 'bad-hash'
    $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding UTF8
    Reject-Update (Join-Path $fixture 'bad-hash')
    Set-Content $manifestPath $originalManifest -Encoding UTF8
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest.PayloadFiles[0].File = '../outside.txt'
    $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding UTF8
    Reject-Update (Join-Path $fixture 'bad-path')
    Assert-Check (-not (Test-Path (Join-Path $fixture 'outside.txt'))) 'An update must stay inside its folders.'
    Write-Host 'PASS: runtime reuse, folder preservation, hashes, destinations, canonical paths, and new app/resource payload requirements.' -ForegroundColor Green
} finally { Remove-Item $fixture -Recurse -Force }
