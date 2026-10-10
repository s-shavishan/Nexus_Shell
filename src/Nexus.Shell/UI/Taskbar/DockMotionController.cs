using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Nexus.Shell.Services;
using System.Numerics;

namespace Nexus.Shell.UI.Taskbar;

// State motion belongs to the image inside the hover container. The underline
// remains in a fixed layout slot; only its compositor scale/opacity change.
internal sealed class DockMotionController : IDisposable
{
    private readonly HashSet<FrameworkElement> _icons = [];
    private readonly Dictionary<Border, DockPresence> _indicators = [];
    private readonly Dictionary<DockMotionCue, (Vector3KeyFrameAnimation Move, Vector3KeyFrameAnimation Scale)> _cues = [];
    private readonly Dictionary<DockPresence, (Vector3KeyFrameAnimation Scale, ScalarKeyFrameAnimation Opacity)> _lines = [];
    private readonly CubicBezierEasingFunction _ease;
    private bool _enabled, _usable = true, _disposed;

    internal DockMotionController(FrameworkElement root)
    {
        var compositor = ElementCompositionPreview.GetElementVisual(root).Compositor;
        _ease = compositor.CreateCubicBezierEasingFunction(new Vector2(.16f, 1), new Vector2(.3f, 1));
        Add(DockMotionCue.Arrive, -3, new Vector3(.88f, .88f, 1), true);
        Add(DockMotionCue.Launch, -8, new Vector3(1.04f, 1.04f, 1));
        Add(DockMotionCue.Activate, -1, new Vector3(1.025f, 1.025f, 1));
        Add(DockMotionCue.Minimize, 3, new Vector3(.98f, .94f, 1));
        Add(DockMotionCue.Restore, -4, new Vector3(1.025f, 1.025f, 1));
        void Add(DockMotionCue cue, float distance, Vector3 scale, bool entering = false)
        {
            var move = compositor.CreateVector3KeyFrameAnimation(); move.Duration = TimeSpan.FromMilliseconds(DockMotionPolicy.Duration(cue));
            move.InsertExpressionKeyFrame(0, "this.StartingValue");
            move.InsertKeyFrame(.3f, new Vector3(0, distance, 0), _ease);
            move.InsertKeyFrame(1, Vector3.Zero, _ease);
            var grow = compositor.CreateVector3KeyFrameAnimation(); grow.Duration = move.Duration;
            if (entering) grow.InsertKeyFrame(0, scale); else grow.InsertExpressionKeyFrame(0, "this.StartingValue");
            if (!entering) grow.InsertKeyFrame(.3f, scale, _ease);
            grow.InsertKeyFrame(1, Vector3.One, _ease);
            _cues.Add(cue, (move, grow));
        }
        foreach (var presence in Enum.GetValues<DockPresence>())
        {
            var scale = compositor.CreateVector3KeyFrameAnimation(); scale.Duration = TimeSpan.FromMilliseconds(167);
            scale.InsertExpressionKeyFrame(0, "this.StartingValue");
            scale.InsertKeyFrame(1, new Vector3(DockMotionPolicy.IndicatorScale(presence), 1, 1), _ease);
            var opacity = compositor.CreateScalarKeyFrameAnimation(); opacity.Duration = scale.Duration;
            opacity.InsertExpressionKeyFrame(0, "this.StartingValue");
            opacity.InsertKeyFrame(1, DockMotionPolicy.IndicatorOpacity(presence), _ease);
            _lines.Add(presence, (scale, opacity));
        }
    }
    internal void SetEnabled(bool enabled)
    {
        if (_disposed) return; _enabled = enabled && _usable;
        if (!_enabled) ResetAll();
    }
    internal void Play(FrameworkElement icon, DockMotionCue cue)
    {
        if (_disposed) return; _icons.Add(icon);
        if (!_enabled || cue == DockMotionCue.None || !icon.IsLoaded) return;
        try
        {
            ElementCompositionPreview.SetIsTranslationEnabled(icon, true);
            var visual = ElementCompositionPreview.GetElementVisual(icon);
            visual.CenterPoint = new Vector3((float)icon.ActualWidth / 2, (float)icon.ActualHeight / 2, 0);
            var animation = _cues[cue];
            visual.StartAnimation("Translation", animation.Move); visual.StartAnimation("Scale", animation.Scale);
        }
        catch (Exception ex) { Disable(ex); }
    }
    internal void SetIndicator(Border indicator, DockPresence presence, bool animate)
    {
        if (_disposed) return;
        bool changed = !_indicators.TryGetValue(indicator, out var previous) || previous != presence;
        _indicators[indicator] = presence;
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(indicator);
            visual.CenterPoint = new Vector3(11, 1.5f, 0);
            if (_enabled && animate && changed && indicator.IsLoaded)
            { var line = _lines[presence]; visual.StartAnimation("Scale", line.Scale); visual.StartAnimation("Opacity", line.Opacity); }
            else if (changed || !animate || !_enabled) SetLine(indicator, presence);
        }
        catch (Exception ex) { Disable(ex); }
    }
    private static void SetLine(Border indicator, DockPresence presence)
    {
        var visual = ElementCompositionPreview.GetElementVisual(indicator);
        visual.StopAnimation("Scale"); visual.StopAnimation("Opacity");
        visual.CenterPoint = new Vector3(11, 1.5f, 0);
        visual.Scale = new Vector3(DockMotionPolicy.IndicatorScale(presence), 1, 1);
        visual.Opacity = DockMotionPolicy.IndicatorOpacity(presence);
    }
    private static void ResetIcon(FrameworkElement icon)
    {
        var visual = ElementCompositionPreview.GetElementVisual(icon);
        visual.StopAnimation("Scale"); visual.StopAnimation("Translation"); visual.Scale = Vector3.One;
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
    }
    internal void Detach(FrameworkElement icon, Border? indicator = null)
    {
        _icons.Remove(icon); try { ResetIcon(icon); } catch { }
        if (indicator is not null && _indicators.Remove(indicator, out var presence))
            try { SetLine(indicator, presence); } catch { }
    }
    private void ResetAll()
    {
        foreach (var icon in _icons) try { ResetIcon(icon); } catch { }
        foreach (var pair in _indicators) try { SetLine(pair.Key, pair.Value); } catch { }
    }
    private void Disable(Exception ex)
    { if (_usable) Log.Write("Dock state motion unavailable", ex); _usable = _enabled = false; ResetAll(); }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _enabled = false; ResetAll();
        _icons.Clear(); _indicators.Clear();
        foreach (var cue in _cues.Values) { cue.Move.Dispose(); cue.Scale.Dispose(); }
        foreach (var line in _lines.Values) { line.Scale.Dispose(); line.Opacity.Dispose(); }
        _cues.Clear(); _lines.Clear(); _ease.Dispose();
    }
}
