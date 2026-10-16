// නමෝ බුද්ධාය 🤍
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Desktop;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Windows.Graphics;
using Windows.System;

namespace Nexus.Shell;
// Keep the published MainWindow PRI identity while replacing Sections with
// Nexus Controls. DesktopEnvironment remains the sole desktop/state owner.
public sealed partial class MainWindow : Window
{
    internal const string NativeWindowTitle = "Nexus Controls";
    private readonly DesktopEnvironment _environment;
    private readonly ShellState _state;
    private readonly AppWindow _appWindow;
    private readonly IntPtr _handle;
    private readonly WindowChrome _chrome;
    private readonly NativeCaptionRegions _caption;
    private readonly WindowTransition _transition;
    private readonly WindowMaterial _material = new();
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };
    private readonly List<Action> _bindings = [], _paints = [];
    private readonly Dictionary<string, FrameworkElement> _anchors = new();
    private readonly List<(Button Button, string Page)> _navigation = [];
    private bool _ready, _syncing, _dialogOpen;
    private string _page = "home";
    private NexusAppearance? _appearanceUndo;
    private bool _highContrast => _environment.Theme.HighContrast;

    internal MainWindow(DesktopEnvironment environment)
    {
        _environment = environment;
        _state = environment.Session.State;
        InitializeComponent();
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle));
        _appWindow.Title = NativeWindowTitle;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
            presenter.SetBorderAndTitleBar(false, false);
        _transition = new(DesktopRoot, () => environment.Theme.Animations && _state.SurfaceAnimations && !_highContrast && !_state.ReducedEffects);
        _chrome = new(_handle, resizable: true, minimumWidth: 480, minimumHeight: 420, stateChanged: _ => RefreshFrame(), minimizeRequested: MinimizeFromDock, closeRequested: RequestClose, role: "Nexus Controls");
        _transition.BindRestore(_chrome);
        _caption = new(_appWindow, _chrome, ControlsDragArea);
        var work = ShellLayerInterop.Monitor(_handle).Work.Bounds;
        double scale = ShellLayerInterop.Scale(_handle);
        int width = Math.Min(work.Width, (int)(1080 * scale)), height = Math.Min(work.Height, (int)(780 * scale));
        _appWindow.MoveAndResize(new RectInt32(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
        string icon = Path.Combine(AppContext.BaseDirectory, "Assets", "Nexus.ico");
        if (File.Exists(icon))
            _appWindow.SetIcon(icon);
        foreach (var page in NexusControlsCatalog.Pages)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12
            };
            row.Children.Add(new FontIcon { Glyph = page.Glyph, FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") });
            row.Children.Add(new TextBlock { Text = page.Title, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button
            {
                Content = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 13, 10, 13),
                Style = (Style)Application.Current.Resources["QuietButton"],
                CornerRadius = new CornerRadius(10)
            };
            AutomationProperties.SetName(button, page.Title);
            button.Click += (_, _) => Navigate(page.Id);
            _navigation.Add((button, page.Id));
            Navigation.Children.Add(button);
        }

        CompactNavigation.ItemsSource = NexusControlsCatalog.Pages;
        var search = new KeyboardAccelerator
        {
            Key = VirtualKey.F,
            Modifiers = VirtualKeyModifiers.Control
        };
        search.Invoked += (_, e) =>
        {
            SettingsSearch.Focus(FocusState.Programmatic);
            e.Handled = true;
        };
        DesktopRoot.KeyboardAccelerators.Add(search);
        var escape = new KeyboardAccelerator
        {
            Key = VirtualKey.Escape
        };
        escape.Invoked += (_, e) =>
        {
            if (!_dialogOpen && SettingsSearch.Text.Length > 0)
            {
                SettingsSearch.Text = "";
                e.Handled = true;
            }
        };
        DesktopRoot.KeyboardAccelerators.Add(escape);
        _timer.Tick += (_, _) => RefreshBindings();
        Activated += (_, args) =>
        {
            if (_ready && args.WindowActivationState != WindowActivationState.Deactivated)
            {
                RefreshSharedAppearance();
                _timer.Start();
            }
            else
                _timer.Stop();
        };
        Closed += Window_Closed;
        _ready = true;
        Navigate("home");
        RefreshSharedAppearance();
    }

    internal void OpenControls(string? page = null, string? setting = null)
    {
        if (!_ready)
            return;
        if (page is not null || setting is not null)
            Navigate(page ?? "home", setting);
        _transition.Restore();
        _appWindow.Show();
        Activate();
        NativeMethods.Activate(_handle);
    }

    private void Navigate(string page, string? setting = null)
    {
        if (!_ready || _dialogOpen)
            return;
        _page = NexusControlsCatalog.PageId(page);
        _syncing = true;
        SettingsSearch.Text = "";
        CompactNavigation.SelectedItem = NexusControlsCatalog.Pages.First(p => p.Id == _page);
        _syncing = false;
        BuildPage();
        RefreshNavigation();
        PageScroll.ChangeView(null, 0, null, true);
        if (setting is not null && _anchors.TryGetValue(setting, out var anchor))
        {
            void Reveal()
            {
                if (_ready && _anchors.TryGetValue(setting, out var current) && ReferenceEquals(current, anchor))
                    anchor.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            }

            if (anchor.IsLoaded)
                DispatcherQueue.TryEnqueue(Reveal);
            else
            {
                RoutedEventHandler? loaded = null;
                loaded = (_, _) =>
                {
                    anchor.Loaded -= loaded;
                    Reveal();
                };
                anchor.Loaded += loaded;
            }
        }
    }

    private void BuildPage()
    {
        _bindings.Clear();
        _paints.Clear();
        _anchors.Clear();
        PageContent.Children.Clear();
        var page = NexusControlsCatalog.Pages.First(p => p.Id == _page);
        PageTitle.Text = page.Title;
        PageSubtitle.Text = page.Description;
        switch (_page)
        {
            case "core":
                BuildCore();
                break;
            case "personalize":
                BuildPersonalize();
                break;
            case "desktop":
                BuildDesktop();
                break;
            case "privacy":
                BuildPrivacy();
                break;
            case "startup":
                PageContent.Children.Add(Card(DesktopModeCard(), "signin"));
                break;
            default:
                BuildHome();
                break;
        }

        RefreshBindings();
        RefreshPaints();
    }

    private void SettingsSearch_Changed(object sender, TextChangedEventArgs args)
    {
        if (!_ready || _syncing)
            return;
        if (string.IsNullOrWhiteSpace(SettingsSearch.Text))
        {
            BuildPage();
            return;
        }

        _bindings.Clear();
        _paints.Clear();
        _anchors.Clear();
        PageContent.Children.Clear();
        PageTitle.Text = "Find your setting";
        var results = NexusControlsCatalog.Find(SettingsSearch.Text);
        PageSubtitle.Text = results.Count + " matching settings";
        foreach (var item in results)
        {
            var content = new StackPanel
            {
                Spacing = 6
            };
            content.Children.Add(Text(item.Title, 16));
            content.Children.Add(Text(NexusControlsCatalog.Pages.First(p => p.Id == item.Page).Title + " · " + item.Detail, 12, true));
            var button = ActionButton("", () => Navigate(item.Page, item.Id));
            button.Content = content;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetName(button, item.Title);
            PageContent.Children.Add(button);
        }

        if (results.Count == 0)
            PageContent.Children.Add(Text("Try “glass”, “dock”, “clipboard” or “startup”.", 14, true));
        RefreshPaints();
    }

    internal void RefreshSharedAppearance()
    {
        if (!_ready)
            return;
        DesktopRoot.RequestedTheme = _environment.Theme.ElementTheme;
        _material.Apply(this, DesktopRoot, _environment);
        Sidebar.Background = _environment.Theme.Surface("Sidebar", _state.ReducedEffects);
        RefreshFrame();
        RefreshNavigation();
        RefreshPaints();
        RefreshBindings();
    }

    private void RefreshFrame()
    {
        if (_chrome is null)
            return;
        DesktopRoot.CornerRadius = new CornerRadius(_highContrast || _chrome.IsAttached ? 0 : 14);
        DesktopRoot.BorderThickness = new Thickness(_chrome.IsAttached ? 0 : 1);
        _chrome.SetCorners(_highContrast);
    }

    private void RefreshNavigation()
    {
        foreach (var(button, page)in _navigation)
        {
            button.Background = page == _page ? _environment.Theme.Brush("NexusSelection") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.Foreground = _environment.Theme.Brush("NexusText");
            AutomationProperties.SetHelpText(button, page == _page ? "Current page" : "");
        }
    }

    private void RefreshPaints()
    {
        foreach (var paint in _paints)
            paint();
    }

    internal void RefreshBindings()
    {
        if (!_ready || _syncing)
            return;
        _syncing = true;
        try
        {
            foreach (var update in _bindings)
                update();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (!_ready)
            return;
        bool compact = NexusControlsCatalog.CompactNavigation(args.NewSize.Width);
        Sidebar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = new GridLength(compact ? 0 : 216);
        CompactNavigation.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CompactNavigation_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (_ready && !_syncing && CompactNavigation.SelectedItem is NexusControlPage page)
            Navigate(page.Id);
    }

    private void ShowStatus(string message)
    {
        if (_ready)
            StatusText.Text = message;
    }

    private void SaveState()
    {
        if (_ready)
            _environment.SaveState();
        RefreshBindings();
    }

    private void WindowsTools_Click(object sender, RoutedEventArgs args) => _environment.ShowWindowsTools();
    private void Close_Click(object sender, RoutedEventArgs args) => RequestClose();
    private async void RequestClose()
    {
        if (!_dialogOpen)
            await _transition.CloseAsync(Close);
    }

    private void Minimize_Click(object sender, RoutedEventArgs args) => MinimizeFromDock();
    internal async void MinimizeFromDock()
    {
        if (_transition.IsMinimizing)
        {
            _transition.Restore();
            return;
        }

        await _transition.MinimizeAsync(() =>
        {
            _timer.Stop();
            if (_appWindow.Presenter is OverlappedPresenter presenter)
                presenter.Minimize();
            _environment.UpdateTaskbar();
        });
    }

    internal void ToggleFromDock()
    {
        if (NativeMethods.GetForegroundWindow() == _handle && !NativeMethods.IsMinimized(_handle))
            MinimizeFromDock();
        else
            OpenControls();
    }

    private void Maximize_Click(object sender, RoutedEventArgs args)
    {
        _transition.Restore();
        if (_appWindow.Presenter is OverlappedPresenter p)
        {
            if (p.State == OverlappedPresenterState.Maximized)
                p.Restore();
            else
                p.Maximize();
        }
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        _ready = false;
        _timer.Stop();
        _bindings.Clear();
        _paints.Clear();
        _anchors.Clear();
        _caption.Dispose();
        _transition.Dispose();
        _chrome.Dispose();
    // Closing Controls never ends or finalizes the shared desktop session.
    }
}
