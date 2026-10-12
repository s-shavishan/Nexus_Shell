using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Nexus.Shell.Interop;

namespace Nexus.Shell.UI;

// Input stays responsive. Native restore can cancel a pending exit before its
// native command runs; completion has a bounded wait.
internal sealed class WindowTransition(FrameworkElement surface, Func<bool> enabled, bool dock = false) : IDisposable
{
    private int _generation;
    private bool _disposed;
    private WindowChrome? _chrome;
    private AnimationSet? _restoreAnimations, _minimizeAnimations, _closeAnimations;
    private readonly FrameworkElement _content = surface is Border { Child: FrameworkElement child } ? child : surface;
    internal bool IsMinimizing { get; private set; }
    internal bool IsClosing { get; private set; }
    // Native preflight checks actual resource-set creation and ownership.
    internal int AnimationSetCreations { get; private set; }
    internal int RetainedAnimationSetCount => (_restoreAnimations is null ? 0 : 1)
        + (_minimizeAnimations is null ? 0 : 1) + (_closeAnimations is null ? 0 : 1);
    // Bounds changes must not replay restore motion or cancel a pending close.
    internal void BindRestore(WindowChrome chrome)
    {
        if (_disposed || ReferenceEquals(_chrome, chrome)) return;
        if (_chrome is not null) _chrome.Restored -= Restore;
        _chrome = chrome; _chrome.Restored += Restore;
    }
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
            var animations = Animations(compositor, closing);
            using var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            batch.Completed += (_, _) => completed.TrySetResult();
            animations.Start(visual); batch.End();
            await completed.Task.WaitAsync(TimeSpan.FromMilliseconds(220));
        }
        catch (TimeoutException)
        {
            // A WinUI compositor batch can finish rendering without delivering
            // its Completed notification within our bounded UI wait (notably
            // on unattended CI desktops). The animation templates remain valid:
            // release them only for genuine compositor/start failures, not a
            // missed completion event. The native action still runs below.
            if (!_disposed && generation == _generation)
                Services.UiTrace.Surface("motion-batch-timeout", dock ? "Dock" : "Window", IntPtr.Zero);
        }
        catch (Exception error)
        {
            if (!_disposed && generation == _generation) ReleaseAnimations();
            Services.Log.Write("Window transition used native fallback", error);
        }
        if (!_disposed && generation == _generation) { IsMinimizing = IsClosing = false; ApplyNative(action); Reset(); }
    }
    internal void Restore()
    {
        if (!Services.WindowExitPolicy.AcceptRestore(_disposed, IsClosing)) return;
        Cancel();
        if (!enabled() || !_content.IsLoaded) return;
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(_content); var compositor = visual.Compositor;
            visual.CenterPoint = new Vector3((float)_content.ActualWidth / 2, (float)_content.ActualHeight / 2, 0);
            ElementCompositionPreview.SetIsTranslationEnabled(_content, true);
            Animations(compositor, null).Start(visual);
        }
        catch (Exception error) { ReleaseAnimations(); Services.Log.Write("Window restore motion unavailable", error); Reset(); }
    }
    private AnimationSet Animations(Compositor compositor, bool? closing)
    {
        var cached = closing is null ? _restoreAnimations : closing.Value ? _closeAnimations : _minimizeAnimations;
        if (cached is not null) return cached;
        using var work = Services.UiTrace.Measure("motion-resources-create", dock ? "Dock" : "Window");
        var created = new AnimationSet(compositor, dock, closing);
        if (closing is null) _restoreAnimations = created;
        else if (closing.Value) _closeAnimations = created;
        else _minimizeAnimations = created;
        AnimationSetCreations++;
        return created;
    }
    private void ReleaseAnimations()
    {
        var restore = _restoreAnimations; var minimize = _minimizeAnimations; var close = _closeAnimations;
        _restoreAnimations = _minimizeAnimations = _closeAnimations = null;
        restore?.Dispose(); minimize?.Dispose(); close?.Dispose();
    }
    internal void Cancel() { ++_generation; IsMinimizing = IsClosing = false; if (!_disposed) Reset(); }
    private void Reset()
    {
        try { var visual = ElementCompositionPreview.GetElementVisual(_content); visual.StopAnimation("Scale"); visual.StopAnimation("Translation"); visual.StopAnimation("Opacity"); visual.Scale = Vector3.One; visual.Opacity = 1; visual.Properties.InsertVector3("Translation", Vector3.Zero); }
        catch { }
    }
    private static void ApplyNative(Action action) { try { action(); } catch (Exception error) { Services.Log.Write("Native minimize did not finish", error); } }
    public void Dispose()
    {
        if (_disposed) return;
        if (_chrome is not null) _chrome.Restored -= Restore;
        _chrome = null; _disposed = true; ++_generation;
        IsMinimizing = IsClosing = false; Reset(); ReleaseAnimations();
    }

    // Immutable animation templates, retained only by this surface. Batches
    // remain per-operation so cancelled completions cannot run a later action.
    private sealed class AnimationSet : IDisposable
    {
        private CubicBezierEasingFunction? _ease;
        private Vector3KeyFrameAnimation? _scale, _move;
        private ScalarKeyFrameAnimation? _opacity;
        private bool _disposed;
        internal AnimationSet(Compositor compositor, bool dock, bool? closing)
        {
            try
            {
                bool restoring = closing is null;
                _ease = compositor.CreateCubicBezierEasingFunction(restoring ? new Vector2(.16f, 1) : new Vector2(.2f, .75f), new Vector2(.3f, 1));
                _scale = compositor.CreateVector3KeyFrameAnimation();
                _move = compositor.CreateVector3KeyFrameAnimation();
                _opacity = compositor.CreateScalarKeyFrameAnimation();
                _scale.Duration = _move.Duration = _opacity.Duration = TimeSpan.FromMilliseconds(restoring ? 130 : closing == true ? 120 : 160);
                if (restoring)
                {
                    _scale.InsertKeyFrame(0, dock ? Vector3.One : new Vector3(.99f, .99f, 1));
                    _scale.InsertKeyFrame(1, Vector3.One, _ease);
                    _move.InsertKeyFrame(0, new Vector3(0, dock ? 6 : 0, 0)); _move.InsertKeyFrame(1, Vector3.Zero, _ease);
                    _opacity.InsertKeyFrame(0, .92f); _opacity.InsertKeyFrame(1, 1, _ease);
                }
                else
                {
                    _scale.InsertExpressionKeyFrame(0, "this.StartingValue");
                    _scale.InsertKeyFrame(1, dock ? Vector3.One : new Vector3(closing == true ? .98f : .94f, closing == true ? .98f : .94f, 1), _ease);
                    _move.InsertExpressionKeyFrame(0, "this.StartingValue"); _move.InsertKeyFrame(1, new Vector3(0, closing == true ? 0 : dock ? 36 : 20, 0), _ease);
                    _opacity.InsertExpressionKeyFrame(0, "this.StartingValue"); _opacity.InsertKeyFrame(1, .92f, _ease);
                }
            }
            catch { Dispose(); throw; }
        }
        internal void Start(Visual visual)
        {
            visual.StartAnimation("Scale", _scale!);
            visual.StartAnimation("Translation", _move!);
            visual.StartAnimation("Opacity", _opacity!);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            void Release(CompositionObject? resource)
            {
                try { resource?.Dispose(); }
                catch (Exception error) { Services.Log.Write("Retained window motion cleanup failed", error); }
            }
            Release(_scale); Release(_move); Release(_opacity); Release(_ease);
            _scale = _move = null; _opacity = null; _ease = null;
        }
    }
}
