using Nexus.Runtime;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Core.Settings;

internal static class NetworkSettings
{
    internal static NetworkState Execute(SettingsRequest request, CancellationToken cancellation)
    {
        var links = new List<LinkState>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Take(64))
        {
            if (adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            try
            {
                var ip = adapter.GetIPProperties();
                links.Add(new(adapter.Name, adapter.NetworkInterfaceType.ToString(), adapter.OperationalStatus.ToString(),
                    string.Join(", ", ip.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(a => a.Address)),
                    string.Join(", ", ip.GatewayAddresses.Select(g => g.Address)), string.Join(", ", ip.DnsAddresses), adapter.Speed / 1_000_000));
            }
            catch (NetworkInformationException) { links.Add(new(adapter.Name, adapter.NetworkInterfaceType.ToString(), adapter.OperationalStatus.ToString(), "", "", "", 0)); }
        }
        uint opened = WlanOpenHandle(2, IntPtr.Zero, out _, out IntPtr client);
        if (opened != 0)
        {
            if (request.Change is not null) Check(opened, "Wi-Fi is unavailable");
            return new(links.ToArray(), [], "Wi-Fi is unavailable: " + new Win32Exception((int)opened).Message);
        }
        try
        {
            Check(WlanEnumInterfaces(client, IntPtr.Zero, out IntPtr list), "Could not read Wi-Fi adapters");
            var result = new List<WifiAdapter>(); bool changed = false;
            try
            {
                int count = Math.Clamp(Marshal.ReadInt32(list), 0, 32);
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var info = Marshal.PtrToStructure<Interface>(IntPtr.Add(list, 8 + i * Marshal.SizeOf<Interface>()));
                    string message = ""; var radio = ReadRadio(client, info.Id);
                    if (request.Change is { } change && Guid.TryParse(change.DeviceId, out Guid selected) && selected == info.Id)
                    {
                        changed = true; cancellation.ThrowIfCancellationRequested();
                        switch (change.Kind)
                        {
                            case "wifi-radio":
                                if (radio.Length == 0) throw new RuntimeFailure("settings-device", "This adapter does not expose its radio controls.");
                                foreach (var phy in radio)
                                {
                                    cancellation.ThrowIfCancellationRequested();
                                    var state = phy; state.Software = change.Enabled == true ? 1 : 2;
                                    Check(WlanSetInterface(client, ref info.Id, 4, (uint)Marshal.SizeOf<PhyRadio>(), ref state, IntPtr.Zero), "Could not change Wi-Fi radio");
                                }
                                radio = ReadRadio(client, info.Id); break;
                            case "wifi-connect":
                                if (!ReadNetworks(client, info.Id, out _).Any(n => n.Profile.Length != 0 && n.Profile == change.ItemId))
                                    throw new RuntimeFailure("settings-target", "Select a visible network with a saved Windows Wi-Fi profile, then Refresh.");
                                var connection = new Connection { Profile = change.ItemId, BssType = 1 };
                                Check(WlanConnect(client, ref info.Id, ref connection, IntPtr.Zero), "Could not request Wi-Fi connection");
                                message = "Connection requested. Refresh to confirm its state."; break;
                            case "wifi-join":
                                var network = ReadNetworks(client, info.Id, out _).FirstOrDefault(n => n.Id == change.ItemId && n.CanJoin);
                                if (network is null) throw new RuntimeFailure("settings-target", "The network changed or needs advanced authentication. Refresh first.");
                                string profile = "Nexus-" + Guid.NewGuid().ToString("N");
                                string xml = WifiProfile.Build(profile, network.SsidHex, change.Secret, network.Authentication, network.Cipher);
                                cancellation.ThrowIfCancellationRequested();
                                uint saved = WlanSetProfile(client, ref info.Id, 2, xml, null, false, IntPtr.Zero, out uint reason);
                                Check(saved, "Could not save this user’s Wi-Fi connection (Windows reason " + reason + ")");
                                bool accepted = false;
                                try
                                {
                                    cancellation.ThrowIfCancellationRequested();
                                    var join = new Connection { Profile = profile, BssType = 1 };
                                    Check(WlanConnect(client, ref info.Id, ref join, IntPtr.Zero), "Could not request Wi-Fi connection"); accepted = true;
                                }
                                finally { if (!accepted) WlanDeleteProfile(client, ref info.Id, profile, IntPtr.Zero); }
                                message = "Connection requested. Refresh to confirm. If authentication fails, forget this Nexus connection and enter its password again."; break;
                            case "wifi-forget":
                                if (!ForgettableProfiles(client, info.Id).Contains(change.ItemId))
                                    throw new RuntimeFailure("settings-target", "Only Nexus-created per-user connections can be forgotten here. Refresh first.");
                                cancellation.ThrowIfCancellationRequested();
                                Check(WlanDeleteProfile(client, ref info.Id, change.ItemId, IntPtr.Zero), "Could not forget this Nexus Wi-Fi connection");
                                message = "Saved connection removed. An active connection may remain until disconnected."; break;
                            case "wifi-disconnect":
                                Check(WlanDisconnect(client, ref info.Id, IntPtr.Zero), "Could not disconnect Wi-Fi");
                                message = "Disconnect requested. Refresh to confirm its state."; break;
                        }
                    }
                    if (request.Scan)
                    {
                        uint scan = WlanScan(client, ref info.Id, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                        message = scan == 0 ? "Scan requested. Networks update on the next refresh." : "Scan unavailable: " + new Win32Exception((int)scan).Message;
                    }
                    var networks = ReadNetworks(client, info.Id, out string networkMessage);
                    result.Add(new(info.Id.ToString(), info.Name, radio.Length != 0,
                        radio.Any(r => r.Software == 1), radio.Any(r => r.Hardware == 1), networks,
                        message.Length != 0 ? message : networkMessage));
                }
            }
            finally { WlanFreeMemory(list); }
            if (request.Change is not null && !changed) throw new RuntimeFailure("settings-target", "The Wi-Fi adapter changed. Refresh first.");
            return new(links.ToArray(), result.ToArray(), result.Count == 0 ? "No Wi-Fi adapter is available. Wired connections remain listed above." : "Connect to saved profiles, open networks or WPA2-Personal/AES networks here. Enterprise, new WPA3 and VPN configuration remain in advanced Windows controls.");
        }
        finally { WlanCloseHandle(client, IntPtr.Zero); }
    }
    private static PhyRadio[] ReadRadio(IntPtr client, Guid id)
    {
        // wlan_intf_opcode_radio_state = 4. Query returns WLAN_RADIO_STATE;
        // Set accepts one WLAN_PHY_RADIO_STATE, never the entire query buffer.
        if (WlanQueryInterface(client, ref id, 4, IntPtr.Zero, out uint size, out IntPtr data, out _) != 0) return [];
        try
        {
            if (size < 4) return [];
            uint count = (uint)Marshal.ReadInt32(data);
            if (count > 64 || 4L + count * Marshal.SizeOf<PhyRadio>() > size) return [];
            return Enumerable.Range(0, (int)count).Select(i => Marshal.PtrToStructure<PhyRadio>(IntPtr.Add(data, 4 + i * Marshal.SizeOf<PhyRadio>()))).ToArray();
        }
        finally { WlanFreeMemory(data); }
    }
    private static WifiNetwork[] ReadNetworks(IntPtr client, Guid id, out string message)
    {
        uint error = WlanGetAvailableNetworkList(client, ref id, 0, IntPtr.Zero, out IntPtr list);
        message = error == 0 ? "" : "Network list unavailable: " + new Win32Exception((int)error).Message;
        if (error != 0) return [];
        try
        {
            var networks = new List<WifiNetwork>(); var forgettable = ForgettableProfiles(client, id); int count = Math.Clamp(Marshal.ReadInt32(list), 0, 256);
            for (int i = 0; i < count; i++)
            {
                var n = Marshal.PtrToStructure<Available>(IntPtr.Add(list, 8 + i * Marshal.SizeOf<Available>()));
                string name = Encoding.UTF8.GetString(n.Ssid.Bytes, 0, (int)Math.Min(n.Ssid.Length, 32)).TrimEnd('\0');
                string hex = Convert.ToHexString(n.Ssid.Bytes.AsSpan(0, (int)Math.Min(n.Ssid.Length, 32)));
                networks.Add(new(name.Length == 0 ? "Hidden network" : name, n.Profile, (int)Math.Min(n.Signal, 100), (n.Flags & 1) != 0, n.Security != 0,
                    hex, n.Auth, n.Cipher, n.BssType == 1 && n.Connectable != 0 && hex.Length != 0 && WifiProfile.Supported(n.Auth, n.Cipher), forgettable.Contains(n.Profile)));
            }
            return networks.OrderByDescending(n => n.Connected).ThenByDescending(n => n.Signal).Take(64).ToArray();
        }
        finally { WlanFreeMemory(list); }
    }
    private static HashSet<string> ForgettableProfiles(IntPtr client, Guid id)
    {
        var profiles = new HashSet<string>(StringComparer.Ordinal);
        if (WlanGetProfileList(client, ref id, IntPtr.Zero, out IntPtr list) != 0) return profiles;
        try
        {
            int count = Math.Clamp(Marshal.ReadInt32(list), 0, 256);
            for (int i = 0; i < count; i++)
            {
                var profile = Marshal.PtrToStructure<ProfileInfo>(IntPtr.Add(list, 8 + i * Marshal.SizeOf<ProfileInfo>()));
                if ((profile.Flags & 2) != 0 && (profile.Flags & 1) == 0 && profile.Name.StartsWith("Nexus-", StringComparison.Ordinal)) profiles.Add(profile.Name);
            }
        }
        finally { WlanFreeMemory(list); }
        return profiles;
    }
    private static void Check(uint code, string context) { if (code != 0) throw new Win32Exception((int)code, context + ": " + new Win32Exception((int)code).Message); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Interface
    { public Guid Id; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name; public int State; }
    [StructLayout(LayoutKind.Sequential)] internal struct PhyRadio { public uint Index; public int Software, Hardware; }
    [StructLayout(LayoutKind.Sequential)] internal struct Ssid
    { public uint Length; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Bytes; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Available
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Profile;
        public Ssid Ssid; public int BssType; public uint BssidCount; public int Connectable; public uint Reason, PhyCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public int[] PhyTypes;
        public int MorePhy; public uint Signal; public int Security, Auth, Cipher; public uint Flags, Reserved;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Connection
    { public int Mode; [MarshalAs(UnmanagedType.LPWStr)] public string Profile; public IntPtr Ssid, Bssid; public int BssType; public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct ProfileInfo
    { [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name; public uint Flags; }
    [DllImport("wlanapi.dll")] private static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr client);
    [DllImport("wlanapi.dll")] private static extern uint WlanCloseHandle(IntPtr client, IntPtr reserved);
    [DllImport("wlanapi.dll")] private static extern void WlanFreeMemory(IntPtr memory);
    [DllImport("wlanapi.dll")] private static extern uint WlanEnumInterfaces(IntPtr client, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] private static extern uint WlanQueryInterface(IntPtr client, ref Guid id, int opcode, IntPtr reserved, out uint size, out IntPtr data, out int type);
    [DllImport("wlanapi.dll")] private static extern uint WlanSetInterface(IntPtr client, ref Guid id, int opcode, uint size, ref PhyRadio data, IntPtr reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanGetAvailableNetworkList(IntPtr client, ref Guid id, uint flags, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] private static extern uint WlanScan(IntPtr client, ref Guid id, IntPtr ssid, IntPtr ie, IntPtr reserved);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint WlanConnect(IntPtr client, ref Guid id, ref Connection parameters, IntPtr reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanDisconnect(IntPtr client, ref Guid id, IntPtr reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanGetProfileList(IntPtr client, ref Guid id, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint WlanSetProfile(IntPtr client, ref Guid id, uint flags, string xml, string? security,
        [MarshalAs(UnmanagedType.Bool)] bool overwrite, IntPtr reserved, out uint reason);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint WlanDeleteProfile(IntPtr client, ref Guid id, string profile, IntPtr reserved);
}
