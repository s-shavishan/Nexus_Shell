using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Nexus.Shell.UI;

internal static class NativeProbeLayout
{
    // Test-only wait for the observed layout, rather than treating a fixed 80 ms
    // delay as proof that Windows has committed the new client geometry.
    internal static async Task WaitAsync(FrameworkElement root, AppWindow window, CancellationToken token = default)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested(); root.UpdateLayout();
            double scale = root.XamlRoot?.RasterizationScale ?? 0;
            if (root.IsLoaded && scale > 0 && Math.Abs(root.ActualWidth * scale - window.Size.Width) <= 2
                && Math.Abs(root.ActualHeight * scale - window.Size.Height) <= 2) return;
            if (elapsed.Elapsed >= TimeSpan.FromSeconds(5))
                throw new TimeoutException($"Native/XAML probe geometry did not converge: native={window.Size.Width}x{window.Size.Height}, XAML={root.ActualWidth:0.##}x{root.ActualHeight:0.##}, scale={scale:0.##}.");
            await Task.Delay(20, token);
        }
    }
}
