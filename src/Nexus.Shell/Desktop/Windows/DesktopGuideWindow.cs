// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Nexus.Shell.Desktop;

internal sealed class DesktopGuideWindow : DesktopPanelWindow
{
    private readonly StackPanel _items;
    internal DesktopGuideWindow(DesktopEnvironment environment) : this(environment, new StackPanel { Spacing = 10 }) { }
    private DesktopGuideWindow(DesktopEnvironment environment, StackPanel items) : base(environment, "Desktop shortcuts", new ScrollViewer { Content = items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 560, 680, center: true)
    { _items = items; ApplyAppearance(); }
    internal void Show() => ShowPanel();
    internal override void ApplyAppearance()
    {
        base.ApplyAppearance(); if (_items is null) return; _items.Children.Clear(); var theme = Environment.Theme;
        void Label(string text, bool heading = false) => _items.Children.Add(new TextBlock { Text = text, FontSize = heading ? 16 : 13, TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush(heading ? "NexusText" : "NexusMuted"), Margin = new Thickness(0, heading ? 12 : 0, 0, 0) });
        Label("Your desktop, within reach", true);
        Label("These controls belong to Nexus. Keyboard shortcuts below work while the named surface has focus.");
        foreach (var (label, action) in new (string, Action)[] { ("File jobs · copy, move and verify", () => Environment.ShowUtility("File jobs")), ("Nexus Controls · core, personalization and privacy", () => Environment.ShowControls()), ("Launchpad · organize and launch apps", () => Environment.ShowMenu()), ("Nexus Search · files, windows and desktop controls", Environment.ShowSearch), ("Places · favorite folders", Environment.ShowPlaces), ("Window board · arrange and restore layouts", Environment.ShowWindowOverview), ("Clipboard shelf · copied text", Environment.ShowClipboardShelf), ("Notifications · read, snooze and search", Environment.ShowNotifications), ("Windows tools · system utilities and keep awake", Environment.ShowWindowsTools), ("Session status · health and recovery", Environment.ShowSessionStatus) })
            _items.Children.Add(ActionButton(label, action));
        Label("On the desktop", true);
        Label("Enter · open selected item\nF2 · rename selected file or folder\nCtrl+Shift+N · new desktop folder\nCtrl+Z · undo the last desktop file edit\nF5 · refresh desktop icons\nCtrl+K · Launchpad\nCtrl+Shift+P · Places\nCtrl+Shift+F · Nexus Search\nF1 · this guide");
        Label("Nexus Search", true);
        Label("Ctrl+Alt+Space opens Search from other apps when the shortcut is available. Inside Search: arrows select, Enter opens, Ctrl+Enter opens the folder, Ctrl+Shift+C copies the path, Ctrl+L focuses the query and F5 refreshes.");
        Label("Nexus Controls", true);
        Label("Open Controls from the desktop, Launchpad or Nexus menu. Ctrl+F focuses settings search; Escape clears the query. Appearance presets include an undo action. Closing Controls leaves the desktop running.");
        Label("Windows integration", true);
        Label("Nexus menu → Windows tools opens system utilities and a temporary Keep awake timer. Search also finds individual tools. The menu-bar language indicator requests an installed input language for the focused app; Win+Space opens Windows’ own switcher. Windows may decline a language or power request under its current policy.");
        Label("Inside Nexus Files", true);
        Label("Ctrl+L · focus the folder path\nAlt+Left / Alt+Right · back / forward\nF5 · refresh\nF2 · rename selected file or folder\nCtrl+Z · undo the last file edit\nEnter · open selected item");
        Label("Across Nexus panels", true); Label("Escape closes the current panel. Tab and arrow keys move between controls. Right-click a Launchpad app to organize it, or a dock pin to change its order.");
    }
    protected override void OnClosed() => Environment.DesktopGuideClosed(this);
}
