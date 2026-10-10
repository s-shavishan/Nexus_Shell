using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.Models;
using Windows.Graphics;
using Windows.System;

namespace Nexus.Shell.Desktop;

internal sealed record SwitcherItem(string Title, string Detail, string IconUri, RunningWindow Window);
internal sealed class SwitcherWindow : Window
{
    private readonly DesktopEnvironment _environment;
    private readonly AppWindow _native;
    private readonly Grid _root = new() { Padding = new Thickness(20), RowSpacing = 12 };
    private readonly Border _frame;
    private readonly WindowChrome _chrome;
    private readonly UI.SurfaceMotion _motion;
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single, IsItemClickEnabled = true };
    private readonly TextBlock _title = new() { Text = "Your windows", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _hint = new() { Text = "Choose a window · Enter to return · Esc to cancel", FontSize = 12, TextWrapping = TextWrapping.Wrap };
    internal bool IsOpen { get; private set; }
    internal SwitcherWindow(DesktopEnvironment environment)
    {
        _environment = environment; _frame = new Border { Child = _root, CornerRadius = new CornerRadius(18) }; Content = _frame;
        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _native = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(handle)); _native.Title = "Nexus window switcher"; WindowSwitcherPolicy.Request(() => _native.IsShownInSwitchers = false, error => Log.Write("Switcher API unavailable; using native tool-window styling", error));
        if (_native.Presenter is OverlappedPresenter presenter) { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false; presenter.IsAlwaysOnTop = true; }
        ShellLayerInterop.ToolWindow(handle);
        _chrome = new(handle);
        _motion = new(_root, () => environment.Theme.Animations && !environment.Theme.HighContrast && environment.Session.State.SurfaceAnimations && !environment.Session.State.ReducedEffects);
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto }); _root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _root.Children.Add(_title); Grid.SetRow(_list, 1); _root.Children.Add(_list); Grid.SetRow(_hint, 2); _root.Children.Add(_hint);
        _list.ItemTemplate = (DataTemplate)XamlReader.Load("""
          <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid Padding="10,8" ColumnSpacing="12" AutomationProperties.Name="{Binding Title}">
              <Grid.ColumnDefinitions><ColumnDefinition Width="36"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
              <Image Source="{Binding IconUri}" Width="30" Height="30"/>
              <StackPanel Grid.Column="1" Spacing="3"><TextBlock Text="{Binding Title}" TextTrimming="CharacterEllipsis"/><TextBlock Text="{Binding Detail}" FontSize="11"/></StackPanel>
            </Grid>
          </DataTemplate>
          """);
        _list.ItemClick += (_, e) => Commit((SwitcherItem)e.ClickedItem);
        _list.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { CommitSelection(); e.Handled = true; } };
        _root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape }; escape.Invoked += (_, e) => { Hide(); e.Handled = true; }; _root.KeyboardAccelerators.Add(escape);
        Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated && IsOpen) Hide(); };
        Closed += (_, _) => { IsOpen = false; _motion.Dispose(); _chrome.Dispose(); environment.SwitcherClosed(this); };
        ApplyAppearance();
    }
    internal void Show(IReadOnlyList<RunningWindow> windows, IntPtr current)
    {
        if (!IsOpen)
        {
            var items = windows.Select(w => new SwitcherItem(w.Title, w.ProcessName, "ms-appx:///Assets/Icons/" + UI.NexusIcons.ForProcess(w.ProcessName) + ".svg", w)).ToArray();
            _list.ItemsSource = items;
            _list.SelectedIndex = Array.FindIndex(items, i => i.Window.Handle == current);
            var work = ShellLayerInterop.Monitor(current).Work.Bounds;
            double scale = ShellLayerInterop.Scale(WinRT.Interop.WindowNative.GetWindowHandle(this));
            int width = Math.Min(work.Width, (int)(540 * scale)), height = Math.Min(work.Height, (int)(400 * scale));
            _native.MoveAndResize(new RectInt32(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
            IsOpen = true; ApplyAppearance(); _native.Show(); Activate(); _motion.Open();
        }
        int count = _list.Items.Count;
        if (count > 0)
        {
            if (_list.SelectedIndex < 0) _list.SelectedIndex = 0;
            _list.ScrollIntoView(_list.SelectedItem); _list.Focus(FocusState.Programmatic);
        }
        _hint.Text = count == 0 ? "No app windows are open." : "Choose a window · Enter to return · Esc to cancel";
    }
    private void Commit(SwitcherItem item) { Hide(); _environment.RestoreDockWindow(item.Window); }
    internal void CommitSelection() { if (!IsOpen) return; if (_list.SelectedItem is SwitcherItem item) Commit(item); else Hide(); }
    internal void Hide() { IsOpen = false; _motion.Hide(); _native.Hide(); }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; _root.RequestedTheme = theme.ElementTheme;
        _frame.Background = _root.Background = theme.Brush("NexusPanel"); _frame.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 18);
        _title.Foreground = _list.Foreground = theme.Brush("NexusText"); _hint.Foreground = theme.Brush("NexusMuted");
        _chrome.SetCorners(theme.HighContrast, 18); _motion.Refresh();
    }
}
