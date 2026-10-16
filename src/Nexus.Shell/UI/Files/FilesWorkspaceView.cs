// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Nexus.Runtime.Files;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Files;

internal sealed class FilesWorkspaceView : Grid
{
    private readonly FilesEnvironment _environment;
    private readonly Action<IReadOnlyList<string>> _complete;
    private readonly FileTabSession _session = new();
    private readonly Dictionary<Guid, FilesView> _views = [];
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 5 };
    private readonly Border _page = new();
    private readonly TextBlock _notice = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18, 0, 18, 0) };
    private readonly Button _add, _reopen;
    private FilesView? _releasedView;
    private bool _released;
    internal event Action<string>? TitleChanged;
    // Worker acknowledgements can arrive after Closed while the environment
    // finishes shutdown. Keep one released view as a safe no-op message sink.
    internal FilesView ActiveView => _releasedView ?? _views[_session.ActiveId];

    internal FilesWorkspaceView(FilesEnvironment environment, string folder, Action<IReadOnlyList<string>> complete)
    {
        _environment = environment;
        _complete = complete;
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var bar = new Grid { ColumnSpacing = 8, Margin = new Thickness(18, 5, 18, 0) };
        bar.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        bar.Children.Add(new ScrollViewer { Content = _tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        _add = Action("+", "New tab · Ctrl+T", () => OpenNew(_session.Active.Location));
        _reopen = Action("↶", "Reopen closed tab · Ctrl+Shift+T", Reopen);
        actions.Children.Add(_add); actions.Children.Add(_reopen);
        Grid.SetColumn(actions, 1); bar.Children.Add(actions); Children.Add(bar);
        Grid.SetRow(_notice, 1); Children.Add(_notice);
        Grid.SetRow(_page, 2); Children.Add(_page);
        Shortcut(VirtualKey.T, VirtualKeyModifiers.Control, () => OpenNew(_session.Active.Location));
        Shortcut(VirtualKey.T, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, Reopen);
        Shortcut(VirtualKey.W, VirtualKeyModifiers.Control, () => Close(_session.ActiveId));
        Shortcut(VirtualKey.Tab, VirtualKeyModifiers.Control, () => Cycle(1));
        Shortcut(VirtualKey.Tab, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => Cycle(-1));
        OpenNew(folder);
    }

    private Button Action(string label, string name, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(10, 7, 10, 7),
            Style = (Style)Application.Current.Resources["QuietButton"] };
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        button.Click += (_, _) => Run(action);
        return button;
    }

    private void Shortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var shortcut = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        shortcut.Invoked += (_, args) => { Run(action); args.Handled = true; };
        KeyboardAccelerators.Add(shortcut);
    }

    private void Run(Action action)
    {
        if (_released) return;
        if (_views.Count > 0 && !ActiveView.CanSwitchTab)
        { _notice.Text = "Finish or cancel the current file action before changing tabs."; return; }
        try { _notice.Text = ""; action(); }
        catch (Exception error) { _notice.Text = error.Message; Log.Write("Files tabs", error); }
    }

    private void AddView(FileTab tab)
    {
        var view = new FilesView(_environment, new(FileSelectionKind.Browse), _complete);
        view.FolderChanged += _ =>
        {
            if (_released || !_views.ContainsKey(tab.Id)) return;
            _session.Update(tab.Id, view.CurrentLocation);
            RenderTabs();
        };
        view.NewTabRequested += folder => Run(() => OpenNew(folder));
        _views.Add(tab.Id, view);
        SelectActive();
        _ = view.NavigateAsync(tab.Location);
    }

    private void OpenNew(string folder)
    {
        var tab = _session.Open(folder);
        try { AddView(tab); }
        catch { _session.Close(tab.Id); throw; }
    }

    internal void OpenLocation(string folder) => Run(() =>
    {
        if (_session.Find(folder) is { } existing)
        {
            _session.Activate(existing.Id);
            SelectActive();
            _ = ActiveView.NavigateAsync(existing.Location);
        }
        else OpenNew(folder);
    });

    private void Cycle(int direction) { _session.Cycle(direction); SelectActive(); }
    private void Reopen()
    {
        if (!_session.CanReopen) return;
        AddView(_session.Reopen());
    }

    private void Close(Guid id)
    {
        if (_session.Tabs.Count == 1) { _complete([]); return; }
        if (!_session.Close(id)) return;
        var view = _views[id];
        if (ReferenceEquals(_page.Child, view)) _page.Child = null;
        _views.Remove(id); view.Release();
        SelectActive();
    }

    private void SelectActive()
    {
        if (_page.Child is FilesView previous && !ReferenceEquals(previous, ActiveView)) previous.Suspend();
        _page.Child = null;
        _page.Child = ActiveView;
        RenderTabs();
    }

    private void RenderTabs()
    {
        _tabs.Children.Clear();
        foreach (var tab in _session.Tabs)
        {
            Guid id = tab.Id;
            var group = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
            var select = Action(tab.Title, (id == _session.ActiveId ? "Selected tab: " : "Switch to tab: ") + tab.Title,
                () => { _session.Activate(id); SelectActive(); });
            select.MaxWidth = 170;
            select.Content = new TextBlock { Text = tab.Title, MaxWidth = 145, TextTrimming = TextTrimming.CharacterEllipsis };
            ToolTipService.SetToolTip(select, tab.Location);
            select.Background = _environment.Theme.Brush(id == _session.ActiveId ? "NexusSelection" : "NexusSidebar");
            group.Children.Add(select);
            group.Children.Add(Action("×", "Close tab " + tab.Title, () => Close(id)));
            _tabs.Children.Add(group);
        }
        _add.IsEnabled = _session.Tabs.Count < FileTabSession.MaximumTabs;
        _reopen.IsEnabled = _session.CanReopen;
        TitleChanged?.Invoke(_session.Active.Title + " · " + _session.Tabs.Count + (_session.Tabs.Count == 1 ? " tab" : " tabs"));
    }

    internal void ApplyAppearance()
    {
        if (_released) return;
        RequestedTheme = _environment.Theme.ElementTheme;
        _notice.Foreground = _environment.Theme.Brush("NexusMuted");
        foreach (var view in _views.Values) view.ApplyAppearance();
        RenderTabs();
    }

    internal void Release()
    {
        if (_released) return;
        _released = true;
        _releasedView = ActiveView;
        foreach (var view in _views.Values) view.Release();
        _views.Clear(); _page.Child = null; _tabs.Children.Clear();
    }
}
