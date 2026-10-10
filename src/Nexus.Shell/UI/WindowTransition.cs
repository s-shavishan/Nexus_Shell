using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace Nexus.Shell.UI;

// Input stays responsive. A newer restore/maximize/close cancels an older
// minimize before its native command can run; completion has a bounded wait.
internal sealed class WindowTransition(FrameworkElement surface, Func<bool> enabled, bool dock = false) : IDisposable
{
    private int _generation;
    private bool _disposed;
    private readonly FrameworkElement _content = surface is Border { Child: FrameworkElement child } ? child : surface;
    internal bool IsMinimizing { get; private set; }
    internal bool IsClosing { get; private set; }
    internal Task MinimizeAsync(Action minimize)
    {
        if (dock) return ExitAsync(minimize, false);
        if (_disposed || IsClosing) return Task.CompletedTask;
        Cancel(); ApplyNative(minimize); return Task.CompletedTask;
    }
    internal Task CloseAsync(Action close) => ExitAsync(close, true);
    private async Task ExitAsync(Action action, bool closing)
    {
        if (IsClosing) return;
        int generation = ++_generation;
        if (_disposed) return;
        IsMinimizing = !closing; IsClosing = closing;
        if (!enabled() || !_content.IsLoaded) { IsMinimizing = IsClosing = false; ApplyNative(action); Reset(); return; }
        try
        {
            ElementCompositionPreview.SetIsTranslationEnabled(_content, true);
            var visual = ElementCompositionPreview.GetElementVisual(_content); var compositor = visual.Compositor;
            visual.CenterPoint = new Vector3((float)_content.ActualWidth / 2, (float)_content.ActualHeight * (closing ? .5f : 1), 0);
            using var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(.2f, .75f), new Vector2(.3f, 1));
            using var scale = compositor.CreateVector3KeyFrameAnimation(); scale.Duration = TimeSpan.FromMilliseconds(closing ? 120 : 160); scale.InsertExpressionKeyFrame(0, "this.StartingValue"); scale.InsertKeyFrame(1, dock ? Vector3.One : new Vector3(closing ? .98f : .94f, closing ? .98f : .94f, 1), ease);
            using var move = compositor.CreateVector3KeyFrameAnimation(); move.Duration = scale.Duration; move.InsertExpressionKeyFrame(0, "this.StartingValue"); move.InsertKeyFrame(1, new Vector3(0, closing ? 0 : dock ? 36 : 20, 0), ease);
            using var opacity = compositor.CreateScalarKeyFrameAnimation(); opacity.Duration = scale.Duration; opacity.InsertExpressionKeyFrame(0, "this.StartingValue"); opacity.InsertKeyFrame(1, .92f, ease);
            using var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            batch.Completed += (_, _) => completed.TrySetResult();
            visual.StartAnimation("Scale", scale); visual.StartAnimation("Translation", move); visual.StartAnimation("Opacity", opacity); batch.End();
            await completed.Task.WaitAsync(TimeSpan.FromMilliseconds(220));
        }
        catch (Exception error) { Services.Log.Write("Window transition used native fallback", error); }
        if (!_disposed && generation == _generation) { IsMinimizing = IsClosing = false; ApplyNative(action); Reset(); }
    }
    internal void Restore()
    {
        Cancel(); if (_disposed) return;
        if (!enabled() || !_content.IsLoaded) return;
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(_content); var compositor = visual.Compositor;
            visual.CenterPoint = new Vector3((float)_content.ActualWidth / 2, (float)_content.ActualHeight / 2, 0);
            using var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(.16f, 1), new Vector2(.3f, 1));
            using var scale = compositor.CreateVector3KeyFrameAnimation(); scale.Duration = TimeSpan.FromMilliseconds(130); scale.InsertKeyFrame(0, dock ? Vector3.One : new Vector3(.99f, .99f, 1)); scale.InsertKeyFrame(1, Vector3.One, ease);
            using var opacity = compositor.CreateScalarKeyFrameAnimation(); opacity.Duration = scale.Duration; opacity.InsertKeyFrame(0, .92f); opacity.InsertKeyFrame(1, 1, ease);
            using var move = compositor.CreateVector3KeyFrameAnimation(); move.Duration = scale.Duration; move.InsertKeyFrame(0, new Vector3(0, dock ? 6 : 0, 0)); move.InsertKeyFrame(1, Vector3.Zero, ease);
            ElementCompositionPreview.SetIsTranslationEnabled(_content, true); visual.StartAnimation("Translation", move);
            visual.StartAnimation("Scale", scale); visual.StartAnimation("Opacity", opacity);
        }
        catch (Exception error) { Services.Log.Write("Window restore motion unavailable", error); Reset(); }
    }
    internal void Cancel() { ++_generation; IsMinimizing = IsClosing = false; if (!_disposed) Reset(); }
    private void Reset()
    {
        try { var visual = ElementCompositionPreview.GetElementVisual(_content); visual.StopAnimation("Scale"); visual.StopAnimation("Translation"); visual.StopAnimation("Opacity"); visual.Scale = Vector3.One; visual.Opacity = 1; visual.Properties.InsertVector3("Translation", Vector3.Zero); }
        catch { }
    }
    private static void ApplyNative(Action action) { try { action(); } catch (Exception error) { Services.Log.Write("Native minimize did not finish", error); } }
    public void Dispose() { _disposed = true; ++_generation; IsMinimizing = IsClosing = false; Reset(); }
}
