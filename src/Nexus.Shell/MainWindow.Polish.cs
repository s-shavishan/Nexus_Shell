using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private void SetSegment(Button button, bool selected)
    {
        button.Background = selected ? Resource(_highContrast ? "NexusAccent" : "NexusSegment") : _transparent;
        button.Foreground = Resource(_highContrast && selected ? "NexusAccentText" : "NexusText");
        button.BorderBrush = selected ? Resource("NexusBorder") : _transparent;
        button.BorderThickness = new Thickness(selected ? 1 : 0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, selected ? "Selected" : "");
    }

}
