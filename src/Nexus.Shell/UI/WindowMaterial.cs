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
    private readonly GlassAttachmentGate<LiquidGlassBackdrop> _attachment = new();
    private ShellTheme? _latestTheme;
    private ShellState? _latestState;
    private string _latestRole = "Frame";
    private string _statusKey = "";
    internal string Status { get; private set; } = "Waiting for the surface.";
    internal GlassQualityDecision Quality { get; private set; } = new(GlassQuality.Default, GlassQuality.Default, "Selected");
    internal event Action? StatusChanged;
    internal void Apply(Window window, Border frame, DesktopEnvironment environment, string role = "Frame")
        => Apply(window, frame, environment.Theme, environment.Session.State, role);
    internal void Apply(Window window, Border frame, ShellTheme theme, ShellState state, string role = "Frame")
    {
        if (_closed) return;
        using var work = UiTrace.Measure("material-apply", role);
        _latestTheme = theme; _latestState = state; _latestRole = role;
        if (!_observedWindow) { _observedWindow = true; window.Closed += (_, _) => { _closed = true; _attachment.Close(); _backdrop = null; StatusChanged = null; }; }
        bool requested = theme.ResolveQuality(state.NativeGlass, state.ReducedEffects).NativeBackdrop;
        if (_requested != requested) { _requested = requested; _failed = false; }
        bool glass = !_failed && requested;
        try
        {
            if (glass && !Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()) { glass = false; _failed = true; }
            GlassPaintRetention.Theme(frame, theme.ElementTheme);
            if (glass != _enabled)
            {
                // Keep a fully painted base while the native target attaches or
                // detaches. Translucent paint is safe only after Active is reported.
                GlassPaintRetention.Background(frame, theme.Material(role, false));
                _backdrop = glass ? new LiquidGlassBackdrop(role, WinRT.Interop.WindowNative.GetWindowHandle(window)) : null;
                _attachment.Attach(_backdrop);
                _backdrop?.SetAppearance(GlassRecipe.For(theme.Palette, role, state.GlassStyle), theme.Palette.IsLight);
                if (_backdrop is { } source) source.Failed += () => GlassUiDispatch.TryPost(
                    action => frame.DispatcherQueue.TryEnqueue(() => action()),
                    () => _attachment.IsCurrent(source),
                    () =>
                    {
                        if (!_attachment.DetachFailed(source)) return;
                        _failed = true; _enabled = false; _backdrop = null;
                        try { window.SystemBackdrop = null; }
                        catch (Exception error) { Log.Write("Glass native target could not detach", error); }
                        // Keep the palette readable even when native detach failed.
                        if (_latestTheme is { } currentTheme && _latestState is { } currentState)
                        {
                            GlassPaintRetention.Background(frame, currentTheme.Material(_latestRole, false));
                            GlassDecorations.Apply(frame, currentTheme, "Solid");
                            UpdateStatus(currentTheme, currentState, _latestRole);
                        }
                    },
                    error => Log.Write("Glass failure callback could not update the solid fallback", error));
                if (_backdrop is { } current)
                {
                    // The queued callback always reads the latest controller
                    // state. Collapse a burst into one safe frame update.
                    var stateUpdates = new GlassStateDispatchGate();
                    current.StateChanged += () => stateUpdates.Request(
                    action => frame.DispatcherQueue.TryEnqueue(() => action()),
                    () => _attachment.IsCurrent(current),
                    () =>
                    {
                        if (_latestTheme is not { } currentTheme || _latestState is not { } currentState) return;
                        GlassPaintRetention.Background(frame, currentTheme.Material(_latestRole, current.State == "Active"));
                        GlassDecorations.Apply(frame, currentTheme, currentTheme.ResolveQuality(currentState.NativeGlass,
                            currentState.ReducedEffects, fallback: current.State is "Fallback" or "HighContrast").Effective);
                        UpdateStatus(currentTheme, currentState, _latestRole);
                    },
                    error => Log.Write("Glass state callback could not update the surface", error));
                }
                window.SystemBackdrop = _backdrop; _enabled = glass;
                UiTrace.Surface(glass ? "backdrop-attached" : "backdrop-detached", role, WinRT.Interop.WindowNative.GetWindowHandle(window), value: _backdrop?.TraceId ?? 0);
            }
            _backdrop?.SetAppearance(GlassRecipe.For(theme.Palette, role, state.GlassStyle), theme.Palette.IsLight);
            GlassPaintRetention.Background(frame, theme.Material(role, glass && _backdrop?.State == "Active"));
            GlassPaintRetention.Edge(frame, theme.Edge);
            GlassDecorations.Apply(frame, theme, theme.ResolveQuality(state.NativeGlass, state.ReducedEffects,
                _failed, _backdrop?.State is "Fallback" or "HighContrast").Effective);
            UpdateStatus(theme, state, role);
        }
        catch (Exception ex)
        {
            _failed = true; _enabled = false; _attachment.Attach(null); _backdrop = null;
            try { GlassPaintRetention.Background(frame, theme.Material(role, false)); window.SystemBackdrop = null; } catch (Exception error) { Log.Write("Window glass fallback cleanup failed", error); }
            Log.Write("Window material unavailable; using the palette fallback", ex);
            try { GlassDecorations.Apply(frame, theme, "Solid"); }
            catch (Exception cleanup) { Log.Write("Window glass decoration fallback failed", cleanup); }
            UpdateStatus(theme, state, role);
        }
    }
    private void UpdateStatus(ShellTheme theme, ShellState state, string role)
    {
        string native = _failed ? "Unavailable" : _backdrop?.State ?? "Disabled";
        Quality = theme.ResolveQuality(state.NativeGlass, state.ReducedEffects, _failed, native is "Fallback" or "HighContrast");
        string key = $"{role}|{theme.Palette.Name}|{state.GlassStyle}|{native}|{Quality}|{theme.HighContrast}|{theme.Transparency}|{state.ReducedEffects}|{state.NativeGlass}";
        if (_statusKey == key) return; _statusKey = key;
        UiTrace.Quality(role, Quality);
        string detail = theme.HighContrast ? "High contrast: Windows system colors."
            : !theme.Transparency ? "Windows transparency is off: palette surfaces."
            : state.ReducedEffects ? "Reduced effects: palette surfaces."
            : !state.NativeGlass ? "Nexus glass is off: palette surfaces."
            : native == "Active" ? $"{state.GlassStyle} glass: Windows reports the acrylic material active."
            : native is "Fallback" or "HighContrast" ? "Windows reports a solid backdrop fallback. Check VM graphics, transparency and power settings."
            : native == "Unavailable" ? "Native acrylic could not attach: using the palette fallback. Toggle Glass surfaces to retry."
            : Quality.Effective is "Essential" or "Solid" ? Quality.Reason == "Selected" ? "Selected palette quality." : Quality.Reason + "."
            : "Glass is preparing for this surface.";
        Status = $"{Quality.Effective} quality · requested {Quality.Requested}. " + detail;
        // LiquidGlassBackdrop emits opt-in ETW state events. Avoid blocking the
        // WinUI thread with a synchronous disk log on normal state changes.
        try { StatusChanged?.Invoke(); }
        catch (Exception error) { Log.Write("Glass status observer could not refresh", error); }
    }
}
