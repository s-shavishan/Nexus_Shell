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
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Open desktop folder", () => environment.OpenTarget(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory))));
        menu.Items.Add(Item("Personalize", () => environment.ShowSections("Personalize")));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item(environment.Mode == DesktopSessionMode.DesktopShell ? "Session…" : "Exit Nexus", environment.RequestExit)); return menu;
    }
    internal MenuFlyout DesktopItemMenu(DesktopShortcut shortcut)
    { var menu = new MenuFlyout(); menu.Items.Add(Item("Open", () => environment.OpenDesktopItem(shortcut))); return menu; }
}
