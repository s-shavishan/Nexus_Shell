using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace Nexus.Shell.UI;

// Input stays responsive. A newer restore/maximize/close cancels an older
// minimize before its native command can run; completion has a bounded wait.
internal sealed class WindowTransition(FrameworkElement surface, Func<bool> enabled, bool dock = false) : IDisposable
{
    private int _generation;
    private bool _disposed;
    internal bool IsMinimizing { get; private set; }
    internal bool IsClosing { get; private set; }
    internal Task MinimizeAsync(Action minimize) => ExitAsync(minimize, false);
    internal Task CloseAsync(Action close) => ExitAsync(close, true);
    private async Task ExitAsync(Action action, bool closing)
    {
        if (IsClosing) return;
        int generation = ++_generation;
        if (_disposed) return;
        IsMinimizing = !closing; IsClosing = closing;
        if (!enabled() || !surface.IsLoaded) { IsMinimizing = IsClosing = false; ApplyNative(action); Reset(); return; }
        try
        {
            ElementCompositionPreview.SetIsTranslationEnabled(surface, true);
            var visual = ElementCompositionPreview.GetElementVisual(surface); var compositor = visual.Compositor;
            visual.CenterPoint = new Vector3((float)surface.ActualWidth / 2, (float)surface.ActualHeight * (closing ? .5f : 1), 0);
            using var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(.2f, .75f), new Vector2(.3f, 1));
            using var scale = compositor.CreateVector3KeyFrameAnimation(); scale.Duration = TimeSpan.FromMilliseconds(closing ? 120 : 160); scale.InsertExpressionKeyFrame(0, "this.StartingValue"); scale.InsertKeyFrame(1, dock ? Vector3.One : new Vector3(closing ? .98f : .94f, closing ? .98f : .94f, 1), ease);
            using var move = compositor.CreateVector3KeyFrameAnimation(); move.Duration = scale.Duration; move.InsertExpressionKeyFrame(0, "this.StartingValue"); move.InsertKeyFrame(1, new Vector3(0, closing ? 0 : dock ? 36 : 20, 0), ease);
            using var opacity = compositor.CreateScalarKeyFrameAnimation(); opacity.Duration = scale.Duration; opacity.InsertExpressionKeyFrame(0, "this.StartingValue"); opacity.InsertKeyFrame(1, dock || closing ? 0 : .55f, ease);
            using var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            batch.Completed += (_, _) => completed.TrySetResult();
            visual.StartAnimation("Scale", scale); visual.StartAnimation("Translation", move); visual.StartAnimation("Opacity", opacity); batch.End();
            await completed.Task.WaitAsync(TimeSpan.FromMilliseconds(350));
        }
        catch (Exception error) { Services.Log.Write("Window transition used native fallback", error); }
        if (!_disposed && generation == _generation) { IsMinimizing = IsClosing = false; ApplyNative(action); Reset(); }
    }
    internal void Restore()
    {
        Cancel(); if (_disposed) return;
        if (!enabled() || !surface.IsLoaded) return;
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(surface); var compositor = visual.Compositor;
            visual.CenterPoint = new Vector3((float)surface.ActualWidth / 2, (float)surface.ActualHeight / 2, 0);
            using var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(.16f, 1), new Vector2(.3f, 1));
            using var scale = compositor.CreateVector3KeyFrameAnimation(); scale.Duration = TimeSpan.FromMilliseconds(190); scale.InsertKeyFrame(0, dock ? Vector3.One : new Vector3(.975f, .975f, 1)); scale.InsertKeyFrame(1, Vector3.One, ease);
            using var opacity = compositor.CreateScalarKeyFrameAnimation(); opacity.Duration = scale.Duration; opacity.InsertKeyFrame(0, dock ? 0 : .8f); opacity.InsertKeyFrame(1, 1, ease);
            using var move = compositor.CreateVector3KeyFrameAnimation(); move.Duration = scale.Duration; move.InsertKeyFrame(0, new Vector3(0, dock ? 36 : 0, 0)); move.InsertKeyFrame(1, Vector3.Zero, ease);
            ElementCompositionPreview.SetIsTranslationEnabled(surface, true); visual.StartAnimation("Translation", move);
            visual.StartAnimation("Scale", scale); visual.StartAnimation("Opacity", opacity);
        }
        catch (Exception error) { Services.Log.Write("Window restore motion unavailable", error); Reset(); }
    }
    internal void Cancel() { ++_generation; IsMinimizing = IsClosing = false; if (!_disposed) Reset(); }
    private void Reset()
    {
        try { var visual = ElementCompositionPreview.GetElementVisual(surface); visual.StopAnimation("Scale"); visual.StopAnimation("Translation"); visual.StopAnimation("Opacity"); visual.Scale = Vector3.One; visual.Opacity = 1; visual.Properties.InsertVector3("Translation", Vector3.Zero); }
        catch { }
    }
    private static void ApplyNative(Action action) { try { action(); } catch (Exception error) { Services.Log.Write("Native minimize did not finish", error); } }
    public void Dispose() { _disposed = true; ++_generation; IsMinimizing = IsClosing = false; Reset(); }
}
