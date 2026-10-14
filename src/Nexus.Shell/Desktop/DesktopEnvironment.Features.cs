using Nexus.Runtime;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed record DesktopStatusRow(string Title, string Detail);
internal sealed partial class DesktopEnvironment
{
    internal ClipboardHistory Clipboard { get; } = new();
    internal event Action? ClipboardChanged;
    private ClipboardCaptureService? _clipboardFeed;
    private ClipboardShelfWindow? _clipboardShelf;
    private SessionStatusWindow? _sessionStatus;
    private RuntimeHealth? _lastCoreHealth;
    private DateTimeOffset _coreHealthAt, _panelHealthAt;
    private PanelStatus? _lastPanelHealth;
    private readonly DateTimeOffset _sessionStarted = DateTimeOffset.Now;
    internal bool ClipboardAvailable => _clipboardFeed?.Available == true;
    internal bool ClipboardSuspended => _clipboardFeed?.Suspended == true;
    internal string ClipboardStatus => _clipboardFeed?.Status ?? "Clipboard notifications are unavailable. Nothing is retained.";
    private void StartFeatureServices()
    {
        try
        {
            _clipboardFeed = new(Desktop.Handle, Clipboard, action => Desktop.DispatcherQueue.TryEnqueue(action.Invoke));
            _clipboardFeed.Changed += ClipboardFeedChanged;
            _clipboardFeed.SessionAvailabilityChanged += SessionAvailabilityChanged;
            RefreshClipboardPreference();
        }
        catch (Exception error) { _clipboardFeed?.Dispose(); _clipboardFeed = null; Clipboard.SetEnabled(false); Log.Write("Clipboard notifications unavailable; capture stays off", error); }
    }
    private void ClipboardFeedChanged()
    {
        if (IsStopping) return;
        if (ClipboardSuspended) { _clipboardShelf?.Hide(); _switcher?.Hide(); _sessionStatus?.Hide(); _search?.Hide(); _places?.Hide(); _desktopGuide?.Hide(); }
        ClipboardChanged?.Invoke();
    }
    private void RefreshClipboardPreference()
    {
        if (_clipboardFeed is null || Clipboard.Enabled == Session.State.ClipboardShelf) return;
        if (!_clipboardFeed.SetEnabled(Session.State.ClipboardShelf) && Session.State.ClipboardShelf)
        { Session.State.ClipboardShelf = false; _dirty = true; _saveTimer.Start(); }
    }
    internal void SetClipboardShelf(bool enabled)
    {
        if (IsStopping) return;
        bool applied = _clipboardFeed?.SetEnabled(enabled) == true;
        Session.State.ClipboardShelf = applied && enabled; SaveState(); ClipboardChanged?.Invoke();
    }
    internal void ClearClipboardShelf() { if (_clipboardFeed is { } feed) feed.Clear(); else Clipboard.Clear(); ClipboardChanged?.Invoke(); }
    internal Task<bool> CopyClipboardAsync(string text)
    {
        var feed = _clipboardFeed;
        return feed is null || IsStopping ? Task.FromResult(false) : Task.Run(() => feed.Copy(text));
    }
    internal void ShowClipboardShelf()
    {
        if (IsStopping) return; HideMenu();
        try { _clipboardShelf ??= new(this); _clipboardShelf.Show(); } catch (Exception error) { Report("The clipboard shelf could not open", error); }
    }
    internal void ShowSessionStatus()
    {
        if (IsStopping) return; HideMenu();
        try { _sessionStatus ??= new(this); _sessionStatus.Show(); } catch (Exception error) { Report("Session status could not open", error); }
    }
    internal void ClipboardShelfClosed(ClipboardShelfWindow window) { if (ReferenceEquals(_clipboardShelf, window)) _clipboardShelf = null; }
    internal void SessionStatusClosed(SessionStatusWindow window) { if (ReferenceEquals(_sessionStatus, window)) _sessionStatus = null; }
    internal void CheckDesktopServices()
    {
        if (IsStopping) return; CheckCore(); _ = SyncControlCenterAsync();
        if (_desktopMonitor is null || _desktopMonitor.HadError) StartDesktopFileMonitor();
        if (_clipboardFeed?.Available != true && !Clipboard.Enabled)
        {
            _clipboardFeed?.Dispose(); _clipboardFeed = null; StartFeatureServices(); ClipboardChanged?.Invoke();
        }
    }
    internal IReadOnlyList<DesktopStatusRow> DesktopServiceStatus()
    {
        string core = _lastCoreHealth is { } health
            ? "Process " + health.CoreProcessId + " · observed " + Math.Max(0, (int)(DateTimeOffset.Now - _coreHealthAt).TotalSeconds) + "s ago · settings revision " + health.Revision + " · " + health.FilesProcesses + " file workers"
            : "Waiting for the first Core health reply.";
        if (_coreFailureReported) core = "Core is reconnecting. Current edits remain in the desktop; automatic recovery has a restart budget.";
        string panel = _lastPanelHealth is { } status ? status.Paused ? "Automatic recovery is paused after repeated stops. It retries within its one-minute budget."
            : status.Recovering ? "Recovering the Control Center worker. The desktop remains available."
            : status.ProcessId > 0 ? "Process " + status.ProcessId + " · " + (status.Visible ? "visible" : "ready in the background") + " · observed " + Math.Max(0, (int)(DateTimeOffset.Now - _panelHealthAt).TotalSeconds) + "s ago"
            : "Idle · starts when a system control is opened." : "Waiting for the supervisor's first status reply.";
        if (_controlFailure) panel = "The desktop is reconnecting to the Control Center supervisor.";
        string save = _saving ? "Saving the current settings through Nexus Core." : _dirty ? "Changes are queued for saving" + (_saveFailures > 0 ? "; a bounded retry is pending." : ".") : "No desktop changes are queued for saving.";
        return [new("Desktop session", Mode + " · active for " + Math.Max(0, (int)(DateTimeOffset.Now - _sessionStarted).TotalMinutes) + " minutes · " + _windows.Count + " app windows"),
            new("Nexus Core", core), new("Control Center", panel), new("Search", SearchServiceStatus), new("Settings persistence", save),
            new("Windows integration", WindowsIntegrationStatus),
            new("Desktop folder updates", _desktopMonitor is null ? "Starting folder observation. F5 always refreshes manually." : _desktopMonitor.HadError ? "Some folder notifications were unavailable. F5 refreshes manually; Check services retries observation." : "Watching " + _desktopMonitor.FolderCount + " desktop folders with coalesced updates."),
            new("Clipboard capture", ClipboardStatus + " · " + Clipboard.Items.Count + " retained text entries"),
            new("Windows foundation", "Windows owns sign-in, app processes, graphics, drivers and system services. Nexus supervises its own desktop services.")];
    }
}
