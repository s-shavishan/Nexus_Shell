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
    private readonly HashSet<FrameworkElement> _overTargets = [], _pressedTargets = [];
    private readonly HashSet<FrameworkElement> _entranceTargets = [];
    private readonly ScalarKeyFrameAnimation _fade;
    private readonly Vector3KeyFrameAnimation _enter, _enterScale, _press, _lift, _settle, _grow, _shrink, _dockGrow, _dockLift;
    private readonly CubicBezierEasingFunction _easing;
    private readonly List<(ScalarKeyFrameAnimation Fade, Vector3KeyFrameAnimation Move, Vector3KeyFrameAnimation Scale)> _stages = [];
    private readonly PointerEventHandler _pressedHandler, _releasedHandler, _canceledHandler;
    private bool _enabled, _usable = true, _disposed;

    public MotionController(FrameworkElement root)
    {
        _pressedHandler = PointerPressed; _releasedHandler = PointerReleased; _canceledHandler = PointerExited;
        var compositor = ElementCompositionPreview.GetElementVisual(root).Compositor;
        var easing = _easing = compositor.CreateCubicBezierEasingFunction(new Vector2(.2f, .8f), new Vector2(.2f, 1));
        _fade = compositor.CreateScalarKeyFrameAnimation();
        _fade.Duration = TimeSpan.FromMilliseconds(110);
        _fade.InsertKeyFrame(0, .92f); _fade.InsertKeyFrame(1, 1, easing);
        _enter = compositor.CreateVector3KeyFrameAnimation();
        _enter.Duration = TimeSpan.FromMilliseconds(140);
        _enter.InsertKeyFrame(0, new Vector3(0, 4, 0));
        _enter.InsertKeyFrame(1, Vector3.Zero, easing);
        _enterScale = compositor.CreateVector3KeyFrameAnimation();
        _enterScale.Duration = TimeSpan.FromMilliseconds(140);
        _enterScale.InsertKeyFrame(0, new Vector3(.995f));
        _enterScale.InsertKeyFrame(1, Vector3.One, easing);
        _stages.Add((_fade, _enter, _enterScale));
        foreach (int delay in new[] { 12, 24 })
        {
            var fade = compositor.CreateScalarKeyFrameAnimation(); fade.Duration = _fade.Duration;
            fade.DelayTime = TimeSpan.FromMilliseconds(delay); fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            fade.InsertKeyFrame(0, .92f); fade.InsertKeyFrame(1, 1, easing);
            var move = compositor.CreateVector3KeyFrameAnimation(); move.Duration = _enter.Duration;
            move.DelayTime = fade.DelayTime; move.DelayBehavior = fade.DelayBehavior;
            move.InsertKeyFrame(0, new Vector3(0, 4, 0)); move.InsertKeyFrame(1, Vector3.Zero, easing);
            var scale = compositor.CreateVector3KeyFrameAnimation(); scale.Duration = _enterScale.Duration;
            scale.DelayTime = fade.DelayTime; scale.DelayBehavior = fade.DelayBehavior;
            scale.InsertKeyFrame(0, new Vector3(.995f)); scale.InsertKeyFrame(1, Vector3.One, easing);
            _stages.Add((fade, move, scale));
        }
        Vector3KeyFrameAnimation Transition(Vector3 end)
        {
            var animation = compositor.CreateVector3KeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(110);
            animation.InsertExpressionKeyFrame(0, "this.StartingValue");
            animation.InsertKeyFrame(1, end, easing);
            return animation;
        }
        _lift = Transition(new Vector3(0, -4, 0)); _settle = Transition(Vector3.Zero);
        _grow = Transition(new Vector3(1.025f)); _shrink = Transition(Vector3.One);
        _dockGrow = Transition(new Vector3(1.09f)); _dockLift = Transition(new Vector3(0, -3, 0));
        _press = Transition(new Vector3(.96f)); _press.Duration = TimeSpan.FromMilliseconds(90);
    }

    public void SetEnabled(bool value)
    {
        if (_disposed) return;
        _enabled = value && _usable;
        if (!_enabled) { _overTargets.Clear(); _pressedTargets.Clear(); ResetAll(); }
    }

    public void Enter(FrameworkElement element) => Enter(element, 0);
    public void EnterStaggered(IEnumerable<FrameworkElement> elements)
    {
        int index = 0;
        foreach (var element in elements.Where(e => e.Visibility == Visibility.Visible).Take(12)) Enter(element, Math.Min(index++, _stages.Count - 1));
    }
    private void Enter(FrameworkElement element, int stage)
    {
        if (_disposed) return;
        try
        {
            if (_entranceTargets.Add(element)) element.Unloaded += EntranceUnloaded;
            Reset(element);
            if (!_enabled) return;
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);
            var animation = _stages[stage];
            visual.StartAnimation("Opacity", animation.Fade);
            visual.StartAnimation("Translation", animation.Move);
            visual.StartAnimation("Scale", animation.Scale);
        }
        catch (Exception ex) { Disable(ex); }
    }

    public void AttachHover(FrameworkElement element)
    {
        if (_disposed) return;
        if (!_hoverTargets.Add(element)) return;
        element.PointerEntered += PointerEntered;
        element.PointerExited += PointerExited;
        // ButtonBase handles press/release before ordinary routed handlers.
        // Observe these events for visual feedback without changing click or capture.
        element.AddHandler(UIElement.PointerCanceledEvent, _canceledHandler, true);
        element.AddHandler(UIElement.PointerPressedEvent, _pressedHandler, true);
        element.AddHandler(UIElement.PointerReleasedEvent, _releasedHandler, true);
        element.AddHandler(UIElement.PointerCaptureLostEvent, _releasedHandler, true);
        element.Unloaded += Unloaded;
    }

    private void PointerEntered(object sender, PointerRoutedEventArgs args)
    { var element = (FrameworkElement)sender; _overTargets.Add(element); Hover(element, true); }
    private void PointerExited(object sender, PointerRoutedEventArgs args)
    { var element = (FrameworkElement)sender; _overTargets.Remove(element); _pressedTargets.Remove(element); Hover(element, false); }
    private void PointerPressed(object sender, PointerRoutedEventArgs args)
    {
        var element = (FrameworkElement)sender;
        if (!args.Pointer.IsInContact || (args.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse &&
            !args.GetCurrentPoint(element).Properties.IsLeftButtonPressed)) return;
        _pressedTargets.Add(element); Hover(element, false);
    }
    private void PointerReleased(object sender, PointerRoutedEventArgs args)
    { var element = (FrameworkElement)sender; _pressedTargets.Remove(element); Hover(element, _overTargets.Contains(element)); }
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
            bool dock = element is Button button && ReferenceEquals(button.Style, Application.Current.Resources["DockButton"]);
            visual.CenterPoint = new Vector3((float)target.ActualWidth / 2, (float)target.ActualHeight * (dock ? .85f : .5f), 0);
            // Replacing the same property animation samples its current presentation
            // value. Do not reset the baseline between opposing hover transitions.
            visual.StartAnimation("Translation", over ? dock ? _dockLift : _lift : _settle);
            visual.StartAnimation("Scale", _pressedTargets.Contains(element) ? _press : over ? dock ? _dockGrow : _grow : _shrink);
        }
        catch (Exception ex) { Disable(ex); }
    }
    private static FrameworkElement HoverTarget(FrameworkElement element) =>
        element is Button { Tag: FrameworkElement iconHost } button && ReferenceEquals(button.Style, Application.Current.Resources["DockButton"])
            ? iconHost : element is ContentControl { Content: FrameworkElement content } ? content : element;

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
    private void EntranceUnloaded(object sender, RoutedEventArgs args)
    {
        var element = (FrameworkElement)sender;
        element.Unloaded -= EntranceUnloaded; _entranceTargets.Remove(element);
        try { Reset(element); } catch { }
    }
    public void DetachHover(FrameworkElement element) => Detach(element);
    private void Detach(FrameworkElement element)
    {
        try { Reset(HoverTarget(element)); } catch { }
        element.PointerEntered -= PointerEntered; element.PointerExited -= PointerExited;
        element.Unloaded -= Unloaded;
        element.RemoveHandler(UIElement.PointerCanceledEvent, _canceledHandler);
        element.RemoveHandler(UIElement.PointerPressedEvent, _pressedHandler);
        element.RemoveHandler(UIElement.PointerReleasedEvent, _releasedHandler);
        element.RemoveHandler(UIElement.PointerCaptureLostEvent, _releasedHandler);
        _hoverTargets.Remove(element);
        _overTargets.Remove(element); _pressedTargets.Remove(element);
    }
    private void Disable(Exception error)
    {
        if (_usable) Log.Write("Motion disabled; native controls remain available", error);
        _usable = false; _enabled = false; ResetAll();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _enabled = false; ResetAll();
        foreach (var element in _hoverTargets.ToArray()) Detach(element);
        foreach (var element in _entranceTargets) element.Unloaded -= EntranceUnloaded;
        _hoverTargets.Clear(); _entranceTargets.Clear();
        _overTargets.Clear(); _pressedTargets.Clear();
        _fade.Dispose(); _enter.Dispose(); _enterScale.Dispose(); _press.Dispose(); _lift.Dispose(); _settle.Dispose(); _grow.Dispose(); _shrink.Dispose();
        _dockGrow.Dispose(); _dockLift.Dispose();
        foreach (var stage in _stages.Skip(1)) { stage.Fade.Dispose(); stage.Move.Dispose(); stage.Scale.Dispose(); } _stages.Clear();
        _easing.Dispose();
    }
}
