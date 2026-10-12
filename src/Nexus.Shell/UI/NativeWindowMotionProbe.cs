using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Windows.Graphics;

namespace Nexus.Shell.UI;

// Passive native preflight: only this test-owned window changes size. Uses the
// same chrome/transition binding as Files, utility windows and Sections.
internal static class NativeWindowMotionProbe
{
    internal static async Task<string> RunAsync()
    {
        var body = new Grid { Background = new SolidColorBrush(Colors.DarkSlateGray) };
        var frame = new Border { Child = body, Background = new SolidColorBrush(Colors.DarkSlateGray) };
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        frame.Loaded += (_, _) => loaded.TrySetResult();
        var window = new Window { Content = frame };
        WindowChrome? chrome = null; WindowTransition? transition = null, dockTransition = null;
        bool motionEnabled = false;
        int closes = 0, restores = 0;
        try
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var native = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(handle));
            if (native.Presenter is OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(false, false);
            chrome = new(handle, resizable: true, role: "MotionProbe");
            chrome.Restored += () => restores++;
            transition = new(frame, () => motionEnabled); transition.BindRestore(chrome);
            window.Activate(); await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            foreach (var size in new[] { (Width: 640, Height: 440), (Width: 760, Height: 540), (Width: 480, Height: 360) })
            {
                native.Resize(new SizeInt32(size.Width, size.Height)); await NativeProbeLayout.WaitAsync(frame, native);
                if (restores != 0)
                    throw new InvalidOperationException("Programmatic resize emitted a native restore request.");
            }
            transition.Restore();
            if (transition.AnimationSetCreations != 0 || transition.RetainedAnimationSetCount != 0)
                throw new InvalidOperationException("Disabled motion allocated native animation resources.");
            motionEnabled = true;
            for (int i = 0; i < 20; i++) transition.Restore();
            if (transition.AnimationSetCreations != 1 || transition.RetainedAnimationSetCount != 1)
                throw new InvalidOperationException("Repeated window restore rebuilt its native animation templates.");
            // Start a real composition batch, then resize before it completes.
            // The action is a counted fixture callback; no user window is closed.
            var close = transition.CloseAsync(() => closes++);
            if (close.IsCompleted || !transition.IsClosing)
                throw new InvalidOperationException("The native motion probe did not start an active composition close batch.");
            native.Resize(new SizeInt32(700, 500));
            await close.WaitAsync(TimeSpan.FromSeconds(2));
            if (closes != 1 || transition.IsClosing)
                throw new InvalidOperationException("Resizing cancelled or duplicated the pending close action.");
            await transition.CloseAsync(() => closes++).WaitAsync(TimeSpan.FromSeconds(2));
            if (closes != 2 || transition.AnimationSetCreations != 2 || transition.RetainedAnimationSetCount != 2)
                throw new InvalidOperationException("Window close did not reuse its separately retained animation templates.");
            var released = transition;
            transition.Dispose(); transition = null;
            released.Dispose(); released.Restore();
            if (released.RetainedAnimationSetCount != 0 || released.AnimationSetCreations != 2)
                throw new InvalidOperationException("Disposed window motion retained or recreated native animation resources.");

            // Exercise the actual dock transition on the same test-owned visual.
            // Actions only increment counters: no desktop/dock window is touched.
            int hides = 0;
            dockTransition = new(frame, () => true, dock: true);
            for (int i = 0; i < 20; i++) dockTransition.Restore();
            for (int i = 0; i < 5; i++)
            {
                var hide = dockTransition.MinimizeAsync(() => hides++);
                if (hide.IsCompleted || !dockTransition.IsMinimizing)
                    throw new InvalidOperationException("The dock probe did not start an active composition hide batch.");
                dockTransition.Restore();
                await hide.WaitAsync(TimeSpan.FromSeconds(2));
                if (hides != 0) throw new InvalidOperationException("Restore did not cancel a pending dock hide action.");
            }
            for (int i = 0; i < 3; i++) await dockTransition.MinimizeAsync(() => hides++).WaitAsync(TimeSpan.FromSeconds(2));
            if (hides != 3 || dockTransition.AnimationSetCreations != 2 || dockTransition.RetainedAnimationSetCount != 2)
                throw new InvalidOperationException("Repeated dock hides/restores rebuilt resources or lost native actions.");
            var pendingHide = dockTransition.MinimizeAsync(() => hides++);
            dockTransition.Dispose();
            await pendingHide.WaitAsync(TimeSpan.FromSeconds(2));
            if (hides != 3 || dockTransition.RetainedAnimationSetCount != 0)
                throw new InvalidOperationException("Disposal retained dock animations or allowed a late hide action.");
            return "Production window/dock motion: native resize preserves close; 40 restores and repeated hides reuse native animations; cancellation, disabled motion and disposal preserve action ownership";
        }
        finally { dockTransition?.Dispose(); transition?.Dispose(); chrome?.Dispose(); window.Close(); }
    }
}
