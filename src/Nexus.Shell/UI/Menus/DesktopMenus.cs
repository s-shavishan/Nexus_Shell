// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Nexus.Shell.Interop;

namespace Nexus.Shell.UI.Menus;

internal sealed class DesktopMenus(DesktopEnvironment environment)
{
    private static MenuFlyoutItem Item(string name, Action action)
        => ShellMenu.Action(name, action);
    internal MenuFlyout DesktopMenu()
    {
        var menu = ShellMenu.Create();
        menu.Items.Add(ShellMenu.Action("New folder…", environment.CreateDesktopFolder, "\uE8F4"));
        var view = ShellMenu.Group("View", "\uE8A9");
        view.Items.Add(ShellMenu.Toggle("Show desktop icons", environment.Session.State.ShowDesktopIcons, environment.ToggleDesktopIcons));
        var sort = ShellMenu.Group("Sort by");
        foreach (string order in new[] { "Name", "Kind", "Newest" }) sort.Items.Add(ShellMenu.Toggle(order, environment.Session.State.DesktopIconOrder == order, () => environment.SetDesktopOrder(order)));
        view.Items.Add(sort); menu.Items.Add(view);
        menu.Items.Add(ShellMenu.Action("Refresh", environment.RefreshDesktop, "\uE72C", "F5"));
        if (environment.CanUndoDesktopEdit) menu.Items.Add(ShellMenu.Action(environment.DesktopUndoLabel, environment.UndoDesktopEdit, "\uE7A7", "Ctrl+Z"));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(ShellMenu.Action("Open desktop folder", () => environment.ShowFiles(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)), "\uED25"));
        var tools = ShellMenu.Group("Tools", "\uE713");
        tools.Items.Add(Item("Search…", environment.ShowSearch)); tools.Items.Add(Item("File jobs…", () => environment.ShowUtility("File jobs")));
        tools.Items.Add(Item("System Monitor…", () => environment.ShowUtility("System Monitor")));
        tools.Items.Add(Item("Places…", environment.ShowPlaces)); tools.Items.Add(Item("Windows tools…", environment.ShowWindowsTools)); tools.Items.Add(Item("Clipboard shelf…", environment.ShowClipboardShelf));
        menu.Items.Add(tools);
        menu.Items.Add(ShellMenu.Action("Personalize…", () => environment.ShowControls("personalize"), "\uE790"));
        return menu;
    }
    internal MenuFlyout NexusMenu()
    {
        var menu = ShellMenu.Create(); menu.Items.Add(ShellMenu.Action("About Nexus…", environment.ShowAbout, "\uE946"));
        menu.Items.Add(ShellMenu.Action("Nexus Controls…", () => environment.ShowControls(), "\uE713"));
        menu.Items.Add(new MenuFlyoutSeparator());
        var tools = ShellMenu.Group("Tools", "\uE8A5"); tools.Items.Add(Item("File jobs…", () => environment.ShowUtility("File jobs"))); tools.Items.Add(Item("Windows tools & keep awake…", environment.ShowWindowsTools)); tools.Items.Add(Item("Session status…", environment.ShowSessionStatus)); tools.Items.Add(Item("Desktop shortcuts…", environment.ShowDesktopGuide)); menu.Items.Add(tools);
        tools.Items.Insert(1, Item("System Monitor…", () => environment.ShowUtility("System Monitor")));
        menu.Items.Add(ShellMenu.Action("Launchpad…", () => environment.ShowMenu(), "\uE80A")); menu.Items.Add(ShellMenu.Action("Search…", environment.ShowSearch, "\uE721"));
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(ShellMenu.Action("Lock screen", environment.LockScreen, "\uE72E"));
        var session = ShellMenu.Group("Session"); session.Items.Add(Item(environment.IsManagedDesktop ? "Return to Windows…" : "Exit Nexus", environment.RequestExit)); session.Items.Add(Item("Startup & recovery…", () => environment.ShowControls("startup"))); menu.Items.Add(session); return menu;
    }
    internal MenuFlyout FileMenu()
    {
        var menu = ShellMenu.Create(); menu.Items.Add(Item("New note", () => environment.ShowUtility("Notes")));
        menu.Items.Add(Item("Places & favorite folders…", environment.ShowPlaces));
        menu.Items.Add(Item("Search files…", environment.ShowSearch));
        menu.Items.Add(Item("Open files…", () => environment.ShowFiles())); menu.Items.Add(Item("Downloads", () => environment.ShowFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))));
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(Item("Calculator", () => environment.ShowUtility("Calculator"))); return menu;
    }
    internal MenuFlyout ViewMenu()
    {
        var menu = ShellMenu.Create(); menu.Items.Add(Item("Launchpad", () => environment.ShowMenu())); menu.Items.Add(Item("Control Center", environment.ShowQuickSettings));
        menu.Items.Add(Item("Windows notifications", () => environment.OpenWindowsSurface(WindowsShellSurface.Notifications)));
        menu.Items.Add(Item("Windows clipboard history", () => environment.OpenWindowsSurface(WindowsShellSurface.Clipboard)));
        var tools = ShellMenu.Group("Nexus tools"); tools.Items.Add(Item("Nexus activity", environment.ShowNotifications)); tools.Items.Add(Item("Clipboard shelf", environment.ShowClipboardShelf)); tools.Items.Add(Item("Session status", environment.ShowSessionStatus)); tools.Items.Add(Item("Places", environment.ShowPlaces)); menu.Items.Add(tools);
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(Item("Desktop appearance…", () => environment.ShowControls("Personalize"))); var focus = ShellMenu.Toggle("Focus desktop", environment.Session.State.FocusMode, () => { environment.Session.State.FocusMode = !environment.Session.State.FocusMode; environment.SaveState(); }); menu.Items.Add(focus); menu.Opening += (_, _) => focus.IsChecked = environment.Session.State.FocusMode; return menu;
    }
    internal MenuFlyout WindowMenu()
    {
        var menu = ShellMenu.Create(); menu.Items.Add(Item("Windows Task View", () => environment.OpenWindowsSurface(WindowsShellSurface.TaskView))); menu.Items.Add(Item("Window board & layouts", environment.ShowWindowOverview)); menu.Items.Add(Item("Show desktop", environment.ShowDesktop));
        menu.Items.Add(Item("Repair maximized window layout", environment.RepairDesktopLayout));
        menu.Items.Add(Item("Nexus Controls", () => environment.ShowControls())); return menu;
    }
    internal MenuFlyout InputLanguageMenu()
    {
        var menu = ShellMenu.Create();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            var target = environment.ActiveWindow;
            try
            {
                var current = environment.CurrentInputLanguage();
                var layouts = WindowsInputLanguage.Installed();
                foreach (var layout in layouts)
                {
                    string suffix = layouts.Count(item => item.Name == layout.Name) > 1 ? " · " + unchecked((uint)layout.Handle.ToInt64()).ToString("X8") : "";
                    var item = new ToggleMenuFlyoutItem { Text = layout.Name + suffix, IsChecked = layout.Handle == current.Handle,
                        IsEnabled = target is not null };
                    item.Click += (_, _) => { if (target is not null) environment.RequestInputLanguage(target, layout.Handle); };
                    menu.Items.Add(item);
                }
                if (layouts.Count == 0) menu.Items.Add(new MenuFlyoutItem { Text = "No input methods available", IsEnabled = false });
                if (target is null) menu.Items.Add(new MenuFlyoutItem { Text = "Focus an app to change its language", IsEnabled = false });
            }
            catch (Exception error) { Log.Write("Input language menu unavailable", error); }
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Item("Language & keyboard settings…", () => environment.RunWindowsTool("language")));
            menu.Items.Add(new MenuFlyoutItem { Text = "Win+Space · Windows input switcher", IsEnabled = false });
        };
        return menu;
    }
    internal MenuFlyout ActiveWindowMenu()
    {
        var menu = ShellMenu.Create();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            if (environment.ActiveWindow is { } window)
            {
                menu.Items.Add(Item("Minimize", () => environment.MinimizeDockWindow(window)));
                menu.Items.Add(Item(environment.ActiveWindowMaximized ? "Restore size" : "Maximize", () => environment.MaximizeDockWindow(window)));
                menu.Items.Add(new MenuFlyoutSeparator());
                var arrange = ShellMenu.Group("Arrange", "\uE9A6"); foreach (string placement in WindowPlacementPlan.Choices) arrange.Items.Add(Item(placement, () => environment.PlaceWindow(window, placement))); menu.Items.Add(arrange);
                var undo = Item("Undo window placement", environment.UndoWindowPlacement); undo.IsEnabled = environment.CanUndoWindowPlacement; menu.Items.Add(undo);
                menu.Items.Add(Item("Show in window overview", environment.ShowWindowOverview));
                menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(Item("Close window", () => environment.CloseDockWindow(window)));
            }
            else
            {
                menu.Items.Add(Item("Launchpad", () => environment.ShowMenu())); menu.Items.Add(Item("Control Center", environment.ShowQuickSettings));
                menu.Items.Add(Item("Refresh desktop", environment.RefreshDesktop)); menu.Items.Add(Item("Personalize", () => environment.ShowControls("Personalize")));
            }
        };
        return menu;
    }
    internal MenuFlyout DesktopItemMenu(DesktopShortcut shortcut)
    {
        var menu = ShellMenu.Create();
        if (!shortcut.IsFileSystem)
        {
            menu.Items.Add(ShellMenu.Action("Open", () => environment.OpenDesktopItem(shortcut), "\uE8A5"));
            return menu;
        }
        var target = shortcut.Target;
        var handlers = new Dictionary<FileContextCommand, Action>
        {
            [FileContextCommand.Open] = () => environment.OpenDesktopItem(shortcut),
            [FileContextCommand.Rename] = () => environment.RenameDesktopItem(shortcut),
            [FileContextCommand.CopyPath] = () => environment.CopyDesktopPath(shortcut),
            [FileContextCommand.Properties] = () => environment.WindowsFileAction(target, "properties"),
            [FileContextCommand.Reveal] = () => environment.ShowFiles(Path.GetDirectoryName(target)),
            [FileContextCommand.WindowsOptions] = () => environment.WindowsFileAction(target)
        };
        if (!shortcut.IsFolder)
            handlers[FileContextCommand.OpenWith] = () => environment.WindowsFileAction(target, "open-with");
        if (shortcut.IsFolder)
            handlers[FileContextCommand.PinFolder] = () => environment.PinDesktopPlace(shortcut);
        TypedFileMenu.Build(menu, [new FileTypeMenuTarget(target, shortcut.IsFolder)], handlers);
        return menu;
    }
    internal MenuFlyout ClockMenu()
    {
        var menu = ShellMenu.Create(); var calendar = environment.WindowsRoute(WindowsShellSurface.Calendar);
        menu.Items.Add(ShellMenu.Action("Open Windows calendar", () => environment.OpenWindowsSurface(WindowsShellSurface.Calendar), "\uE787", calendar.Shortcut));
        menu.Items.Add(Item("Date & time settings…", () => environment.RunWindowsTool("date-time")));
        menu.Items.Add(Item("Windows connection…", () => environment.ShowControls("core", "windows")));
        var clock = ShellMenu.Toggle("24-hour time", environment.Session.State.Clock24Hour, () => { environment.Session.State.Clock24Hour = !environment.Session.State.Clock24Hour; environment.SaveState(); }); menu.Items.Add(clock); menu.Opening += (_, _) => clock.IsChecked = environment.Session.State.Clock24Hour; return menu;
    }
    internal MenuFlyout NotificationsMenu()
    {
        var menu = ShellMenu.Create(); var route = environment.WindowsRoute(WindowsShellSurface.Notifications);
        menu.Items.Add(ShellMenu.Action(route.Name, () => environment.OpenWindowsSurface(WindowsShellSurface.Notifications), "\uE7E7", route.Shortcut));
        menu.Items.Add(Item("Windows notification settings…", () => environment.RunWindowsTool("notifications")));
        menu.Items.Add(Item("Windows connection…", () => environment.ShowControls("core", "windows")));
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(Item("Nexus activity…", environment.ShowNotifications)); return menu;
    }
    internal MenuFlyout MenuBarMenu()
    {
        var menu = ShellMenu.Create(); menu.Items.Add(Item("Nexus Controls…", () => environment.ShowControls())); menu.Items.Add(Item("Windows calendar", () => environment.OpenWindowsSurface(WindowsShellSurface.Calendar))); menu.Items.Add(Item("Windows notifications", () => environment.OpenWindowsSurface(WindowsShellSurface.Notifications)));
        menu.Items.Add(Item("Windows tools…", environment.ShowWindowsTools)); menu.Items.Add(Item("About Nexus…", environment.ShowAbout)); return menu;
    }
    internal MenuFlyout DockMenu()
    {
        var menu = ShellMenu.Create(); var compact = ShellMenu.Toggle("Compact icons", environment.Session.State.CompactDock, () => { environment.Session.State.CompactDock = !environment.Session.State.CompactDock; environment.SaveState(); });
        var previews = ShellMenu.Toggle("Hover previews", environment.Session.State.DockPreviews, () => { environment.Session.State.DockPreviews = !environment.Session.State.DockPreviews; environment.SaveState(); });
        menu.Items.Add(compact); menu.Items.Add(previews); menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Windows Task View", () => environment.OpenWindowsSurface(WindowsShellSurface.TaskView))); menu.Items.Add(Item("Window board & layouts", environment.ShowWindowOverview)); menu.Items.Add(Item("Dock preferences…", () => environment.ShowControls("desktop", "dock")));
        menu.Opening += (_, _) => { compact.IsChecked = environment.Session.State.CompactDock; previews.IsChecked = environment.Session.State.DockPreviews; }; return menu;
    }
    internal MenuFlyout SystemControlMenu(string section)
    {
        var menu = ShellMenu.Create(); menu.Items.Add(Item("Nexus " + section.ToLowerInvariant() + " controls", () => environment.ShowControlCenter(section)));
        menu.Items.Add(Item("Windows quick settings", () => environment.OpenWindowsSurface(WindowsShellSurface.QuickSettings)));
        var uri = section switch { "Sound" => "ms-settings:sound", "Network" => "ms-settings:network-status", "Bluetooth" => "ms-settings:bluetooth", "Display" => "ms-settings:display", _ => "ms-settings:" };
        menu.Items.Add(Item("Advanced Windows settings…", async () => { try { await AppCatalog.OpenSettingsAsync(uri); } catch (Exception error) { environment.Report("Windows settings could not open", error); } })); return menu;
    }
}
