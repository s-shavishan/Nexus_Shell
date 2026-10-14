using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Nexus.Shell.Interop;

namespace Nexus.Shell.UI.Menus;

internal sealed class DesktopMenus(DesktopEnvironment environment)
{
    private static MenuFlyoutItem Item(string name, Action action)
    { var item = new MenuFlyoutItem { Text = name }; item.Click += (_, _) => action(); return item; }
    internal MenuFlyout DesktopMenu()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(Item("Refresh", environment.RefreshDesktop));
        menu.Items.Add(Item("New folder…", environment.CreateDesktopFolder));
        var undo = Item(environment.DesktopUndoLabel, environment.UndoDesktopEdit); undo.IsEnabled = environment.CanUndoDesktopEdit; menu.Items.Add(undo);
        var sort = new MenuFlyoutSubItem { Text = "Sort desktop icons" };
        foreach (string order in new[] { "Name", "Kind", "Newest" }) { var choice = new ToggleMenuFlyoutItem { Text = order, IsChecked = environment.Session.State.DesktopIconOrder == order }; choice.Click += (_, _) => environment.SetDesktopOrder(order); sort.Items.Add(choice); } menu.Items.Add(sort);
        var icons = new ToggleMenuFlyoutItem { Text = "Show desktop icons", IsChecked = environment.Session.State.ShowDesktopIcons }; icons.Click += (_, _) => environment.ToggleDesktopIcons(); menu.Items.Add(icons);
        menu.Items.Add(Item("Nexus Controls…", () => environment.ShowControls()));
        menu.Items.Add(Item("Control Center", environment.ShowQuickSettings));
        menu.Items.Add(Item("Windows tools", environment.ShowWindowsTools));
        menu.Items.Add(Item("Launchpad", () => environment.ShowMenu()));
        menu.Items.Add(Item("Notifications", environment.ShowNotifications));
        menu.Items.Add(Item("Nexus Search", environment.ShowSearch));
        menu.Items.Add(Item("Clipboard shelf", environment.ShowClipboardShelf));
        menu.Items.Add(Item("Places", environment.ShowPlaces));
        menu.Items.Add(Item("Desktop shortcuts", environment.ShowDesktopGuide));
        menu.Items.Add(Item("Window board", environment.ShowWindowOverview));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Open desktop folder", () => environment.OpenTarget(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory))));
        menu.Items.Add(Item("Personalize", () => environment.ShowControls("Personalize")));
        return menu;
    }
    internal MenuFlyout NexusMenu()
    {
        var menu = new MenuFlyout(); menu.Items.Add(Item("About this session", () => environment.ShowControls("core")));
        menu.Items.Add(Item("Nexus Controls…", () => environment.OpenTarget("nexus:controls")));
        menu.Items.Add(Item("Session status…", environment.ShowSessionStatus));
        menu.Items.Add(Item("Windows tools & keep awake…", environment.ShowWindowsTools));
        menu.Items.Add(Item("Search…", environment.ShowSearch));
        menu.Items.Add(Item("Desktop shortcuts…", environment.ShowDesktopGuide));
        menu.Items.Add(Item("Launchpad…", () => environment.ShowMenu())); menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Refresh desktop", environment.RefreshDesktop)); menu.Items.Add(Item("Lock screen", environment.LockScreen));
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(Item(environment.IsManagedDesktop ? "Return to Windows…" : "Exit Nexus", environment.RequestExit)); return menu;
    }
    internal MenuFlyout FileMenu()
    {
        var menu = new MenuFlyout(); menu.Items.Add(Item("New note", () => environment.ShowUtility("Notes")));
        menu.Items.Add(Item("Places & favorite folders…", environment.ShowPlaces));
        menu.Items.Add(Item("Search files…", environment.ShowSearch));
        menu.Items.Add(Item("Open files…", () => environment.ShowFiles())); menu.Items.Add(Item("Downloads", () => environment.ShowFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))));
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(Item("Calculator", () => environment.ShowUtility("Calculator"))); return menu;
    }
    internal MenuFlyout ViewMenu()
    {
        var menu = new MenuFlyout(); menu.Items.Add(Item("Launchpad", () => environment.ShowMenu())); menu.Items.Add(Item("Control Center", environment.ShowQuickSettings)); menu.Items.Add(Item("Notifications", environment.ShowNotifications));
        menu.Items.Add(Item("Clipboard shelf", environment.ShowClipboardShelf)); menu.Items.Add(Item("Session status", environment.ShowSessionStatus));
        menu.Items.Add(Item("Places", environment.ShowPlaces)); menu.Items.Add(Item("Desktop shortcuts", environment.ShowDesktopGuide));
        menu.Items.Add(Item("Windows tools", environment.ShowWindowsTools));
        menu.Items.Add(Item("Desktop appearance…", () => environment.ShowControls("Personalize"))); menu.Items.Add(Item("Focus desktop", () => { environment.Session.State.FocusMode = !environment.Session.State.FocusMode; environment.SaveState(); })); return menu;
    }
    internal MenuFlyout WindowMenu()
    {
        var menu = new MenuFlyout(); menu.Items.Add(Item("Window board & layouts", environment.ShowWindowOverview)); menu.Items.Add(Item("Show desktop", environment.ShowDesktop));
        menu.Items.Add(Item("Repair maximized window layout", environment.RepairDesktopLayout));
        menu.Items.Add(Item("Nexus Controls", () => environment.ShowControls())); return menu;
    }
    internal MenuFlyout InputLanguageMenu()
    {
        var menu = new MenuFlyout();
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
        var menu = new MenuFlyout();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            if (environment.ActiveWindow is { } window)
            {
                menu.Items.Add(Item("Minimize", () => environment.MinimizeDockWindow(window)));
                menu.Items.Add(Item(environment.ActiveWindowMaximized ? "Restore size" : "Maximize", () => environment.MaximizeDockWindow(window)));
                menu.Items.Add(new MenuFlyoutSeparator());
                foreach (string placement in WindowPlacementPlan.Choices) menu.Items.Add(Item(placement, () => environment.PlaceWindow(window, placement)));
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
        var menu = new MenuFlyout(); menu.Items.Add(Item("Open", () => environment.OpenDesktopItem(shortcut)));
        if (shortcut.IsFileSystem)
        {
            menu.Items.Add(Item("Rename…", () => environment.RenameDesktopItem(shortcut)));
            menu.Items.Add(Item("Copy path", () => environment.CopyDesktopPath(shortcut)));
            menu.Items.Add(Item("Open containing folder", () => environment.ShowFiles(Path.GetDirectoryName(shortcut.Target))));
            if (shortcut.IsFolder) menu.Items.Add(Item("Pin folder to Places", () => environment.PinDesktopPlace(shortcut)));
        }
        return menu;
    }
}
