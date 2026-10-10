using Nexus.Shell.Models;
using Nexus.Shell.Interop;
using Nexus.Shell.UI.Controls;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    private readonly Dictionary<string, UtilityWindow> _utilities = [];
    internal void LockScreen() { HideMenu(); if (!NativeMethods.LockScreen()) Report("Windows could not lock this session."); }
    internal void ShowUtility(string name)
    {
        if (IsStopping) return; HideMenu();
        if (_utilities.TryGetValue(name, out var existing)) { existing.Restore(); return; }
        try
        {
            UtilityWindow window;
            if (name == "Notes")
            { var view = new NotesView(this); try { window = new(this, name, view, 710, 450, view.ApplyAppearance, view.Release); } catch { view.Release(); throw; } }
            else if (name == "Calculator")
            { var view = new CalculatorView(this); window = new(this, name, view, 330, 425, view.ApplyAppearance, () => { }); }
            else return;
            _utilities.Add(name, window);
            window.Closed += (_, _) => { _utilities.Remove(name); if (!IsStopping) UpdateTaskbar(); };
            UpdateTaskbar();
        }
        catch (Exception ex) { Report("Could not open " + name, ex); }
    }
    private UtilityWindow? UtilityFor(RunningWindow window) => _utilities.Values.FirstOrDefault(w => w.Handle == window.Handle);
    private IEnumerable<RunningWindow> UtilityWindows() => _utilities.Values.Select(w => new RunningWindow(w.Handle, w.UtilityTitle, "nexus", Environment.ProcessId));
    private bool IsOwnAppWindow(IntPtr handle) => _utilities.Values.Any(w => w.Handle == handle) || _files?.Handle == handle || _pickers.Any(w => w.Handle == handle)
        || _sections is not null && WinRT.Interop.WindowNative.GetWindowHandle(_sections) == handle;
}
