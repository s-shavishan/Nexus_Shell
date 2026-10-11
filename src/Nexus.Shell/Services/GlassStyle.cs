namespace Nexus.Shell.Services;

public static class GlassStyle
{
    public const string Default = "Balanced";
    public static readonly string[] Choices = [Default, "Clear", "Frosted"];
    public static string Normalize(string? value) => Choices.Contains(value ?? "") ? value! : Default;
}
