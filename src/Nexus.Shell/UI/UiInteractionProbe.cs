using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Windows.Graphics;

namespace Nexus.Shell.UI;

// Separate opt-in experiment. Never creates DesktopEnvironment/Core, changes
// shell policy, writes preferences or attaches to another application's HWND.
internal static class UiInteractionProbe
{
    private static CancellationTokenSource? _stop;
    private static readonly ConcurrentQueue<string> Errors = new();
    internal static void Fail(Exception error) { Errors.Enqueue(error.GetType().Name + ": " + error.Message); _stop?.Cancel(); }
    private sealed class Moves : EventListener
    {
        internal readonly ConcurrentQueue<(long Window, string Stage, bool LeftDown)> Events = new();
        protected override void OnEventSourceCreated(EventSource source)
        { if (source.Name == "Nexus-Shell-UI") EnableEvents(source, EventLevel.Informational); }
        protected override void OnEventWritten(EventWrittenEventArgs args)
        {
            if (args.EventId == 2 && args.Payload is { Count: 8 })
                Events?.Enqueue(((long)args.Payload[2]!, (string)args.Payload[0]!, (bool)args.Payload[4]!));
        }
    }
    internal static async Task RunAsync(string reportPath, string[] args)
    {
        reportPath = Path.GetFullPath(reportPath);
        if (File.Exists(reportPath)) throw new IOException("Select an unused input-report path; an existing file will not be overwritten.");
        Window? window = null; WindowChrome? chrome = null; NativeCaptionRegions? caption = null;
        var results = new List<InputProbeDriver.Result>(); var failures = new List<string>();
        int requested = 0, buttonClicks = 0, searchClicks = 0, enters = 0, exits = 0;
        double scale = 0;
        using var stop = new CancellationTokenSource(); _stop = stop;
        _ = UiTrace.Source; using var moves = new Moves();
        try
        {
            var options = InputProbeOptions.Parse(args); requested = options.Cycles;
            var root = new Grid { Background = new SolidColorBrush(Colors.DarkSlateGray) };
            root.RowDefinitions.Add(new() { Height = new GridLength(44) }); root.RowDefinitions.Add(new());
            var bar = new Grid(); bar.ColumnDefinitions.Add(new() { Width = new GridLength(90) }); bar.ColumnDefinitions.Add(new());
            var button = new Button { Content = "Control", Margin = new Thickness(8, 6, 0, 6) };
            button.Click += (_, _) => buttonClicks++; bar.Children.Add(button);
            var area = new Border { Background = new SolidColorBrush(Colors.Transparent) }; Grid.SetColumn(area, 1); bar.Children.Add(area);
            var search = new Button { Content = "Search control", Width = 150, Margin = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Right };
            search.Click += (_, _) => searchClicks++; Grid.SetColumn(search, 1); bar.Children.Add(search); root.Children.Add(bar);
            var message = new TextBlock { Text = "Nexus input experiment\nKeep the mouse and keyboard idle.\nClose this window to stop.", Margin = new Thickness(24), FontSize = 18 };
            Grid.SetRow(message, 1); root.Children.Add(message);
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            root.Loaded += (_, _) => loaded.TrySetResult();
            window = new() { Content = root }; window.Closed += (_, _) => stop.Cancel();
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var native = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(handle)); native.Title = "Nexus isolated input experiment";
            if (native.Presenter is OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(false, false);
            chrome = new(handle, resizable: true, role: "InputProbe"); caption = new(native, chrome, area, null, search);
            window.Activate(); await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15), stop.Token);
            var work = ShellLayerInterop.Monitor(handle).Work.Bounds;
            scale = root.XamlRoot.RasterizationScale;
            int width = (int)Math.Round(640 * scale), height = (int)Math.Round(420 * scale);
            if (work.Width < width + 280 || work.Height < height + 180) throw new InvalidOperationException("This monitor lacks space for the isolated drag fixture at its current DPI.");
            var placement = new RectInt32(work.X + (work.Width - width) / 2 - 80, work.Y + (work.Height - height) / 2 - 40, width, height);
            native.MoveAndResize(placement);
            var driver = new InputProbeDriver(handle);
            message.Text += "\nStarting in five seconds…"; await Task.Delay(5000, stop.Token);
            for (int i = 0; i < requested; i++)
            {
                stop.Token.ThrowIfCancellationRequested();
                message.Text = $"Nexus input experiment · {i + 1}/{requested}\nKeep the mouse and keyboard idle.\nClose this window to stop.";
                native.MoveAndResize(placement); await NativeProbeLayout.WaitAsync(root, native, stop.Token); caption.Refresh();
                if (caption.UsesSystemFrame) throw new InvalidOperationException("The production caption adapter entered fallback; this run cannot certify native regions.");
                if (Math.Abs(root.ActualWidth * scale - native.Size.Width) > 2 || Math.Abs(root.ActualHeight * scale - native.Size.Height) > 2)
                    throw new InvalidOperationException("The fixture's native/XAML size has not converged.");
                var region = InputNonClientPointerSource.GetForWindowId(native.Id).GetRegionRects(NonClientRegionKind.Caption)
                    .OrderByDescending(r => (long)r.Width * r.Height).FirstOrDefault();
                if (region.Width < 16 || region.Height < 12) throw new InvalidOperationException("No usable production caption region is registered.");
                int kind = i % 6; bool move = kind is 1 or 2 or 3;
                int localX = region.X + region.Width / 2, localY = region.Y + region.Height / 2;
                if (kind is 4 or 5)
                {
                    var element = kind == 4 ? button : search;
                    var point = element.TransformToVisual(root).TransformPoint(new(element.ActualWidth / 2, element.ActualHeight / 2));
                    localX = (int)Math.Round(point.X * scale); localY = (int)Math.Round(point.Y * scale);
                }
                string scenario = new[] { "caption-click", "held-drag", "diagonal-drag", "escape-cancel", "client-control", "search-control" }[kind];
                int clicksBefore = kind == 4 ? buttonClicks : searchClicks;
                int eventsBefore = moves.Events.Count;
                await InputProbeDriver.WaitBetweenPressesAsync(stop.Token);
                var result = await Task.Run(() => driver.RunAsync(scenario, placement.X + localX, placement.Y + localY,
                    kind == 2 ? 100 : 40, kind == 2 ? 60 : 24, (int)(100 * scale), (int)(120 * scale), move, kind == 3, stop.Token));
                var cycleEvents = moves.Events.ToArray().Skip(eventsBefore).Where(e => e.Window == handle.ToInt64()).ToArray();
                int cycleEnters = cycleEvents.Count(e => e.Stage == "enter"), cycleExits = cycleEvents.Count(e => e.Stage == "exit");
                if (cycleEnters != cycleExits || (move && cycleEnters == 0)) throw new InvalidOperationException("The production chrome did not balance native move-size entry/exit.");
                if (kind != 3 && cycleEvents.Any(e => e.Stage == "exit" && e.LeftDown)) throw new InvalidOperationException("Native move-size exit still reported the primary button held.");
                if (kind is 4 or 5 && (kind == 4 ? buttonClicks : searchClicks) != clicksBefore + 1)
                    throw new InvalidOperationException("Interactive title control did not receive exactly one native click.");
                enters += cycleEnters; exits += cycleExits; results.Add(result);
            }
        }
        catch (Exception error) { failures.Add(error.GetType().Name + ": " + error.Message); }
        finally
        {
            _stop = null; caption?.Dispose(); chrome?.Dispose();
            try { window?.Close(); } catch (Exception error) { failures.Add("Fixture cleanup: " + error.Message); }
        }
        while (Errors.TryDequeue(out var error)) failures.Add(error);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        string json = JsonSerializer.Serialize(new { Status = failures.Count == 0 && results.Count == requested ? "PASS" : "FAIL",
            RequestedCycles = requested, CompletedCycles = results.Count, ButtonClicks = buttonClicks, SearchClicks = searchClicks, MoveEnters = enters, MoveExits = exits,
            Windows = Environment.OSVersion.ToString(), Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), RasterizationScale = scale,
            Results = results, Failures = failures,
            Limitations = "Injected Windows input against an isolated production-caption fixture. Does not certify physical devices, Alt+Tab, snap, multiple DPI monitors, actual Files/Sections controls, rendered black regions or performance." }, new JsonSerializerOptions { WriteIndented = true });
        using (var stream = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream)) writer.Write(json);
        if (failures.Count > 0 || results.Count != requested) throw new InvalidOperationException("Interactive input checks failed. See " + reportPath);
    }
}
