[CmdletBinding()]
param([switch]$NoStartExplorer)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Desktop recovery runs on Windows.' }
$backupPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WhiteDreams\NexusShell\desktop-shell-backup.json'
if (-not (Test-Path $backupPath -PathType Leaf)) { throw 'No saved Nexus desktop setting was found. No registry value changed.' }
if ((Get-Item $backupPath).Length -gt 65536) { throw 'The desktop recovery record is too large.' }
$backup = Get-Content $backupPath -Raw | ConvertFrom-Json
if ($backup.Format -ne 1 -or [string]::IsNullOrWhiteSpace($backup.Command)) { throw 'The desktop recovery record is invalid.' }
foreach ($saved in @($backup.PreviousShell, $backup.PreviousStartup)) {
    if ($null -ne $saved -and ([int]$saved.Kind -notin @(1,2) -or $null -eq $saved.Text)) { throw 'The saved registry value is invalid.' }
}
$shellPath = 'Software\Microsoft\Windows\CurrentVersion\Policies\System'
$runPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
$shell = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($shellPath)
try {
    $current = $shell.GetValue('Shell', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    $alreadyRestored = if ($null -eq $backup.PreviousShell) { $null -eq $current } else {
        $current -ceq $backup.PreviousShell.Text -and $shell.GetValueKind('Shell') -eq [Microsoft.Win32.RegistryValueKind][int]$backup.PreviousShell.Kind
    }
    if (-not $alreadyRestored -and $current -cne $backup.Command) { throw 'Another desktop policy is now configured. Recovery will not overwrite it.' }
    if (-not $alreadyRestored) {
        if ($null -eq $backup.PreviousShell) { $shell.DeleteValue('Shell', $false) }
        else { $shell.SetValue('Shell', [string]$backup.PreviousShell.Text, [Microsoft.Win32.RegistryValueKind][int]$backup.PreviousShell.Kind) }
    }
} finally { $shell.Dispose() }
$run = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runPath)
try {
    if ($null -eq $run.GetValue('WhiteDreamsNexusShell') -and $null -ne $backup.PreviousStartup) {
        $run.SetValue('WhiteDreamsNexusShell', [string]$backup.PreviousStartup.Text, [Microsoft.Win32.RegistryValueKind][int]$backup.PreviousStartup.Kind)
    }
} finally { $run.Dispose() }
Write-Host 'Your previous desktop sign-in setting is restored.' -ForegroundColor Green
if (-not $NoStartExplorer) {
    # Stop only Nexus executables from the selected folder in this session.
    # Stop the host first so it cannot restart the UI during recovery.
    $hostPath = ([string]$backup.Command).Trim('"')
    if (-not [IO.Path]::IsPathRooted($hostPath) -or [IO.Path]::GetFileName($hostPath) -ine 'Nexus.DesktopHost.exe') { throw 'The saved Nexus host path is invalid.' }
    $shellExe = Join-Path ([IO.Path]::GetDirectoryName($hostPath)) 'Nexus.Shell.exe'
    $sessionId = (Get-Process -Id $PID).SessionId
    foreach ($entry in @(@{ Name = 'Nexus.DesktopHost'; Path = $hostPath }, @{ Name = 'Nexus.Shell'; Path = $shellExe })) {
        foreach ($process in Get-Process -Name $entry.Name -ErrorAction SilentlyContinue) {
            try { if ($process.SessionId -eq $sessionId -and $process.Path -ieq $entry.Path) { Stop-Process -Id $process.Id -Force -ErrorAction Stop } }
            catch { Write-Warning ('Could not stop a Nexus process: ' + $_.Exception.Message) }
        }
    }
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class NexusRecoveryWorkArea {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Info { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref Info info);
    [DllImport("user32.dll", EntryPoint="SystemParametersInfoW")] static extern bool SetWorkArea(uint action, uint parameter, ref Rect area, uint flags);
    public static void Reset() { var info = new Info { Size = (uint)Marshal.SizeOf(typeof(Info)) }; if (GetMonitorInfo(MonitorFromWindow(IntPtr.Zero, 1), ref info)) SetWorkArea(0x002F, 0, ref info.Monitor, 2); }
}
'@
    [NexusRecoveryWorkArea]::Reset()
    Start-Process (Join-Path $env:WINDIR 'explorer.exe')
}
