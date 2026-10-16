// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Windows.Foundation;
using Windows.Graphics;

namespace Nexus.Shell.UI;

// Register real non-client input with WinUI's window input source. No pointer
// press handler, synthetic message, capture, or application-owned move loop.
internal sealed class NativeCaptionRegions : IDisposable
{
    private readonly AppWindow _window;
    private readonly WindowChrome _chrome;
    private readonly FrameworkElement _area;
    private readonly FrameworkElement[] _excluded;
    private readonly Func<bool> _enabled;
    private InputNonClientPointerSource? _source;
    private XamlRoot? _root;
    private ShellRect[] _applied = [];
    private bool _disposed, _hasApplied;
    internal bool UsesSystemFrame { get; private set; }

    internal NativeCaptionRegions(AppWindow window, WindowChrome chrome, FrameworkElement area,
        Func<bool>? enabled = null, params FrameworkElement[] excluded)
    {
        _window = window; _chrome = chrome; _area = area; _excluded = excluded; _enabled = enabled ?? (() => true);
        try { _source = InputNonClientPointerSource.GetForWindowId(window.Id); }
        catch (Exception error) { UseSystemFrame(error); }
        area.Loaded += Loaded; area.Unloaded += Unloaded; area.LayoutUpdated += LayoutUpdated;
        Refresh();
    }

    private void Loaded(object sender, RoutedEventArgs args) => Refresh();
    private void Unloaded(object sender, RoutedEventArgs args)
    { DetachRoot(); try { Apply([]); } catch (Exception error) { UseSystemFrame(error); } }
    private void LayoutUpdated(object? sender, object args) => Refresh();
    private void RootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Refresh();
    internal void Refresh()
    {
        if (_disposed || UsesSystemFrame) return;
        try
        {
            if (!_area.IsLoaded || _area.Visibility != Visibility.Visible || !_enabled()) { Apply([]); return; }
            var root = _area.XamlRoot;
            if (!ReferenceEquals(_root, root)) { DetachRoot(); _root = root; if (_root is not null) _root.Changed += RootChanged; }
            if (root?.Content is not FrameworkElement content) { Apply([]); return; }
            double scale = root.RasterizationScale;
            var caption = Bounds(_area, content, scale, false);
            var client = CaptionRegionLayout.ToPixels(0, 0, content.ActualWidth, content.ActualHeight, scale);
            if (caption is null || client is null) { Apply([]); return; }
            var exclusions = new List<ShellRect>();
            foreach (var control in _excluded)
            {
                if (control.Visibility != Visibility.Visible) continue;
                if (!control.IsLoaded || !ReferenceEquals(control.XamlRoot, root)
                    || Bounds(control, content, scale, true) is not { } bounds) { Apply([]); return; }
                exclusions.Add(bounds);
            }
            // Same inset as WindowChrome.ResizeHit; caption input must not steal its edges.
            int edge = Math.Max(4, (int)Math.Round(7 * scale));
            Apply(CaptionRegionLayout.Build(caption.Value, client.Value, exclusions, edge));
        }
        catch (Exception error) { UseSystemFrame(error); }
    }

    private static ShellRect? Bounds(FrameworkElement element, FrameworkElement root, double scale, bool outward)
    {
        var rect = element.TransformToVisual(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return CaptionRegionLayout.ToPixels(rect.X, rect.Y, rect.Width, rect.Height, scale, outward);
    }
    private void Apply(ShellRect[] regions)
    {
        if (_disposed || _source is null || (_hasApplied && _applied.SequenceEqual(regions))) return;
        if (regions.Length == 0) _source.ClearRegionRects(NonClientRegionKind.Caption);
        else _source.SetRegionRects(NonClientRegionKind.Caption, regions.Select(r => new RectInt32(r.X, r.Y, r.Width, r.Height)).ToArray());
        _applied = regions; _hasApplied = true;
        _chrome.Trace("caption-regions", value: regions.Length);
    }
    private void UseSystemFrame(Exception error)
    {
        if (UsesSystemFrame) return;
        UsesSystemFrame = true;
        try { _source?.ClearRegionRects(NonClientRegionKind.Caption); } catch { }
        _source = null;
        _chrome.UseSystemFrame();
        if (_window.Presenter is OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(true, true);
        Log.Write("Native caption regions unavailable; restored Windows title bar", error);
    }
    private void DetachRoot() { if (_root is not null) _root.Changed -= RootChanged; _root = null; }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _area.Loaded -= Loaded; _area.Unloaded -= Unloaded; _area.LayoutUpdated -= LayoutUpdated; DetachRoot();
        try { _source?.ClearRegionRects(NonClientRegionKind.Caption); } catch { /* The HWND may already be destroyed. */ }
        _source = null; _applied = [];
    }
}
