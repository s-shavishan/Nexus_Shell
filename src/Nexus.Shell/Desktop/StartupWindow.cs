using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Windows.Graphics;

namespace Nexus.Shell.Desktop;

// A post-authentication readiness surface. Its rows reflect completed work;
// no fixed animation delay, credentials, or simulated percentage is used.
internal sealed class StartupWindow : Window
{
    private readonly TextBlock[] _status = new TextBlock[4];
    private readonly TextBlock _detail = new() { Text = "Connecting to your Nexus session", FontSize = 13, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressRing _activity = new() { IsActive = true, Width = 22, Height = 22 };
    private readonly Button _cancel = new() { Content = "Return to Windows", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly WindowChrome _chrome;
    private bool _closed, _transition;
    internal event Action? Cancelled;
    private static SolidColorBrush Color(byte r, byte g, byte b) => new(Windows.UI.Color.FromArgb(255, r, g, b));
    internal StartupWindow()
    {
        var content = new StackPanel { Spacing = 19, Margin = new Thickness(34) };
        content.Children.Add(new TextBlock { Text = "NEXUS", FontSize = 38, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Color(241, 245, 255) });
        content.Children.Add(new TextBlock { Text = "Your space is coming together", FontSize = 18, Foreground = Color(210, 221, 244) });
        string[] names = ["Nexus Core", "Saved workspace", "Desktop", "Dock"];
        for (int i = 0; i < names.Length; i++)
        {
            var row = new Grid { ColumnSpacing = 16 };
            row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = names[i], FontSize = 14, Foreground = Color(225, 232, 246) });
            _status[i] = new() { Text = i == 0 ? "Starting" : "Waiting", FontSize = 13, Foreground = Color(167, 186, 217) };
            Grid.SetColumn(_status[i], 1); row.Children.Add(_status[i]); content.Children.Add(row);
        }
        var progress = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _detail.Foreground = Color(198, 214, 242); _activity.Foreground = Color(155, 194, 255);
        progress.Children.Add(_activity); progress.Children.Add(_detail); content.Children.Add(progress);
        _cancel.Click += (_, _) => { _cancel.IsEnabled = false; Cancelled?.Invoke(); }; content.Children.Add(_cancel);
        var background = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
        background.GradientStops.Add(new() { Color = Windows.UI.Color.FromArgb(255, 14, 22, 40), Offset = 0 });
        background.GradientStops.Add(new() { Color = Windows.UI.Color.FromArgb(255, 35, 43, 70), Offset = 1 });
        Content = new Border { Background = background, BorderBrush = Color(69, 87, 123), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(22), Child = content };
        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var window = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(handle)); window.Title = "Nexus startup";
        WindowSwitcherPolicy.Request(() => window.IsShownInSwitchers = false, error => Log.Write("Startup switcher fallback", error));
        if (window.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false; }
        ShellLayerInterop.ToolWindow(handle); _chrome = new(handle); _chrome.SetCorners(false, 22);
        var work = ShellLayerInterop.Monitor(handle).Work; double scale = ShellLayerInterop.Scale(handle);
        int width = (int)Math.Min(work.Right - work.Left, 600 * scale), height = (int)Math.Min(work.Bottom - work.Top, 445 * scale);
        window.MoveAndResize(new RectInt32(work.Left + (work.Right - work.Left - width) / 2, work.Top + (work.Bottom - work.Top - height) / 2, width, height));
        Closed += (_, _) => { _closed = true; _activity.IsActive = false; _chrome.Dispose(); if (!_transition) Cancelled?.Invoke(); };
        Activate();
    }
    internal void Report(StartupStep step)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed) return;
            int current = (int)step; _status[current].Text = "Ready"; _status[current].Foreground = Color(152, 226, 206);
            if (current + 1 < _status.Length) _status[current + 1].Text = "Starting";
            _detail.Text = step switch { StartupStep.Core => "Loading your saved workspace", StartupStep.Workspace => "Preparing your desktop", StartupStep.Desktop => "Preparing your dock", _ => "Your Nexus desktop is ready" };
        });
    }
    internal void Complete()
    {
        if (_closed) return; _transition = true; _activity.IsActive = false; Close();
    }
}
