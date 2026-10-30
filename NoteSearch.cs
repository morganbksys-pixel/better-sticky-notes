using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace BetterStickyNotes;

public readonly record struct NoteSearchMatch(int Start, int Length);

public static class NoteSearch
{
    public static List<NoteSearchMatch> Find(string text, string query)
    {
        List<NoteSearchMatch> matches = [];
        if (query.Length == 0) return matches;
        for (var offset = 0; offset <= text.Length - query.Length;)
        {
            var start = text.IndexOf(query, offset, StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;
            matches.Add(new(start, query.Length));
            offset = start + query.Length;
        }
        return matches;
    }
}

// Map rendered text to WPF positions, including words split across Markdown runs.
public sealed class NoteSearchDocument
{
    private readonly List<(int Start, int Length, TextPointer Position)> runs = [];
    public string Text { get; }

    public NoteSearchDocument(FlowDocument document)
    {
        var text = new StringBuilder();
        var position = document.ContentStart;
        while (position != null && position.CompareTo(document.ContentEnd) < 0)
        {
            var context = position.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                var value = position.GetTextInRun(LogicalDirection.Forward);
                runs.Add((text.Length, value.Length, position));
                text.Append(value);
                position = position.GetPositionAtOffset(value.Length);
                continue;
            }
            if (context == TextPointerContext.ElementEnd && position.GetAdjacentElement(LogicalDirection.Forward) is Paragraph or LineBreak)
                text.Append('\n');
            else if (context == TextPointerContext.EmbeddedElement) text.Append('\uFFFC');
            position = position.GetNextContextPosition(LogicalDirection.Forward);
        }
        Text = text.ToString();
    }

    public TextPointer? PositionAt(int offset)
    {
        var low = 0; var high = runs.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var run = runs[middle];
            if (offset < run.Start) high = middle - 1;
            else if (offset >= run.Start + run.Length) low = middle + 1;
            else return run.Position.GetPositionAtOffset(offset - run.Start);
        }
        return null;
    }
}

// Translucent overlays leave the editor's text, formatting and undo stack untouched.
internal sealed class NoteSearchAdorner(UIElement target, Func<IEnumerable<(Rect Rect, bool Active)>> rectangles) : Adorner(target)
{
    private static readonly Brush matchBrush = new SolidColorBrush(Color.FromArgb(95, 255, 205, 0));
    private static readonly Brush activeBrush = new SolidColorBrush(Color.FromArgb(125, 255, 140, 0));
    private static readonly Pen activeBorder = new(new SolidColorBrush(Color.FromRgb(155, 80, 0)), 1);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var viewport = new Rect(AdornedElement.RenderSize);
        drawingContext.PushClip(new RectangleGeometry(viewport));
        foreach (var (rect, active) in rectangles())
        {
            if (rect.IsEmpty || !rect.IntersectsWith(viewport)) continue;
            drawingContext.DrawRoundedRectangle(active ? activeBrush : matchBrush, active ? activeBorder : null, rect, 2, 2);
        }
        drawingContext.Pop();
    }
}
