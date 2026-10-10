using Nexus.Runtime;
using System.Xml.Linq;

namespace Nexus.Core.Settings;

internal static class WifiProfile
{
    internal static bool Supported(int authentication, int cipher) => authentication == 1 && cipher == 0 || authentication == 7 && cipher == 4;
    // The SSID remains bytes, not a lossy decoded display name. XElement escapes
    // credentials in memory; Windows encrypts the saved per-user profile key.
    // No profile XML or password is written to a Nexus file or diagnostic log.
    internal static string Build(string name, string ssidHex, string password, int authentication, int cipher)
    {
        if (!Supported(authentication, cipher)) throw new RuntimeFailure("settings-network", "This network needs advanced Windows authentication tools.");
        if (ssidHex.Length is < 2 or > 64 || ssidHex.Length % 2 != 0 || !ssidHex.All(Uri.IsHexDigit)) throw new RuntimeFailure("settings-target", "The network changed. Refresh before connecting.");
        bool secured = authentication == 7;
        bool rawKey = password.Length == 64 && password.All(Uri.IsHexDigit);
        if (secured && !rawKey && (password.Length is < 8 or > 63 || password.Any(c => c is < ' ' or > '~')))
            throw new RuntimeFailure("settings-password", "WPA2 requires 8–63 ASCII characters or a 64-digit hexadecimal key.");
        if (!secured && password.Length != 0) throw new RuntimeFailure("settings-password", "An open network does not use a Wi-Fi password.");
        XNamespace ns = "http://www.microsoft.com/networking/WLAN/profile/v1";
        var security = new XElement(ns + "security", new XElement(ns + "authEncryption",
            new XElement(ns + "authentication", secured ? "WPA2PSK" : "open"), new XElement(ns + "encryption", secured ? "AES" : "none"), new XElement(ns + "useOneX", "false")));
        if (secured) security.Add(new XElement(ns + "sharedKey", new XElement(ns + "keyType", rawKey ? "networkKey" : "passPhrase"),
            new XElement(ns + "protected", "false"), new XElement(ns + "keyMaterial", password)));
        return new XElement(ns + "WLANProfile", new XElement(ns + "name", name), new XElement(ns + "SSIDConfig", new XElement(ns + "SSID", new XElement(ns + "hex", ssidHex))),
            new XElement(ns + "connectionType", "ESS"), new XElement(ns + "connectionMode", "manual"), new XElement(ns + "MSM", security)).ToString(SaveOptions.DisableFormatting);
    }
}
