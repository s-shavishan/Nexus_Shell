// නමෝ බුද්ධාය 🤍
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Runtime;
using Nexus.Shell.UI;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed class AboutNexusWindow : DesktopPanelWindow
{
    private readonly StackPanel _body = new() { Spacing = 16 };
    private readonly TextBlock _copied = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    internal static string Version => typeof(AboutNexusWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? typeof(AboutNexusWindow).Assembly.GetName().Version?.ToString() ?? "Development build";
    internal AboutNexusWindow(DesktopEnvironment environment) : base(environment, "About Nexus", new ScrollViewer(), 540, 610, center: true)
    {
        ((ScrollViewer)Root.Children[1]).Content = _body;
        _body.Children.Add(NexusIcons.Image("Launchpad", 68));
        _body.Children.Add(new TextBlock { Text = "Nexus Shell", FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        _body.Children.Add(new TextBlock { Text = "White Dreams · A desktop that works with Windows.", TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        _body.Children.Add(new TextBlock { Text = "Version " + Version, FontSize = 12, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, IsTextSelectionEnabled = true });
        _body.Children.Add(new TextBlock { Text = RuntimeInformation.OSDescription + " · " + RuntimeInformation.ProcessArchitecture + "\n" + (environment.IsManagedDesktop ? "Supervised Nexus desktop session" : "Nexus preview session") + "\nLocal runtime protocol " + RuntimeProtocol.Version, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        _body.Children.Add(new TextBlock { Text = "Native C# and WinUI · Launchpad, Files, File Jobs, Controls and a dock for your Windows apps.\nExplorer stays running for Windows shell services.", FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var links = new Grid { ColumnSpacing = 8 }; links.ColumnDefinitions.Add(new()); links.ColumnDefinitions.Add(new());
        links.Children.Add(ActionButton("Project & releases", () => environment.OpenTarget("https://github.com/s-shavishan/Nexus_Shell")));
        var status = ActionButton("Session diagnostics", environment.ShowSessionStatus); Grid.SetColumn(status, 1); links.Children.Add(status); _body.Children.Add(links);
        var copy = ActionButton("Copy diagnostics", async () => _copied.Text = await environment.CopyClipboardAsync(environment.WindowsShellDiagnostics() + "\nRuntime protocol: " + RuntimeProtocol.Version) ? "Diagnostics copied." : "The clipboard is busy. Try again.");
        _body.Children.Add(copy); _body.Children.Add(_copied); ApplyAppearance();
    }
    internal void Show() => ShowPanel();
    internal override void ApplyAppearance() { base.ApplyAppearance(); foreach (var text in _body.Children.OfType<TextBlock>()) text.Foreground = Environment.Theme.Brush("NexusText"); }
    protected override void OnClosed() => Environment.AboutClosed(this);
}
