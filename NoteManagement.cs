using System.Globalization;
using System.Windows;

namespace BetterStickyNotes;

public static class NoteManagement
{
    public static bool Matches(Note note, string query, bool deleted = false)
    {
        query = query.Trim();
        var compare = CultureInfo.GetCultureInfo("fr-FR").CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return note.Deleted == deleted && (query.Length == 0 || !note.IsLocked && (compare.IndexOf(note.Title, query, options) >= 0 || compare.IndexOf(note.Text, query, options) >= 0));
    }
    // Coordinates and dimensions are all physical pixels; no mixed-monitor DPI conversion.
    public static Point Cascade(Rect area, Size size, int index, double step = 48)
    {
        var margin = Math.Min(16, Math.Min(area.Width, area.Height) / 8);
        var availableX = Math.Max(0, area.Width - size.Width - margin * 2);
        var availableY = Math.Max(0, area.Height - size.Height - margin * 2);
        var rows = Math.Max(1, (int)(availableY / step) + 1);
        // Each new column starts to the left of the preceding stack.
        var columnStride = Math.Max(size.Width + 16, (rows - 1) * 24 + 48);
        var columns = Math.Max(1, (int)(availableX / columnStride) + 1);
        var page = index / (rows * columns);
        var row = index % rows;
        var column = index / rows % columns;
        var x = area.Left + margin + availableX - Math.Min(availableX, column * columnStride + row * 24 + page % 4 * 8);
        var y = area.Top + margin + availableY - row * step;
        return new Point(Math.Min(x, Math.Max(area.Left, area.Right - size.Width)), Math.Max(area.Top, y));
    }
}
