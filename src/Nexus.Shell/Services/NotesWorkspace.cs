using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public sealed record DesktopNote(string Id, string Title, string Text, bool Pinned);

// Notes reuse the saved board and existing quick note. One source of truth;
// edits made in Explore or Study are visible in the floating Notes window.
public static class NotesWorkspace
{
    public const string QuickId = "nexus:quick-note";
    public static DesktopNote[] Read(ShellState state, string query = "", bool pinned = false) =>
        new[] { new DesktopNote(QuickId, "Quick note", state.QuickNote, false) }
        .Concat(state.SavedItems.Where(i => i.Kind == "Note").Select(i => new DesktopNote(i.Id, i.Title, i.Note, i.Favorite)))
        .Where(n => (!pinned || n.Pinned) && (n.Title.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase) || n.Text.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        .OrderByDescending(n => n.Pinned).ToArray();
    public static string Add(ShellState state)
    {
        var item = new SavedItem(Guid.NewGuid().ToString("N"), "Untitled note", "", "Note", Note: "");
        ExploreWorkspace.Add(state, item); return item.Id;
    }
    public static bool Update(ShellState state, string id, string title, string text)
    {
        if (id == QuickId) { state.QuickNote = text[..Math.Min(text.Length, 10_000)]; return true; }
        int index = state.SavedItems.FindIndex(i => i.Id == id && i.Kind == "Note");
        if (index < 0) return false;
        title = string.IsNullOrWhiteSpace(title) ? "Untitled note" : title.Trim();
        state.SavedItems[index] = state.SavedItems[index] with { Title = title[..Math.Min(title.Length, 100)], Note = text[..Math.Min(text.Length, ExploreWorkspace.NoteLimit)] };
        return true;
    }
    public static bool TogglePin(ShellState state, string id)
    {
        int index = state.SavedItems.FindIndex(i => i.Id == id && i.Kind == "Note");
        if (index < 0) return false;
        state.SavedItems[index] = state.SavedItems[index] with { Favorite = !state.SavedItems[index].Favorite }; return true;
    }
    public static bool Remove(ShellState state, string id) => state.SavedItems.RemoveAll(i => i.Id == id && i.Kind == "Note") > 0;
}
