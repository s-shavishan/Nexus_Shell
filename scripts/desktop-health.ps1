[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $env:LOCALAPPDATA ('WhiteDreams\NexusShell\diagnostics\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Run this check inside the Windows desktop session.' }
New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
# Only reads geometry and process counters. No window moves, settings changes,
# input hooks or app-title collection; excludes borderless fullscreen windows.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class NexusDesktopHealth {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    public sealed class Display { public long Handle; public Rect Monitor, Work; public bool Primary; }
    public sealed class Frame { public long Handle, Monitor; public uint ProcessId, Dpi; public Rect Outer, Visible; public bool VisibleFrameAvailable; }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
    private delegate bool WindowCallback(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetStyle64(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] private static extern int GetStyle32(IntPtr window, int index);
    [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] private static extern int GetFrame(IntPtr window, uint attribute, out Rect rect, uint size);
    [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] private static extern int GetCloaked(IntPtr window, uint attribute, out int cloaked, uint size);
    public static Display[] Displays() {
        var result = new List<Display>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr h, IntPtr dc, IntPtr r, IntPtr d) {
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (GetMonitorInfo(h, ref info)) result.Add(new Display { Handle = h.ToInt64(), Monitor = info.Monitor, Work = info.Work, Primary = (info.Flags & 1) != 0 });
            return true;
        }, IntPtr.Zero);
        return result.ToArray();
    }
    public static Frame[] Maximized() {
        var result = new List<Frame>(); IntPtr previous = IntPtr.Zero;
        try {
            try { previous = SetThreadDpiAwarenessContext(new IntPtr(-4)); } catch (EntryPointNotFoundException) { }
            EnumWindows(delegate(IntPtr h, IntPtr data) {
                long style = IntPtr.Size == 8 ? GetStyle64(h, -16).ToInt64() : GetStyle32(h, -16);
                int cloaked; Rect outer, visible;
                if (!IsWindowVisible(h) || IsIconic(h) || !IsZoomed(h) || (style & 0x00C00000) != 0x00C00000
                    || (GetCloaked(h, 14, out cloaked, 4) == 0 && cloaked != 0) || !GetWindowRect(h, out outer)) return true;
                uint process, dpi = 96; GetWindowThreadProcessId(h, out process);
                try { dpi = GetDpiForWindow(h); } catch (EntryPointNotFoundException) { }
                bool available = GetFrame(h, 9, out visible, 16) == 0;
                result.Add(new Frame { Handle = h.ToInt64(), ProcessId = process, Dpi = dpi, Outer = outer, Visible = visible,
                    VisibleFrameAvailable = available, Monitor = MonitorFromWindow(h, 2).ToInt64() });
                return true;
            }, IntPtr.Zero);
        } finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        return result.ToArray();
    }
}
'@
$sessionId = (Get-Process -Id $PID).SessionId
$processes = @(Get-Process -Name @('Nexus.Shell','Nexus.Core','Nexus.DesktopHost') -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $sessionId })
$before = @{}
foreach ($process in $processes) { try { $before[$process.Id] = $process.TotalProcessorTime.TotalSeconds } catch { } }
$clock = [Diagnostics.Stopwatch]::StartNew()
Write-Host 'Reading desktop geometry and a five-second Nexus CPU sample...'
Start-Sleep -Seconds 5
$elapsed = $clock.Elapsed.TotalSeconds
$cpu = @()
foreach ($process in $processes) {
    try {
        $process.Refresh()
        if ($process.HasExited -or -not $before.ContainsKey($process.Id)) { continue }
        $cpu += [ordered]@{ Process = $process.ProcessName; Id = $process.Id; CpuPercentOfMachine = [Math]::Round(100 * ($process.TotalProcessorTime.TotalSeconds - $before[$process.Id]) / $elapsed / [Environment]::ProcessorCount, 2); WorkingSetMB = [Math]::Round($process.WorkingSet64 / 1MB, 1); Responding = $process.Responding }
    } catch { }
}
$monitors = @([NexusDesktopHealth]::Displays())
$nexusIds = @($processes | ForEach-Object { $_.Id })
$windows = @([NexusDesktopHealth]::Maximized() | Where-Object { $_.ProcessId -notin $nexusIds })
$issues = @()
foreach ($window in $windows) {
    $monitor = @($monitors | Where-Object { $_.Handle -eq $window.Monitor }) | Select-Object -First 1
    if ($null -eq $monitor -or -not $window.VisibleFrameAvailable) { continue }
    $frame = $window.Visible; $work = $monitor.Work
    if ([Math]::Abs([long]$frame.Top - $work.Top) -gt 2 -or [Math]::Abs([long]$frame.Bottom - $work.Bottom) -gt 2 -or
        [Math]::Abs([long]$frame.Left - $work.Left) -gt 2 -or [Math]::Abs([long]$frame.Right - $work.Right) -gt 2) {
        $issues += [ordered]@{ Window = $window.Handle; ProcessId = $window.ProcessId; TopDifference = $frame.Top - $work.Top; BottomDifference = $frame.Bottom - $work.Bottom; Note = 'Maximized visible frame differs from its monitor work area; inspect title visibility and any unused strip.' }
    }
}
$glass = @{}
$log = Join-Path $env:LOCALAPPDATA 'WhiteDreams\NexusShell\nexus.log'
if (Test-Path $log) {
    foreach ($line in Get-Content $log -Tail 1500) { if ($line -match 'Glass\[([^\]]+)\] (.*)$') { $glass[$matches[1]] = $matches[2] } }
}
[ordered]@{ Timestamp = (Get-Date).ToString('o'); ManagedHostPresent = [bool]($processes | Where-Object { $_.ProcessName -eq 'Nexus.DesktopHost' }); CpuSampleSeconds = $elapsed;
    NexusProcesses = $cpu; Monitors = $monitors; MaximizedWindows = $windows; GeometryObservations = $issues; LastLoggedGlassStates = $glass;
    Limitations = 'Read-only snapshot; stale glass logs and constrained/custom app geometry are possible. CPU sample is not a frame-latency benchmark. No app titles or personal settings are collected.' } |
    ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'desktop-health.json') -Encoding UTF8
& (Join-Path $PSScriptRoot 'diagnostics.ps1') -OutputDirectory $OutputDirectory
Write-Host "Desktop report: $(Join-Path $OutputDirectory 'desktop-health.json')" -ForegroundColor Green
