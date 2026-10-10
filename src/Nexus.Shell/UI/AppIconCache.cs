using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Nexus.Shell.UI;

// Bounded, asynchronous Windows thumbnails. Layout always has its shared
// vector first; it never waits for a shell icon during layout. Direct network
// paths and mapped network drives are excluded from automatic icon requests.
internal static class AppIconCache
{
    private static readonly Dictionary<string, Task<ImageSource?>> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim Slots = new(4);
    internal static void RetryFailures()
    {
        foreach (var key in Cache.Where(entry => entry.Value.IsCompletedSuccessfully && entry.Value.Result is null).Select(entry => entry.Key).ToArray()) Cache.Remove(key);
    }
    internal static Task<ImageSource?> GetAsync(AppEntry app)
    {
        if (!AppIconPolicy.IsLocalFile(app.Target)) return Task.FromResult<ImageSource?>(null);
        if (Cache.TryGetValue(app.Target, out var known)) return known;
        if (Cache.Count >= AppIconPolicy.CacheLimit)
        {
            var oldest = Cache.FirstOrDefault(entry => entry.Value.IsCompleted);
            if (oldest.Key is null) return Task.FromResult<ImageSource?>(null);
            Cache.Remove(oldest.Key);
        }
        return Cache[app.Target] = LoadAsync(app.Target);
    }
    private static async Task<ImageSource?> LoadAsync(string path)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        bool entered = false;
        try
        {
            await Slots.WaitAsync(deadline.Token); entered = true;
            bool local = await Task.Run(() => new DriveInfo(Path.GetPathRoot(path)!).DriveType is DriveType.Fixed or DriveType.Removable, deadline.Token).WaitAsync(deadline.Token);
            if (!local) return null;
            var file = await StorageFile.GetFileFromPathAsync(path).AsTask(deadline.Token);
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.ListView, AppIconPolicy.PixelSize).AsTask(deadline.Token);
            if (thumbnail is null || thumbnail.Size is 0 or > AppIconPolicy.MaximumBytes) return null;
            var image = new BitmapImage { DecodePixelWidth = AppIconPolicy.PixelSize, DecodePixelHeight = AppIconPolicy.PixelSize };
            await image.SetSourceAsync(thumbnail).AsTask(deadline.Token); return image;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { return null; }
        finally { if (entered) Slots.Release(); }
    }
    internal static async Task ApplyAsync(Image image, AppEntry app)
    {
        var fallback = image.Source;
        if (await GetAsync(app) is { } source && ReferenceEquals(image.Source, fallback)) image.Source = source;
    }
}
