using Nexus.Runtime;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Core.Settings;

internal static class PowerSettings
{
    internal static PowerState Execute(SettingsChange? change, CancellationToken cancellation)
    {
        var schemes = new List<(Guid Id, string Name)>();
        for (uint i = 0; i < 128; i++)
        {
            uint size = 16;
            uint error = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16, i, out Guid id, ref size);
            if (error == 259) break;
            Check(error); if (size != 16) throw new InvalidDataException("Windows returned an invalid power plan.");
            uint bytes = 0;
            error = PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, null, ref bytes);
            if (error != 0 && error != 234) Check(error);
            if (bytes is 0 or > 4096) continue;
            byte[] buffer = new byte[bytes]; Check(PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, buffer, ref bytes));
            schemes.Add((id, Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(bytes, buffer.Length)).TrimEnd('\0')));
        }
        if (change is not null)
        {
            if (!Guid.TryParse(change.DeviceId, out Guid selected) || !schemes.Any(s => s.Id == selected))
                throw new RuntimeFailure("settings-target", "That power plan no longer exists. Refresh first.");
            cancellation.ThrowIfCancellationRequested(); Check(PowerSetActiveScheme(IntPtr.Zero, ref selected));
        }
        Check(PowerGetActiveScheme(IntPtr.Zero, out IntPtr pointer));
        Guid active;
        try { active = Marshal.PtrToStructure<Guid>(pointer); } finally { LocalFree(pointer); }
        string status = "Power status unavailable";
        if (GetSystemPowerStatus(out var power))
            status = power.BatteryFlag == 128 ? (power.Ac == 1 ? "Connected to power · no battery" : "No battery")
                : (power.Ac == 1 ? "Connected to power" : power.Ac == 0 ? "On battery" : "Power source unknown")
                  + (power.Percent <= 100 ? $" · {power.Percent}%" : "") + (power.BatteryFlag != 255 && (power.BatteryFlag & 8) != 0 ? " · charging" : "");
        return new(schemes.Select(s => new PowerPlan(s.Id.ToString(), s.Name, s.Id == active)).ToArray(), status,
            schemes.Count == 0 ? "No power plans are available for this Windows installation." : "");
    }
    private static void Check(uint code) { if (code != 0) throw new Win32Exception((int)code); }
    [StructLayout(LayoutKind.Sequential)] internal struct Status { public byte Ac, BatteryFlag, Percent, Reserved; public uint Remaining, Full; }
    [DllImport("powrprof.dll")] private static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup, uint access, uint index, out Guid buffer, ref uint size);
    [DllImport("powrprof.dll")] private static extern uint PowerReadFriendlyName(IntPtr root, ref Guid scheme, IntPtr subgroup, IntPtr setting, [Out] byte[]? buffer, ref uint size);
    [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
    [DllImport("powrprof.dll")] private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemPowerStatus(out Status status);
}
