using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell.Desktop;

internal sealed class ClipboardShelfWindow : DesktopPanelWindow
{
    private readonly Grid _body;
    private readonly ToggleSwitch _enabled = new() { Header = "Remember copied text", OffContent = "Off", OnContent = "This session" };
    private readonly TextBox _query = new() { PlaceholderText = "Find copied text", MaxLength = 128 };
    private readonly StackPanel _items = new() { Spacing = 9 };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _message = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Button _clear;
    private bool _binding, _copying;
    private long _shownRevision = -1;
    private string _shownQuery = "", _appearance = "";
    private int _queued;
    internal ClipboardShelfWindow(DesktopEnvironment environment) : base(environment, "Clipboard shelf", new Grid(), 430, 710)
    {
        _body = (Grid)Root.Children[1]; _body.RowSpacing = 12;
        for (int i = 0; i < 5; i++) _body.RowDefinitions.Add(new() { Height = i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var intro = new TextBlock { Text = "Nexus keeps text in memory for this session and does not save or sync these entries. Apps that mark a copy as private are excluded; unmarked sensitive text can still appear here.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _body.Children.Add(intro);
        var controls = new Grid { ColumnSpacing = 10 }; controls.ColumnDefinitions.Add(new()); controls.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); controls.Children.Add(_enabled);
        _clear = ActionButton("Clear all", environment.ClearClipboardShelf); Grid.SetColumn(_clear, 1); controls.Children.Add(_clear); Grid.SetRow(controls, 1); _body.Children.Add(controls);
        Grid.SetRow(_query, 2); _body.Children.Add(_query);
        var scroll = new ScrollViewer { Content = _items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 3); _body.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 5 }; footer.Children.Add(_status); footer.Children.Add(_message); Grid.SetRow(footer, 4); _body.Children.Add(footer);
        _enabled.Toggled += (_, _) => { if (!_binding) environment.SetClipboardShelf(_enabled.IsOn); };
        _query.TextChanged += (_, _) => Refresh(); environment.ClipboardChanged += QueueRefresh;
        ApplyAppearance();
    }
    internal void Show() { ShowPanel(); Refresh(); _query.Focus(FocusState.Programmatic); }
    private void QueueRefresh()
    {
        if (IsClosed) return;
        if (Environment.ClipboardSuspended) { Hide(); return; }
        if (!IsOpen || Interlocked.Exchange(ref _queued, 1) != 0) return;
        if (!DispatcherQueue.TryEnqueue(() => { Interlocked.Exchange(ref _queued, 0); Refresh(); })) Interlocked.Exchange(ref _queued, 0);
    }
    private void Refresh()
    {
        if (!IsOpen || IsClosed) return;
        var history = Environment.Clipboard; _binding = true; _enabled.IsOn = history.Enabled; _enabled.IsEnabled = Environment.ClipboardAvailable; _binding = false;
        _clear.IsEnabled = history.Items.Count > 0; _query.IsEnabled = history.Enabled;
        _status.Text = Environment.ClipboardStatus + "\n" + history.Items.Count + " / 32 items · up to 8 pins · cleared on lock";
        string query = _query.Text.Trim(), appearance = Environment.Theme.Palette.Name + "|" + Environment.Theme.HighContrast + "|" + Environment.Session.State.NativeGlass + "|" + Environment.Session.State.GlassStyle;
        if (_shownRevision == history.Revision && _shownQuery == query && _appearance == appearance) return;
        _shownRevision = history.Revision; _shownQuery = query; _appearance = appearance; _items.Children.Clear();
        var theme = Environment.Theme; var entries = history.Search(query);
        if (entries.Count == 0) _items.Children.Add(new TextBlock { Text = !history.Enabled ? "Turn on the shelf to keep new copies here." : "Your next copied text will appear here.", TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = theme.Brush("NexusMuted"), Margin = new Thickness(4, 35, 4, 0) });
        foreach (var entry in entries)
        {
            var body = new Grid { RowSpacing = 8 }; body.RowDefinitions.Add(new()); body.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var copy = new Button { Content = new TextBlock { Text = entry.Preview, TextWrapping = TextWrapping.Wrap, MaxLines = 3, FontSize = 13, Foreground = theme.Brush("NexusText") }, HorizontalContentAlignment = HorizontalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), Style = (Style)Application.Current.Resources["QuietButton"] };
            AutomationProperties.SetName(copy, "Copy item from " + entry.Copied.ToLocalTime().ToString("HH:mm")); copy.Click += async (_, _) => await CopyAsync(entry); body.Children.Add(copy);
            var tools = new Grid { ColumnSpacing = 8 }; tools.ColumnDefinitions.Add(new()); tools.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); tools.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            tools.Children.Add(new TextBlock { Text = entry.Copied.ToLocalTime().ToString("HH:mm") + " · " + entry.Text.Length + " characters" + (entry.Pinned ? " · pinned" : ""), FontSize = 11, Foreground = theme.Brush("NexusMuted"), VerticalAlignment = VerticalAlignment.Center });
            var pin = ShellControls.IconButton(entry.Pinned ? "\uE77A" : "\uE718", entry.Pinned ? "Unpin copied text" : "Pin copied text", () => { if (!history.TogglePin(entry.Id)) _message.Text = "Up to eight entries can be pinned."; Refresh(); }); Grid.SetColumn(pin, 1); tools.Children.Add(pin);
            var remove = ShellControls.IconButton("\uE74D", "Remove copied text", () => { history.Remove(entry.Id); Refresh(); }); Grid.SetColumn(remove, 2); tools.Children.Add(remove); Grid.SetRow(tools, 1); body.Children.Add(tools);
            _items.Children.Add(new Border { Child = body, Padding = new Thickness(13), Background = theme.Material("Card", Environment.Session.State.NativeGlass), BorderBrush = theme.Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 13) });
        }
    }
    private async Task CopyAsync(ClipboardItem item)
    {
        if (_copying || !Environment.Clipboard.Items.Any(entry => entry.Id == item.Id)) return; _copying = true;
        _message.Text = "Windows is processing the copy…";
        try { bool copied = await Environment.CopyClipboardAsync(item.Text); if (!IsClosed) _message.Text = copied ? "Copied. Return to your app and paste normally." : "Windows could not copy this item. Try again."; }
        catch { if (!IsClosed) _message.Text = "Windows could not copy this item. Try again."; }
        finally { _copying = false; }
    }
    internal override void ApplyAppearance()
    {
        base.ApplyAppearance(); var theme = Environment.Theme; _status.Foreground = _message.Foreground = theme.Brush("NexusMuted");
        if (_body.Children.FirstOrDefault() is TextBlock intro) intro.Foreground = theme.Brush("NexusMuted"); _appearance = ""; Refresh();
    }
    protected override void OnHidden() { _items.Children.Clear(); _shownRevision = -1; _query.Text = ""; _message.Text = ""; }
    protected override void OnShown() => Refresh();
    protected override void OnClosed() { Environment.ClipboardChanged -= QueueRefresh; Environment.ClipboardShelfClosed(this); }
}
