using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Runtime;

public enum DesktopPreference { Wallpaper, NativeGlass, ReducedEffects, FloatingDock, CompactDock, WindowPreviews, Clock24Hour, ClockWidget, SpaceWidget, QuietAlerts }
public sealed record ControlCenterPreferences(string Wallpaper, bool NativeGlass, bool ReducedEffects, bool FloatingDock, bool CompactDock,
    bool WindowPreviews, bool Clock24Hour, bool ClockWidget, bool SpaceWidget, bool QuietAlerts)
{
    public static ControlCenterPreferences From(ShellState state) => new(state.Wallpaper, state.NativeGlass, state.ReducedEffects, state.FloatingTaskbar,
        state.CompactDock, state.DockPreviews, state.Clock24Hour, state.ShowClockWidget, state.ShowSpaceWidget, state.QuietNotifications);
    public void Apply(ShellState state)
    {
        state.Wallpaper = Wallpaper; state.NativeGlass = NativeGlass; state.ReducedEffects = ReducedEffects; state.FloatingTaskbar = FloatingDock;
        state.CompactDock = CompactDock; state.DockPreviews = WindowPreviews; state.Clock24Hour = Clock24Hour;
        state.ShowClockWidget = ClockWidget; state.ShowSpaceWidget = SpaceWidget; state.QuietNotifications = QuietAlerts;
    }
    public static void Set(ShellState state, PanelAction command)
    {
        Validate(command);
        if (command.Action != "preference") throw new RuntimeFailure("panel-action", "This is not a desktop preference.");
        bool value = command.Enabled ?? false;
        switch (command.Preference)
        {
            case DesktopPreference.Wallpaper: state.Wallpaper = command.Value!; break;
            case DesktopPreference.NativeGlass: state.NativeGlass = value; break;
            case DesktopPreference.ReducedEffects: state.ReducedEffects = value; break;
            case DesktopPreference.FloatingDock: state.FloatingTaskbar = value; break;
            case DesktopPreference.CompactDock: state.CompactDock = value; break;
            case DesktopPreference.WindowPreviews: state.DockPreviews = value; break;
            case DesktopPreference.Clock24Hour: state.Clock24Hour = value; break;
            case DesktopPreference.ClockWidget: state.ShowClockWidget = value; break;
            case DesktopPreference.SpaceWidget: state.ShowSpaceWidget = value; break;
            case DesktopPreference.QuietAlerts: state.QuietNotifications = value; break;
        }
    }
    public static void Validate(PanelAction command)
    {
        if (command.Id == Guid.Empty) throw new RuntimeFailure("panel-action", "The panel action needs an identity.");
        if (command.Action == "preference")
        {
            if (command.Preference is not { } preference || !Enum.IsDefined(preference)) throw new RuntimeFailure("panel-preference", "This desktop preference is unavailable.");
            if (preference == DesktopPreference.Wallpaper ? command.Enabled is not null || command.Value is null || !AuraPalette.Moods.Contains(command.Value)
                : command.Enabled is null || command.Value is not null) throw new RuntimeFailure("panel-preference", "The desktop preference has an invalid value.");
        }
        else if (command.Action == "advanced")
        {
            if (!Sections.Contains(command.Value ?? "") || command.Preference is not null || command.Enabled is not null) throw new RuntimeFailure("panel-action", "This advanced settings section is unavailable.");
        }
        else if (command.Action is "personalize" or "lock")
        {
            if (command.Preference is not null || command.Enabled is not null || command.Value is not null) throw new RuntimeFailure("panel-action", "This panel action has unexpected arguments.");
        }
        else throw new RuntimeFailure("panel-action", "This panel action is unavailable.");
    }
    public static readonly string[] Sections = ["Sound", "Network", "Bluetooth", "Display", "Power", "Desktop"];
}
public sealed record PanelMonitor(int X, int Y, int Width, int Height, double Scale, long DisplayWindow, bool Managed);
public sealed record PanelDesired(long Sequence, bool Visible, string Section, PanelMonitor Monitor);
public sealed record PanelAction(Guid Id, string Action, DesktopPreference? Preference = null, bool? Enabled = null, string? Value = null);
public sealed record PanelSync(PanelDesired Desired, ControlCenterPreferences Preferences, Guid[] Acknowledged);
public sealed record PanelStatus(int ProcessId, long Window, bool Visible, bool Recovering, bool Paused, Guid IssueId, string Issue);
public sealed record PanelSyncResult(PanelStatus Status, PanelAction[] Actions);
public sealed record PanelSnapshot(Guid ToolId, long Revision, ControlCenterPreferences Preferences, PanelDesired Desired);
public sealed record PanelReady(long Window);
public sealed record PanelHidden(long Sequence);
