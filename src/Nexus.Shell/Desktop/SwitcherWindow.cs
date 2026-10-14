using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Microsoft.UI.Xaml.Automation;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Windows.System;

namespace Nexus.Shell.Desktop;

// Six compositor relationships at most, released before hiding or paging.
internal sealed class SwitcherWindow : DesktopPanelWindow
{
    private sealed class Preview(DesktopWindowEntry entry, Border viewport, FrameworkElement fallback)
    { internal readonly DesktopWindowEntry Entry = entry; internal readonly Border Viewport = viewport; internal readonly FrameworkElement Fallback = fallback; internal WindowThumbnail? Thumbnail; internal bool Attempted; }
    private readonly Grid _body, _cards = new() { RowSpacing = 12, ColumnSpacing = 12 };
    private readonly TextBox _query = new() { PlaceholderText = "Find a window or app", MaxLength = 128 };
    private readonly ComboBox _monitor = new() { MinWidth = 150 };
    private readonly ComboBox _layouts = new() { MinWidth = 140, PlaceholderText = "Saved layouts" };
    private readonly TextBox _layoutName = new() { PlaceholderText = "Layout name", MaxLength = 40, MinWidth = 90 };
    private readonly TextBlock _summary = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _message = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Button _previous, _next, _undo, _restore, _delete, _save, _grid, _columns, _stack, _move, _apply, _discard;
    private readonly Dictionary<string, WindowMove> _staged = [];
    private long _suppressCardClickUntil;
    protected override bool DismissOnDeactivate => false;
    protected override ShellRect ResolveBounds(ShellRect work, ShellRect monitor, double scale) => monitor;
    private readonly Flyout _saveFlyout = new();
    private readonly WindowLayouts _placement = new();
    private readonly HashSet<string> _selected = [];
    private readonly List<Preview> _previews = [];
    private IReadOnlyList<DesktopWindowEntry> _windows = [];
    private IReadOnlyList<DesktopMonitor> _monitors = [];
    private IReadOnlyList<DesktopWindowEntry> _filtered = [];
    private IntPtr _current;
    private int _page, _columnCount = 3, _rowCount = 2, _previewQueued;
    private bool _refreshing, _refreshAgain, _binding, _applying;
    internal SwitcherWindow(DesktopEnvironment environment) : base(environment, "Window overview", new Grid(), 1060, 740, center: true, animateBody: false)
    {
        _body = (Grid)Root.Children[1]; _body.RowSpacing = 10;
        for (int i = 0; i < 5; i++) _body.RowDefinitions.Add(new() { Height = i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var search = new Grid { ColumnSpacing = 10 }; search.ColumnDefinitions.Add(new()); search.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); search.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        search.Children.Add(_query); Grid.SetColumn(_monitor, 1); search.Children.Add(_monitor); var refresh = ActionButton("Refresh", () => _ = RefreshAsync()); Grid.SetColumn(refresh, 2); search.Children.Add(refresh); _body.Children.Add(search);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _grid = ActionButton("Grid", () => Arrange("Grid")); _columns = ActionButton("Columns", () => Arrange("Columns")); _stack = ActionButton("Stack", () => Arrange("Stack"));
        _move = ActionButton("Stage on display", Move); _undo = ActionButton("Undo last arrangement", Undo);
        _apply = ActionButton("Apply arrangement", ApplyStaged);
        _discard = ActionButton("Cancel", Hide);
        foreach (var button in new[] { _grid, _columns, _stack, _move, _undo, _apply, _discard }) tools.Children.Add(button);
        Grid.SetRow(tools, 1); _body.Children.Add(tools);
        var layouts = new Grid { ColumnSpacing = 6 }; foreach (var width in new[] { new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto }) layouts.ColumnDefinitions.Add(new() { Width = width });
        layouts.Children.Add(_layouts);
        _save = ActionButton("Save layout…", () => _saveFlyout.ShowAt(_save)); _restore = ActionButton("Restore", RestoreLayout); _delete = ShellControls.IconButton("\uE74D", "Delete saved layout", DeleteLayout);
        Grid.SetColumn(_save, 1); layouts.Children.Add(_save); Grid.SetColumn(_restore, 2); layouts.Children.Add(_restore); Grid.SetColumn(_delete, 3); layouts.Children.Add(_delete); Grid.SetRow(layouts, 2); _body.Children.Add(layouts);
        var saveForm = new StackPanel { Spacing = 10, Width = 260 }; saveForm.Children.Add(new TextBlock { Text = "Save selected windows, or this view (up to 12).", TextWrapping = TextWrapping.Wrap });
        saveForm.Children.Add(_layoutName); saveForm.Children.Add(ActionButton("Save / replace by name", SaveLayout)); _saveFlyout.Content = saveForm;
        _saveFlyout.Opening += (_, _) => { foreach (var preview in _previews) { preview.Thumbnail?.Dispose(); preview.Thumbnail = null; preview.Attempted = true; preview.Fallback.Visibility = Visibility.Visible; } };
        _saveFlyout.Closed += (_, _) => { if (IsOpen) Render(); };
        Grid.SetRow(_cards, 3); _body.Children.Add(_cards);
        var footer = new Grid { ColumnSpacing = 6 }; footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var hints = new StackPanel { Spacing = 4 }; hints.Children.Add(_summary); hints.Children.Add(_message); footer.Children.Add(hints);
        _previous = ActionButton("‹", () => { _page--; Render(); }); _next = ActionButton("›", () => { _page++; Render(); }); AutomationProperties.SetName(_previous, "Previous windows"); AutomationProperties.SetName(_next, "Next windows");
        Grid.SetColumn(_previous, 1); footer.Children.Add(_previous); Grid.SetColumn(_next, 2); footer.Children.Add(_next); Grid.SetRow(footer, 4); _body.Children.Add(footer);
        _query.TextChanged += (_, _) => { _page = 0; Render(); };
        _monitor.SelectionChanged += (_, _) => { if (!_binding) { _page = 0; Render(); } };
        _layouts.SelectionChanged += (_, _) => { if (!_binding && SelectedLayout is { } layout) _layoutName.Text = layout.Name; UpdateActions(); };
        Root.SizeChanged += (_, _) => { var page = WindowBoardPolicy.Page(_filtered.Count, _page, Root.ActualWidth, Root.ActualHeight); if (_columnCount != page.Columns || _rowCount != page.Rows) { _columnCount = page.Columns; _rowCount = page.Rows; _page = 0; Render(); } QueuePreviews(); };
        Root.LayoutUpdated += (_, _) => QueuePreviews();
        _query.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { CommitSelection(); e.Handled = true; } };
    }
    private SavedWindowLayout? SelectedLayout => Environment.Session.State.WindowLayouts.FirstOrDefault(layout => layout.Id == ((_layouts.SelectedItem as ComboBoxItem)?.Tag as string));
    private string MonitorId => (_monitor.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
    internal void Show(IReadOnlyList<RunningWindow> windows, IntPtr current)
    {
        _staged.Clear(); _current = current; _query.Text = "";
        ShowPanel(current); _query.Focus(FocusState.Programmatic); _ = RefreshAsync(windows);
    }
    internal override void ApplyAppearance()
    {
        base.ApplyAppearance();
        // Overview must cover the entire monitor, not resemble another tool dialog.
        Frame.CornerRadius = new CornerRadius(0);
        Frame.BorderThickness = new Thickness(0);
        Root.Padding = new Thickness(28, 22, 28, 24);
        Render();
    }
    internal void RefreshOpen() { if (IsOpen) _ = RefreshAsync(); }
    internal void PopulateValidation()
    {
        _windows = Enumerable.Range(1, 7).Select(i => new DesktopWindowEntry(new RunningWindow(IntPtr.Zero, "Production window card " + i, "nexus", 0), "", "", false, false, false)).ToArray();
        Render();
    }
    private async Task RefreshAsync(IReadOnlyList<RunningWindow>? source = null)
    {
        if (!IsOpen || IsClosed) return; if (_refreshing) { _refreshAgain = true; return; } _refreshing = true;
        try
        {
            var windows = source ?? Environment.WindowBoardWindows();
            var result = await Task.Run(() => { var monitors = DesktopWindowCatalog.Monitors(); return (Monitors: monitors, Windows: DesktopWindowCatalog.Read(windows, monitors)); });
            if (IsClosed || !IsOpen) return;
            bool unchanged = _monitors.SequenceEqual(result.Monitors) && _windows.SequenceEqual(result.Windows);
            string selected = MonitorId; _monitors = result.Monitors; _windows = result.Windows;
            if (unchanged) { UpdateActions(); return; }
            _selected.RemoveWhere(key => !_windows.Any(entry => entry.Key == key));
            foreach (var key in _staged.Keys.Where(key => !_windows.Any(entry => entry.Key == key)).ToArray()) _staged.Remove(key);
            _binding = true; _monitor.Items.Clear(); _monitor.Items.Add(new ComboBoxItem { Content = "All displays", Tag = "" });
            foreach (var monitor in _monitors) _monitor.Items.Add(new ComboBoxItem { Content = monitor.Name, Tag = monitor.Id });
            _monitor.SelectedItem = _monitor.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == selected) ?? _monitor.Items[0]; _binding = false;
            BindLayouts(); Render();
        }
        catch (Exception error) { _message.Text = "The window list could not refresh. Try Refresh."; Log.Write("Window board refresh failed", error); }
        finally { _binding = false; _refreshing = false; if (_refreshAgain) { _refreshAgain = false; _ = RefreshAsync(); } }
    }
    private void BindLayouts(string? choose = null)
    {
        string? id = choose ?? SelectedLayout?.Id; _binding = true; _layouts.Items.Clear();
        foreach (var layout in Environment.Session.State.WindowLayouts) _layouts.Items.Add(new ComboBoxItem { Content = layout.Name + " · " + layout.Windows.Count, Tag = layout.Id });
        _layouts.SelectedItem = _layouts.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == id);
        _binding = false; UpdateActions();
    }
    private void Render()
    {
        if (IsClosed) return; ReleasePreviews(); _cards.Children.Clear(); _cards.RowDefinitions.Clear(); _cards.ColumnDefinitions.Clear();
        string query = _query.Text.Trim(), monitor = MonitorId;
        _filtered = _windows.Where(entry => (monitor.Length == 0 || entry.MonitorId == monitor) && (query.Length == 0
            || entry.Window.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || entry.Window.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        var paging = WindowBoardPolicy.Page(_filtered.Count, _page, Root.ActualWidth, Root.ActualHeight); _page = paging.Index; _columnCount = paging.Columns; _rowCount = paging.Rows;
        for (int i = 0; i < _columnCount; i++) _cards.ColumnDefinitions.Add(new()); for (int i = 0; i < _rowCount; i++) _cards.RowDefinitions.Add(new());
        var page = _filtered.Skip(paging.Offset).Take(paging.Count).ToArray();
        for (int i = 0; i < page.Length; i++) { var card = Card(page[i]); Grid.SetRow(card, i / _columnCount); Grid.SetColumn(card, i % _columnCount); _cards.Children.Add(card); }
        if (page.Length == 0)
        {
            var empty = new TextBlock { Text = _windows.Count == 0 ? "Open an app to see it here." : "No windows match this view.", Foreground = Environment.Theme.Brush("NexusMuted"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumnSpan(empty, _columnCount); Grid.SetRowSpan(empty, _rowCount); _cards.Children.Add(empty);
        }
        _previous.IsEnabled = paging.Previous; _next.IsEnabled = paging.Next; UpdateActions(); QueuePreviews();
    }
    private Border Card(DesktopWindowEntry entry)
    {
        var theme = Environment.Theme; var content = new Grid { RowSpacing = 7, Padding = new Thickness(10) }; content.RowDefinitions.Add(new()); content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var fallback = new StackPanel { Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        fallback.Children.Add(NexusIcons.Image(NexusIcons.ForProcess(entry.Window.ProcessName), 34)); fallback.Children.Add(new TextBlock { Text = entry.Minimized ? "Minimized · click to restore" : "Click to switch", FontSize = 11, Foreground = theme.Brush("NexusMuted"), TextAlignment = TextAlignment.Center });
        var viewport = new Border { Child = fallback, Background = theme.Brush("NexusInput"), MinHeight = 45 };
        var switchButton = new Button { Content = viewport, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = theme.Brush("NexusInput"), Style = (Style)Application.Current.Resources["QuietButton"] };
        AutomationProperties.SetName(switchButton, "Switch to or drag " + entry.Window.Title);
        switchButton.Click += (_, _) => { if (System.Environment.TickCount64 >= _suppressCardClickUntil) Commit(entry.Window); };
        content.Children.Add(switchButton);
        var label = new Grid { ColumnSpacing = 7 }; label.ColumnDefinitions.Add(new()); label.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 2 }; titles.Children.Add(new TextBlock { Text = entry.Window.Title, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = theme.Brush("NexusText"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        titles.Children.Add(new TextBlock { Text = entry.Window.ProcessName + (entry.Maximized ? " · maximized" : ""), FontSize = 11, Foreground = theme.Brush("NexusMuted"), TextTrimming = TextTrimming.CharacterEllipsis });
        var staged = _staged.TryGetValue(entry.Key, out var pending) ? pending : null;
        var placementNote = new TextBlock
        {
            Text = staged is null ? "Drag preview to stage a position" : $"Staged → {staged.Bounds.X}, {staged.Bounds.Y} · {staged.Bounds.Width} × {staged.Bounds.Height}",
            FontSize = 10, Foreground = theme.Brush(staged is null ? "NexusMuted" : "NexusAccent"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        titles.Children.Add(placementNote);
        AttachDrag(switchButton, entry, placementNote);
        label.Children.Add(titles);
        var select = new CheckBox { IsChecked = _selected.Contains(entry.Key), IsEnabled = entry.CanArrange, MinWidth = 26, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(select, "Select " + entry.Window.Title + " for layout"); select.Checked += (_, _) => { if (_selected.Count >= SavedWindowLayouts.MaximumWindows && !_selected.Contains(entry.Key)) { select.IsChecked = false; _message.Text = "Select up to twelve windows."; return; } _selected.Add(entry.Key); UpdateActions(); }; select.Unchecked += (_, _) => { _selected.Remove(entry.Key); UpdateActions(); }; Grid.SetColumn(select, 1); label.Children.Add(select); Grid.SetRow(label, 1); content.Children.Add(label);
        _previews.Add(new(entry, viewport, fallback));
        return new Border { Child = content, Background = theme.Material("Card", Environment.Session.State.NativeGlass), BorderBrush = theme.Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 18) };
    }
    private void UpdateActions()
    {
        int count = _windows.Count(entry => _selected.Contains(entry.Key)); _grid.IsEnabled = _columns.IsEnabled = _stack.IsEnabled = !_applying && count is > 0 and <= 4;
        _move.IsEnabled = !_applying && count is > 0 and <= SavedWindowLayouts.MaximumWindows && MonitorId.Length > 0;
        _undo.IsEnabled = !_applying && (_staged.Count > 0 || _placement.CanUndo);
        _apply.IsEnabled = !_applying && _staged.Count > 0;
        _discard.IsEnabled = !_applying;
        _save.IsEnabled = !_applying && _windows.Any(entry => entry.CanArrange && entry.ProcessPath.Length > 0); _delete.IsEnabled = !_applying && SelectedLayout is not null;
        int matched = SelectedLayout is { } layout ? SavedWindowLayouts.Match(layout.Windows, _windows.Where(entry => entry.CanArrange).Select(entry => entry.Candidate).ToArray()).Count : 0;
        _restore.IsEnabled = !_applying && matched > 0; _restore.Content = matched > 0 ? "Restore " + matched : "Restore";
        _summary.Foreground = _message.Foreground = Environment.Theme.Brush("NexusMuted");
        _summary.Text = _filtered.Count + " windows · " + _staged.Count + " staged · " + count + " selected · page " + (_page + 1) + " · drag a preview, then Apply";
    }
    private IReadOnlyList<DesktopWindowEntry> Selected() => _windows.Where(entry => _selected.Contains(entry.Key) && entry.CanArrange).ToArray();
    private void Arrange(string layout)
    {
        try
        {
            var entries = Selected(); var monitor = _monitors.FirstOrDefault(m => m.Id == MonitorId) ?? _monitors.FirstOrDefault(m => m.Id == entries.FirstOrDefault()?.MonitorId);
            if (monitor is null) return; var work = monitor.Work; var plan = WindowLayouts.Plan(new(work.X, work.Y, work.Width, work.Height), entries.Count, layout);
            Stage(entries.Select((entry, i) => new WindowMove(entry.Window, new(plan[i].X, plan[i].Y, plan[i].Width, plan[i].Height))).ToArray());
            _message.Text = "Arrangement staged. Apply to reposition all selected windows.";
        }
        catch (Exception error) { _message.Text = error.Message; }
    }
    private void Move()
    {
        try
        {
            var monitor = _monitors.FirstOrDefault(m => m.Id == MonitorId); if (monitor is null) return;
            var moves = Selected().Select(entry => (Entry: entry, Position: DesktopWindowCatalog.Capture(entry, _monitors))).Where(item => item.Position is not null)
                .Select(item => new WindowMove(item.Entry.Window, SavedWindowLayouts.Project(item.Position!, monitor.Work), item.Position!.Maximized)).ToArray();
            if (moves.Length == 0) { _message.Text = "These windows cannot be moved safely."; return; }
            Stage(moves); _message.Text = "Move staged for " + moves.Length + " windows. Apply to commit.";
        }
        catch (Exception error) { _message.Text = error.Message; }
    }
    private void Undo()
    {
        if (_staged.Count > 0) { _staged.Clear(); Render(); _message.Text = "Unapplied changes discarded."; }
        else { _message.Text = _placement.Undo() + " previous window positions requested."; UpdateActions(); Environment.UpdateTaskbar(); }
    }
    private void Stage(IReadOnlyList<WindowMove> changes)
    {
        if (_staged.Keys.Union(changes.Select(m => m.Window.Handle.ToInt64() + ":" + m.Window.ProcessId)).Count() > SavedWindowLayouts.MaximumWindows)
            throw new InvalidOperationException("Stage at most twelve windows per arrangement.");
        foreach (var change in changes) _staged[change.Window.Handle.ToInt64() + ":" + change.Window.ProcessId] = change;
        Render();
    }
    private void ApplyStaged()
    {
        if (_applying || _staged.Count == 0) return;
        _applying = true;
        try
        {
            var moves = _staged.Values.ToArray();
            _placement.Apply(moves);
            _staged.Clear();
            Hide();
            Environment.UpdateTaskbar();
        }
        catch (Exception error)
        {
            _message.Text = "Arrangement not applied. " + error.Message + " · Cancel leaves existing windows in place.";
            Log.Write("Window overview apply failed", error);
        }
        finally { _applying = false; if (!IsClosed) UpdateActions(); }
    }
    private void AttachDrag(Button button, DesktopWindowEntry entry, TextBlock note)
    {
        Point? start = null;
        WindowMove? candidate = null;
        bool dragging = false;
        var transform = new TranslateTransform();
        button.RenderTransform = transform;
        void Reset(bool commit)
        {
            if (commit && dragging && candidate is { } move)
            {
                try
                {
                    Stage([move]);
                    _message.Text = "Window position staged · Apply to commit or Cancel to discard.";
                }
                catch (Exception error) { _message.Text = error.Message; }
                _suppressCardClickUntil = System.Environment.TickCount64 + 400;
            }
            start = null; candidate = null; dragging = false;
            transform.X = transform.Y = 0;
            button.ReleasePointerCaptures();
        }
        button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) =>
        {
            if (_applying || !entry.CanArrange || !NativeMethods.OwnsWindow(entry.Window)
                || !args.GetCurrentPoint(Root).Properties.IsLeftButtonPressed) return;
            start = args.GetCurrentPoint(Root).Position;
            _ = button.CapturePointer(args.Pointer);
            candidate = null; dragging = false;
        }), true);
        button.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, args) =>
        {
            if (start is not { } origin || _applying || !entry.CanArrange) return;
            if (!args.GetCurrentPoint(Root).Properties.IsLeftButtonPressed) { Reset(false); return; }
            var current = args.GetCurrentPoint(Root).Position;
            double dx = current.X - origin.X, dy = current.Y - origin.Y;
            if (!dragging && !SelectionMarqueeGeometry.HasStarted(origin.X, origin.Y, current.X, current.Y)) return;
            dragging = true;
            var originMonitor = _monitors.FirstOrDefault(m => m.Id == entry.MonitorId);
            var target = _monitors.FirstOrDefault(m => m.Id == MonitorId) ?? originMonitor;
            if (target is null) return;
            try
            {
                var saved = DesktopWindowCatalog.Capture(entry, _monitors);
                if (saved is null) { Reset(false); return; }
                var original = _staged.TryGetValue(entry.Key, out var pending)
                    ? pending.Bounds : SavedWindowLayouts.Project(saved, target.Work);
                double scale = ShellLayerInterop.Scale(Handle);
                int cursorX = target.Work.X + (int)Math.Round(Math.Clamp(current.X / Math.Max(1, Root.ActualWidth), 0, 1) * target.Work.Width);
                int cursorY = target.Work.Y + (int)Math.Round(Math.Clamp(current.Y / Math.Max(1, Root.ActualHeight), 0, 1) * target.Work.Height);
                var drop = WindowOverviewGeometry.Drag(target.Work, original, dx, dy, cursorX, cursorY, scale);
                candidate = new WindowMove(entry.Window, drop.Bounds);
                note.Text = drop.Zone + $" → {drop.Bounds.X}, {drop.Bounds.Y} · {drop.Bounds.Width} × {drop.Bounds.Height}";
                transform.X = dx; transform.Y = dy;
                args.Handled = true;
            }
            catch (Exception error) { _message.Text = error.Message; Reset(false); }
        }), true);
        button.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, args) =>
        {
            if (start is null) return;
            bool wasDragging = dragging;
            Reset(true);
            if (wasDragging) args.Handled = true;
        }), true);
        button.PointerCanceled += (_, _) => Reset(false);
        button.PointerCaptureLost += (_, _) => { if (start is not null) Reset(false); };
    }
    private void SaveLayout()
    {
        try
        {
            string name = _layoutName.Text.Trim(); if (name.Length == 0) { _message.Text = "Give this layout a name first."; _layoutName.Focus(FocusState.Programmatic); return; }
            var entries = (_selected.Count > 0 ? Selected() : _filtered.Where(entry => entry.CanArrange).ToArray()).Take(SavedWindowLayouts.MaximumWindows);
            var positions = entries.Select(entry => DesktopWindowCatalog.Capture(entry, _monitors)).Where(position => position is not null).Cast<SavedWindowPosition>().ToList();
            if (positions.Count == 0) { _message.Text = "No identifiable, resizable windows are available to save."; return; }
            var layouts = Environment.Session.State.WindowLayouts; var existing = layouts.FirstOrDefault(layout => layout.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is null && layouts.Count >= SavedWindowLayouts.MaximumLayouts) { _message.Text = "Eight layouts are saved. Replace one by name or delete one."; return; }
            string id = existing?.Id ?? Guid.NewGuid().ToString("N"); if (existing is not null) layouts.Remove(existing); layouts.Add(new(id, name, positions));
            _saveFlyout.Hide(); Environment.SaveState(); BindLayouts(id); _message.Text = "Saved " + positions.Count + " window positions. Restore matches open apps; it does not launch them.";
        }
        catch (Exception error) { _message.Text = error.Message; }
    }
    private async void RestoreLayout()
    {
        var layout = SelectedLayout; if (layout is null || _applying) return; _applying = true; UpdateActions();
        try
        {
            // Refresh independently of any already-running UI refresh: restore
            // must never use a cached process path or a reused native handle.
            var source = Environment.FreshWindowBoardSource();
            var current = await Task.Run(() => { var monitors = DesktopWindowCatalog.Monitors(); return (Monitors: monitors, Windows: DesktopWindowCatalog.Read(source(), monitors)); });
            if (!IsOpen || IsClosed) return; _monitors = current.Monitors; _windows = current.Windows;
            var matches = SavedWindowLayouts.Match(layout.Windows, _windows.Select(entry => entry.Candidate).ToArray()); var moves = new List<WindowMove>();
            foreach (var match in matches)
            {
                var entry = _windows.First(item => item.Key == match.CandidateKey); if (!entry.CanArrange) continue; var position = layout.Windows[match.EntryIndex];
                var monitor = _monitors.FirstOrDefault(m => m.Id == position.MonitorId) ?? _monitors.FirstOrDefault(m => m.Id == MonitorId) ?? _monitors.FirstOrDefault(m => m.Id == entry.MonitorId) ?? _monitors.FirstOrDefault();
                if (monitor is not null) moves.Add(new(entry.Window, SavedWindowLayouts.Project(position, monitor.Work), position.Maximized));
            }
            if (moves.Count == 0) { _message.Text = "No windows can be matched safely. Open the saved apps and try again."; return; }
            Stage(moves); _message.Text = "Saved layout staged for " + moves.Count + " windows · " + (layout.Windows.Count - moves.Count) + " unmatched. Apply to commit.";
        }
        catch (Exception error) { _message.Text = error.Message; }
        finally { _applying = false; if (!IsClosed) UpdateActions(); }
    }
    private void DeleteLayout() { if (SelectedLayout is { } layout) { Environment.Session.State.WindowLayouts.Remove(layout); Environment.SaveState(); BindLayouts(); _message.Text = "Saved layout removed."; } }
    private void QueuePreviews()
    {
        if (!IsOpen || IsClosed || Interlocked.Exchange(ref _previewQueued, 1) != 0) return;
        if (!DispatcherQueue.TryEnqueue(() => { Interlocked.Exchange(ref _previewQueued, 0); UpdatePreviews(); })) Interlocked.Exchange(ref _previewQueued, 0);
    }
    private void UpdatePreviews()
    {
        if (!IsOpen || IsClosed || !Root.IsLoaded) return; double scale = ShellLayerInterop.Scale(Handle);
        foreach (var preview in _previews)
        {
            if (preview.Viewport.ActualWidth < 1 || preview.Viewport.ActualHeight < 1) continue;
            try
            {
                if (!preview.Attempted) { preview.Attempted = true; if (!Environment.Theme.HighContrast && !Environment.Session.State.ReducedEffects) preview.Thumbnail = WindowThumbnail.TryCreate(Handle, preview.Entry.Window); }
                var point = preview.Viewport.TransformToVisual(Frame).TransformPoint(new Windows.Foundation.Point(0, 0));
                var rect = new ShellRect((int)Math.Round(point.X * scale), (int)Math.Round(point.Y * scale), (int)Math.Round(preview.Viewport.ActualWidth * scale), (int)Math.Round(preview.Viewport.ActualHeight * scale));
                if (preview.Thumbnail is { } thumbnail && !thumbnail.Update(rect)) { thumbnail.Dispose(); preview.Thumbnail = null; }
                preview.Fallback.Visibility = preview.Thumbnail is null ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { preview.Thumbnail?.Dispose(); preview.Thumbnail = null; preview.Fallback.Visibility = Visibility.Visible; }
        }
    }
    private void ReleasePreviews() { foreach (var preview in _previews) preview.Thumbnail?.Dispose(); _previews.Clear(); }
    private void Commit(RunningWindow window) { Hide(); Environment.RestoreDockWindow(window); }
    internal void CommitSelection() { if (!IsOpen) return; var entry = _filtered.FirstOrDefault(item => item.Window.Handle == _current) ?? _filtered.FirstOrDefault(); if (entry is not null) Commit(entry.Window); else Hide(); }
    protected override void OnHidden() { _staged.Clear(); _saveFlyout.Hide(); ReleasePreviews(); }
    protected override void OnClosed() => Environment.SwitcherClosed(this);
}
