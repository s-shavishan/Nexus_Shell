[CmdletBinding()]
param(
    [switch]$Run,
    [switch]$UseMSBuild,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$projectFile = Join-Path $projectRoot 'src\Nexus.Shell\Nexus.Shell.csproj'
$publishDirectory = Join-Path $projectRoot 'artifacts\Nexus-Shell-1.5.0-win-x64'
$logDirectory = Join-Path $projectRoot 'artifacts\logs'

if ($env:OS -ne 'Windows_NT') { throw 'WinUI must be built on Windows. Use an included Windows cloud-build route in START-HERE.md.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK is missing. To avoid local SDK downloads, use a cloud-build route in START-HERE.md. The script does not install tools.'
}
$sdkVersions = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdkVersions -match '^8\.0\.')) { throw 'Install .NET 8 SDK (x64), or use a Windows cloud-build route. A runtime alone cannot compile source.' }
New-Item $publishDirectory -ItemType Directory -Force | Out-Null
New-Item $logDirectory -ItemType Directory -Force | Out-Null
$binlog = Join-Path $logDirectory 'build.binlog'
$buildLog = Join-Path $logDirectory 'build.txt'
$properties = @('/p:Platform=x64', '/p:RuntimeIdentifier=win-x64', '/p:SelfContained=true', '/p:WindowsAppSDKSelfContained=true', "/p:Configuration=$Configuration", "/p:PublishDir=$publishDirectory/")
$msbuild = $null
if ($UseMSBuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) { $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1 }
    if (-not $msbuild) { throw 'Visual Studio MSBuild was not found. Omit -UseMSBuild to try dotnet publish, or use a Windows cloud-build route.' }
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
    if ($buildExit -ne 0) { throw "Build failed with exit code $buildExit. Logs: $buildLog. If this is a XAML compiler/MSBuild task error, try scripts\build.ps1 -UseMSBuild or a Windows cloud-build route." }
    # Publish the desktop host with the same SDK/runtime, then merge its owned
    # files. Shared runtime files must match the app publish byte-for-byte.
    $hostProject = Join-Path $projectRoot 'src\Nexus.DesktopHost\Nexus.DesktopHost.csproj'
    $hostOutput = Join-Path $projectRoot 'artifacts\desktop-host'
    if (Test-Path $hostOutput) { Remove-Item $hostOutput -Recurse -Force }
    & dotnet publish $hostProject --configuration $Configuration --runtime win-x64 --self-contained true --output $hostOutput 2>&1 | Tee-Object -FilePath (Join-Path $logDirectory 'desktop-host-build.txt')
    if ($LASTEXITCODE -ne 0) { throw 'The Nexus desktop host could not be published.' }
    foreach ($hostFile in Get-ChildItem $hostOutput -Recurse -File) {
        $relative = $hostFile.FullName.Substring($hostOutput.Length + 1)
        $destination = Join-Path $publishDirectory $relative
        if ($relative -like 'Nexus.DesktopHost.*') {
            New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
            Copy-Item $hostFile.FullName $destination -Force
        } elseif (-not (Test-Path $destination) -or (Get-FileHash $destination -Algorithm SHA256).Hash -ne (Get-FileHash $hostFile.FullName -Algorithm SHA256).Hash) {
            throw "Desktop host runtime differs from the shell runtime: $relative. Publish both with the same .NET SDK."
        }
    }
    foreach ($name in @('Nexus.DesktopHost.exe', 'Nexus.DesktopHost.dll', 'Nexus.DesktopHost.deps.json', 'Nexus.DesktopHost.runtimeconfig.json')) {
        if (-not (Test-Path (Join-Path $publishDirectory $name))) { throw "Desktop host publish is incomplete: $name" }
    }
    $exe = Join-Path $publishDirectory 'Nexus.Shell.exe'
    if (-not (Test-Path $exe)) { throw 'Build returned success without Nexus.Shell.exe.' }
    if (-not (Get-ChildItem $publishDirectory -Recurse -Filter 'Microsoft.UI.Xaml.dll')) { throw 'The native WinUI runtime is missing from the publish directory.' }
    [xml]$projectXml = Get-Content $projectFile -Raw
    $toolsReference = $projectXml.SelectSingleNode("//PackageReference[@Include='Microsoft.Windows.SDK.BuildTools']")
    $packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages' }
    $buildTools = Join-Path $packageRoot ("microsoft.windows.sdk.buildtools\" + $toolsReference.GetAttribute('Version'))
    $makePri = Get-ChildItem $buildTools -Recurse -File -Filter 'makepri.exe' | Where-Object { $_.FullName -match '[\\/]x64[\\/]' } | Sort-Object FullName -Descending | Select-Object -First 1
    if ($null -eq $makePri) { throw 'The restored Windows SDK BuildTools package does not contain x64 MakePri.exe.' }
    $appVersion = $projectXml.SelectSingleNode('//PropertyGroup/Version').InnerText
    & (Join-Path $PSScriptRoot 'verify-resources.ps1') -PublishDirectory $publishDirectory -MakePri $makePri.FullName -DumpFile (Join-Path $logDirectory 'app-resources.xml') -AppVersion $appVersion
    Copy-Item (Join-Path $projectRoot 'docs\TEST-WINDOWS.md') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'docs\RUN-PORTABLE.md') (Join-Path $publishDirectory 'READ-ME-FIRST.md')
    Copy-Item (Join-Path $projectRoot 'scripts\diagnostics.ps1') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'scripts\disable-startup.ps1') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'scripts\measure-resources.ps1') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'docs\DESIGN-AND-PERFORMANCE.md') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'docs\UPDATING.md') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'docs\NEXUS-DESKTOP-MODE.md') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'docs\TEST-DESKTOP-MODE.md') $publishDirectory
    Copy-Item (Join-Path $projectRoot 'scripts\restore-windows-desktop.ps1') $publishDirectory
    Set-Content (Join-Path $publishDirectory 'Launch-Nexus.bat') "@echo off`r`nstart `"`" `"%~dp0Nexus.Shell.exe`"" -Encoding ASCII
    Set-Content (Join-Path $publishDirectory 'Launch-Nexus-Desktop.bat') "@echo off`r`nstart `"`" `"%~dp0Nexus.DesktopHost.exe`" --nexus-session" -Encoding ASCII
    Set-Content (Join-Path $publishDirectory 'Restore-Windows-Desktop.bat') "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0restore-windows-desktop.ps1`"`r`nif errorlevel 1 pause" -Encoding ASCII
    Write-Host 'Build and MainWindow resource verification complete. Keep the entire output folder together.' -ForegroundColor Green
    if ($Run) { Start-Process $exe -WorkingDirectory $publishDirectory }
} finally { Pop-Location }
