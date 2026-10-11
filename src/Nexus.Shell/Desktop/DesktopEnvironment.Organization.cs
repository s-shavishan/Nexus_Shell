using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    private PlacesWindow? _places;
    private DesktopGuideWindow? _desktopGuide;
    private readonly FileMutationHistory _desktopEdits = new();
    private bool _editingDesktop;
    private DesktopFileMonitor? _desktopMonitor;
    private bool _startingDesktopMonitor;
    private int _desktopRefreshQueued;
    private readonly Microsoft.UI.Xaml.DispatcherTimer _desktopFilesTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _desktopFilesTimerConnected;
    private async void StartDesktopFileMonitor()
    {
        if (IsStopping || _startingDesktopMonitor) return; _startingDesktopMonitor = true;
        if (!_desktopFilesTimerConnected) { _desktopFilesTimerConnected = true; _desktopFilesTimer.Tick += (_, _) => { _desktopFilesTimer.Stop(); RefreshDesktop(); }; }
        try
        {
            var folders = new[] { System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory), System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonDesktopDirectory) };
            var monitor = await Task.Run(() => new DesktopFileMonitor(folders, QueueDesktopRefresh, error => Log.Write("Desktop folder updates unavailable; F5 remains available", error)));
            if (IsStopping) { monitor.Dispose(); return; }
            _desktopMonitor?.Dispose(); _desktopMonitor = monitor;
        }
        catch (Exception error) { if (!IsStopping) Log.Write("Desktop folder observation could not start", error); }
        finally { _startingDesktopMonitor = false; }
    }
    private void QueueDesktopRefresh()
    {
        if (IsStopping || Interlocked.Exchange(ref _desktopRefreshQueued, 1) != 0) return;
        if (!Desktop.DispatcherQueue.TryEnqueue(() => { Interlocked.Exchange(ref _desktopRefreshQueued, 0); if (!IsStopping && !_desktopFilesTimer.IsEnabled) _desktopFilesTimer.Start(); })) Interlocked.Exchange(ref _desktopRefreshQueued, 0);
    }
    internal bool CanUndoDesktopEdit => !_editingDesktop && _desktopEdits.CanUndo;
    internal string DesktopUndoLabel => _desktopEdits.UndoLabel;
    internal void ShowPlaces()
    {
        if (IsStopping) return; HideMenu();
        try { _places ??= new(this); _places.Show(); } catch (Exception error) { Report("Places could not open", error); }
    }
    internal void PlacesClosed(PlacesWindow window) { if (ReferenceEquals(_places, window)) _places = null; }
    internal void ShowDesktopGuide()
    {
        if (IsStopping) return; HideMenu();
        try { _desktopGuide ??= new(this); _desktopGuide.Show(); } catch (Exception error) { Report("Desktop shortcuts could not open", error); }
    }
    internal void DesktopGuideClosed(DesktopGuideWindow window) { if (ReferenceEquals(_desktopGuide, window)) _desktopGuide = null; }
    internal void SetDesktopOrder(string order) { Session.State.DesktopIconOrder = order; SaveState(); Desktop.Surface.ApplyIconPreferences(); }
    internal void ToggleDesktopIcons() { Session.State.ShowDesktopIcons = !Session.State.ShowDesktopIcons; SaveState(); Desktop.Surface.ApplyIconPreferences(); }
    internal void PinDesktopPlace(DesktopShortcut shortcut)
    {
        try { if (!shortcut.IsFolder) return; DesktopOrganization.AddPlace(Session.State, shortcut.Name[..Math.Min(40, shortcut.Name.Length)], shortcut.Target); SaveState(); ShowPlaces(); }
        catch (Exception error) { Report("The folder could not be pinned", error); }
    }
    internal async void CopyDesktopPath(DesktopShortcut shortcut)
    {
        if (IsStopping || !shortcut.IsFileSystem) return;
        bool copied = await CopyClipboardAsync(shortcut.Target);
        if (!IsStopping) Desktop.Surface.Feedback(copied ? "Path copied." : "The clipboard is busy. Try copying again.");
    }
    internal void RenameDesktopItem(DesktopShortcut shortcut)
    { if (shortcut.IsFileSystem) _ = EditDesktopAsync(shortcut); }
    internal void CreateDesktopFolder() => _ = EditDesktopAsync(null);
    private async Task EditDesktopAsync(DesktopShortcut? shortcut)
    {
        if (IsStopping || _editingDesktop || _sessionDialog) return; _editingDesktop = true; _sessionDialog = true; HideMenu();
        try
        {
            Desktop.Activate();
            var name = new TextBox { Header = shortcut is null ? "Folder name" : "File or folder name, including its extension", Text = shortcut is null ? "New folder" : Path.GetFileName(shortcut.Target), MaxLength = 240, MinWidth = 280 };
            var dialog = new ContentDialog { XamlRoot = Desktop.Surface.XamlRoot, RequestedTheme = Theme.ElementTheme, Title = shortcut is null ? "New desktop folder" : "Rename item", Content = name,
                PrimaryButtonText = shortcut is null ? "Create" : "Rename", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || IsStopping) return;
            string value = name.Text, desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
            string result = await Task.Run(() => shortcut is null ? _desktopEdits.CreateFolder(desktop, value) : _desktopEdits.Rename(shortcut.Target, value));
            if (!IsStopping) { await Desktop.Surface.RefreshAsync(); Desktop.Surface.SelectPath(result); Desktop.Surface.Feedback(_desktopEdits.CanUndo ? "Done. " + _desktopEdits.UndoLabel + " with Ctrl+Z." : "Done. Metadata was unavailable, so this edit cannot be undone here."); }
        }
        catch (Exception error) { if (!IsStopping) Desktop.Surface.Report(error.Message); }
        finally { _editingDesktop = false; _sessionDialog = false; }
    }
    internal async void UndoDesktopEdit()
    {
        if (IsStopping || _editingDesktop || _sessionDialog) return; _editingDesktop = true;
        try { string result = await Task.Run(_desktopEdits.Undo); if (!IsStopping) { await Desktop.Surface.RefreshAsync(); Desktop.Surface.SelectPath(result); Desktop.Surface.Feedback("File edit undone."); } }
        catch (Exception error) { if (!IsStopping) Desktop.Surface.Report(error.Message); }
        finally { _editingDesktop = false; }
    }
}
