namespace Nexus.Runtime.Monitoring;

public static class ProcessActionRules
{
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "registry", "secure system", "smss", "csrss", "wininit", "winlogon", "services",
        "lsass", "lsaiso", "svchost", "dwm", "fontdrvhost", "sihost", "taskhostw", "explorer",
        "shellexperiencehost", "startmenuexperiencehost", "searchhost", "searchapp", "searchui",
        "runtimebroker", "applicationframehost", "securityhealthservice", "securityhealthsystray"
    };
    public static string Refusal(ProcessObservation process, int ownId)
    {
        if (process.Identity.Id <= 4 || process.Identity.CreatedFileTime <= 0) return "This process cannot be identified safely.";
        string name = process.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? process.Name[..^4] : process.Name;
        if (process.Identity.Id == ownId || name.Equals("nexus", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("nexus.", StringComparison.OrdinalIgnoreCase) || Protected.Contains(name))
            return "Windows desktop and Nexus infrastructure are protected here.";
        if (!process.SameUser || !process.SameSession) return "Only processes belonging to your current Windows session can be controlled here.";
        if (process.Critical is not false) return "Windows critical-process status could not be cleared for this process.";
        if (string.IsNullOrWhiteSpace(process.ImagePath) || process.Unavailable.Length > 0) return "Process details are unavailable. Refresh and try again.";
        return "";
    }
}

public static class ProcessActions
{
    public static ProcessActionResult Execute(IProcessActionSource source, ProcessIdentity identity, ProcessAction action, int ownId)
    {
        if (!Enum.IsDefined(action) || identity.Id <= 4 || identity.CreatedFileTime <= 0)
            return new(false, false, "This process action is invalid.");
        // Open once, then re-read and act on that same OS process handle. A PID
        // reused while the confirmation is open must never select a new process.
        using var target = source.Open(identity, action);
        var current = target.Read();
        if (current.Identity != identity) return new(false, false, "The selected process exited or changed. Refresh the list.");
        string refusal = ProcessActionRules.Refusal(current, ownId);
        if (refusal.Length > 0) return new(false, false, refusal);
        return action == ProcessAction.CloseWindows ? target.CloseWindows() : target.EndProcess();
    }
}
