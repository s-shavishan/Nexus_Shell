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
        WindowChrome? chrome = null; WindowTransition? transition = null;
        int closes = 0, restores = 0;
        try
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var native = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(handle));
            if (native.Presenter is OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(false, false);
            chrome = new(handle, resizable: true, role: "MotionProbe");
            chrome.Restored += () => restores++;
            transition = new(frame, () => true); transition.BindRestore(chrome);
            window.Activate(); await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            foreach (var size in new[] { (Width: 640, Height: 440), (Width: 760, Height: 540), (Width: 480, Height: 360) })
            {
                native.Resize(new SizeInt32(size.Width, size.Height)); await NativeProbeLayout.WaitAsync(frame, native);
                if (restores != 0)
                    throw new InvalidOperationException("Programmatic resize emitted a native restore request.");
            }
            // Start a real composition batch, then resize before it completes.
            // The action is a counted fixture callback; no user window is closed.
            var close = transition.CloseAsync(() => closes++);
            if (close.IsCompleted || !transition.IsClosing)
                throw new InvalidOperationException("The native motion probe did not start an active composition close batch.");
            native.Resize(new SizeInt32(700, 500));
            await close.WaitAsync(TimeSpan.FromSeconds(2));
            if (closes != 1 || transition.IsClosing)
                throw new InvalidOperationException("Resizing cancelled or duplicated the pending close action.");
            transition.Dispose(); transition = null;
            return "Production window transition binding: three native resizes emit no restore request; resize during an active composition close preserves one completion";
        }
        finally { transition?.Dispose(); chrome?.Dispose(); window.Close(); }
    }
}
