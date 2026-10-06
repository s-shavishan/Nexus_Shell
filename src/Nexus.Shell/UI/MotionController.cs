using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI;

// Finite compositor animations. No render loop, layout animation, or completion
// callback can delay input or decide whether a page becomes visible.
public sealed class MotionController : IDisposable
{
    private readonly HashSet<FrameworkElement> _hoverTargets = [];
    private readonly HashSet<FrameworkElement> _entranceTargets = [];
    private readonly ScalarKeyFrameAnimation _fade;
    private readonly Vector3KeyFrameAnimation _enter, _lift, _settle, _grow, _shrink;
    private readonly CubicBezierEasingFunction _easing;
    private bool _enabled, _usable = true;

    public MotionController(FrameworkElement root)
    {
        var compositor = ElementCompositionPreview.GetElementVisual(root).Compositor;
        var easing = _easing = compositor.CreateCubicBezierEasingFunction(new Vector2(.2f, .8f), new Vector2(.2f, 1));
        _fade = compositor.CreateScalarKeyFrameAnimation();
        _fade.Duration = TimeSpan.FromMilliseconds(180);
        _fade.InsertKeyFrame(0, .84f); _fade.InsertKeyFrame(1, 1, easing);
        _enter = compositor.CreateVector3KeyFrameAnimation();
        _enter.Duration = TimeSpan.FromMilliseconds(200);
        _enter.InsertKeyFrame(0, new Vector3(0, 8, 0)); _enter.InsertKeyFrame(1, Vector3.Zero, easing);
        Vector3KeyFrameAnimation Transition(Vector3 end)
        {
            var animation = compositor.CreateVector3KeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(140);
            animation.InsertExpressionKeyFrame(0, "this.StartingValue");
            animation.InsertKeyFrame(1, end, easing);
            return animation;
        }
        _lift = Transition(new Vector3(0, -4, 0)); _settle = Transition(Vector3.Zero);
        _grow = Transition(new Vector3(1.045f)); _shrink = Transition(Vector3.One);
    }

    public void SetEnabled(bool value)
    {
        _enabled = value && _usable;
        if (!_enabled) ResetAll();
    }

    public void Enter(FrameworkElement element)
    {
        try
        {
            _entranceTargets.Add(element);
            Reset(element);
            if (!_enabled) return;
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.StartAnimation("Opacity", _fade);
            visual.StartAnimation("Translation", _enter);
        }
        catch (Exception ex) { Disable(ex); }
    }

    public void AttachHover(FrameworkElement element)
    {
        if (!_hoverTargets.Add(element)) return;
        element.PointerEntered += PointerEntered;
        element.PointerExited += PointerExited;
        element.PointerCanceled += PointerExited;
        element.Unloaded += Unloaded;
    }

    private void PointerEntered(object sender, PointerRoutedEventArgs args) => Hover((FrameworkElement)sender, true);
    private void PointerExited(object sender, PointerRoutedEventArgs args) => Hover((FrameworkElement)sender, false);
    private void Hover(FrameworkElement element, bool over)
    {
        try
        {
            // Keep the input button stationary. Moving its content avoids hover
            // boundary oscillation when the pointer rests on an edge of the tile.
            var target = HoverTarget(element);
            if (!_enabled) { Reset(target); return; }
            ElementCompositionPreview.SetIsTranslationEnabled(target, true);
            var visual = ElementCompositionPreview.GetElementVisual(target);
            visual.CenterPoint = new Vector3((float)target.ActualWidth / 2, (float)target.ActualHeight / 2, 0);
            // Replacing the same property animation samples its current presentation
            // value. Do not reset the baseline between opposing hover transitions.
            visual.StartAnimation("Translation", over ? _lift : _settle);
            visual.StartAnimation("Scale", over ? _grow : _shrink);
        }
        catch (Exception ex) { Disable(ex); }
    }
    private static FrameworkElement HoverTarget(FrameworkElement element) =>
        element is ContentControl { Content: FrameworkElement content } ? content : element;

    private static void Reset(FrameworkElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity"); visual.StopAnimation("Translation"); visual.StopAnimation("Scale");
        visual.Opacity = 1; visual.Scale = Vector3.One;
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
    }
    private void ResetAll()
    {
        foreach (var element in _hoverTargets.Select(HoverTarget).Concat(_entranceTargets))
            try { Reset(element); } catch { /* The element can already be detached. */ }
    }
    private void Unloaded(object sender, RoutedEventArgs args)
    {
        Detach((FrameworkElement)sender);
    }
    private void Detach(FrameworkElement element)
    {
        try { Reset(HoverTarget(element)); } catch { }
        element.PointerEntered -= PointerEntered; element.PointerExited -= PointerExited;
        element.PointerCanceled -= PointerExited; element.Unloaded -= Unloaded;
        _hoverTargets.Remove(element);
    }
    private void Disable(Exception error)
    {
        if (_usable) Log.Write("Motion disabled; native controls remain available", error);
        _usable = false; _enabled = false; ResetAll();
    }
    public void Dispose()
    {
        _enabled = false; ResetAll();
        foreach (var element in _hoverTargets.ToArray()) Detach(element);
        _hoverTargets.Clear(); _entranceTargets.Clear();
        _fade.Dispose(); _enter.Dispose(); _lift.Dispose(); _settle.Dispose(); _grow.Dispose(); _shrink.Dispose();
        _easing.Dispose();
    }
}
