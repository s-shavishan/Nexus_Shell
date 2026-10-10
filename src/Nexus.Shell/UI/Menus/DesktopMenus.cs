using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI.Menus;

internal sealed class DesktopMenus(DesktopEnvironment environment)
{
    private static MenuFlyoutItem Item(string name, Action action)
    { var item = new MenuFlyoutItem { Text = name }; item.Click += (_, _) => action(); return item; }
    internal MenuFlyout DesktopMenu()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(Item("Refresh", environment.RefreshDesktop));
        menu.Items.Add(Item("Open Sections", () => environment.ShowSections()));
        menu.Items.Add(Item("Quick settings", environment.ShowQuickSettings));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Open desktop folder", () => environment.OpenTarget(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory))));
        menu.Items.Add(Item("Personalize", () => environment.ShowSections("Personalize")));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item(environment.Mode == DesktopSessionMode.DesktopShell ? "Session…" : "Exit Nexus", environment.RequestExit)); return menu;
    }
    internal MenuFlyout NexusMenu()
    {
        var menu = new MenuFlyout(); menu.Items.Add(Item("About Nexus Shell", () => environment.ShowSections("Personalize")));
        menu.Items.Add(Item("System preferences…", () => environment.ShowSections("PC controls")));
        menu.Items.Add(Item("Apps…", () => environment.ShowSections("Apps"))); menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Refresh desktop", environment.RefreshDesktop)); menu.Items.Add(Item("Lock screen", environment.LockScreen));
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(Item(environment.IsManagedDesktop ? "Return to Windows…" : "Exit Nexus", environment.RequestExit)); return menu;
    }
    internal MenuFlyout FileMenu()
    {
        var menu = new MenuFlyout(); menu.Items.Add(Item("New note", () => environment.ShowUtility("Notes")));
        menu.Items.Add(Item("Open files…", () => environment.ShowFiles())); menu.Items.Add(Item("Downloads", () => environment.ShowFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))));
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(Item("Calculator", () => environment.ShowUtility("Calculator"))); return menu;
    }
    internal MenuFlyout ViewMenu()
    {
        var menu = new MenuFlyout(); menu.Items.Add(Item("Search…", () => environment.ShowMenu(true))); menu.Items.Add(Item("Control Center", environment.ShowQuickSettings));
        menu.Items.Add(Item("Desktop appearance…", () => environment.ShowSections("Personalize"))); menu.Items.Add(Item("Focus desktop", () => { environment.Session.State.FocusMode = !environment.Session.State.FocusMode; environment.SaveState(); })); return menu;
    }
    internal MenuFlyout WindowMenu()
    {
        var menu = new MenuFlyout(); menu.Items.Add(Item("Window overview", environment.ShowWindowOverview)); menu.Items.Add(Item("Show desktop", environment.ShowDesktop));
        menu.Items.Add(Item("Nexus Sections", () => environment.ShowSections())); return menu;
    }
    internal MenuFlyout DesktopItemMenu(DesktopShortcut shortcut)
    { var menu = new MenuFlyout(); menu.Items.Add(Item("Open", () => environment.OpenDesktopItem(shortcut))); return menu; }
}
