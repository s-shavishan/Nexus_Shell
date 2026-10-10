namespace Nexus.Shell.Services;

public sealed record DesktopPolicyRequest(bool Enable, string UserSid)
{
    public static DesktopPolicyRequest Parse(string[] args)
    {
        if (args.Length != 4 || args[0] != "--policy-action" || args[1] is not ("enable" or "restore")
            || args[2] != "--user-sid" || !args[3].StartsWith("S-1-", StringComparison.Ordinal))
            throw new ArgumentException("The desktop policy request is invalid.");
        return new(args[1] == "enable", args[3]);
    }
    public void VerifyUser(string actualSid)
    {
        if (!UserSid.Equals(actualSid, StringComparison.Ordinal))
            throw new System.Security.SecurityException("Nexus cannot change another account's desktop policy. Use administrator permission for the same signed-in account, or ask your administrator to restore its policy.");
    }
}
