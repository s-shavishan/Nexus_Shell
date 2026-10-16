// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

public readonly record struct WindowBoardPage(int Index, int Offset, int Count, int Pages, int Columns, int Rows)
{ public bool Previous => Index > 0; public bool Next => Index + 1 < Pages; }
public static class WindowBoardPolicy
{
    // A fresh overview opening must rebuild cards/initial selection even if
    // all native HWND snapshots are identical to the previous session.
    public static bool ShouldRenderSnapshot(bool unchanged, bool selectionInitialized)
        => !unchanged || !selectionInitialized;
    public static WindowBoardPage Page(int count, int index, double width, double height)
    {
        count = Math.Clamp(count, 0, 200);
        int columns = double.IsFinite(width) && width < 800 ? 2 : 3;
        int rows = double.IsFinite(height) && height < 550 ? 1 : 2;
        int capacity = columns * rows, pages = Math.Max(1, (count + capacity - 1) / capacity);
        index = Math.Clamp(index, 0, pages - 1); int offset = index * capacity;
        return new(index, offset, Math.Min(capacity, count - offset), pages, columns, rows);
    }
}
