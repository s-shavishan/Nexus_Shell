// නමෝ බුද්ධාය 🤍
using Nexus.Runtime;

namespace Nexus.Shell.Services;

// A compact Control Center must foreground usable connections without hiding
// other adapters or letting unstable Windows enumeration order shuffle cards.
public static class NetworkPresentationPolicy
{
    public const int InitialWifiRows = 6;

    public static bool Connected(LinkState link)
        => string.Equals(link.State, "Up", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<LinkState> OrderedLinks(IEnumerable<LinkState> links)
        => links.OrderByDescending(Connected)
            .ThenBy(link => link.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link.Name, StringComparer.OrdinalIgnoreCase)
            .Take(64).ToArray();

    public static int InitiallyVisibleLinks(IReadOnlyList<LinkState> sorted)
    {
        int connected = sorted.Count(Connected);
        // All live links stay visible (including VPNs/virtual adapters that
        // Windows reports as up); allow two rows on an offline machine.
        return Math.Min(sorted.Count, Math.Max(connected, 2));
    }

    public static IReadOnlyList<WifiNetwork> OrderedWifi(IEnumerable<WifiNetwork> networks)
        => networks.OrderByDescending(network => network.Connected)
            .ThenByDescending(network => network.Signal)
            .ThenBy(network => network.Name, StringComparer.OrdinalIgnoreCase)
            .Take(64).ToArray();
}
