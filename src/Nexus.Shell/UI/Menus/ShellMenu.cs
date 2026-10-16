// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI.Menus;

// Every Nexus context uses the same native WinUI sizing, icons and hint style.
// Text editing remains the platform's own context menu.
internal static class ShellMenu
{
    internal static MenuFlyout Create() => new() { MenuFlyoutPresenterStyle = (Style)Application.Current.Resources["NexusContextPresenter"] };
    internal static MenuFlyoutItem Action(string text, Action action, string glyph = "", string hint = "", bool enabled = true)
    {
        var item = new MenuFlyoutItem { Text = text, IsEnabled = enabled, KeyboardAcceleratorTextOverride = hint };
        if (glyph.Length > 0) item.Icon = new FontIcon { Glyph = glyph, FontSize = 14 };
        item.Click += (_, _) => { try { action(); } catch (Exception error) { Log.Write("Context action failed: " + text, error); } }; return item;
    }
    internal static MenuFlyoutSubItem Group(string text, string glyph = "")
    { var item = new MenuFlyoutSubItem { Text = text }; if (glyph.Length > 0) item.Icon = new FontIcon { Glyph = glyph, FontSize = 14 }; return item; }
    internal static ToggleMenuFlyoutItem Toggle(string text, bool selected, Action action)
    { var item = new ToggleMenuFlyoutItem { Text = text, IsChecked = selected }; item.Click += (_, _) => { try { action(); } catch (Exception error) { Log.Write("Context preference failed: " + text, error); } }; return item; }
}
