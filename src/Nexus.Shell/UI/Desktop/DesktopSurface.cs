using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Desktop;

internal sealed class DesktopSurface : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly Image _wallpaper = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
    private readonly GridView _icons;
    private readonly StackPanel _widgetRail = new()
    {
        Width = 336, Spacing = 12,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(20, 60, 24, 106)
    };
    private readonly Button _calendar = new()
    {
        Width = 336, Padding = new Thickness(20),
        CornerRadius = new CornerRadius(21), BorderThickness = new Thickness(1),
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Style = (Style)Application.Current.Resources["QuietButton"]
    };
    private readonly Border _quickFrame = new()
    {
        Width = 336, CornerRadius = new CornerRadius(MidnightGlassMetrics.CardRadius),
        BorderThickness = new Thickness(1), Padding = new Thickness(12)
    };
    private readonly TextBlock _quickHeading = new()
    {
        Text = "QUICK ACCESS", FontSize = 11,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
    };
    private readonly TextBlock _weekday = new() { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _day = new() { FontSize = 42, FontWeight = Microsoft.UI.Text.FontWeights.Light };
    private readonly TextBlock _month = new() { FontSize = 12 };
    private readonly StackPanel _tasks = new() { Spacing = 10 };
    private (string Palette, bool Contrast, bool Focus, bool Visible, string Tasks)? _calendarContent;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly InfoBar _status = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20), MaxWidth = 540 };
    private string? _wallpaperName;
    private bool _refreshing;
    private bool _refreshAgain;
    private IReadOnlyList<DesktopShortcut> _allIcons = [];
    private string? _selectPath;
    private bool _applyingIcons;
    internal DesktopSurface(DesktopEnvironment environment)
    {
        _environment = environment; KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        _wallpaper.ImageFailed += (_, error) =>
        {
            if (environment.IsStopping) return;
            Log.Write("Desktop wallpaper used its fallback: " + error.ErrorMessage);
            _wallpaper.Source = _wallpaperName == "Midnight" && _wallpaper.Source is SvgImageSource
                ? new BitmapImage(new Uri("ms-appx:///Assets/Wallpapers/Midnight.png")) : null;
        };
        Children.Add(_wallpaper);
        BuildCalendar();
        BuildWidgetRail();
        _icons = new GridView { Margin = new Thickness(20, 48, 20, 100), SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = false, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch };
        _icons.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><ItemsWrapGrid Orientation='Vertical' ItemWidth='108' ItemHeight='112'/></ItemsPanelTemplate>");
        _icons.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Width="96" Padding="4,8" Spacing="7" AutomationProperties.Name="{Binding Name}">
                <Image Width="58" Height="58" Source="{Binding IconUri}" HorizontalAlignment="Center"/>
                <Border Background="{ThemeResource NexusDesktopLabel}" Padding="5,3,5,3" CornerRadius="5">
                  <TextBlock Text="{Binding Name}" Foreground="{ThemeResource NexusDesktopText}" FontSize="12" TextAlignment="Center" TextWrapping="Wrap" MaxLines="2" TextTrimming="CharacterEllipsis"/>
                </Border>
              </StackPanel>
            </DataTemplate>
            """);
        _icons.DoubleTapped += OpenDoubleTapped;
        _icons.SelectionChanged += (_, _) => { if (!_applyingIcons) _selectPath = null; };
        _icons.KeyDown += (_, args) =>
        {
            if (_icons.SelectedItem is not DesktopShortcut item) return;
            if (args.Key == VirtualKey.Enter) { environment.OpenDesktopItem(item); args.Handled = true; }
            else if (args.Key == VirtualKey.F2) { environment.RenameDesktopItem(item); args.Handled = true; }
        };
        _icons.RightTapped += (_, args) =>
        {
            var item = ItemAt(args.OriginalSource);
            if (item is null) return;
            _icons.SelectedItem = item;
            environment.Menus.DesktopItemMenu(item).ShowAt(_icons, new FlyoutShowOptions { Position = args.GetPosition(_icons) }); args.Handled = true;
        };
        RightTapped += (_, args) =>
        {
            if (ItemAt(args.OriginalSource) is not null) return;
            _icons.SelectedItem = null;
            environment.Menus.DesktopMenu().ShowAt(this, new FlyoutShowOptions { Position = args.GetPosition(this) }); args.Handled = true;
        };
        Children.Add(_icons); Children.Add(_widgetRail); Children.Add(_status);
        _clockTimer.Tick += (_, _) => RefreshClock();
        Loaded += (_, _) => { RefreshClock(); _clockTimer.Start(); };
        Unloaded += (_, _) => _clockTimer.Stop();
        SizeChanged += (_, _) =>
        {
            _widgetRail.MaxWidth = Math.Max(1, ActualWidth - 48);
            ApplyWidgetVisibility();
        };
        var enter = new KeyboardAccelerator { Key = VirtualKey.K, Modifiers = VirtualKeyModifiers.Control };
        enter.Invoked += (_, args) => { environment.ShowMenu(true); args.Handled = true; }; KeyboardAccelerators.Add(enter);
        void Shortcut(UIElement owner, VirtualKey key, VirtualKeyModifiers modifiers, Action action)
        { var shortcut = new KeyboardAccelerator { Key = key, Modifiers = modifiers }; shortcut.Invoked += (_, args) => { action(); args.Handled = true; }; owner.KeyboardAccelerators.Add(shortcut); }
        Shortcut(this, VirtualKey.F5, VirtualKeyModifiers.None, environment.RefreshDesktop);
        Shortcut(this, VirtualKey.F1, VirtualKeyModifiers.None, environment.ShowDesktopGuide);
        Shortcut(this, VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, environment.CreateDesktopFolder);
        Shortcut(this, VirtualKey.P, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, environment.ShowPlaces);
        Shortcut(this, VirtualKey.F, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, environment.ShowSearch);
        Shortcut(_icons, VirtualKey.Z, VirtualKeyModifiers.Control, environment.UndoDesktopEdit);
        ApplyAppearance();
        RefreshContent();
    }
    private void RefreshClock()
    {
        var now = DateTime.Now; _weekday.Text = now.ToString("dddd"); _day.Text = now.ToString("dd"); _month.Text = now.ToString("MMM yyyy");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_calendar, now.ToString("D") + ". Saved tasks. Open Notes.");
    }
    private void BuildCalendar()
    {
        var body = new Grid { ColumnSpacing = 16 }; body.ColumnDefinitions.Add(new() { Width = new GridLength(94) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var date = new StackPanel(); date.Children.Add(_weekday); date.Children.Add(_day); date.Children.Add(_month); body.Children.Add(date);
        Grid.SetColumn(_tasks, 1); body.Children.Add(_tasks); _calendar.Content = body;
        _calendar.Click += (_, _) => _environment.OpenTarget("nexus:notes");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_calendar, "Today's date and saved tasks. Open Notes.");
    }
    private void BuildWidgetRail()
    {
        _widgetRail.Children.Add(_calendar);
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(_quickHeading);
        var actions = new Grid { ColumnSpacing = 8 };
        for (int i = 0; i < 3; i++) actions.ColumnDefinitions.Add(new());
        void AddAction(int column, string title, string glyph, Action open)
        {
            var contents = new StackPanel
            {
                Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            contents.Children.Add(Nexus.Shell.UI.ShellControls.Icon(glyph, 22));
            contents.Children.Add(new TextBlock
            {
                Text = title, FontSize = 12,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var button = new Button
            {
                Content = contents, MinHeight = 70, Padding = new Thickness(6),
                CornerRadius = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)Application.Current.Resources["QuietButton"]
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Open " + title);
            ToolTipService.SetToolTip(button, "Open " + title);
            button.Click += (_, _) => open();
            Grid.SetColumn(button, column);
            actions.Children.Add(button);
        }
        // ShowFiles has an optional path argument: use an explicit
        // zero-argument delegate to keep the launcher call type-safe.
        AddAction(0, "Files", "\uE8B7", () => _environment.ShowFiles());
        AddAction(1, "Search", "\uE721", _environment.ShowSearch);
        AddAction(2, "Controls", "\uE713", _environment.ShowQuickSettings);
        body.Children.Add(actions);
        _quickFrame.Child = body;
        _widgetRail.Children.Add(_quickFrame);
    }
    internal void RefreshContent()
    {
        _icons.Visibility = _environment.Session.State.FocusMode || !_environment.Session.State.ShowDesktopIcons ? Visibility.Collapsed : Visibility.Visible;
        var tasks = _environment.Session.State.Tasks.Where(t => !t.Completed).Take(3).ToArray();
        var content = (_environment.Theme.Palette.Name, _environment.Theme.HighContrast, _environment.Session.State.FocusMode, _environment.Session.State.ShowClockWidget, string.Join("\0", tasks.Select(t => t.Id + ":" + t.Title)));
        if (_calendarContent == content) return; _calendarContent = content;
        ApplyWidgetVisibility();
        _tasks.Children.Clear();
        if (tasks.Length == 0) _tasks.Children.Add(new TextBlock { Text = "A little space for today.\nOpen Study to add a task.", FontSize = 12, Foreground = _environment.Theme.Brush("NexusMuted"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        foreach (var task in tasks)
        {
            var line = new Grid { ColumnSpacing = 10 }; line.ColumnDefinitions.Add(new() { Width = new GridLength(4) }); line.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            line.Children.Add(new Border { Width = 4, Height = 25, CornerRadius = new CornerRadius(2), Background = _environment.Theme.Brush("NexusAccent") });
            var text = new TextBlock { Text = task.Title, FontSize = 12, Foreground = _environment.Theme.Brush("NexusText"), MaxLines = 2, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(text, 1); line.Children.Add(text); _tasks.Children.Add(line);
        }
        RefreshClock();
    }
    private void ApplyWidgetVisibility()
    {
        var placement = DesktopWidgetLayoutPolicy.Resolve(
            ActualWidth, ActualHeight, _environment.Session.State.ShowClockWidget,
            _environment.Session.State.FocusMode);
        _widgetRail.Visibility = placement.ShowToday ? Visibility.Visible : Visibility.Collapsed;
        _quickFrame.Visibility = placement.ShowActions ? Visibility.Visible : Visibility.Collapsed;
        _widgetRail.VerticalAlignment = placement.AtTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        _widgetRail.Margin = placement.AtTop
            ? new Thickness(20, 58, 24, 106)
            : new Thickness(20, 20, 20, 106);
    }
    private DesktopShortcut? ItemAt(object source)
    {
        var element = source as DependencyObject;
        while (element is not null && !ReferenceEquals(element, _icons))
        { if (element is GridViewItem container) return container.Content as DesktopShortcut; element = VisualTreeHelper.GetParent(element); }
        return null;
    }
    private void OpenDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    { if (ItemAt(args.OriginalSource) is DesktopShortcut item) { _environment.OpenDesktopItem(item); args.Handled = true; } }
    internal void SetWorkArea(double bottomInset) => Padding = new Thickness(0, 0, 0, Math.Max(0, bottomInset));
    internal async Task RefreshAsync()
    {
        if (_environment.IsStopping) return;
        if (_refreshing) { _refreshAgain = true; return; } _refreshing = true;
        try
        {
            var items = await Task.Run(DesktopCatalog.Read);
            if (_environment.IsStopping) return;
            _allIcons = items; ApplyIconPreferences();
        }
        catch (Exception ex) { _environment.Report("Could not refresh desktop icons", ex); }
        finally { _refreshing = false; if (_refreshAgain && !_environment.IsStopping) { _refreshAgain = false; _ = RefreshAsync(); } }
    }
    internal void SelectPath(string path) { _selectPath = path; ApplyIconPreferences(); _icons.Focus(FocusState.Programmatic); }
    internal void ApplyIconPreferences()
    {
        string? selected = _selectPath ?? (_icons.SelectedItem as DesktopShortcut)?.Id;
        var items = DesktopCatalog.Sort(_allIcons, _environment.Session.State.DesktopIconOrder);
        _applyingIcons = true;
        try { _icons.ItemsSource = items; _icons.SelectedItem = items.FirstOrDefault(i => string.Equals(i.Id, selected, StringComparison.OrdinalIgnoreCase)); if (_icons.SelectedItem is not null) _selectPath = null; }
        finally { _applyingIcons = false; }
        RefreshContent();
    }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme;
        bool reference = MidnightGlassMetrics.ReferenceEnabled(theme.Palette, theme.HighContrast);
        _calendar.Background = reference
            ? theme.Surface("Sidebar") : theme.Material("Card", _environment.Session.State.NativeGlass);
        _calendar.BorderBrush = theme.Edge;
        _calendar.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : MidnightGlassMetrics.CardRadius);
        _quickFrame.Background = reference
            ? theme.Surface("Dock") : theme.Material("Card", _environment.Session.State.NativeGlass);
        _quickFrame.BorderBrush = theme.Edge;
        _quickFrame.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : MidnightGlassMetrics.CardRadius);
        _quickHeading.Foreground = theme.Brush("NexusMuted");
        _weekday.Foreground = theme.Brush("NexusAccent");
        _day.Foreground = theme.Brush("NexusText");
        _month.Foreground = theme.Brush("NexusMuted");
        _wallpaper.Visibility = theme.HighContrast || _environment.Session.State.ReducedEffects ? Visibility.Collapsed : Visibility.Visible;
        Background = theme.Surface("Canvas");
        if (_wallpaperName != theme.Palette.Name)
        {
            _wallpaper.Source = theme.Palette.Name == "Midnight"
                ? new SvgImageSource(new Uri("ms-appx:///Assets/Wallpapers/MidnightGlass.svg"))
                : new BitmapImage(new Uri("ms-appx:///Assets/Wallpapers/" + theme.Palette.Name + ".png"));
            _wallpaperName = theme.Palette.Name;
        }
        RefreshContent();
    }
    internal void Report(string message) { _status.Message = message; _status.Severity = InfoBarSeverity.Error; _status.IsOpen = true; }
    internal void Feedback(string message) { _status.Message = message; _status.Severity = InfoBarSeverity.Informational; _status.IsOpen = true; }
}
