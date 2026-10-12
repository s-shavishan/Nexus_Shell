using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Nexus.Shell.Models;

namespace Nexus.Shell.UI;

// Windows owns material composition and its accessibility/battery fallback.
// Each HWND gets its own backdrop; no cross-window shared controller lifetime.
internal sealed class WindowMaterial
{
    private bool _failed, _enabled;
    private bool _observedWindow, _closed;
    private bool? _requested;
    private LiquidGlassBackdrop? _backdrop;
    private string _statusKey = "";
    internal string Status { get; private set; } = "Waiting for the surface.";
    internal event Action? StatusChanged;
    internal void Apply(Window window, Border frame, DesktopEnvironment environment, string role = "Frame")
        => Apply(window, frame, environment.Theme, environment.Session.State, role);
    internal void Apply(Window window, Border frame, ShellTheme theme, ShellState state, string role = "Frame")
    {
        if (_closed) return;
        using var work = UiTrace.Measure("material-apply", role);
        if (!_observedWindow) { _observedWindow = true; window.Closed += (_, _) => { _closed = true; StatusChanged = null; }; }
        bool requested = state.NativeGlass && !state.ReducedEffects && !theme.HighContrast && theme.Transparency;
        if (_requested != requested) { _requested = requested; _failed = false; }
        bool glass = !_failed && requested;
        GlassDecorations.Apply(frame, theme);
        try
        {
            if (glass && !Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()) { glass = false; _failed = true; }
            frame.RequestedTheme = theme.ElementTheme;
            if (glass != _enabled)
            {
                // Keep a fully painted base while the native target attaches or
                // detaches. Translucent paint is safe only after Active is reported.
                frame.Background = theme.Material(role, false);
                _backdrop = glass ? new LiquidGlassBackdrop(role, WinRT.Interop.WindowNative.GetWindowHandle(window)) : null;
                _backdrop?.SetAppearance(GlassRecipe.For(theme.Palette, role, state.GlassStyle), theme.Palette.IsLight);
                if (_backdrop is { } source) source.Failed += () => frame.DispatcherQueue.TryEnqueue(() =>
                { if (_closed || !ReferenceEquals(source, _backdrop)) return; _failed = true; _enabled = false; try { frame.Background = theme.Material(role, false); window.SystemBackdrop = null; UpdateStatus(theme, state, role); } catch (Exception error) { Log.Write("Glass fallback arrived after window close", error); } });
                if (_backdrop is { } current) current.StateChanged += () => frame.DispatcherQueue.TryEnqueue(() =>
                { if (_closed || !ReferenceEquals(current, _backdrop)) return; frame.Background = theme.Material(role, current.State == "Active"); UpdateStatus(theme, state, role); });
                window.SystemBackdrop = _backdrop; _enabled = glass;
                UiTrace.Surface(glass ? "backdrop-attached" : "backdrop-detached", role, WinRT.Interop.WindowNative.GetWindowHandle(window), value: _backdrop?.TraceId ?? 0);
            }
            _backdrop?.SetAppearance(GlassRecipe.For(theme.Palette, role, state.GlassStyle), theme.Palette.IsLight);
            frame.Background = theme.Material(role, glass && _backdrop?.State == "Active");
            frame.BorderBrush = theme.Edge;
            UpdateStatus(theme, state, role);
        }
        catch (Exception ex)
        {
            _failed = true; _enabled = false;
            try { frame.Background = theme.Material(role, false); window.SystemBackdrop = null; } catch (Exception error) { Log.Write("Window glass fallback cleanup failed", error); }
            Log.Write("Window material unavailable; using the palette fallback", ex);
            UpdateStatus(theme, state, role);
        }
    }
    private void UpdateStatus(ShellTheme theme, ShellState state, string role)
    {
        string native = _failed ? "Unavailable" : _backdrop?.State ?? "Disabled";
        string key = $"{role}|{theme.Palette.Name}|{state.GlassStyle}|{native}|{theme.HighContrast}|{theme.Transparency}|{state.ReducedEffects}|{state.NativeGlass}";
        if (_statusKey == key) return; _statusKey = key;
        Status = theme.HighContrast ? "High contrast: Windows system colors."
            : !theme.Transparency ? "Windows transparency is off: palette surfaces."
            : state.ReducedEffects ? "Reduced effects: palette surfaces."
            : !state.NativeGlass ? "Nexus glass is off: palette surfaces."
            : native == "Active" ? $"{state.GlassStyle} glass: Windows reports the acrylic material active."
            : native is "Fallback" or "HighContrast" ? "Windows reports a solid backdrop fallback. Check VM graphics, transparency and power settings."
            : native == "Unavailable" ? "Native acrylic could not attach: using the palette fallback. Toggle Glass surfaces to retry."
            : "Glass is preparing for this surface.";
        Log.Write($"Glass[{role}] palette={theme.Palette.Name} style={state.GlassStyle} state={native} transparency={theme.Transparency}");
        StatusChanged?.Invoke();
    }
}
