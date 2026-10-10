using Microsoft.UI.Xaml;
using Nexus.Runtime;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using System.Diagnostics;

namespace Nexus.Shell.Desktop;

// A desktop component, launched and authenticated by Core. It owns no desktop
// policy, taskbar, settings file, global input hook, or standalone app entry.
internal sealed class ControlCenterEnvironment
{
    internal ShellState State { get; } = new();
    internal ShellTheme Theme { get; } = new();
    internal bool IsStopping { get; private set; }
    internal bool IsManagedDesktop => Snapshot.Desired.Monitor.Managed;
    internal PanelSnapshot Snapshot { get; private set; } = null!;
    internal event Action? Stopped;
    private readonly Process _core;
    private readonly RuntimeClient _client;
    private readonly Guid _toolId;
    private readonly CancellationTokenSource _cancel = new();
    private QuickSettingsWindow? _window;
    private int _failures;
    private long _revision = -1, _sequence = -1;
    private long? _hidePending;
    internal ControlCenterEnvironment(string[] args)
    {
        if (args.Length != 7 || args[0] != "--control-center-worker" || args[1] != "--core-pipe" || args[3] != "--core-pid"
            || !int.TryParse(args[4], out int id) || id <= 0 || args[5] != "--tool-id" || !Guid.TryParseExact(args[6], "N", out _toolId))
            throw new ArgumentException("Start Control Center through the Nexus desktop.");
        _core = Process.GetProcessById(id);
        try
        {
            using var own = Process.GetCurrentProcess(); string prefix = "WhiteDreams.Nexus.Core." + own.SessionId + ".";
            if (_core.HasExited || _core.SessionId != own.SessionId || !string.Equals(_core.MainModule?.FileName,
                Path.Combine(AppContext.BaseDirectory, "Nexus.Core.exe"), StringComparison.OrdinalIgnoreCase)
                || !args[2].StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(args[2][prefix.Length..], "N", out _))
                throw new InvalidOperationException("The panel owner does not belong to this Nexus folder and session.");
            _ = _core.Handle; _client = new(args[2], "controlcenter", id);
        }
        catch { _core.Dispose(); throw; }
    }
    internal async Task StartAsync()
    {
        Apply(await _client.CallAsync<PanelSnapshot>(RuntimeOperations.PanelPulse, new { }, TimeSpan.FromSeconds(3), _cancel.Token));
        _window = new(this);
        Apply(await _client.CallAsync<PanelSnapshot>(RuntimeOperations.PanelReady, new PanelReady(_window.Handle.ToInt64()), TimeSpan.FromSeconds(3), _cancel.Token), force: true);
        _ = WatchAsync(); Log.Write("Control Center worker ready for Core " + _core.Id);
    }
    private void Apply(PanelSnapshot snapshot, bool force = false)
    {
        if (IsStopping || snapshot.Revision < _revision) return;
        if (snapshot.ToolId != _toolId) throw new InvalidDataException("The panel state belongs to another instance.");
        bool changed = Snapshot?.Preferences != snapshot.Preferences; bool moved = Snapshot?.Desired.Monitor != snapshot.Desired.Monitor;
        Snapshot = snapshot; _revision = snapshot.Revision; snapshot.Preferences.Apply(State);
        if (_hidePending is { } hiding && (snapshot.Desired.Sequence != hiding || !snapshot.Desired.Visible)) _hidePending = null;
        changed |= Theme.Apply(State.Wallpaper, State.ReducedEffects);
        if (changed) { _window?.ApplyAppearance(); _window?.RefreshPreferences(); }
        if (_window is null) return;
        if (moved) _window.Position();
        if (force || _sequence != snapshot.Desired.Sequence || _window.IsOpen != snapshot.Desired.Visible)
        {
            _sequence = snapshot.Desired.Sequence;
            if (snapshot.Desired.Visible && _hidePending != snapshot.Desired.Sequence) _window.Show(snapshot.Desired.Section, snapshot.Desired.Compact); else _window.HideSurface();
        }
    }
    private async Task WatchAsync()
    {
        while (!IsStopping)
        {
            try
            {
                if (_core.HasExited) { Stop(); return; }
                var snapshot = await _client.CallAsync<PanelSnapshot>(RuntimeOperations.PanelWait, new PanelWait(_revision), TimeSpan.FromSeconds(3), _cancel.Token);
                if (_hidePending == snapshot.Desired.Sequence && snapshot.Desired.Visible)
                    snapshot = await _client.CallAsync<PanelSnapshot>(RuntimeOperations.PanelHide, new PanelHidden(snapshot.Desired.Sequence), TimeSpan.FromSeconds(3), _cancel.Token);
                if (!IsStopping) { _failures = 0; Apply(snapshot); }
            }
            catch (OperationCanceledException) when (IsStopping) { return; }
            catch (Exception error)
            {
                if (IsStopping) return;
                if (++_failures >= 3) { Log.Write("Control Center lost its Core owner", error); Stop(); return; }
                try { await Task.Delay(500, _cancel.Token); } catch (OperationCanceledException) { return; }
            }
        }
    }
    internal async void Hide()
    {
        if (IsStopping || Snapshot is null) return;
        long sequence = Snapshot.Desired.Sequence; _hidePending = sequence; _window?.HideSurface();
        try { Apply(await _client.CallAsync<PanelSnapshot>(RuntimeOperations.PanelHide, new PanelHidden(sequence), TimeSpan.FromSeconds(3), _cancel.Token)); }
        catch (OperationCanceledException) when (IsStopping) { }
        catch (Exception error) { Log.Write("Panel hide was not acknowledged", error); }
    }
    internal Task<SettingsSnapshot> ExecuteSettingsAsync(SettingsRequest request) => _client.CallAsync<SettingsSnapshot>(RuntimeOperations.Settings, request, TimeSpan.FromSeconds(10), _cancel.Token);
    internal async Task SetPreferenceAsync(DesktopPreference preference, bool? enabled = null, string? value = null)
        => Apply(await _client.CallAsync<PanelSnapshot>(RuntimeOperations.PanelAction, new PanelAction(Guid.NewGuid(), "preference", preference, enabled, value), TimeSpan.FromSeconds(6), _cancel.Token));
    private async void Action(string kind, string? value = null)
    {
        if (IsStopping) return;
        try { Apply(await _client.CallAsync<PanelSnapshot>(RuntimeOperations.PanelAction, new PanelAction(Guid.NewGuid(), kind, Value: value), TimeSpan.FromSeconds(6), _cancel.Token)); }
        catch (OperationCanceledException) when (IsStopping) { }
        catch (Exception error) { _window?.View.ShowError("The desktop action could not finish: " + error.Message); }
    }
    internal void LockScreen() => Action("lock");
    internal void ShowSections(string? page = null) => Action("personalize");
    internal void Advanced(string section) => Action("advanced", section);
    internal void Expand() => Action("expand");
    internal void Stop()
    {
        if (IsStopping) return; IsStopping = true; _cancel.Cancel();
        try { _window?.Close(); } finally { _core.Dispose(); Stopped?.Invoke(); }
    }
}
