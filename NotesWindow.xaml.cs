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
    public sealed class Row(Note note) : INotifyPropertyChanged
    {
        public Note Note { get; } = note;
        public string Title => Note.IsProtected ? "Note protégée 🔒" : Note.DisplayTitle;
        public string Excerpt => Note.IsProtected ? "Contenu protégé" : Note.Text[..Math.Min(Note.Text.Length, 180)].Replace('\r', ' ').Replace('\n', ' ');
        public bool Hidden => Note.Hidden;
        public bool Pinned => Note.Pinned;
        public string Color => Note.Color;
        public string State => (Note.IsProtected ? (Note.IsLocked ? "Verrouillée · " : "Protégée · ") : "") + (Note.Hidden ? "Masquée" : "Visible") + (Note.Pinned ? " · Épinglée" : "");
        public string ToggleLabel => Note.IsLocked ? "Déverrouiller" : Note.Hidden ? "Afficher" : "Masquer";
        private (string, string, string, bool, bool, bool, bool, bool)? previous;
        public void Refresh()
        {
            var current = (Title, Excerpt, Note.Color, Note.Hidden, Note.Pinned, Note.IsProtected, Note.IsLocked, Note.Deleted);
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
    private bool initialized;
    private bool InTrash => NoteTabs.SelectedIndex == 1;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };
    public NotesWindow(App app)
    {
        this.app = app; InitializeComponent(); initialized = true;
        Title = $"Mes notes — {AppInfo.DisplayName}";
        NoteList.ItemsSource = rows;
        AutoPurge.IsChecked = app.Store.TrashRetentionDays != null;
        PurgeDays.Text = (app.Store.TrashRetentionDays ?? 30).ToString();
        refresh.Tick += (_, _) => { refresh.Stop(); Refresh(); };
        app.NotesChanged += ScheduleRefresh;
        Closed += (_, _) => { refresh.Stop(); app.NotesChanged -= ScheduleRefresh; };
        Refresh();
    }
    public void RefreshPrivacy() { refresh.Stop(); foreach (var row in rowCache.Values) row.Refresh(); Refresh(); }
    private void ScheduleRefresh() { refresh.Stop(); refresh.Start(); }
    private void Refresh(HashSet<Guid>? selected = null)
    {
        selected ??= NoteList.SelectedItems.Cast<Row>().Select(r => r.Note.Id).ToHashSet();
        var matches = app.Store.Notes.Where(n => NoteManagement.Matches(n, SearchBox.Text, InTrash)).ToList();
        var ids = matches.Select(n => n.Id).ToHashSet();
        var activeIds = app.Store.Notes.Select(n => n.Id).ToHashSet();
        updatingRows = true;
        for (var i = rows.Count - 1; i >= 0; i--) if (!ids.Contains(rows[i].Note.Id)) rows.RemoveAt(i);
        foreach (var id in rowCache.Keys.Where(id => !activeIds.Contains(id)).ToList()) rowCache.Remove(id);
        for (var i = 0; i < matches.Count; i++)
        {
            var note = matches[i];
            if (!rowCache.TryGetValue(note.Id, out var row)) rowCache[note.Id] = row = new Row(note);
            if (i >= rows.Count || rows[i] != row) { if (rows.Contains(row)) rows.Move(rows.IndexOf(row), i); else rows.Insert(i, row); }
            row.Refresh();
            if (selected.Contains(note.Id)) { if (!NoteList.SelectedItems.Contains(row)) NoteList.SelectedItems.Add(row); }
            else NoteList.SelectedItems.Remove(row);
        }
        Counter.Text = $"{rows.Count} résultat(s) / {app.Store.Notes.Count(n => n.Deleted == InTrash)} note(s)";
        updatingRows = false;
        UpdateSelection();
    }
    public void SelectImported(IEnumerable<Guid> ids)
    {
        NoteTabs.SelectedIndex = 0; SearchBox.Text = ""; Refresh(ids.ToHashSet());
        if (NoteList.SelectedItems.Cast<Row>().FirstOrDefault() is { } first) NoteList.ScrollIntoView(first);
    }
    private void UpdateSelection()
    {
        if (SelectionInfo == null || updatingRows) return;
        var count = NoteList.SelectedItems.Count;
        SelectionInfo.Text = $"{count} note(s) sélectionnée(s)" + (InTrash ? " · Restaurer remet les notes dans Mes notes sans les ouvrir." : " · Double-clic : retrouver · Ctrl+clic, Maj+clic, Ctrl+A · Suppr : corbeille.");
        Actions.Visibility = InTrash ? Visibility.Collapsed : Visibility.Visible;
        PurgeSettings.Visibility = InTrash ? Visibility.Visible : Visibility.Collapsed;
        TrashActions.Visibility = InTrash ? Visibility.Visible : Visibility.Collapsed;
        RestoreButton.IsEnabled = PermanentDeleteButton.IsEnabled = count > 0;
        EmptyTrashButton.IsEnabled = app.Store.Notes.Any(n => n.Deleted);
        VisibilityColumn.Visibility = InTrash ? Visibility.Collapsed : Visibility.Visible;
        Actions.IsEnabled = count > 0;
    }
    private List<Note> Selected() => NoteList.SelectedItems.Cast<Row>().Where(r => r.Note.Deleted == InTrash).Select(r => r.Note).ToList();
    private void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || e.Source != NoteTabs) return;
        SearchBox.Text = ""; Refresh([]);
    }
    private void RestoreClick(object sender, RoutedEventArgs e) { app.RestoreNotes(Selected()); Refresh([]); }
    private void PermanentDeleteClick(object sender, RoutedEventArgs e) { app.ConfirmPermanentDeletion(Selected()); Refresh([]); }
    private void EmptyTrashClick(object sender, RoutedEventArgs e) { app.ConfirmPermanentDeletion(app.Store.Notes.Where(n => n.Deleted)); Refresh([]); }
    private void SearchChanged(object sender, TextChangedEventArgs e) { if (initialized) Refresh([]); }
    private void SelectAllClick(object sender, RoutedEventArgs e) { NoteList.SelectAll(); }
    private void ClearClick(object sender, RoutedEventArgs e) { NoteList.UnselectAll(); }
    private void NewClick(object sender, RoutedEventArgs e) => app.NewNote();
    private void ShowClick(object sender, RoutedEventArgs e) => app.ShowNotes(Selected());
    private void HideClick(object sender, RoutedEventArgs e) => app.HideNotes(Selected());
    private void CascadeClick(object sender, RoutedEventArgs e) => app.ShowNotes(Selected(), rearrange: true);
    private void DeleteClick(object sender, RoutedEventArgs e) => app.DeleteNotes(Selected());
    private void ToggleRowClick(object sender, RoutedEventArgs e)
    {
        if (InTrash || ((FrameworkElement)sender).DataContext is not Row row) return;
        if (row.Note.Hidden) app.ShowNotes([row.Note]); else app.HideNotes([row.Note]);
        e.Handled = true;
    }
    private void OpenRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (InTrash || e.ChangedButton != MouseButton.Left || sender is not DataGridRow { Item: Row row }) return;
        for (var node = e.OriginalSource as DependencyObject; node != null && node != sender; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.Primitives.ButtonBase) return;
        e.Handled = true;
        app.FocusNote(row.Note);
    }
    private void ListSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelection();
    private void ListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || !NoteList.IsKeyboardFocusWithin || Keyboard.Modifiers != ModifierKeys.None) return;
        e.Handled = true;
        if (e.IsRepeat) return;
        if (InTrash) app.ConfirmPermanentDeletion(Selected()); else app.DeleteNotes(Selected());
        Refresh([]);
    }
    private void ToggleRowMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ToggleRowClick(sender, e);
    }
    private void ApplyPurgeClick(object sender, RoutedEventArgs e)
    {
        int? days = null;
        if (AutoPurge.IsChecked == true)
        {
            if (!int.TryParse(PurgeDays.Text, out var value) || value < 1 || value > 36500)
            { PurgeStatus.Text = "Saisissez un nombre de jours entre 1 et 36 500."; return; }
            days = value;
        }
        if (app.ConfigureTrashRetention(days))
            PurgeStatus.Text = days == null ? "Purge automatique désactivée." : $"Purge activée après {days} jours.";
        else PurgeStatus.Text = "Réglage non enregistré. Réessayez.";
    }
}
