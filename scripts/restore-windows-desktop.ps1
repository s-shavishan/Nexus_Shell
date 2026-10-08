[CmdletBinding()]
param([switch]$NoStartExplorer)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Desktop recovery runs on Windows.' }

function Read-DesktopValue([string]$Path, [string]$Name) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Path)
    try {
        if ($null -eq $key -or $Name -notin $key.GetValueNames()) { return $null }
        $kind = [int]$key.GetValueKind($Name)
        if ($kind -notin @(1,2)) { throw 'The current desktop registry value has an unsupported type.' }
        return [pscustomobject]@{ Text = [string]$key.GetValue($Name, '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames); Kind = $kind }
    } finally { if ($null -ne $key) { $key.Dispose() } }
}
function Same-DesktopValue($Left, $Right) {
    if ($null -eq $Left -or $null -eq $Right) { return $null -eq $Left -and $null -eq $Right }
    return $Left.Text -ceq $Right.Text -and [int]$Left.Kind -eq [int]$Right.Kind
}
function Write-DesktopValue([string]$Path, [string]$Name, $Value) {
    if (Same-DesktopValue (Read-DesktopValue $Path $Name) $Value) { return }
    $rights = [Security.AccessControl.RegistryRights]([int][Security.AccessControl.RegistryRights]::QueryValues -bor [int][Security.AccessControl.RegistryRights]::SetValue)
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Path, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, $rights)
    if ($null -eq $key) { $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($Path) }
    try {
        if ($null -eq $Value) { $key.DeleteValue($Name, $false) }
        else { $key.SetValue($Name, [string]$Value.Text, [Microsoft.Win32.RegistryValueKind][int]$Value.Kind) }
    } finally { $key.Dispose() }
}

$policyError = $null; $backup = $null
$dataFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WhiteDreams\NexusShell'
$backupPath = Join-Path $dataFolder 'desktop-shell-backup.json'
try {
    if (Test-Path $backupPath -PathType Leaf) {
        if ((Get-Item $backupPath).Length -gt 65536) { throw 'The desktop recovery record is too large.' }
        $backup = Get-Content $backupPath -Raw | ConvertFrom-Json
        if ($backup.Format -ne 1 -or [string]::IsNullOrWhiteSpace($backup.Command)) { throw 'The desktop recovery record is invalid.' }
        foreach ($saved in @($backup.PreviousShell, $backup.PreviousStartup)) {
            if ($null -ne $saved -and ([int]$saved.Kind -notin @(1,2) -or $null -eq $saved.Text)) { throw 'The saved registry value is invalid.' }
        }
        $shellPath = 'Software\Microsoft\Windows\CurrentVersion\Policies\System'
        $runPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
        $current = Read-DesktopValue $shellPath 'Shell'
        if (-not (Same-DesktopValue $current $backup.PreviousShell)) {
            if ($null -eq $current -or $current.Text -cne $backup.Command) { throw 'Another desktop policy is now configured. Recovery will not overwrite it.' }
            Write-DesktopValue $shellPath 'Shell' $backup.PreviousShell
        }
        if ($null -eq (Read-DesktopValue $runPath 'WhiteDreamsNexusShell') -and $null -ne $backup.PreviousStartup) {
            Write-DesktopValue $runPath 'WhiteDreamsNexusShell' $backup.PreviousStartup
        }
        Write-Host 'Your previous desktop sign-in setting is restored.' -ForegroundColor Green
    } else { Write-Host 'No Nexus sign-in setting was saved; sign-in remains unchanged.' }
} catch { $policyError = $_; Write-Warning ('Sign-in settings could not be restored: ' + $_.Exception.Message) }
if ($NoStartExplorer) { if ($null -ne $policyError) { throw $policyError }; return }

# Current-session recovery always continues, including denied sign-in policy data.
$sessionId = (Get-Process -Id $PID).SessionId
$sessionRecord = $null
$sessionPath = Join-Path $dataFolder ('desktop-session-' + $sessionId + '.json')
try {
    if (Test-Path $sessionPath -PathType Leaf) {
        if ((Get-Item $sessionPath).Length -gt 65536) { throw 'The saved desktop session is too large.' }
        $candidate = Get-Content $sessionPath -Raw | ConvertFrom-Json
        if ($candidate.Format -ne 1 -or $candidate.SessionId -ne $sessionId -or
            -not [IO.Path]::IsPathRooted($candidate.HostPath) -or [IO.Path]::GetFileName($candidate.HostPath) -ine 'Nexus.DesktopHost.exe' -or
            $null -eq $candidate.Surfaces -or @($candidate.Surfaces).Count -gt 256) { throw 'The saved desktop session is invalid.' }
        $m = $candidate.Monitor; $w = $candidate.Work
        if ($null -eq $m -or $null -eq $w -or $m.Left -ge $m.Right -or $m.Top -ge $m.Bottom -or
            $w.Left -ge $w.Right -or $w.Top -ge $w.Bottom -or $w.Left -lt $m.Left -or $w.Top -lt $m.Top -or
            $w.Right -gt $m.Right -or $w.Bottom -gt $m.Bottom) { throw 'The saved desktop working area is invalid.' }
        foreach ($surface in $candidate.Surfaces) {
            if ($surface.Handle -eq 0 -or $surface.ProcessId -le 0 -or $surface.Visible -isnot [bool] -or
                $surface.ClassName -notin @('Progman','WorkerW','Shell_TrayWnd','Shell_SecondaryTrayWnd')) { throw 'The saved desktop surface is invalid.' }
        }
        $sessionRecord = $candidate
    }
} catch { Write-Warning ('Could not read saved desktop visibility: ' + $_.Exception.Message) }
# Stop only the matching Nexus folder in this interactive session; host first.
$folder = if (Test-Path (Join-Path $PSScriptRoot 'Nexus.DesktopHost.exe')) { $PSScriptRoot } else { $null }
if ($null -eq $folder -and $null -ne $sessionRecord) { $folder = [IO.Path]::GetDirectoryName($sessionRecord.HostPath) }
if ($null -eq $folder -and $null -ne $backup -and $backup.Format -eq 1) {
    $savedHost = ([string]$backup.Command).Trim('"')
    if ([IO.Path]::IsPathRooted($savedHost) -and [IO.Path]::GetFileName($savedHost) -ieq 'Nexus.DesktopHost.exe') { $folder = [IO.Path]::GetDirectoryName($savedHost) }
}
if ($null -ne $folder) {
    foreach ($name in @('Nexus.DesktopHost', 'Nexus.Shell')) {
        $expectedPath = Join-Path $folder ($name + '.exe')
        foreach ($process in Get-Process -Name $name -ErrorAction SilentlyContinue) {
            try {
                if ($process.SessionId -eq $sessionId -and $process.Path -ieq $expectedPath) {
                    Stop-Process -Id $process.Id -Force -ErrorAction Stop
                    if (-not $process.WaitForExit(5000)) { Write-Warning ('Nexus is still closing: ' + $name) }
                }
            } catch { Write-Warning ('Could not stop a Nexus process: ' + $_.Exception.Message) }
        }
    }
}
$restored = $false
if ($null -ne $folder -and (Test-Path (Join-Path $folder 'Nexus.DesktopHost.exe') -PathType Leaf)) {
    try {
        $hostPath = Join-Path $folder 'Nexus.DesktopHost.exe'
        $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($hostPath)
        $version = [version]('{0}.{1}.{2}.{3}' -f $info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart, $info.FilePrivatePart)
        # Older hosts do not recognize --restore-session; use the independent fallback.
        if ($version -ge [version]'1.3.1.0') {
            $helper = Start-Process $hostPath -ArgumentList '--restore-session' -Wait -PassThru
            $restored = $helper.ExitCode -eq 0
        }
    } catch { Write-Warning ('The native recovery helper could not run: ' + $_.Exception.Message) }
}
if (-not $restored) {
    # Independent fallback also works when the published .NET host is unavailable.
    $startExplorer = $true; $surfacesRepaired = $false; $areaRepaired = $false
    try {
        if (-not ('NexusRecoveryWorkArea' -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class NexusRecoveryWorkArea {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Info { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] public struct Bar { public uint Size; public IntPtr Window; public uint Callback, Edge; public Rect Area; public IntPtr Parameter; }
    delegate bool Visitor(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumWindows(Visitor visitor, IntPtr state);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("user32.dll", SetLastError=true)] static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string title);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref Info info);
    [DllImport("user32.dll", EntryPoint="SystemParametersInfoW")] static extern bool SetWorkArea(uint action, uint parameter, ref Rect area, uint flags);
    [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint message, ref Bar data);
    static string ClassOf(IntPtr window) { var text = new StringBuilder(128); GetClassName(window, text, text.Capacity); return text.ToString(); }
    static bool Explorer(int id) {
        try { using (var p = Process.GetProcessById(id)) {
            return p.SessionId == Process.GetCurrentProcess().SessionId && p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.MainModule.FileName, System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), StringComparison.OrdinalIgnoreCase);
        } } catch (ArgumentException) { return false; } catch (InvalidOperationException) { return false; }
    }
    public static bool HasDesktop { get { return GetShellWindow() != IntPtr.Zero; } }
    public static void RestoreSurface(long handle, int process, string name, bool visible) {
        var window = new IntPtr(handle); uint id;
        if (GetWindowThreadProcessId(window, out id) == 0 || id != process || ClassOf(window) != name || !Explorer(process)) return;
        if (!ShowWindowAsync(window, visible ? 8 : 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Desktop visibility could not be restored.");
    }
    public static void RestoreDefaults() {
        var errors = new List<Exception>();
        EnumWindows(delegate(IntPtr window, IntPtr state) {
            try {
                string name = ClassOf(window);
                if (name != "Shell_TrayWnd" && name != "Shell_SecondaryTrayWnd" && name != "Progman" && name != "WorkerW") return true;
                if (name == "WorkerW" && FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero) return true;
                uint id; if (GetWindowThreadProcessId(window, out id) == 0 || id > int.MaxValue) return true;
                RestoreSurface(window.ToInt64(), (int)id, name, true);
            } catch (Exception ex) { errors.Add(ex); }
            return true;
        }, IntPtr.Zero);
        if (errors.Count != 0) throw new AggregateException(errors);
    }
    static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
    public static void RestoreWorkArea(int[] savedMonitor, int[] savedWork) {
        var info = new Info { Size = (uint)Marshal.SizeOf(typeof(Info)) };
        if (!GetMonitorInfo(MonitorFromWindow(IntPtr.Zero, 1), ref info)) throw new InvalidOperationException("The primary monitor could not be read.");
        var area = info.Monitor;
        if (savedMonitor != null && savedMonitor.Length == 4 && savedWork != null && savedWork.Length == 4
            && savedMonitor[0] == area.Left && savedMonitor[1] == area.Top && savedMonitor[2] == area.Right && savedMonitor[3] == area.Bottom
            && savedWork[0] >= area.Left && savedWork[1] >= area.Top && savedWork[2] <= area.Right && savedWork[3] <= area.Bottom
            && savedWork[0] < savedWork[2] && savedWork[1] < savedWork[3]) {
            area = new Rect { Left = savedWork[0], Top = savedWork[1], Right = savedWork[2], Bottom = savedWork[3] };
        } else {
            var bar = new Bar { Size = (uint)Marshal.SizeOf(typeof(Bar)) };
            if ((SHAppBarMessage(4, ref bar).ToUInt64() & 1) == 0 && SHAppBarMessage(5, ref bar) != UIntPtr.Zero) {
                switch (bar.Edge) {
                    case 0: area.Left = Clamp(bar.Area.Right, area.Left, area.Right - 1); break;
                    case 1: area.Top = Clamp(bar.Area.Bottom, area.Top, area.Bottom - 1); break;
                    case 2: area.Right = Clamp(bar.Area.Left, area.Left + 1, area.Right); break;
                    case 3: area.Bottom = Clamp(bar.Area.Top, area.Top + 1, area.Bottom); break;
                }
            }
        }
        if (!SetWorkArea(0x002F, 0, ref area, 2)) throw new InvalidOperationException("The working area could not be restored.");
    }
}
'@
        }
        try {
            if ($null -ne $sessionRecord) {
                $surfaceFailed = $false
                foreach ($surface in $sessionRecord.Surfaces) {
                    try { [NexusRecoveryWorkArea]::RestoreSurface([long]$surface.Handle, [int]$surface.ProcessId, [string]$surface.ClassName, [bool]$surface.Visible) }
                    catch { $surfaceFailed = $true; Write-Warning ('A desktop surface could not be restored: ' + $_.Exception.Message) }
                }
                $surfacesRepaired = -not $surfaceFailed
            } else { [NexusRecoveryWorkArea]::RestoreDefaults(); $surfacesRepaired = $true }
        } catch { Write-Warning ('Desktop visibility repair failed: ' + $_.Exception.Message) }
        try {
            $monitor = $null; $work = $null
            if ($null -ne $sessionRecord) {
                $monitor = [int[]]@($sessionRecord.Monitor.Left, $sessionRecord.Monitor.Top, $sessionRecord.Monitor.Right, $sessionRecord.Monitor.Bottom)
                $work = [int[]]@($sessionRecord.Work.Left, $sessionRecord.Work.Top, $sessionRecord.Work.Right, $sessionRecord.Work.Bottom)
            }
            [NexusRecoveryWorkArea]::RestoreWorkArea($monitor, $work)
            $areaRepaired = $true
        } catch { Write-Warning ('Working area repair failed: ' + $_.Exception.Message) }
        $startExplorer = -not [NexusRecoveryWorkArea]::HasDesktop
    } catch { Write-Warning ('Independent desktop repair could not run: ' + $_.Exception.Message) }
    # Even Add-Type or registry permission failure must not skip starting Explorer.
    if ($startExplorer) { Start-Process (Join-Path $env:WINDIR 'explorer.exe') }
    $restored = $surfacesRepaired -and $areaRepaired
    if ($restored -and $null -ne $sessionRecord -and (Test-Path $sessionPath)) {
        try { Remove-Item $sessionPath -Force } catch { Write-Warning ('The recovered session record could not be removed: ' + $_.Exception.Message) }
    }
}
Write-Host 'Windows desktop recovery was requested for this session.' -ForegroundColor Green
if ($null -ne $policyError) { Write-Warning 'The saved sign-in setting still needs permission to be restored. Its recovery record is retained.'; exit 1 }
if (-not $restored) { Write-Warning 'Some desktop repair steps could not finish. Use Task Manager to check explorer.exe and the Windows taskbar.'; exit 1 }
