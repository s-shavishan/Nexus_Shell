using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI.Controls;

internal sealed class NotesView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly TextBox _search = new() { PlaceholderText = "Search notes", FontSize = 12 };
    private readonly TextBox _title = new() { PlaceholderText = "Note title", FontSize = 18, MaxLength = 100 };
    private readonly TextBox _editor = new() { PlaceholderText = "Start typing something…", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 15, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock _status = new() { FontSize = 11, Text = "Changes save automatically" };
    private readonly Border _sidebar;
    private readonly Button _pin, _delete;
    private readonly ColumnDefinition _sidebarWidth = new() { Width = new GridLength(195) };
    private readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private string _selected = NotesWorkspace.QuickId;
    private bool _syncing, _released, _dialog, _pinnedOnly;
    internal NotesView(DesktopEnvironment environment)
    {
        _environment = environment; ColumnSpacing = 12; Padding = new Thickness(12, 2, 12, 14);
        ColumnDefinitions.Add(_sidebarWidth); ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var sidebar = new Grid { RowSpacing = 10, Padding = new Thickness(9) };
        sidebar.RowDefinitions.Add(new() { Height = GridLength.Auto }); sidebar.RowDefinitions.Add(new() { Height = GridLength.Auto }); sidebar.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.Children.Add(_search);
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var all = Button("All notes", () => { _pinnedOnly = false; Render(); }); var pinned = Button("Pinned", () => { _pinnedOnly = true; Render(); }); filters.Children.Add(all); filters.Children.Add(pinned); Grid.SetRow(filters, 1); sidebar.Children.Add(filters);
        _list.ItemTemplate = (DataTemplate)XamlReader.Load("""
          <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <StackPanel Padding="5,9" Spacing="4" AutomationProperties.Name="{Binding Title}">
              <TextBlock Text="{Binding Title}" FontSize="13" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
              <TextBlock Text="{Binding Text}" FontSize="11" Foreground="{ThemeResource NexusMuted}" MaxLines="2" TextWrapping="Wrap" TextTrimming="CharacterEllipsis"/>
            </StackPanel>
          </DataTemplate>
          """);
        Grid.SetRow(_list, 2); sidebar.Children.Add(_list); _sidebar = new() { Child = sidebar, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) }; Children.Add(_sidebar);
        var body = new Grid { RowSpacing = 10 }; body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        actions.Children.Add(Button("＋ New", () =>
        {
            try { _selected = NotesWorkspace.Add(environment.Session.State); _pinnedOnly = false; _search.Text = ""; environment.SaveState(); Render(); }
            catch (Exception ex) { _status.Text = ex.Message; }
        }));
        _pin = Button("Pin", () => { if (NotesWorkspace.TogglePin(environment.Session.State, _selected)) { environment.SaveState(); Render(); } }); actions.Children.Add(_pin);
        _delete = Button("Delete", () => _ = DeleteAsync()); actions.Children.Add(_delete); body.Children.Add(actions);
        Grid.SetRow(_title, 1); body.Children.Add(_title); Grid.SetRow(_editor, 2); body.Children.Add(_editor); Grid.SetRow(_status, 3); body.Children.Add(_status); Grid.SetColumn(body, 1); Children.Add(body);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_title, "Note title"); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_editor, "Note text");
        _search.TextChanged += (_, _) => Render();
        _list.SelectionChanged += (_, _) => { if (_syncing) return; if (_list.SelectedItem is DesktopNote note) _selected = note.Id; ShowSelection(); };
        _editor.TextChanged += (_, _) => Edited(); _title.TextChanged += (_, _) => Edited();
        _save.Tick += (_, _) => { _save.Stop(); environment.SaveState(); _status.Text = "Changes queued to save"; };
        SizeChanged += (_, _) => _sidebarWidth.Width = new GridLength(ActualWidth < 570 ? 145 : 195);
        environment.Session.Changed += Changed; ApplyAppearance(); Render();
    }
    private static Button Button(string name, Action action)
    { var button = new Button { Content = name, FontSize = 12, Padding = new Thickness(9, 6, 9, 6), Style = (Style)Application.Current.Resources["QuietButton"] }; button.Click += (_, _) => action(); return button; }
    private void Changed() { if (!_released) Render(); }
    private void Render()
    {
        if (_released) return; _syncing = true;
        try { var notes = NotesWorkspace.Read(_environment.Session.State, _search.Text, _pinnedOnly); _list.ItemsSource = notes; _list.SelectedItem = notes.FirstOrDefault(n => n.Id == _selected); }
        finally { _syncing = false; }
        ShowSelection();
    }
    private void ShowSelection()
    {
        var note = NotesWorkspace.Read(_environment.Session.State).FirstOrDefault(n => n.Id == _selected);
        _syncing = true;
        try
        {
            _editor.IsEnabled = _title.IsEnabled = note is not null; _pin.IsEnabled = _delete.IsEnabled = note is not null && note.Id != NotesWorkspace.QuickId;
            _title.IsReadOnly = note?.Id == NotesWorkspace.QuickId;
            _editor.MaxLength = note?.Id == NotesWorkspace.QuickId ? 10_000 : ExploreWorkspace.NoteLimit;
            if (_title.Text != (note?.Title ?? "")) _title.Text = note?.Title ?? "";
            if (_editor.Text != (note?.Text ?? "")) _editor.Text = note?.Text ?? "";
            _pin.Content = note?.Pinned == true ? "Unpin" : "Pin";
        }
        finally { _syncing = false; }
    }
    private void Edited()
    {
        if (_released || _syncing) return;
        if (!NotesWorkspace.Update(_environment.Session.State, _selected, _title.Text, _editor.Text)) { _status.Text = "This note was removed in another window."; ShowSelection(); return; }
        _status.Text = "Editing · " + _editor.Text.Length + " characters"; _save.Stop(); _save.Start();
    }
    private async Task DeleteAsync()
    {
        if (_dialog || _released || _selected == NotesWorkspace.QuickId) return; _dialog = true; string id = _selected;
        try
        {
            var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = RequestedTheme, Title = "Delete this note?", Content = "This removes the note from Notes and the saved board.", PrimaryButtonText = "Delete", CloseButtonText = "Keep", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !_released)
            { NotesWorkspace.Remove(_environment.Session.State, id); _selected = NotesWorkspace.QuickId; _environment.SaveState(); Render(); }
        }
        catch (Exception ex) { if (!_released) _status.Text = ex.Message; }
        finally { _dialog = false; }
    }
    internal void ApplyAppearance()
    { var theme = _environment.Theme; RequestedTheme = theme.ElementTheme; _sidebar.Background = theme.Material("Panel", _environment.Session.State.NativeGlass); _sidebar.BorderBrush = theme.Edge; _list.Foreground = _editor.Foreground = _title.Foreground = theme.Brush("NexusText"); _editor.Background = _title.Background = theme.Brush("NexusInput"); _status.Foreground = theme.Brush("NexusMuted"); }
    internal void Release()
    { if (_released) return; _released = true; bool pending = _save.IsEnabled; _save.Stop(); _environment.Session.Changed -= Changed; if (pending && !_environment.IsStopping) _environment.SaveState(); }
}
