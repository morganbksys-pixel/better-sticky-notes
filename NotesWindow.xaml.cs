using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BetterStickyNotes;

public partial class NotesWindow : Window
{
    public sealed class Row(Note note, Action changed) : INotifyPropertyChanged
    {
        public Note Note { get; } = note;
        public string Title => string.IsNullOrWhiteSpace(Note.Title) ? "Sans titre" : Note.Title;
        public string Excerpt => Note.Text[..Math.Min(Note.Text.Length, 180)].Replace('\r', ' ').Replace('\n', ' ');
        public bool Hidden => Note.Hidden;
        public bool Pinned => Note.Pinned;
        public string Color => Note.Color;
        public string State => (Note.Hidden ? "Masquée" : "Visible") + (Note.Pinned ? " · Épinglée" : "");
        public string ToggleLabel => Note.Hidden ? "Afficher" : "Masquer";
        private bool selected;
        public bool Selected { get => selected; set { if (selected == value) return; selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); changed(); } }
        private (string, string, string, bool, bool)? previous;
        public void Refresh()
        {
            var current = (Note.Title, Note.Text, Note.Color, Note.Hidden, Note.Pinned);
            if (previous == current) return;
            previous = current;
            PropertyChanged?.Invoke(this, new(null));
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private readonly App app;
    private readonly ObservableCollection<Row> rows = [];
    private readonly Dictionary<Guid, Row> rowCache = [];
    private bool updatingRows;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };
    public NotesWindow(App app)
    {
        this.app = app; InitializeComponent();
        NoteList.ItemsSource = rows;
        refresh.Tick += (_, _) => { refresh.Stop(); Refresh(); };
        app.NotesChanged += ScheduleRefresh;
        Closed += (_, _) => { refresh.Stop(); app.NotesChanged -= ScheduleRefresh; };
        Refresh();
    }
    private void ScheduleRefresh() { refresh.Stop(); refresh.Start(); }
    private void Refresh(HashSet<Guid>? selected = null)
    {
        selected ??= rows.Where(r => r.Selected).Select(r => r.Note.Id).ToHashSet();
        var matches = app.Store.Notes.Where(n => NoteManagement.Matches(n, SearchBox.Text)).ToList();
        var ids = matches.Select(n => n.Id).ToHashSet();
        var activeIds = app.Store.Notes.Where(n => !n.Deleted).Select(n => n.Id).ToHashSet();
        updatingRows = true;
        for (var i = rows.Count - 1; i >= 0; i--) if (!ids.Contains(rows[i].Note.Id)) rows.RemoveAt(i);
        foreach (var id in rowCache.Keys.Where(id => !activeIds.Contains(id)).ToList()) rowCache.Remove(id);
        for (var i = 0; i < matches.Count; i++)
        {
            var note = matches[i];
            if (!rowCache.TryGetValue(note.Id, out var row)) rowCache[note.Id] = row = new Row(note, UpdateSelection);
            if (i >= rows.Count || rows[i] != row) { if (rows.Contains(row)) rows.Move(rows.IndexOf(row), i); else rows.Insert(i, row); }
            row.Refresh(); row.Selected = selected.Contains(note.Id);
        }
        Counter.Text = $"{rows.Count} résultat(s) / {app.Store.Notes.Count(n => !n.Deleted)} note(s)";
        updatingRows = false;
        UpdateSelection();
    }
    public void SelectImported(IEnumerable<Guid> ids)
    {
        SearchBox.Text = ""; Refresh(ids.ToHashSet());
        if (rows.FirstOrDefault(r => r.Selected) is { } first) NoteList.ScrollIntoView(first);
    }
    private void UpdateSelection()
    {
        if (SelectionInfo == null || updatingRows) return;
        var count = rows.Count(r => r.Selected);
        SelectionInfo.Text = $"{count} note(s) sélectionnée(s) · Double-clic sur une ligne pour afficher / masquer la note.";
        Actions.IsEnabled = count > 0;
    }
    private List<Note> Selected() => rows.Where(r => r.Selected && !r.Note.Deleted).Select(r => r.Note).ToList();
    private void SearchChanged(object sender, TextChangedEventArgs e) { if (app != null && NoteList != null) Refresh([]); }
    private void SelectAllClick(object sender, RoutedEventArgs e) { updatingRows = true; foreach (var row in rows) row.Selected = true; updatingRows = false; UpdateSelection(); }
    private void ClearClick(object sender, RoutedEventArgs e) { updatingRows = true; foreach (var row in rows) row.Selected = false; updatingRows = false; UpdateSelection(); }
    private void NewClick(object sender, RoutedEventArgs e) => app.NewNote();
    private void ShowClick(object sender, RoutedEventArgs e) => app.ShowNotes(Selected());
    private void HideClick(object sender, RoutedEventArgs e) => app.HideNotes(Selected());
    private void CascadeClick(object sender, RoutedEventArgs e) => app.ShowNotes(Selected(), rearrange: true);
    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        var selected = Selected();
        if (selected.Count == 0) return;
        if (System.Windows.MessageBox.Show(this, $"Mettre {selected.Count} note(s) à la corbeille ?\n\nElles resteront récupérables via Options → Corbeille.", "Confirmer la suppression", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            app.DeleteNotes(selected);
    }
    private void ToggleRowClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not Row row) return;
        if (row.Note.Hidden) app.ShowNotes([row.Note]); else app.HideNotes([row.Note]);
        e.Handled = true;
    }
    private void OpenDoubleClick(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase) return;
            if (node is DataGridRow { Item: Row row })
            {
                if (row.Note.Hidden) app.ShowNotes([row.Note]); else app.HideNotes([row.Note]);
                e.Handled = true; return;
            }
        }
    }
}
