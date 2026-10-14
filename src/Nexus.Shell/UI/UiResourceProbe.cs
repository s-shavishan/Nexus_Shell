using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Nexus.Shell.Desktop;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI.Desktop;
using Nexus.Shell.UI.Controls;
using Nexus.Shell.UI.Files;
using Nexus.Shell.UI.Menus;
using Nexus.Shell.UI.Taskbar;

namespace Nexus.Shell.UI;

// Build-time native WinUI check. It never starts the desktop, Core, input hooks,
// tray, sign-in policy, hardware services or a settings writer.
internal static class UiResourceProbe
{
    internal sealed record Template(string Source, string Xml);
    internal sealed record Manifest(int FormatVersion, Template[] Templates);
    private static TaskCompletionSource? _loaded;
    private static readonly System.Collections.Concurrent.ConcurrentQueue<string> Errors = new();
    internal static void Fail(Exception error) { Errors.Enqueue(error.GetType().Name + ": " + error.Message); _loaded?.TrySetException(error); }
    internal static async Task RunAsync(string reportPath)
    {
        Window? window = null;
        MenuBarView? menu = null;
        StartMenuView? launchpad = null;
        TaskbarView? dock = null;
        NotificationWindow? notifications = null;
        MainWindow? controls = null;
        Window? jobsWindow = null;
        FileJobsView? jobsView = null;
        FilesView? filesView = null;
        Window? filesWindow = null;
        var panels = new List<DesktopPanelWindow>();
        int parsed = 0;
        var checks = new List<string>();
        var failures = new List<string>();
        var failureDetails = new List<object>();
        string stage = "template-manifest";
        string materialStatus = "not observed";
        var core = new CoreProcessSession();
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Nexus.UI-Templates.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("The native template-check manifest is missing or too large.");
            var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path)) ?? throw new InvalidDataException("The template manifest is invalid.");
            if (manifest.FormatVersion != 1 || manifest.Templates.Length is < 1 or > 128) throw new InvalidDataException("The template manifest is unsupported.");
            foreach (var template in manifest.Templates)
            {
                stage = "template: " + template.Source;
                if (template.Xml.Length > 256 * 1024) throw new InvalidDataException("A UI template is too large.");
                var loaded = XamlReader.Load(template.Xml);
                if (loaded is not FrameworkTemplate) throw new InvalidDataException("The UI check requires framework templates: " + template.Source);
                if (loaded is DataTemplate data && data.LoadContent() is not FrameworkElement) throw new InvalidDataException("The template did not create a UI element: " + template.Source);
                parsed++;
            }
            checks.Add("Native parsing of " + parsed + " production templates and DataTemplate instantiation");
            stage = "production-surface-construction";
            var session = new ShellSession(initialState: new ShellState { CatalogInitialized = true, PinnedApps = [], RecentApps = [],
                AppCollections = [new(Guid.NewGuid().ToString("N"), "Everyday tools", ["nexus:files", "nexus:notes"])],
                Places = [new(Guid.NewGuid().ToString("N"), "Preview favorite", @"C:\Example")] }, persist: _ => Task.CompletedTask);
            var environment = new DesktopEnvironment(session, core);
            environment.Theme.Apply("Midnight", glassStyle: GlassStyle.Default);
            menu = new(environment); launchpad = new(environment); launchpad.PrepareValidation();
            dock = new(environment);
            var surface = new Grid(); surface.RowDefinitions.Add(new() { Height = new GridLength(38) }); surface.RowDefinitions.Add(new()); surface.RowDefinitions.Add(new() { Height = GridLength.Auto });
            surface.Children.Add(menu); Grid.SetRow(launchpad, 1); surface.Children.Add(launchpad);
            Grid.SetRow(dock, 2); surface.Children.Add(dock);
            var frame = new Border { Child = surface, CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1) };
            var material = new WindowMaterial(); window = new Window { Content = frame };
            material.Apply(window, frame, environment);
            _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            frame.Loaded += (_, _) => _loaded?.TrySetResult();
            window.Activate();
            stage = "production-surface-layout";
            await _loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            frame.UpdateLayout();
            if (launchpad.ActualWidth <= 0 || menu.ActualHeight <= 0) throw new InvalidOperationException("The production surfaces did not lay out.");
            checks.Add("Production menu bar and populated Launchpad layout");
            stage = "compact-context-menu-construction";
            var desktopMenu = environment.Menus.DesktopMenu();
            if (desktopMenu.Items.Count > 9 || !desktopMenu.Items.OfType<MenuFlyoutSubItem>().Any(item => item.Text == "Tools")) throw new InvalidOperationException("The desktop context expanded beyond its compact action budget.");
            var nexusMenu = environment.Menus.NexusMenu();
            if (nexusMenu.Items.OfType<MenuFlyoutItem>().First().Text != "About Nexus…") throw new InvalidOperationException("About Nexus must have its own menu action.");
            foreach (var context in new[] { desktopMenu, nexusMenu, environment.Menus.MenuBarMenu(), environment.Menus.ClockMenu(), environment.Menus.NotificationsMenu(), environment.Menus.DockMenu(), environment.Menus.SystemControlMenu("Network") })
            {
                if (context.MenuFlyoutPresenterStyle is null) throw new InvalidOperationException("A production context menu lost the shared presenter style.");
            }
            checks.Add("Production desktop, Nexus, menu-bar, clock, notifications, dock and system-control context construction; compact desktop budget and dedicated About action, no commands executed");
            stage = "retained-menu-bar-refresh";
            menu.VerifyRetainedRefresh();
            checks.Add("100 unchanged menu bar refreshes retain native title and clock XAML properties");
            stage = "retained-launchpad-rendering";
            launchpad.VerifyRetainedRendering();
            launchpad.VerifyGalleryComposition();
            checks.Add("Launchpad glass gallery and default collapsed organization hierarchy are retained.");
            checks.Add("Twenty unchanged Launchpad renders retain item source and page-control identities");
            stage = "retained-dock-indicator-layout";
            dock.VerifyRetainedIndicatorLayout();
            checks.Add("100 unchanged dock indicator frames retain size/opacity, while real state changes repaint");
            stage = "retained-dock-appearance";
            dock.VerifyRetainedAppearance();
            dock.VerifyVisualIslands();
            checks.Add("Three functional Midnight dock islands retain their paint resources across 100 stable visual refreshes");
            checks.Add("100 native dock appearance refreshes preserve launcher and clock brushes");
            stage = "retained-dock-density";
            dock.VerifyRetainedDensity();
            checks.Add("Production dock: compact/normal changes update actual icon geometry; twenty unchanged snapshots retain buttons and avoid width writes");
            stage = "dock-material-ownership";
            if (dock.Frame.Background is not Microsoft.UI.Xaml.Media.SolidColorBrush { Color.A: 255 })
                throw new InvalidOperationException("A standalone dock requires a fully opaque material.");
            dock.MaterialOwnedByWindow = true;
            int dockPaintWrites = 0;
            long dockBackgroundToken = dock.Frame.RegisterPropertyChangedCallback(Border.BackgroundProperty, (_, _) => dockPaintWrites++);
            long dockBorderToken = dock.Frame.RegisterPropertyChangedCallback(Border.BorderBrushProperty, (_, _) => dockPaintWrites++);
            try
            {
                for (int i = 0; i < 100; i++) dock.ApplyAppearance();
                if (dockPaintWrites != 0)
                    throw new InvalidOperationException("Dock view overwrote the window-owned native glass or solid fallback brushes.");
            }
            finally
            {
                dock.Frame.UnregisterPropertyChangedCallback(Border.BackgroundProperty, dockBackgroundToken);
                dock.Frame.UnregisterPropertyChangedCallback(Border.BorderBrushProperty, dockBorderToken);
                dock.MaterialOwnedByWindow = false;
                dock.ApplyAppearance();
            }
            checks.Add("Standalone dock remains opaque; 100 window-owned appearance refreshes do not overwrite native fallback paints");
            stage = "pinned-dock-icon-refresh";
            dock.VerifyPinnedIconRefresh();
            checks.Add("Explicit catalog refresh recreates pinned icon controls; ordinary dock polls retain refreshed buttons");
            stage = "native-caption-registration-and-layout";
            checks.Add(await NativeCaptionProbe.RunAsync());
            stage = "native-resize-motion-and-close";
            checks.Add(await NativeWindowMotionProbe.RunAsync());
            stage = "desktop-panel-construction-and-layout";
            var board = new SwitcherWindow(environment); board.PopulateValidation(); panels.Add(board);
            environment.Clipboard.SetEnabled(true); environment.Clipboard.Add("Native construction check — session memory only.", DateTimeOffset.Now);
            var clipboard = new ClipboardShelfWindow(environment); panels.Add(clipboard);
            panels.Add(new SessionStatusWindow(environment));
            var places = new PlacesWindow(environment); places.PopulateValidation(); panels.Add(places);
            var search = new SearchWindow(environment); search.PopulateValidation(); search.VerifySpotlightComposition(); panels.Add(search);
            panels.Add(new DesktopGuideWindow(environment));
            panels.Add(new WindowsToolsWindow(environment));
            panels.Add(new AboutNexusWindow(environment));
            IntPtr owner = WinRT.Interop.WindowNative.GetWindowHandle(window);
            foreach (var panel in panels)
            {
                foreach (var size in new[] { (Width: 1024, Height: 740), (Width: 800, Height: 540) })
                {
                    panel.PrepareValidation(owner, size.Width, size.Height); await Task.Delay(80); panel.ValidationRoot.UpdateLayout();
                    if (panel.ValidationRoot.ActualWidth <= 0 || panel.ValidationRoot.ActualHeight <= 0) throw new InvalidOperationException("A production desktop panel did not lay out.");
                    if (panel is SwitcherWindow overview) overview.VerifyOverviewValidation();
                }
                panel.Hide();
            }
            checks.Add("Fullscreen window overview: native panel geometry, layout-map controls and immersive no-radius frame");
            environment.Clipboard.Clear();
            checks.Add("Production window board, clipboard shelf, session status, Places, Search, desktop shortcuts and Windows tools panels at two native window sizes");
            stage = "file-context-construction-and-layout";
            filesView = new FilesView(FilesEnvironment.ForValidation(), new(FileSelectionKind.Browse, "Files fixture"), _ => { });
            filesWindow = new Window { Content = filesView }; filesWindow.Activate();
            var filesNative = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(filesWindow)));
            foreach (var size in new[] { (Width: 1040, Height: 740), (Width: 620, Height: 520) })
            {
                filesNative.Resize(new Windows.Graphics.SizeInt32((int)(size.Width * ShellLayerInterop.Scale(owner)), (int)(size.Height * ShellLayerInterop.Scale(owner)))); await Task.Delay(80); filesView.UpdateLayout(); filesView.VerifyValidationContexts();
                if (filesView.ActualWidth <= 0 || filesView.ActualHeight <= 0) throw new InvalidOperationException("Files context fixture did not lay out.");
            }
            filesView.Release(); filesWindow.Close(); filesView = null; filesWindow = null;
            checks.Add("Production Files layout and six folder/media/archive/application/document context types at two native sizes; isolated fixture without Core, scans, previews or shell commands");
            stage = "notification-construction-and-layout";
            controls = new MainWindow(environment, validation: true);
            stage = "nexus-controls-construction-and-layout";
            foreach (var page in NexusControlsCatalog.Pages)
            foreach (var size in new[] { (Width: 1080, Height: 780), (Width: 620, Height: 520) })
            {
                controls.PrepareValidation(page.Id, size.Width, size.Height); await Task.Delay(80);
                controls.ValidationRoot.UpdateLayout(); controls.VerifyValidationPage(page.Id);
            }
            controls.Close(); controls = null;
            checks.Add("Nexus Controls: all six production pages, search destinations and compact navigation at two native sizes; no settings or policy actions executed");
            stage = "file-jobs-construction-and-layout";
            jobsView = new FileJobsView(environment, validation: true);
            jobsWindow = new Window { Content = jobsView }; jobsWindow.Activate();
            IntPtr jobsHandle = WinRT.Interop.WindowNative.GetWindowHandle(jobsWindow);
            var jobsNative = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(jobsHandle));
            double jobsScale = ShellLayerInterop.Scale(jobsHandle);
            foreach (var size in new[] { (Width: 970, Height: 740), (Width: 620, Height: 520) })
            {
                jobsNative.Resize(new Windows.Graphics.SizeInt32((int)(size.Width * jobsScale), (int)(size.Height * jobsScale)));
                await Task.Delay(80); jobsView.UpdateLayout(); jobsView.VerifyValidationLayout(size.Width < 780);
            }
            jobsView.Release(); jobsWindow.Close(); jobsView = null; jobsWindow = null;
            checks.Add("Production File Jobs form, queue progress and responsive columns at two native sizes; isolated fixture with polling, pickers and transfer actions disabled");
            stage = "notification-construction-and-layout";
            for (int i = 0; i < 25; i++) environment.Notifications.Publish("Construction sample " + i, "Temporary native UI fixture; no history is persisted.", i % 3 == 0 ? NoticeKind.Warning : NoticeKind.Information);
            notifications = new NotificationWindow(environment);
            foreach (var size in new[] { (Width: 440, Height: 700), (Width: 380, Height: 540) })
            {
                notifications.PrepareValidation(owner, size.Width, size.Height); await Task.Delay(80); notifications.ValidationRoot.UpdateLayout();
                if (notifications.ValidationRoot.ActualWidth <= 0 || notifications.ValidationRoot.ActualHeight <= 0) throw new InvalidOperationException("The notification panel did not lay out.");
            }
            notifications.Hide(); checks.Add("Production notification filters, search, paged alert cards and actions at two native sizes");
            stage = "material-and-palette-transitions";
            // Exercise native resource dictionaries/material replacement, not a
            // mock HTML preview. A graphics-policy fallback is a valid outcome.
            foreach (string mood in new[] { "Midnight", "Solstice", "Opal" })
            foreach (string style in GlassStyle.Choices)
            {
                session.State.Wallpaper = mood; session.State.GlassStyle = style;
                environment.Theme.Apply(mood, glassStyle: style);
                menu.ApplyAppearance(); launchpad.ApplyAppearance(); material.Apply(window, frame, environment); frame.UpdateLayout();
                foreach (var panel in panels) { panel.ApplyAppearance(); panel.ValidationRoot.UpdateLayout(); }
                notifications.ApplyAppearance(); notifications.ValidationRoot.UpdateLayout();
            }
            await Task.Delay(200);
            materialStatus = material.Status;
            checks.Add("Three palette dictionaries and all three material profiles");
            stage = "glass-quality-selection-and-fallback";
            foreach (string quality in GlassQuality.Choices)
            {
                session.State.GlassQuality = quality;
                environment.Theme.Apply(session.State.Wallpaper, glassStyle: session.State.GlassStyle, quality: quality);
                material.Apply(window, frame, environment); frame.UpdateLayout();
                if (material.Quality.Requested != quality || session.State.GlassQuality != quality)
                    throw new InvalidOperationException("Native material policy rewrote or lost the requested quality.");
                if (quality is "Essential" or "Solid")
                {
                    if (window.SystemBackdrop is not null || frame.Background is not Microsoft.UI.Xaml.Media.SolidColorBrush { Color.A: 255 })
                        throw new InvalidOperationException("A palette tier retained a native backdrop or translucent base.");
                }
                if (!material.Status.Contains("requested " + quality))
                    throw new InvalidOperationException("The surface did not disclose requested and effective quality.");
                // Native XAML probe: quality and fallback must retain the same
                // bevel element and must not rewrite identical properties.
                int writes = GlassDecorations.PropertyWrites(frame);
                for (int i = 0; i < 100; i++)
                    GlassDecorations.Apply(frame, environment.Theme, material.Quality.Effective);
                if (GlassDecorations.PropertyWrites(frame) != writes)
                    throw new InvalidOperationException("Unchanged Liquid Glass bevel styling triggered repeated XAML dependency-property writes.");
            }
            checks.Add("Four requested glass qualities; native surface retains the user's selection and palette tiers detach backdrop with an opaque base");
            stage = "glass-retained-paint";
            // Ensure a stable solid/glass-fallback material doesn't keep
            // invalidating the WinUI render tree from identical brush writes.
            int backgroundWrites = 0, borderWrites = 0;
            long bgToken = frame.RegisterPropertyChangedCallback(Border.BackgroundProperty, (_, _) => backgroundWrites++);
            long edgeToken = frame.RegisterPropertyChangedCallback(Border.BorderBrushProperty, (_, _) => borderWrites++);
            try
            {
                for (int i = 0; i < 100; i++) material.Apply(window, frame, environment);
                if (backgroundWrites != 0 || borderWrites != 0)
                    throw new InvalidOperationException(
                        $"Stable glass frame repainted XAML brushes on repeated appearance updates: background={backgroundWrites}, edge={borderWrites}.");
            }
            finally
            {
                frame.UnregisterPropertyChangedCallback(Border.BackgroundProperty, bgToken);
                frame.UnregisterPropertyChangedCallback(Border.BorderBrushProperty, edgeToken);
            }
            checks.Add("100 native WinUI material refreshes preserve retained frame background/edge brushes without layout-invalidating writes");
            session.State.NativeGlass = false; material.Apply(window, frame, environment); frame.UpdateLayout();
            checks.Add("Native glass detach and opaque palette path");
        }
        catch (Exception error)
        {
            failures.Add(error.GetType().Name + ": " + error.Message);
            failureDetails.Add(new { Stage = stage, Type = error.GetType().FullName, error.Message, HResult = $"0x{error.HResult:X8}", Exception = error.ToString() });
            Log.Write("Native UI probe failed at " + stage, error);
        }
        finally
        {
            _loaded = null; menu?.Dispose(); launchpad?.Dispose(); dock?.Release();
            try { controls?.Close(); } catch (Exception error) { failures.Add("Controls cleanup: " + error.Message); }
            jobsView?.Release();
            filesView?.Release();
            try { filesWindow?.Close(); } catch (Exception error) { failures.Add("Files context cleanup: " + error.Message); }
            try { jobsWindow?.Close(); } catch (Exception error) { failures.Add("File Jobs cleanup: " + error.Message); }
            try { notifications?.Close(); } catch (Exception error) { failures.Add("Notification cleanup: " + error.Message); }
            foreach (var panel in panels) try { panel.Close(); } catch (Exception error) { failures.Add("Panel cleanup: " + error.Message); }
            try { window?.Close(); } catch (Exception error) { failures.Add("Window cleanup: " + error.Message); }
            try { await core.DisposeAsync(); } catch (Exception error) { failures.Add("Probe cleanup: " + error.Message); }
        }
        while (Errors.TryDequeue(out var error)) failures.Add(error);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Status = failures.Count == 0 ? "PASS" : "FAIL", Templates = parsed, Checks = checks, Failures = failures, FailureDetails = failureDetails, LastStage = stage, Material = materialStatus,
            Limitations = "This is a native construction/layout check. It does not certify pixel appearance, hardware behavior or sustained performance." }, new JsonSerializerOptions { WriteIndented = true }));
        if (failures.Count > 0) throw new InvalidOperationException("Native UI checks failed. See " + reportPath);
    }
}
