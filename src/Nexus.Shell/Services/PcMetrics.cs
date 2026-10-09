using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Nexus.Shell.Services;

public sealed record CpuTimes(ulong Idle, ulong Kernel, ulong User);
public sealed record PcSnapshot(double? Cpu, ulong TotalMemory, ulong AvailableMemory, string Power,
    string Network, string Address, string Storage, TimeSpan Uptime);

public static class PcMetrics
{
    public static double? CpuPercent(CpuTimes? before, CpuTimes after)
    {
        if (before is null || after.Idle < before.Idle || after.Kernel < before.Kernel || after.User < before.User) return null;
        double total = (double)(after.Kernel - before.Kernel) + (after.User - before.User);
        if (total <= 0) return null;
        return Math.Clamp((total - (after.Idle - before.Idle)) / total * 100, 0, 100);
    }
    public static string Size(ulong bytes) => bytes >= 1073741824 ? $"{bytes / 1073741824d:0.0} GB" : $"{bytes / 1048576d:0} MB";

    public static PcSnapshot Read(ref CpuTimes? previous, bool includeStorage = true)
    {
        double? cpu = null; ulong total = 0, available = 0;
        if (GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
        { var times = new CpuTimes(idle, kernel, user); cpu = CpuPercent(previous, times); previous = times; }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (GlobalMemoryStatusEx(ref memory)) { total = memory.TotalPhysical; available = memory.AvailablePhysical; }
        string power = "Power information unavailable";
        if (GetSystemPowerStatus(out var battery))
        {
            power = battery.BatteryFlag == 255 ? "Power status unknown" : (battery.BatteryFlag & 128) != 0
                ? "Desktop · no battery" : battery.BatteryLifePercent <= 100 ? $"Battery {battery.BatteryLifePercent}%" : "Battery level unavailable";
            if (battery.AcLineStatus == 1) power += " · plugged in";
            else if (battery.AcLineStatus == 0) power += " · on battery";
        }
        string network = "No active network link", address = "";
        try
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                && n.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(System.Net.IPAddress.Any)));
            if (adapter is not null)
            {
                network = adapter.Name + " · " + (adapter.Speed > 0 ? $"{adapter.Speed / 1000000d:0} Mbps link" : "connected link");
                address = adapter.GetIPProperties().UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString() ?? "";
            }
        }
        catch { network = "Network information unavailable"; }
        var storage = new List<string>();
        try
        {
            foreach (var drive in (includeStorage ? DriveInfo.GetDrives() : []).Where(d => d.DriveType == DriveType.Fixed).Take(8))
                try { if (drive.IsReady) storage.Add(drive.Name + "  " + Size((ulong)drive.AvailableFreeSpace) + " free of " + Size((ulong)drive.TotalSize)); } catch { }
        }
        catch { }
        return new(cpu, total, available, power, network, address, string.Join("\n", storage), TimeSpan.FromMilliseconds(Environment.TickCount64));
    }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [StructLayout(LayoutKind.Sequential)] private struct PowerStatus
    { public byte AcLineStatus, BatteryFlag, BatteryLifePercent, Saver; public int BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus value);
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus value);
}
