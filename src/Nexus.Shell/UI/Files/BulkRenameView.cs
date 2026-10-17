// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Nexus.Runtime.Files;
using Windows.ApplicationModel.DataTransfer;

namespace Nexus.Shell.UI.Files;

internal sealed class BulkRenameView : Grid
{
    private readonly BulkRenameSession _session;
    private string[] _sources;
    private readonly TextBlock _selectionSummary = Text("");
    private readonly TextBox _prefix = Field("Prefix"), _suffix = Field("Suffix"), _find = Field("Find literal text"), _replace = Field("Replace with");
    private readonly TextBox _start = Field("Start", "1", 10), _digits = Field("Digits", "3", 1), _separator = Field("Number separator", "_", 16);
    private readonly CheckBox _extensions = new() { Content = "Preserve file extensions", IsChecked = true };
    private readonly CheckBox _ignoreCase = new() { Content = "Ignore case when finding text" };
    private readonly CheckBox _numbering = new() { Content = "Add sequential numbers" };
    private readonly CheckBox _confirm = new() { Content = "I reviewed every target name" };
    private readonly CheckBox _discard = new() { Content = "Discard this receipt and its remaining Undo" };
    private readonly ComboBox _mode = new() { ItemsSource = new[] { "Preview", "Last batch receipt" }, SelectedIndex = 0, MinWidth = 180 };
    private readonly TextBlock _status = Text("Set rules, then build a preview. No files change until Apply.");
    private readonly TextBlock _summary = Text("");
    private readonly ListView _rows = new() { SelectionMode = ListViewSelectionMode.Single, Height = 240 };
    private readonly TextBox _destination = new() { Header = "New CSV receipt path", MaxLength = 32700 };
    private readonly Button _preview, _apply, _undo, _cancel, _export, _clear, _copy;
    private readonly Control[] _inputs;
    private CancellationTokenSource? _cancellation;
    private BulkRenamePlan? _plan;
    private bool _fresh, _busy, _released;
    internal bool Busy => _busy;

    internal BulkRenameView(BulkRenameSession session, IEnumerable<string> sources, string folder)
    {
        _session = session; _sources = sources.ToArray();
        MinWidth = 240; MaxWidth = 760;
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(_selectionSummary);
        body.Children.Add(Text("Names already in use, case-only changes, links and unsupported filesystems are refused. A batch can finish partially; inspect its receipt and use Undo for unchanged outputs."));
        _inputs = [_prefix, _suffix, _find, _replace, _start, _digits, _separator, _extensions, _ignoreCase, _numbering];
        body.Children.Add(Pair(_prefix, _suffix)); body.Children.Add(Pair(_find, _replace));
        body.Children.Add(_extensions); body.Children.Add(_ignoreCase); body.Children.Add(_numbering);
        body.Children.Add(Pair(_start, _digits)); body.Children.Add(_separator);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _preview = Button("Build preview", PreviewAsync);
        _apply = Button("Apply rename", ApplyAsync);
        _undo = Button("Undo remaining", UndoAsync);
        _cancel = Button("Cancel operation", () => { RequestCancel(); return Task.CompletedTask; });
        foreach (var button in new[] { _preview, _apply, _undo, _cancel }) actions.Children.Add(button);
        body.Children.Add(_confirm); body.Children.Add(Scrollable(actions));
        body.Children.Add(_mode); body.Children.Add(_summary); body.Children.Add(_rows);
        _copy = Button("Copy selected result", () =>
        {
            if ((_rows.SelectedItem as ListViewItem)?.Tag is string detail)
            { var data = new DataPackage(); data.SetText(detail); Clipboard.SetContent(data); _status.Text = "Selected result copied."; }
            return Task.CompletedTask;
        });
        body.Children.Add(_copy);
        _destination.Text = Path.Combine(folder, "Nexus-rename-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6] + ".csv");
        body.Children.Add(_destination);
        _export = Button("Export receipt CSV", ExportAsync); body.Children.Add(_export);
        body.Children.Add(_discard);
        _clear = Button("Clear receipt for a new batch", () =>
        {
            if (!_discard.IsChecked.GetValueOrDefault() || _busy) return Task.CompletedTask;
            _session.ClearReceipt(); _discard.IsChecked = false; _fresh = false; _plan = null;
            _mode.SelectedIndex = 0; Render(); _status.Text = "Receipt cleared. Select rules and build a fresh preview.";
            return Task.CompletedTask;
        });
        body.Children.Add(_clear); body.Children.Add(_status);
        body.Children.Add(Text("The last receipt and Undo live in this Files tab. Export before closing the tab or Files. No automatic rollback or restart replay runs."));
        var scroll = new ScrollViewer { Content = body, MaxHeight = 660, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Children.Add(scroll);
        void Fit() { if (!_released && XamlRoot is { } root) scroll.MaxHeight = Math.Clamp(root.Size.Height - 180, 140, 660); }
        Loaded += (_, _) => Fit(); SizeChanged += (_, _) => Fit();
        foreach (var text in new[] { _prefix, _suffix, _find, _replace, _start, _digits, _separator }) text.TextChanged += (_, _) => Dirty();
        foreach (var check in new[] { _extensions, _ignoreCase, _numbering })
        { check.Checked += (_, _) => Dirty(); check.Unchecked += (_, _) => Dirty(); }
        foreach (var check in new[] { _confirm, _discard })
        { check.Checked += (_, _) => UpdateActions(); check.Unchecked += (_, _) => UpdateActions(); }
        _mode.SelectionChanged += (_, _) => Render(); _rows.SelectionChanged += (_, _) => UpdateActions();
        AutomationProperties.SetName(_mode, "Bulk rename preview or receipt");
        AutomationProperties.SetName(_rows, "Every original and target name with validation or operation result");
        if (session.Receipt is not null) _mode.SelectedIndex = 1;
        Render();
    }
    private static TextBox Field(string label, string value = "", int max = 160) => new() { Header = label, Text = value, MaxLength = max, MinWidth = 100 };
    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = 12, IsTextSelectionEnabled = true };
    private static Grid Pair(FrameworkElement first, FrameworkElement second)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
        grid.Children.Add(first); Grid.SetColumn(second, 1); grid.Children.Add(second); return grid;
    }
    private static ScrollViewer Scrollable(UIElement content) => new() { Content = content, HorizontalScrollMode = ScrollMode.Enabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private Button Button(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Style = (Style)Application.Current.Resources["QuietButton"] };
        AutomationProperties.SetName(button, label);
        button.Click += async (_, _) =>
        {
            if (_released) return;
            try { await action(); }
            catch (Exception error) { if (!_released) _status.Text = error.Message; }
            finally { if (!_released) UpdateActions(); }
        };
        return button;
    }
    private void Dirty()
    {
        if (_released || _busy) return;
        _fresh = false; _confirm.IsChecked = false;
        _status.Text = "Rules changed. Build a fresh preview before applying."; UpdateActions();
    }
    private Task PreviewAsync()
    {
        if (_busy) return Task.CompletedTask;
        if (!int.TryParse(_start.Text, out int start) || !int.TryParse(_digits.Text, out int digits))
            throw new ArgumentException("Enter whole numbers for Start and Digits.");
        var rules = new BulkRenameRules(_prefix.Text, _suffix.Text, _find.Text, _replace.Text, _ignoreCase.IsChecked == true,
            _numbering.IsChecked == true, start, digits, _separator.Text, _extensions.IsChecked == true);
        _fresh = false; _plan = null; _confirm.IsChecked = false;
        return Work((token, _) => _session.Preview(_sources, rules, token), plan =>
        {
            _plan = plan; _fresh = true; _mode.SelectedIndex = 0; Render();
            _status.Text = plan.CanApply ? "Review every target, then confirm and Apply. No changes have been made." : "No valid batch to apply. Resolve blocked rows or change the rules and preview again.";
        });
    }
    private Task ApplyAsync()
    {
        if (_busy || !_fresh || _plan?.CanApply != true || _confirm.IsChecked != true || _session.Receipt is not null) return Task.CompletedTask;
        var plan = _plan; _fresh = false;
        return Work((token, progress) => _session.Apply(plan, token, progress), receipt =>
        { CurrentBatchPaths(receipt); _mode.SelectedIndex = 1; Render(); _status.Text = $"{receipt.Renamed} renamed; {receipt.Rows.Count(r => r.State == RenameItemState.Failed)} failed. Inspect every result. Undo rechecks identity, metadata and original-name availability."; });
    }
    private Task UndoAsync()
    {
        if (_busy || _session.Receipt?.CanUndo != true) return Task.CompletedTask;
        return Work((token, progress) => _session.Undo(token, progress), receipt =>
        { CurrentBatchPaths(receipt); _mode.SelectedIndex = 1; Render(); _status.Text = $"{receipt.Restored} original names restored. Refused/pending rows retain their receipt; resolve the conflict and retry Undo."; });
    }
    private Task ExportAsync()
    {
        if (_busy || _session.Receipt is not { } receipt) return Task.CompletedTask;
        string destination = _destination.Text;
        return Work((_, _) => { BulkRenameReport.ExportNew(receipt, destination); return destination; }, path => _status.Text = "Receipt exported to " + path);
    }
    private async Task Work<T>(Func<CancellationToken, Action<RenameProgress>, T> action, Action<T> completed)
    {
        if (_busy || _released) return;
        _busy = true; _cancellation = new(); var token = _cancellation.Token;
        _status.Text = "Working… Cancellation takes effect between filesystem calls."; UpdateActions();
        try
        {
            var result = await BulkRenameWork.RunAsync(() => action(token, p => DispatcherQueue.TryEnqueue(() =>
            { if (!_released && _busy) _status.Text = $"{p.Phase} · {p.Completed}/{p.Total}\n{p.Path}"; })));
            if (!_released) completed(result);
        }
        catch (OperationCanceledException) { if (!_released) _status.Text = "Preview cancelled. Build a fresh preview to continue."; }
        finally
        {
            _cancellation.Dispose(); _cancellation = null; _busy = false;
            if (!_released) { Render(); UpdateActions(); }
        }
    }
    private void Render()
    {
        if (_released) return;
        _selectionSummary.Text = $"{_sources.Length} selected · numbering follows original name order · up to 100 sibling items";
        _rows.Items.Clear();
        bool receiptMode = _mode.SelectedIndex == 1;
        if (receiptMode && _session.Receipt is { } receipt)
        {
            _summary.Text = $"Receipt · {receipt.Renamed} renamed · {receipt.Restored} restored · {receipt.Rows.Count} items" + (receipt.Cancelled ? " · Cancelled" : "");
            foreach (var row in receipt.Rows) Add(row.Source, row.Target, row.State + " · " + row.Detail +
                (row.UndoState == RenameUndoState.None ? "" : "\nUndo: " + row.UndoState + " · " + row.UndoDetail));
        }
        else if (!receiptMode && _plan is { } plan)
        {
            _summary.Text = $"Preview · {plan.Rows.Count(r => r.State == RenamePreviewState.Ready)} changes · {plan.Rows.Count(r => r.State == RenamePreviewState.Blocked)} blocked" + (_fresh ? "" : " · Rebuild required");
            foreach (var row in plan.Rows) Add(row.Source, row.Target, row.State + " · " + row.Detail);
        }
        else _summary.Text = receiptMode ? "No batch receipt yet." : "Build a preview to see every original → target mapping.";
        UpdateActions();
    }
    private void CurrentBatchPaths(BulkRenameReceipt receipt) => _sources = receipt.Rows.Select(row =>
        row.State == RenameItemState.Renamed && row.UndoState != RenameUndoState.Restored ? row.Target : row.Source).ToArray();
    private void Add(string source, string target, string detail)
    {
        var content = new StackPanel { Spacing = 4, MaxWidth = 650 };
        content.Children.Add(Text(Path.GetFileName(source) + " → " + Path.GetFileName(target))); content.Children.Add(Text(detail));
        string full = source + "\n→ " + target + "\n" + detail;
        var row = new ListViewItem { Content = content, Tag = full, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        ToolTipService.SetToolTip(row, full); AutomationProperties.SetName(row, full); _rows.Items.Add(row);
    }
    private void UpdateActions()
    {
        if (_released) return;
        foreach (var input in _inputs) input.IsEnabled = !_busy;
        _confirm.IsEnabled = !_busy && _fresh && _plan?.CanApply == true && _session.Receipt is null;
        _preview.IsEnabled = !_busy && _sources.Length is >= 1 and <= BulkRenameNames.MaximumItems;
        _apply.IsEnabled = _confirm.IsEnabled && _confirm.IsChecked == true;
        _undo.IsEnabled = !_busy && _session.Receipt?.CanUndo == true;
        _cancel.IsEnabled = _busy && _cancellation?.IsCancellationRequested == false;
        _export.IsEnabled = _destination.IsEnabled = _discard.IsEnabled = !_busy && _session.Receipt is not null;
        _clear.IsEnabled = _discard.IsEnabled && _discard.IsChecked == true;
        _copy.IsEnabled = !_busy && _rows.SelectedItem is not null;
    }
    internal void RequestCancel()
    {
        _cancellation?.Cancel();
        if (!_released && _busy) _status.Text = "Stopping after the current filesystem call returns…";
        UpdateActions();
    }
    internal void Release() { _released = true; _cancellation?.Cancel(); }
}
