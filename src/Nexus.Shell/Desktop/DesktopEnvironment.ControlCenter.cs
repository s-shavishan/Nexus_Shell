using Microsoft.UI.Xaml;
using Nexus.Runtime;
using Nexus.Shell.Interop;
using System.Runtime.InteropServices;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    private readonly DispatcherTimer _controlTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly HashSet<Guid> _controlAck = [], _controlApplied = [];
    private readonly Queue<Guid> _controlHistory = [];
    private bool _controlVisible, _controlSyncing, _controlFailure;
    private string _controlSection = "Sound";
    private long _controlSequence, _controlActivatedSequence = -1, _controlWindow, _lastControlWindow;
    private int _controlAllowedProcess;
    private Guid _controlIssue;
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AllowSetForegroundWindow(uint processId);
    internal bool IsControlCenterWindow(IntPtr handle) => handle != IntPtr.Zero && handle.ToInt64() == _controlWindow;
    private void StartControlCenterSupervision()
    { _controlTimer.Tick += async (_, _) => await SyncControlCenterAsync(); _controlTimer.Start(); }
    private void HideControlCenter()
    { if (!_controlVisible) return; _controlVisible = false; ++_controlSequence; _ = SyncControlCenterAsync(); }
    private async Task SyncControlCenterAsync()
    {
        if (IsStopping || _controlSyncing || Taskbar is null) return; _controlSyncing = true;
        long sequence = _controlSequence; var ack = _controlAck.ToArray();
        try
        {
            var monitor = ShellLayerInterop.Monitor(Taskbar.Handle).Monitor.Bounds;
            var desired = new PanelDesired(sequence, _controlVisible, _controlSection, new(monitor.X, monitor.Y, monitor.Width, monitor.Height, ShellLayerInterop.Scale(Taskbar.Handle), Taskbar.Handle.ToInt64(), IsManagedDesktop));
            var result = await _core.SyncControlCenterAsync(new(desired, ControlCenterPreferences.From(Session.State), ack), _cancel.Token);
            if (IsStopping) return; _controlFailure = false; foreach (var id in ack) _controlAck.Remove(id);
            _controlTimer.Interval = TimeSpan.FromMilliseconds(result.Status.ProcessId > 0 || _controlVisible ? 250 : 1500);
            if (sequence == _controlSequence) _controlVisible = result.Status.Visible;
            _controlWindow = result.Status.Window;
            if (result.Status.IssueId != Guid.Empty && result.Status.IssueId != _controlIssue)
            { _controlIssue = result.Status.IssueId; Report(result.Status.Issue); }
            if (_controlVisible && result.Status.ProcessId > 0)
            {
                if (_controlAllowedProcess != result.Status.ProcessId) { _controlAllowedProcess = result.Status.ProcessId; _ = AllowSetForegroundWindow((uint)result.Status.ProcessId); }
                if (result.Status.Window != 0 && (sequence != _controlActivatedSequence || _controlWindow != _lastControlWindow))
                {
                    _controlActivatedSequence = sequence; _lastControlWindow = _controlWindow;
                    NativeMethods.Activate(new Nexus.Shell.Models.RunningWindow(new IntPtr(_controlWindow), "Control Center", "Nexus.Shell", result.Status.ProcessId));
                }
            }
            foreach (var action in result.Actions)
            {
                if (_controlApplied.Add(action.Id))
                {
                    try
                    {
                        ControlCenterPreferences.Validate(action);
                        switch (action.Action)
                        {
                            case "preference": ControlCenterPreferences.Set(Session.State, action); SaveState(); break;
                            case "personalize": HideControlCenter(); ShowSections("Personalize"); break;
                            case "lock": HideControlCenter(); LockScreen(); break;
                            case "advanced": HideControlCenter(); OpenAdvancedWindowsSettings(action.Value switch
                                { "Sound" => "ms-settings:sound", "Network" => "ms-settings:network-status", "Bluetooth" => "ms-settings:bluetooth", "Display" => "ms-settings:display", "Power" => "ms-settings:powersleep", _ => "ms-settings:" }); break;
                        }
                    }
                    catch (Exception error) { Report("A Control Center desktop action could not finish", error); }
                    _controlHistory.Enqueue(action.Id); while (_controlHistory.Count > 128) _controlApplied.Remove(_controlHistory.Dequeue());
                }
                _controlAck.Add(action.Id);
            }
        }
        catch (OperationCanceledException) when (IsStopping) { }
        catch (Exception error) { if (!IsStopping && !_controlFailure) { _controlFailure = true; Report("Control Center is reconnecting to Nexus Core; the desktop remains available", error); } }
        finally { _controlSyncing = false; }
    }
}
