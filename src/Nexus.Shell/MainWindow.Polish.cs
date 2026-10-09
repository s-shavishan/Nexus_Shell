using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private void SetSegment(Button button, bool selected)
    {
        button.Background = selected ? _environment.Theme.Surface("Accent") : _transparent;
        button.Foreground = Resource(selected ? "NexusAccentText" : "NexusMuted");
        button.BorderBrush = selected ? Resource("NexusAccent") : _transparent;
        button.BorderThickness = new Thickness(_highContrast && selected ? 1 : 0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, selected ? "Selected" : "");
    }

}
