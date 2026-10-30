using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BetterStickyNotes;

public partial class NoteWindow
{
    private List<NoteSearchMatch> findMatches = [];
    private NoteSearchDocument? findDocument;
    private int findIndex = -1;
    private NoteSearchAdorner? findAdorner;
    private AdornerLayer? findLayer;
    private DispatcherOperation? findScroll;
    private readonly DispatcherTimer findTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };

    private void InitializeFind()
    {
        findTimer.Tick += (_, _) => { findTimer.Stop(); RefreshFind(); };
        Editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => findAdorner?.InvalidateVisual()));
        Previewer.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => findAdorner?.InvalidateVisual()));
        SizeChanged += (_, _) => findAdorner?.InvalidateVisual();
    }

    private void OpenFind()
    {
        if (clearingSensitiveContent || note.IsLocked) return;
        var selected = !note.Preview ? Editor.SelectedText : Previewer.Selection?.Text;
        SetCollapsed(false);
        EndTitleEdit();
        FindBar.Visibility = Visibility.Visible;
        if (!string.IsNullOrEmpty(selected) && !selected.Contains('\n') && !selected.Contains('\r')) FindBox.Text = selected;
        RefreshFind();
        FindBox.Focus(); FindBox.SelectAll();
    }

    private void FindTextChanged(object sender, TextChangedEventArgs e)
    {
        if (FindBar.Visibility != Visibility.Visible) return;
        findTimer.Stop(); findTimer.Start();
    }

    private void RefreshFind()
    {
        if (FindBar.Visibility != Visibility.Visible || clearingSensitiveContent) return;
        findTimer.Stop();
        RemoveFindAdorner();
        if (note.Preview)
        {
            if (Previewer.Document == null || renderedText != note.Text)
            {
                Previewer.Document = MarkdownView.Render(note.Text, ToggleTask);
                renderedText = note.Text;
            }
            findDocument = new NoteSearchDocument(Previewer.Document);
        }
        else findDocument = null;
        findMatches = NoteSearch.Find(findDocument?.Text ?? Editor.Text, FindBox.Text);
        findIndex = findMatches.Count == 0 ? -1 : 0;
        UpdateFindCounter();
        AttachFindAdorner();
        QueueFindScroll();
    }

    private void AttachFindAdorner()
    {
        var target = note.Preview ? (UIElement)Previewer : Editor;
        findLayer = AdornerLayer.GetAdornerLayer(target);
        if (findLayer == null || findMatches.Count == 0) return;
        findAdorner = new NoteSearchAdorner(target, FindRectangles) { IsHitTestVisible = false };
        findLayer.Add(findAdorner);
    }

    private void RemoveFindAdorner()
    {
        if (findAdorner != null) findLayer?.Remove(findAdorner);
        findAdorner = null; findLayer = null;
    }

    private void UpdateFindCounter()
    {
        FindCounter.Text = $"{findIndex + 1} / {findMatches.Count}";
        FindCounter.ToolTip = findMatches.Count == 0 ? "Aucun résultat" : $"Résultat {findIndex + 1} sur {findMatches.Count}";
        FindPreviousButton.IsEnabled = FindNextButton.IsEnabled = findMatches.Count > 0;
    }

    private void FindPrevious(object sender, RoutedEventArgs e) => MoveFind(-1);
    private void FindNext(object sender, RoutedEventArgs e) => MoveFind(1);

    private void MoveFind(int direction)
    {
        if (findTimer.IsEnabled) RefreshFind();
        if (findMatches.Count == 0) return;
        findIndex = (findIndex + direction + findMatches.Count) % findMatches.Count;
        UpdateFindCounter();
        findAdorner?.InvalidateVisual();
        QueueFindScroll();
    }

    private void QueueFindScroll()
    {
        findScroll?.Abort();
        findScroll = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            findScroll = null;
            if (FindBar.Visibility != Visibility.Visible || findIndex < 0 || clearingSensitiveContent) return;
            var match = findMatches[findIndex];
            if (!note.Preview)
            {
                Editor.UpdateLayout();
                var line = Editor.GetLineIndexFromCharacterIndex(match.Start);
                if (line >= 0) Editor.ScrollToLine(line);
            }
            else if (findDocument?.PositionAt(match.Start) is { } position)
            {
                Previewer.UpdateLayout();
                var scroll = FindVisual<ScrollViewer>(Previewer);
                // Character rectangles already include the document's scroll offset.
                // Transform from the scroll content, not its translated page visual.
                var host = scroll?.Content as Visual;
                if (host != null && scroll != null && position.HasValidLayout)
                {
                    var rect = host.TransformToAncestor(Previewer).TransformBounds(position.GetCharacterRect(LogicalDirection.Forward));
                    if (rect.Top < 0 || rect.Bottom > Previewer.ActualHeight)
                        scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + rect.Top - Previewer.ActualHeight / 3));
                }
            }
            findAdorner?.InvalidateVisual();
        });
    }

    private IEnumerable<(Rect Rect, bool Active)> FindRectangles()
    {
        var host = note.Preview ? FindVisual<ScrollViewer>(Previewer)?.Content as Visual : null;
        if (note.Preview && host == null) yield break;
        for (var index = 0; index < findMatches.Count; index++)
        {
            var match = findMatches[index];
            var line = Rect.Empty;
            for (var offset = match.Start; offset < match.Start + match.Length; offset++)
            {
                Rect rect;
                if (note.Preview)
                {
                    var start = findDocument?.PositionAt(offset);
                    var end = start?.GetPositionAtOffset(1);
                    if (start == null || end == null || !start.HasValidLayout) continue;
                    var first = start.GetCharacterRect(LogicalDirection.Forward);
                    var last = end.GetCharacterRect(LogicalDirection.Backward);
                    if (first.IsEmpty || last.IsEmpty) continue;
                    rect = new Rect(new Point(Math.Min(first.Left, last.Left), first.Top), new Size(Math.Max(1, Math.Abs(last.Left - first.Left)), first.Height));
                    rect = host!.TransformToAncestor(Previewer).TransformBounds(rect);
                }
                else
                {
                    var first = Editor.GetRectFromCharacterIndex(offset);
                    var last = Editor.GetRectFromCharacterIndex(offset, trailingEdge: true);
                    if (first.IsEmpty || last.IsEmpty) continue;
                    rect = new Rect(new Point(Math.Min(first.Left, last.Left), first.Top), new Size(Math.Max(1, Math.Abs(last.Left - first.Left)), first.Height));
                }
                if (!line.IsEmpty && Math.Abs(line.Top - rect.Top) > 1)
                {
                    yield return (line, index == findIndex);
                    line = Rect.Empty;
                }
                line.Union(rect);
            }
            if (!line.IsEmpty) yield return (line, index == findIndex);
        }
    }

    private static T? FindVisual<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T value && (predicate == null || predicate(value))) return value;
            if (FindVisual(child, predicate) is { } nested) return nested;
        }
        return null;
    }

    private void CloseFindClick(object sender, RoutedEventArgs e) => CloseFind(restoreFocus: true);

    private void CloseFind(bool restoreFocus = false)
    {
        findTimer.Stop(); findScroll?.Abort(); findScroll = null;
        FindBar.Visibility = Visibility.Collapsed;
        FindBox.IsUndoEnabled = false; FindBox.Clear(); FindBox.IsUndoEnabled = true;
        RemoveFindAdorner(); findMatches.Clear(); findDocument = null; findIndex = -1;
        UpdateFindCounter();
        if (restoreFocus && !clearingSensitiveContent && !note.Collapsed)
        {
            if (note.Preview) Previewer.Focus(); else Editor.Focus();
        }
    }
}
