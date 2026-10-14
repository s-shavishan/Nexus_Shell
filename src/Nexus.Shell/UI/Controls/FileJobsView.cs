using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Nexus.Runtime;
using Nexus.Runtime.Transfers;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI.Controls;

internal sealed class FileJobsView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly bool _validation;
    private readonly CancellationTokenSource _life = new();
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<string> _sources = [];
    private readonly StackPanel _sourceRows = new() { Spacing = 5 }, _jobs = new() { Spacing = 10 };
    private readonly TextBox _destination = new() { Header = "Destination folder", MaxLength = 4096 };
    private readonly ComboBox _kind = new() { Header = "Operation", ItemsSource = new[] { "Copy", "Move" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _collision = new() { Header = "When a name exists", ItemsSource = new[] { "Keep both", "Skip existing" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _verify = new() { Content = "Verify copied contents with SHA-256", IsChecked = true };
    private readonly TextBlock _status = new() { Text = "Choose sources and a destination. Existing items are never overwritten.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _summary = new() { Text = "Connecting to Core…", FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Grid _body = new() { ColumnSpacing = 18, RowSpacing = 14 };
    private readonly Border _form, _queue;
    private readonly Button _submit, _pause, _clearForm;
    private readonly List<Button> _formActions = [];
    private readonly List<Button> _sourceActions = [];
    private FileJobsSnapshot? _snapshot;
    private FileJobSpec? _pendingSubmission;
    private Guid _pendingEpoch;
    private bool _released, _reading, _busy, _narrow;
    internal FileJobsView(DesktopEnvironment environment, bool validation = false)
    {
        _environment = environment; _validation = validation; Padding = new Thickness(20, 8, 20, 18); RowSpacing = 15;
        RowDefinitions.Add(new() { Height = GridLength.Auto }); RowDefinitions.Add(new()); RowDefinitions.Add(new() { Height = GridLength.Auto });
        var intro = new StackPanel { Spacing = 5 };
        intro.Children.Add(new TextBlock { Text = "Your files. Your pace.", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        intro.Children.Add(new TextBlock { Text = "A single queue for copying and moving. Jobs continue when this window closes; closing Nexus stops them.", FontSize = 12, TextWrapping = TextWrapping.Wrap }); Children.Add(intro);
        var form = new Grid { RowSpacing = 12, Padding = new Thickness(16) }; form.RowDefinitions.Add(new()); form.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var fields = new StackPanel { Spacing = 12 }; fields.Children.Add(new TextBlock { Text = "NEW TRANSFER", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var select = new Grid { ColumnSpacing = 5 }; for (int i = 0; i < 3; i++) select.ColumnDefinitions.Add(new());
        var addFiles = Button("Add files", () => PickSourcesAsync(FileSelectionKind.OpenFiles)); var addFolder = Button("Add folder", () => PickSourcesAsync(FileSelectionKind.Folder));
        var clear = _clearForm = Button("Clear form", ClearFormAsync);
        select.Children.Add(addFiles); Grid.SetColumn(addFolder, 1); select.Children.Add(addFolder); Grid.SetColumn(clear, 2); select.Children.Add(clear); _formActions.AddRange([addFiles, addFolder]); fields.Children.Add(select); fields.Children.Add(_sourceRows);
        var destination = new Grid { ColumnSpacing = 6 }; destination.ColumnDefinitions.Add(new()); destination.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); destination.Children.Add(_destination);
        var browse = Button("Choose…", async () => { var picked = await environment.PickAsync(new(FileSelectionKind.Folder, "Choose destination folder"), _life.Token); if (!_released && picked.Count > 0) _destination.Text = picked[0]; });
        browse.VerticalAlignment = VerticalAlignment.Bottom; Grid.SetColumn(browse, 1); destination.Children.Add(browse); _formActions.Add(browse); fields.Children.Add(destination);
        fields.Children.Add(_kind); fields.Children.Add(_collision); fields.Children.Add(_verify);
        fields.Children.Add(new TextBlock { Text = "Folder moves use a same-drive rename. To transfer a folder across drives, choose Copy. Links, encrypted and offline items require their original Windows/provider tools.", FontSize = 11, TextWrapping = TextWrapping.Wrap });
        form.Children.Add(new ScrollViewer { Content = fields, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        _submit = Button("Add to queue", SubmitAsync); Grid.SetRow(_submit, 1); form.Children.Add(_submit); _form = new Border { Child = form, BorderThickness = new Thickness(1) }; _body.Children.Add(_form);
        var queue = new Grid { RowSpacing = 12, Padding = new Thickness(16) }; queue.RowDefinitions.Add(new() { Height = GridLength.Auto }); queue.RowDefinitions.Add(new() { Height = GridLength.Auto }); queue.RowDefinitions.Add(new());
        var commands = new Grid { ColumnSpacing = 8 }; commands.ColumnDefinitions.Add(new()); commands.ColumnDefinitions.Add(new());
        _pause = Button("Pause queue", () => ControlAsync(Guid.Empty, _snapshot?.Paused == true ? "resume" : "pause")); commands.Children.Add(_pause);
        var history = Button("Clear finished jobs", () => ControlAsync(Guid.Empty, "clear")); Grid.SetColumn(history, 1); commands.Children.Add(history); queue.Children.Add(commands);
        Grid.SetRow(_summary, 1); queue.Children.Add(_summary); var scroll = new ScrollViewer { Content = _jobs, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 2); queue.Children.Add(scroll);
        _queue = new Border { Child = queue, BorderThickness = new Thickness(1) }; _body.Children.Add(_queue); Grid.SetRow(_body, 1); Children.Add(_body);
        Grid.SetRow(_status, 2); Children.Add(_status); AutomationProperties.SetLiveSetting(_status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        _poll.Tick += async (_, _) => await RefreshAsync(); SizeChanged += (_, _) => Layout(); RenderSources(); Layout(); ApplyAppearance();
        if (validation) { _snapshot = new(Guid.NewGuid(), 1, false, [new(new(Guid.NewGuid(), FileJobKind.Copy, [@"C:\Example\Notes.txt"], @"C:\Destination"), FileJobState.Running, DateTimeOffset.UtcNow, "Copying", new("Copying", "Notes.txt", 1024, 8192, 0, 0, 1))]); RenderJobs(); }
    }
    private Button Button(string name, Func<Task> action)
    {
        var button = new Button { Content = new TextBlock { Text = name, TextWrapping = TextWrapping.Wrap }, FontSize = 12, Padding = new Thickness(10, 8, 10, 8), HorizontalAlignment = HorizontalAlignment.Stretch, Style = (Style)Application.Current.Resources["AuraSurfaceButton"], IsEnabled = !_validation };
        AutomationProperties.SetName(button, name);
        button.Click += async (_, _) => { if (_released || _busy || _validation) return; _busy = true; UpdateActions(); try { await action(); } catch (Exception error) { if (!_released) _status.Text = error.Message; } finally { _busy = false; if (!_released) UpdateActions(); } }; return button;
    }
    private void Layout()
    {
        bool narrow = ActualWidth < 780; if (_body.ColumnDefinitions.Count > 0 && narrow == _narrow) return; _narrow = narrow;
        _body.ColumnDefinitions.Clear(); _body.RowDefinitions.Clear();
        _body.ColumnDefinitions.Add(new()); if (!narrow) _body.ColumnDefinitions.Add(new());
        _body.RowDefinitions.Add(new()); if (narrow) _body.RowDefinitions.Add(new());
        Grid.SetColumn(_queue, narrow ? 0 : 1); Grid.SetRow(_queue, narrow ? 1 : 0);
    }
    private async Task PickSourcesAsync(FileSelectionKind kind)
    {
        if (_pendingSubmission is not null) throw new InvalidOperationException("Refresh the pending submission before changing its sources.");
        var picked = await _environment.PickAsync(new(kind, kind == FileSelectionKind.Folder ? "Choose source folder" : "Choose files to transfer"), _life.Token);
        if (_released) return;
        foreach (string path in picked) if (!_sources.Contains(path, FileJobRules.Paths) && _sources.Count < FileJobRules.MaximumSources) _sources.Add(path);
        RenderSources(); _status.Text = _sources.Count + " sources selected · up to 24 sources per job.";
    }
    private void RenderSources()
    {
        _sourceRows.Children.Clear(); _sourceActions.Clear();
        if (_sources.Count == 0) _sourceRows.Children.Add(new TextBlock { Text = "No sources selected", FontSize = 12, Opacity = .7 });
        foreach (string source in _sources)
        {
            var row = new Grid { ColumnSpacing = 5 }; row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = source, FontSize = 11, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            var remove = Button("×", () => { _sources.Remove(source); RenderSources(); return Task.CompletedTask; }); AutomationProperties.SetName(remove, "Remove " + Path.GetFileName(source) + " from selection");
            _sourceActions.Add(remove); Grid.SetColumn(remove, 1); row.Children.Add(remove); _sourceRows.Children.Add(row);
        }
        UpdateActions();
    }
    private async Task SubmitAsync()
    {
        bool pending = _pendingSubmission is not null; await RefreshAsync(); if (_released || _snapshot is null || pending && _pendingSubmission is null) return;
        if (_pendingSubmission is not null && _pendingEpoch != _snapshot.Epoch) throw new InvalidOperationException("Core restarted and this submission is absent from retained history. Review the destination, then use Clear form before creating another job.");
        _pendingSubmission ??= FileJobRules.Validate(new(Guid.NewGuid(), (FileJobKind)_kind.SelectedIndex, _sources.ToArray(), _destination.Text.Trim(), (FileCollision)_collision.SelectedIndex, _verify.IsChecked == true));
        _pendingEpoch = _snapshot.Epoch;
        _snapshot = await _environment.FileJobsAsync(RuntimeOperations.SubmitFileJob, new FileJobSubmit(_snapshot.Epoch, _pendingSubmission), _life.Token);
        if (_released) return; _pendingSubmission = null; _sources.Clear(); RenderSources(); RenderJobs(); _status.Text = "Accepted by Nexus Core. The queue runs one job at a time.";
    }
    private async Task ClearFormAsync()
    {
        if (_pendingSubmission is not null)
        {
            var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = RequestedTheme, Title = "Discard the pending request?", Content = "This clears only the local form. A transfer already accepted by Core keeps running. Review the queue and destination before creating another transfer.", PrimaryButtonText = "Discard local request", CloseButtonText = "Keep", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || _released) return;
        }
        _pendingSubmission = null; _pendingEpoch = Guid.Empty; _sources.Clear(); RenderSources();
    }
    private async Task ControlAsync(Guid id, string action)
    {
        if (_snapshot is null) await RefreshAsync(); if (_snapshot is null || _released) return;
        _snapshot = await _environment.FileJobsAsync(RuntimeOperations.ControlFileJob, new FileJobControl(_snapshot.Epoch, id, action), _life.Token);
        if (!_released) { RenderJobs(); _status.Text = action == "pause" ? "Queue paused. The current job continues; the next job waits." : "Queue updated."; }
    }
    private async Task RefreshAsync()
    {
        if (_released || _reading || _validation) return; _reading = true;
        try
        {
            var snapshot = await _environment.FileJobsAsync(RuntimeOperations.FileJobs, new { }, _life.Token); if (_released) return;
            bool changed = _snapshot is null || _snapshot.Epoch != snapshot.Epoch || _snapshot.Revision != snapshot.Revision; _snapshot = snapshot;
            if (_pendingSubmission is { } pending && snapshot.Jobs.Any(j => j.Job.Id == pending.Id)) { _pendingSubmission = null; _sources.Clear(); RenderSources(); _status.Text = "The earlier submission was accepted. It will not be added again."; }
            if (changed) RenderJobs(); UpdateActions();
        }
        catch (OperationCanceledException) when (_released) { }
        catch (Exception error) { if (!_released) _status.Text = "Queue status unavailable: " + error.Message; }
        finally { _reading = false; }
    }
    private void RenderJobs()
    {
        if (_released || _snapshot is null) return; _jobs.Children.Clear();
        _summary.Text = _snapshot.Jobs.Length + " jobs · " + (_snapshot.Paused ? "queue paused" : "one active job at a time") + (_snapshot.Notice.Length > 0 ? "\n" + _snapshot.Notice : "");
        _pause.Content = new TextBlock { Text = _snapshot.Paused ? "Resume queue" : "Pause queue", TextWrapping = TextWrapping.Wrap };
        foreach (var record in _snapshot.Jobs)
        {
            var content = new StackPanel { Spacing = 7 }; content.Children.Add(new TextBlock { Text = record.Job.Kind + " · " + Path.GetFileName(record.Job.Sources[0]) + (record.Job.Sources.Length > 1 ? " + " + (record.Job.Sources.Length - 1) : ""), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = record.State + " · " + record.Detail, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new ProgressBar { Minimum = 0, Maximum = Math.Max(1, record.Progress.TotalBytes), Value = Math.Clamp(record.Progress.BytesCopied, 0, Math.Max(1, record.Progress.TotalBytes)), IsIndeterminate = record.State == FileJobState.Planning });
            content.Children.Add(new TextBlock { Text = record.Progress.Completed + " committed · " + record.Progress.Skipped + " skipped · " + record.Progress.TotalFiles + " files · " + Size(record.Progress.BytesCopied) + " / " + Size(record.Progress.TotalBytes), FontSize = 11, TextWrapping = TextWrapping.Wrap });
            if (record.Progress.Current.Length > 0) content.Children.Add(new TextBlock { Text = record.Progress.Phase + " · " + record.Progress.Current, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var actions = new Grid { ColumnSpacing = 6 }; actions.ColumnDefinitions.Add(new()); actions.ColumnDefinitions.Add(new());
            actions.Children.Add(Button("Open destination", () => { _environment.OpenTarget(record.Job.Destination); return Task.CompletedTask; }));
            if (!FileJobRules.Terminal(record.State)) { var cancel = Button("Cancel job", () => ControlAsync(record.Job.Id, "cancel")); Grid.SetColumn(cancel, 1); actions.Children.Add(cancel); }
            else
            {
                var load = Button("Load into form", () =>
                {
                    if (_pendingSubmission is not null) throw new InvalidOperationException("Reconcile or clear the pending request first.");
                    _sources.Clear(); _sources.AddRange(record.Job.Sources); _destination.Text = record.Job.Destination; _kind.SelectedIndex = (int)record.Job.Kind;
                    _collision.SelectedIndex = (int)record.Job.Collision; _verify.IsChecked = record.Job.Verify; RenderSources();
                    _status.Text = "Review the destination and remove sources already moved. Add to queue creates a new job; it does not resume or replace previous output.";
                    return Task.CompletedTask;
                });
                Grid.SetColumn(load, 1); actions.Children.Add(load);
            }
            content.Children.Add(actions); _jobs.Children.Add(new Border { Child = content, Padding = new Thickness(13), BorderThickness = new Thickness(1), Background = _environment.Theme.Brush("NexusCard"), BorderBrush = _environment.Theme.Edge, CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 12) });
        }
        if (_snapshot.Jobs.Length == 0) _jobs.Children.Add(new TextBlock { Text = "Your queue is clear. Add a transfer to begin.", FontSize = 14, TextWrapping = TextWrapping.Wrap });
    }
    private static string Size(long value) => value >= 1024 * 1024 * 1024 ? $"{value / (1024d * 1024 * 1024):F1} GB" : value >= 1024 * 1024 ? $"{value / (1024d * 1024):F1} MB" : value >= 1024 ? $"{value / 1024d:F1} KB" : value + " B";
    private void UpdateActions()
    { if (_submit is null) return; _submit.IsEnabled = !_validation && !_busy && (_pendingSubmission is not null || _sources.Count > 0); foreach (var action in _formActions.Concat(_sourceActions)) action.IsEnabled = !_validation && !_busy && _pendingSubmission is null; _clearForm.IsEnabled = !_validation && !_busy; _destination.IsEnabled = _kind.IsEnabled = _collision.IsEnabled = _verify.IsEnabled = !_validation && !_busy && _pendingSubmission is null; _pause.IsEnabled = !_validation && !_busy && _snapshot is not null; }
    internal void SetActive(bool active)
    { if (_released || _validation) return; if (active) { _poll.Start(); _ = RefreshAsync(); } else _poll.Stop(); }
    internal void ApplyAppearance()
    { RequestedTheme = _environment.Theme.ElementTheme; _form.Background = _queue.Background = _environment.Theme.Brush("NexusPanel"); _form.BorderBrush = _queue.BorderBrush = _environment.Theme.Edge; _form.CornerRadius = _queue.CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 14); _status.Foreground = _summary.Foreground = _environment.Theme.Brush("NexusMuted"); RenderJobs(); }
    internal void Release()
    { if (_released) return; _released = true; _poll.Stop(); _life.Cancel(); _life.Dispose(); }
    internal void VerifyValidationLayout(bool narrow)
    {
        if (!_validation || _poll.IsEnabled || _snapshot?.Jobs.Length != 1 || _jobs.Children.Count != 1)
            throw new InvalidOperationException("File Jobs validation must use an isolated fixture without starting a Core request or poll.");
        if (_narrow != narrow || _body.ColumnDefinitions.Count != (narrow ? 1 : 2) || _body.RowDefinitions.Count != (narrow ? 2 : 1)
            || _form.ActualWidth <= 0 || _form.ActualHeight <= 0 || _queue.ActualWidth <= 0 || _queue.ActualHeight <= 0)
            throw new InvalidOperationException("The production File Jobs form and queue did not lay out at the requested size.");
    }
}
