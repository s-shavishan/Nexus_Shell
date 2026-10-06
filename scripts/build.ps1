[CmdletBinding()]
param(
    [switch]$Run,
    [switch]$UseMSBuild,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$projectFile = Join-Path $projectRoot 'src\Nexus.Shell\Nexus.Shell.csproj'
$publishDirectory = Join-Path $projectRoot 'artifacts\Nexus-Shell-0.2.0-win-x64'
$logDirectory = Join-Path $projectRoot 'artifacts\logs'

if ($env:OS -ne 'Windows_NT') { throw 'WinUI must be built on Windows. Use the included GitHub Actions workflow.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK is missing. To avoid local SDK downloads, use the GitHub build route in START-HERE.md. The script does not install tools.'
}
$sdkVersions = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdkVersions -match '^8\.0\.')) { throw 'Install .NET 8 SDK (x64), or use the GitHub build route. A runtime alone cannot compile source.' }
New-Item $publishDirectory -ItemType Directory -Force | Out-Null
New-Item $logDirectory -ItemType Directory -Force | Out-Null
$binlog = Join-Path $logDirectory 'build.binlog'
$buildLog = Join-Path $logDirectory 'build.txt'
$properties = @('/p:Platform=x64', '/p:RuntimeIdentifier=win-x64', '/p:SelfContained=true', '/p:WindowsAppSDKSelfContained=true', "/p:Configuration=$Configuration", "/p:PublishDir=$publishDirectory/")
$msbuild = $null
if ($UseMSBuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) { $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1 }
    if (-not $msbuild) { throw 'Visual Studio MSBuild was not found. Omit -UseMSBuild to try dotnet publish, or use the GitHub workflow.' }
}
Push-Location $projectRoot
try {
    Write-Host 'Building a native x64 Windows preview. The first restore downloads NuGet dependencies.'
    Write-Host "Output: $publishDirectory"
    if ($UseMSBuild) {
        & $msbuild $projectFile /restore /t:Publish /m /nologo "/bl:$binlog" @properties 2>&1 | Tee-Object -FilePath $buildLog
    } else {
        & dotnet publish $projectFile --configuration $Configuration --runtime win-x64 --self-contained true --output $publishDirectory '/p:Platform=x64' '/p:WindowsAppSDKSelfContained=true' "/bl:$binlog" 2>&1 | Tee-Object -FilePath $buildLog
    }
    $buildExit = $LASTEXITCODE
    if ($buildExit -ne 0) { throw "Build failed with exit code $buildExit. Logs: $buildLog. If this is a XAML compiler/MSBuild task error, try scripts\build.ps1 -UseMSBuild or the GitHub workflow." }
    $exe = Join-Path $publishDirectory 'Nexus.Shell.exe'
    if (-not (Test-Path $exe)) { throw 'Build returned success without Nexus.Shell.exe.' }
    if (-not (Get-ChildItem $publishDirectory -Recurse -Filter 'Microsoft.UI.Xaml.dll')) { throw 'The native WinUI runtime is missing from the publish directory.' }
    Copy-Item (Join-Path $projectRoot 'docs\TEST-WINDOWS.md') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'docs\RUN-PORTABLE.md') (Join-Path $publishDirectory 'READ-ME-FIRST.md')
    Copy-Item (Join-Path $projectRoot 'scripts\diagnostics.ps1') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'scripts\disable-startup.ps1') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'scripts\measure-resources.ps1') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'docs\DESIGN-AND-PERFORMANCE.md') $publishDirectory
    Set-Content (Join-Path $publishDirectory 'Launch-Nexus.bat') "@echo off`r`nstart `"`" `"%~dp0Nexus.Shell.exe`"" -Encoding ASCII
    Write-Host 'Build complete. Keep the entire output folder together.' -ForegroundColor Green
    if ($Run) { Start-Process $exe -WorkingDirectory $publishDirectory }
} finally { Pop-Location }
