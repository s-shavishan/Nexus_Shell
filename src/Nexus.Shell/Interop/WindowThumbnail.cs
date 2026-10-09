using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Runtime.InteropServices;

namespace Nexus.Shell.Interop;

// Exactly one relationship, owned by the preview's top-level HWND. DWM updates
// it; Nexus never captures bitmaps, polls screenshots, or writes preview images.
internal sealed class WindowThumbnail : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Properties
    {
        internal uint Flags;
        internal ShellLayerInterop.Rect Destination, Source;
        internal byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] internal bool Visible;
        [MarshalAs(UnmanagedType.Bool)] internal bool ClientOnly;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Size { internal int Width, Height; }
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail, out Size size);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref Properties properties);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    private IntPtr _thumbnail;
    private readonly RunningWindow _source;
    private bool _disposed;
    internal bool IsAttached => !_disposed && _thumbnail != IntPtr.Zero;
    private WindowThumbnail(IntPtr thumbnail, RunningWindow source) { _thumbnail = thumbnail; _source = source; }
    internal static bool CompositionAvailable() => OperatingSystem.IsWindows() && DwmIsCompositionEnabled(out bool enabled) >= 0 && enabled;
    internal static WindowThumbnail? TryCreate(IntPtr destination, RunningWindow source)
    {
        if (!OperatingSystem.IsWindows() || !NativeMethods.OwnsWindow(source) || !NativeMethods.Visible(source.Handle)
            || NativeMethods.IsMinimized(source.Handle) || NativeMethods.IsCloaked(source.Handle) || destination == source.Handle || !CompositionAvailable()) return null;
        var owner = new RunningWindow(destination, "", "nexus", Environment.ProcessId);
        if (!NativeMethods.OwnsWindow(owner) || NativeMethods.GetAncestor(destination, 2) != destination
            || NativeMethods.GetAncestor(source.Handle, 2) != source.Handle) return null; // GA_ROOT: both must be top-level
        if (DwmRegisterThumbnail(destination, source.Handle, out var thumbnail) < 0 || thumbnail == IntPtr.Zero) return null;
        return new(thumbnail, source);
    }
    internal bool Update(ShellRect viewport)
    {
        if (!IsAttached || !NativeMethods.OwnsWindow(_source) || !NativeMethods.Visible(_source.Handle)
            || NativeMethods.IsMinimized(_source.Handle) || NativeMethods.IsCloaked(_source.Handle)
            || DwmQueryThumbnailSourceSize(_thumbnail, out var size) < 0) return false;
        var fit = DockPreviewPolicy.FitSource(viewport, size.Width, size.Height);
        if (fit.Width <= 0 || fit.Height <= 0) return false;
        var properties = new Properties
        {
            Flags = 1 | 4 | 8 | 16, // destination, opacity, visibility, source client area only
            Destination = new() { Left = fit.X, Top = fit.Y, Right = fit.Right, Bottom = fit.Bottom },
            Opacity = 255, Visible = true, ClientOnly = false
        };
        return DwmUpdateThumbnailProperties(_thumbnail, ref properties) >= 0;
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        var thumbnail = _thumbnail; _thumbnail = IntPtr.Zero;
        if (thumbnail != IntPtr.Zero) _ = DwmUnregisterThumbnail(thumbnail);
    }
}
