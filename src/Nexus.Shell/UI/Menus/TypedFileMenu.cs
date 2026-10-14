using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI.Menus;

// Shared renderer for Nexus Files and desktop shortcuts. Config contains no
// handlers: host surfaces supply a fixed allowlist of actual working actions.
// Unsupported capabilities are omitted rather than presented as fake buttons.
internal enum FileContextCommand
{
    Open, Edit, OpenWith, CopyTo, MoveTo, Rename, CopyPath,
    Properties, Reveal, WindowsOptions, PinFolder, Undo
}

internal static class TypedFileMenu
{
    internal static void Build(MenuFlyout menu, IReadOnlyList<FileTypeMenuTarget> targets,
        IReadOnlyDictionary<FileContextCommand, Action> handlers, string undoLabel = "Undo edit")
    {
        if (targets.Count == 0) throw new ArgumentException("Select at least one item.", nameof(targets));
        menu.Items.Clear();
        var plan = FileTypeContextCatalog.Plan(targets);
        bool multiple = targets.Count > 1;
        Action? Find(FileContextCommand command)
            => handlers.TryGetValue(command, out var action) ? action : null;

        void Add(MenuFlyout menuTarget, string label, FileContextCommand command,
            string glyph, string shortcut = "")
        {
            if (Find(command) is { } action)
                menuTarget.Items.Add(ShellMenu.Action(label, action, glyph, shortcut));
        }
        // Multi-open/run is deliberately prohibited; copy/move remain governed
        // batch operations in Nexus Core.
        if (multiple)
        {
            Add(menu, "Copy " + targets.Count + " items to…", FileContextCommand.CopyTo, "\uE8C8");
            Add(menu, "Move " + targets.Count + " items to…", FileContextCommand.MoveTo, "\uE8DE");
            if (menu.Items.Count == 0)
                menu.Items.Add(new MenuFlyoutItem { Text = targets.Count + " selected items", IsEnabled = false });
        }
        else
        {
            string first = plan.Primary switch
            {
                "play" => "Play", "run" => "Run application",
                _ => targets[0].IsFolder ? "Open folder" : "Open"
            };
            Add(menu, first, FileContextCommand.Open,
                plan.Primary is "play" or "run" ? "\uE768" : "\uE8A5");
            if (plan.CommonActions.Contains("edit"))
                Add(menu, "Edit", FileContextCommand.Edit, "\uE70F");
            if (plan.CommonActions.Contains("open_with"))
                Add(menu, "Open with…", FileContextCommand.OpenWith, "\uE8A5");
        }

        // Keep the full top level small even when many possible actions exist.
        if (!multiple && (Find(FileContextCommand.CopyTo) is not null || Find(FileContextCommand.MoveTo) is not null))
        {
            var transfer = ShellMenu.Group("Transfer", "\uE8B7");
            Add(transfer, "Copy to…", FileContextCommand.CopyTo, "\uE8C8");
            Add(transfer, "Move to…", FileContextCommand.MoveTo, "\uE8DE");
            if (transfer.Items.Count > 0) menu.Items.Add(transfer);
        }
        var organize = ShellMenu.Group("Organize", "\uE8A9");
        if (!multiple) Add(organize, "Rename…", FileContextCommand.Rename, "\uE8AC", "F2");
        Add(organize, multiple ? "Copy paths" : "Copy path", FileContextCommand.CopyPath, "\uE8C8");
        if (!multiple && targets[0].IsFolder) Add(organize, "Pin to Places", FileContextCommand.PinFolder, "\uE718");
        Add(organize, undoLabel, FileContextCommand.Undo, "\uE7A7", "Ctrl+Z");
        if (organize.Items.Count > 0) menu.Items.Add(organize);

        if (!multiple && Find(FileContextCommand.Properties) is not null)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            Add(menu, "Properties…", FileContextCommand.Properties, "\uE946", "Alt+Enter");
        }
        var advanced = ShellMenu.Group("Advanced", "\uE712");
        if (!multiple) Add(advanced, "Show in Windows Explorer", FileContextCommand.Reveal, "\uED25");
        if (!multiple) Add(advanced, "Windows options…", FileContextCommand.WindowsOptions, "\uE713");
        if (advanced.Items.Count > 0) menu.Items.Add(advanced);
        // Maximum seven actionable root entries. A separator isn't an action.
        if (menu.Items.Count(x => x is not MenuFlyoutSeparator) > 7)
            throw new InvalidOperationException("File-type context menu exceeded its compact root budget.");
    }
}
