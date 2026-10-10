using Microsoft.UI.Xaml;

namespace Nexus.Shell.UI;

// Motion stays inside the fixed window frame. It never exposes the
// default white HWND background or delays opening, hiding, focus or input.
internal sealed class SurfaceMotion : IDisposable
{
    private readonly FrameworkElement _content;
    private readonly Func<bool> _enabled;
    private MotionController? _motion;
    private bool _pending, _disposed;
    internal SurfaceMotion(FrameworkElement content, Func<bool> enabled)
    { _content = content is Microsoft.UI.Xaml.Controls.Border { Child: FrameworkElement child } ? child : content; _enabled = enabled; _content.Loaded += Loaded; }
    private void Loaded(object sender, RoutedEventArgs args) { if (_pending) Open(); }
    internal void Open()
    {
        if (_disposed) return; _pending = true;
        if (!_content.IsLoaded) return;
        try
        { _motion ??= new(_content); _motion.SetEnabled(_enabled()); _motion.Enter(_content); _pending = false; }
        catch (Exception ex) { Services.Log.Write("Surface motion unavailable", ex); _pending = false; }
    }
    internal void Refresh() { if (!_disposed) _motion?.SetEnabled(_enabled()); }
    internal void Hide() { _pending = false; _motion?.SetEnabled(false); }
    public void Dispose()
    { if (_disposed) return; _disposed = true; _content.Loaded -= Loaded; _motion?.Dispose(); }
}
