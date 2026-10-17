// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI.Menus;

// Shared renderer for Nexus Files and desktop shortcuts. Config contains no
// handlers: host surfaces supply a fixed allowlist of actual working actions.
// Unsupported capabilities are omitted rather than presented as fake buttons.
internal enum FileContextCommand
{
    Open, Edit, OpenWith, CopyTo, MoveTo, Rename, CopyPath,
    Properties, Reveal, WindowsOptions, PinFolder, Undo, Copy, Cut, Recycle, CreateZip, ExtractZip, BulkRename
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

        MenuFlyoutItem? Create(string label, FileContextCommand command,
            string glyph, string shortcut = "")
            => Find(command) is { } action
                ? ShellMenu.Action(label, action, glyph, shortcut) : null;
        void AddRoot(string label, FileContextCommand command, string glyph, string shortcut = "")
        {
            if (Create(label, command, glyph, shortcut) is { } item) menu.Items.Add(item);
        }
        void AddChild(MenuFlyoutSubItem group, string label, FileContextCommand command,
            string glyph, string shortcut = "")
        {
            if (Create(label, command, glyph, shortcut) is { } item) group.Items.Add(item);
        }
        // Multi-open/run is deliberately prohibited; copy/move remain governed
        // batch operations in Nexus Core.
        if (multiple)
        {
            AddRoot("Copy " + targets.Count + " items to…", FileContextCommand.CopyTo, "\uE8C8");
            AddRoot("Move " + targets.Count + " items to…", FileContextCommand.MoveTo, "\uE8DE");
            if (menu.Items.Count == 0)
                menu.Items.Add(new MenuFlyoutItem { Text = targets.Count + " selected items", IsEnabled = false });
        }
        else
        {
            string first = plan.Primary switch
            {
                "play" => "Play", "run" => "Run application",
                _ => targets[0].IsFolder ? "Open folder"
                    : plan.ProfileId == "archive.generic" ? "Open archive" : "Open"
            };
            AddRoot(first, FileContextCommand.Open,
                plan.Primary is "play" or "run" ? "\uE768" : "\uE8A5");
            if (plan.CommonActions.Contains("edit"))
                AddRoot("Edit", FileContextCommand.Edit, "\uE70F");
            if (plan.CommonActions.Contains("open_with"))
                AddRoot("Open with…", FileContextCommand.OpenWith, "\uE8A5");
        }

        // Keep the full top level small even when many possible actions exist.
        if (!multiple && (Find(FileContextCommand.CopyTo) is not null || Find(FileContextCommand.MoveTo) is not null))
        {
            var transfer = ShellMenu.Group("Transfer", "\uE8B7");
            AddChild(transfer, "Copy to…", FileContextCommand.CopyTo, "\uE8C8");
            AddChild(transfer, "Move to…", FileContextCommand.MoveTo, "\uE8DE");
            if (transfer.Items.Count > 0) menu.Items.Add(transfer);
        }
        var organize = ShellMenu.Group("Organize", "\uE8A9");
        AddChild(organize, "Copy", FileContextCommand.Copy, "\uE8C8", "Ctrl+C");
        AddChild(organize, "Cut", FileContextCommand.Cut, "\uE8C6", "Ctrl+X");
        AddChild(organize, "Create ZIP…", FileContextCommand.CreateZip, "\uF012");
        AddChild(organize, "Extract ZIP…", FileContextCommand.ExtractZip, "\uE8B7");
        if (!multiple) AddChild(organize, "Rename…", FileContextCommand.Rename, "\uE8AC", "F2");
        AddChild(organize, "Bulk rename…", FileContextCommand.BulkRename, "\uE8AC", "Ctrl+Shift+R");
        AddChild(organize, multiple ? "Copy paths" : "Copy path", FileContextCommand.CopyPath, "\uE8C8");
        if (!multiple && targets[0].IsFolder) AddChild(organize, "Pin to Places", FileContextCommand.PinFolder, "\uE718");
        AddChild(organize, undoLabel, FileContextCommand.Undo, "\uE7A7", "Ctrl+Z");
        AddChild(organize, "Move to Recycle Bin…", FileContextCommand.Recycle, "\uE74D", "Delete");
        if (organize.Items.Count > 0) menu.Items.Add(organize);

        if (!multiple && Find(FileContextCommand.Properties) is not null)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            AddRoot("Properties…", FileContextCommand.Properties, "\uE946", "Alt+Enter");
        }
        var advanced = ShellMenu.Group("Advanced", "\uE712");
        if (!multiple) AddChild(advanced, "Show in Windows Explorer", FileContextCommand.Reveal, "\uED25");
        if (!multiple) AddChild(advanced, "Windows options…", FileContextCommand.WindowsOptions, "\uE713");
        if (advanced.Items.Count > 0) menu.Items.Add(advanced);
        // Maximum seven actionable root entries. A separator isn't an action.
        if (menu.Items.Count(x => x is not MenuFlyoutSeparator) > 7)
            throw new InvalidOperationException("File-type context menu exceeded its compact root budget.");
    }
}
