// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

public static class ClipboardCapturePolicy
{
    public static bool Allows(bool excluded, bool permissionPresent, int? permission)
        => !excluded && (!permissionPresent || permission == 1);
    public static bool PayloadSize(ulong bytes) => bytes >= 2 && bytes <= (ClipboardHistory.MaximumCharacters + 1) * 2UL + 16 && (bytes & 1) == 0;
}
