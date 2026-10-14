using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;

namespace Nexus.Shell;

// Nexus Controls is the visible front door to existing native services.
// Legacy workspaces remain in the window during migration so saved data and
// old navigation targets are not silently discarded.
public sealed partial class MainWindow
{
    private readonly List<Button> _controlsHomeButtons = [];
    private int _controlsHomeColumns;

    private void InitializeControlsHome()
    {
        if (_controlsHomeButtons.Count != 0) return;
        var items = new (string Title, string Detail, string Glyph, Action Open)[]
        {
            ("System controls", "Audio, network, displays and devices", "\uE713", () => Navigate("PC controls")),
            ("Personalize", "Theme, dock, wallpaper and animations", "\uE790", () => Navigate("Personalize")),
            ("Control Center", "Live system controls from Nexus Core", "\uE8D4", _environment.ShowQuickSettings),
            ("Windows tools", "Built-in utilities and timed keep-awake", "\uE90F", _environment.ShowWindowsTools),
            ("Session health", "Service status, diagnostics and recovery", "\uE9CE", _environment.ShowSessionStatus),
            ("Nexus Files", "Browse folders and drives", "\uE8B7", () => _environment.ShowFiles())
        };
        foreach (var item in items)
        {
            var face = new Grid { ColumnSpacing = 12 };
            face.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            face.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            var iconWell = new Border
            {
                Child = Glyph(item.Glyph, 22), Width = 46, Height = 46,
                CornerRadius = new CornerRadius(14),
                Background = Resource("NexusSelection"),
                VerticalAlignment = VerticalAlignment.Center
            };
            face.Children.Add(iconWell);
            var labels = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            var heading = Text(item.Title, 15);
            heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            var detail = Text(item.Detail, 11, true);
            detail.MaxLines = 2;
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            labels.Children.Add(heading);
            labels.Children.Add(detail);
            Grid.SetColumn(labels, 1);
            face.Children.Add(labels);
            var button = new Button
            {
                Content = face, Padding = new Thickness(15), MinHeight = 102,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(18),
                Style = (Style)Application.Current.Resources["QuietButton"],
                Background = Resource("NexusCard"),
                BorderBrush = Resource("NexusBorder"),
                BorderThickness = new Thickness(1)
            };
            AutomationProperties.SetName(button, item.Title + ". " + item.Detail);
            ToolTipService.SetToolTip(button, item.Detail);
            button.Click += (_, _) => item.Open();
            ControlsActionGrid.Children.Add(button);
            _controlsHomeButtons.Add(button);
        }
        LayoutControlsHome();
        RefreshControlsHomeStatus();
    }

    private void LayoutControlsHome()
    {
        if (_controlsHomeButtons.Count == 0) return;
        int columns = NexusControlsHomePolicy.Columns(PageHost.ActualWidth);
        if (_controlsHomeColumns == columns) return;
        _controlsHomeColumns = columns;
        ControlsActionGrid.ColumnDefinitions.Clear();
        ControlsActionGrid.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++) ControlsActionGrid.ColumnDefinitions.Add(new());
        for (int i = 0; i < (6 + columns - 1) / columns; i++)
            ControlsActionGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int i = 0; i < _controlsHomeButtons.Count; i++)
        {
            Grid.SetRow(_controlsHomeButtons[i], i / columns);
            Grid.SetColumn(_controlsHomeButtons[i], i % columns);
        }
    }

    private void RefreshControlsHomeStatus()
    {
        if (_page != "Home") return;
        string integration = _environment.WindowsIntegrationStatus;
        if (ControlsWindowsStatus.Text != integration) ControlsWindowsStatus.Text = integration;
        string awake = _environment.KeepAwake.Status;
        if (ControlsAwakeStatus.Text != awake) ControlsAwakeStatus.Text = awake;
    }

    private void ControlsWindowsTools_Click(object sender, RoutedEventArgs args)
        => _environment.ShowWindowsTools();
}
