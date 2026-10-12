using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Windows.Foundation;
using Windows.Graphics;

namespace Nexus.Shell.UI;

// Included in --verify-ui on Windows. Only a probe-owned WinUI window
// is changed. This verifies registration/lifetime, not physical mouse release.
internal static class NativeCaptionProbe
{
    internal static async Task<string> RunAsync()
    {
        var root = new Grid { Background = new SolidColorBrush(Colors.DarkSlateGray) };
        root.RowDefinitions.Add(new() { Height = new GridLength(44) }); root.RowDefinitions.Add(new());
        var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new GridLength(90) }); row.ColumnDefinitions.Add(new());
        var controls = new Button { Content = "Controls", Margin = new Thickness(8, 6, 0, 6) }; row.Children.Add(controls);
        var area = new Border { Background = new SolidColorBrush(Colors.Transparent) }; Grid.SetColumn(area, 1); row.Children.Add(area);
        var search = new Button { Content = "Search input", Width = 180, Margin = new Thickness(20, 8, 20, 8), HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetColumn(search, 1); row.Children.Add(search); root.Children.Add(row);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler onLoaded = (_, _) => loaded.TrySetResult(); root.Loaded += onLoaded;
        var window = new Window { Content = root };
        WindowChrome? chrome = null; NativeCaptionRegions? caption = null;
        bool enabled = true;
        try
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var native = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(handle));
            if (native.Presenter is OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(false, false);
            chrome = new(handle, resizable: true);
            caption = new(native, chrome, area, () => enabled, search);
            window.Activate(); await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var source = InputNonClientPointerSource.GetForWindowId(native.Id);
            foreach (var size in new[] { (Width: 800, Height: 540), (Width: 480, Height: 360) })
            {
                native.Resize(new SizeInt32(size.Width, size.Height)); await Task.Delay(80); root.UpdateLayout(); caption.Refresh();
                if (caption.UsesSystemFrame) throw new InvalidOperationException("The caption API probe used the Windows title-bar fallback.");
                var registered = source.GetRegionRects(NonClientRegionKind.Caption);
                if (registered.Length == 0) throw new InvalidOperationException("No native drag region reached the WinUI input source.");
                double scale = root.XamlRoot.RasterizationScale;
                if (Math.Abs(root.ActualWidth * scale - native.Size.Width) > 2 || Math.Abs(root.ActualHeight * scale - native.Size.Height) > 2)
                    throw new InvalidOperationException("The XAML caption probe has not reached the current native window size.");
                foreach (var button in new[] { controls, search })
                {
                    var bounds = button.TransformToVisual(root).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
                    var excluded = CaptionRegionLayout.ToPixels(bounds.X, bounds.Y, bounds.Width, bounds.Height, scale, true)!.Value;
                    foreach (var r in registered)
                        if (r.X < excluded.Right && (long)r.X + r.Width > excluded.X && r.Y < excluded.Bottom && (long)r.Y + r.Height > excluded.Y)
                            throw new InvalidOperationException("A native drag region overlaps an interactive control.");
                }
                enabled = false; caption.Refresh();
                if (source.GetRegionRects(NonClientRegionKind.Caption).Length != 0) throw new InvalidOperationException("Disabling caption input left a stale region.");
                enabled = true; caption.Refresh();
            }
            caption.Dispose(); caption = null;
            if (source.GetRegionRects(NonClientRegionKind.Caption).Length != 0) throw new InvalidOperationException("Closing the caption owner left native regions attached.");
            return "Production native caption adapter: WinUI registration at two sizes, control exclusion, disable/re-enable and disposal (physical input still requires acceptance)";
        }
        finally { root.Loaded -= onLoaded; caption?.Dispose(); chrome?.Dispose(); window.Close(); }
    }
}
