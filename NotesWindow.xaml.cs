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
        public string Title => Note.DisplayTitle;
        public string Excerpt => Note.IsLocked ? "Contenu protégé" : Note.Text[..Math.Min(Note.Text.Length, 180)].Replace('\r', ' ').Replace('\n', ' ');
        public string ProtectionMarker => !Note.IsProtected ? "" : Note.IsLocked ? "\uE72E" : "\uE785";
        public string ProtectionState => Note.IsLocked ? "Note protégée verrouillée" : Note.IsProtected ? "Note protégée déverrouillée" : "";
        public string GroupLabel { get; set; } = "";
        public string Detail => GroupLabel.Length == 0 ? Excerpt : GroupLabel + " · " + Excerpt;
        public bool Hidden => Note.Hidden;
        public bool Pinned => Note.Pinned;
        public string Color => Note.Color;
        public string State => (Note.IsProtected ? (Note.IsLocked ? "Verrouillée · " : "Protégée · ") : "") + (Note.Hidden ? "Masquée" : "Visible") + (Note.Pinned ? " · Épinglée" : "");
        public string ToggleLabel => Note.IsLocked ? "Déverrouiller" : Note.Hidden ? "Afficher" : "Masquer";
        public string ToggleToolTip => $"{ToggleLabel} la note · {State}";
        public int CaptureCount => Note.IsProtected ? 0 : Note.Captures.Count;
        public string CaptureSummary => CaptureCount == 0 ? "" : CaptureCount == 1 ? "1 capture" : $"{CaptureCount} captures";
        private (string, string, string, bool, bool, bool, bool, bool, int)? previous;
        public void Refresh()
        {
            var current = (Title, Detail, Note.Color, Note.Hidden, Note.Pinned, Note.IsProtected, Note.IsLocked, Note.Deleted, CaptureCount);
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
    private bool updatingGroups;
    public sealed record GroupChoice(string Key, string Name, int Count, string Color);
    private string GroupKey = "all";
    private Guid? SelectedGroupId => Guid.TryParse(GroupKey, out var id) ? id : null;
    private bool MatchesGroup(Note note) => GroupKey is "all" or "trash" || note.GroupId == SelectedGroupId;
    private bool InTrash => GroupKey == "trash";
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };
    public NotesWindow(App app)
    {
        this.app = app; InitializeComponent(); WindowAppearance.Apply(this); initialized = true;
        PreviewKeyDown += HomeKeyDown;
        Title = $"Mes notes — {AppInfo.DisplayName}";
        NoteList.ItemsSource = rows;
        NoteList.ContextMenu = new ContextMenu();
        GroupList.ContextMenu = new ContextMenu();
        TrashButton.ContextMenu = new ContextMenu();
        TrashButton.ContextMenuOpening += TrashContextOpening;
        UngroupedButton.ContextMenu = BuildGroupContextMenu(null);
        refresh.Tick += (_, _) => { refresh.Stop(); Refresh(); };
        app.NotesChanged += ScheduleRefresh;
        Closed += (_, _) => { refresh.Stop(); app.NotesChanged -= ScheduleRefresh; CloseCaptures(); HideGallery(); };
        Refresh();
    }
    public void RefreshPrivacy() { refresh.Stop(); foreach (var row in rowCache.Values) row.Refresh(); Refresh(); }
    private void ScheduleRefresh() { refresh.Stop(); refresh.Start(); }
    private void Refresh(HashSet<Guid>? selected = null)
    {
        RefreshGroups();
        if (GalleryVisible) { gallery?.Refresh(); return; }
        selected ??= NoteList.SelectedItems.Cast<Row>().Select(r => r.Note.Id).ToHashSet();
        var matches = app.Store.Notes.Where(n => MatchesGroup(n) && NoteManagement.Matches(n, SearchBox.Text, InTrash))
            .OrderByDescending(n => n.ModifiedAtUtc ?? n.CreatedAtUtc ?? DateTimeOffset.MinValue).ToList();
        var ids = matches.Select(n => n.Id).ToHashSet();
        var activeIds = app.Store.Notes.Select(n => n.Id).ToHashSet();
        updatingRows = true;
        for (var i = rows.Count - 1; i >= 0; i--) if (!ids.Contains(rows[i].Note.Id)) rows.RemoveAt(i);
        foreach (var id in rowCache.Keys.Where(id => !activeIds.Contains(id)).ToList()) rowCache.Remove(id);
        for (var i = 0; i < matches.Count; i++)
        {
            var note = matches[i];
            if (!rowCache.TryGetValue(note.Id, out var row)) rowCache[note.Id] = row = new Row(note);
            row.GroupLabel = GroupKey == "all" ? app.Store.Groups.FirstOrDefault(g => g.Id == note.GroupId)?.Name ?? "Sans groupe" : "";
            if (i >= rows.Count || rows[i] != row) { if (rows.Contains(row)) rows.Move(rows.IndexOf(row), i); else rows.Insert(i, row); }
            row.Refresh();
            if (selected.Contains(note.Id)) { if (!NoteList.SelectedItems.Contains(row)) NoteList.SelectedItems.Add(row); }
            else NoteList.SelectedItems.Remove(row);
        }
        var scope = app.Store.Notes.Where(n => MatchesGroup(n) && n.Deleted == InTrash).ToList();
        ViewTitle.Text = InTrash ? "Corbeille" : GroupKey == "all" ? "Toutes les notes" : SelectedGroupId == null ? "Sans groupe" : app.Store.Groups.First(g => g.Id == SelectedGroupId).Name;
        Counter.Text = SearchBox.Text.Trim().Length > 0 ? $"{rows.Count} résultat(s) / {scope.Count} note(s)" : $"{scope.Count} note(s)" + (InTrash ? " · Restaurer remet les notes dans Mes notes" : $" · {scope.Count(n => !n.Hidden)} visible(s) sur le bureau");
        SearchPlaceholder.Text = $"Rechercher dans {ViewTitle.Text}…";
        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var searching = SearchBox.Text.Length > 0;
        EmptyMessage.Text = searching ? "Aucune note ne correspond à cette recherche." : InTrash ? "La corbeille est vide." : SelectedGroupId != null ? "Ce groupe ne contient aucune note." : "Aucune note dans cette vue.";
        EmptyAction.Content = searching ? "Effacer la recherche" : "Créer une note";
        EmptyAction.Visibility = !InTrash || searching ? Visibility.Visible : Visibility.Collapsed;
        updatingRows = false;
        CaptureCountColumn.Visibility = rows.Any(r => r.CaptureCount > 0) ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelection();
    }
    public void SelectImported(IEnumerable<Guid> ids)
    {
        SelectView("all"); SearchBox.Text = ""; Refresh(ids.ToHashSet());
        if (NoteList.SelectedItems.Cast<Row>().FirstOrDefault() is { } first) NoteList.ScrollIntoView(first);
    }
    private void UpdateSelection()
    {
        if (SelectionInfo == null || updatingRows) return;
        var count = NoteList.SelectedItems.Count;
        SelectionInfo.Text = $"{count} note(s) sélectionnée(s)";
        NewNoteButton.Visibility = InTrash ? Visibility.Collapsed : Visibility.Visible;
        EmptyTrashButton.Visibility = InTrash ? Visibility.Visible : Visibility.Collapsed;
        EmptyTrashButton.IsEnabled = app.Store.Notes.Any(n => n.Deleted);
        VisibilityColumn.Visibility = InTrash ? Visibility.Collapsed : Visibility.Visible;
        RefreshCapturePanel();
    }
    private List<Note> Selected() => NoteList.SelectedItems.Cast<Row>().Where(r => r.Note.Deleted == InTrash).Select(r => r.Note).ToList();
    public void SelectView(string key)
    {
        HideGallery();
        NotesContent.Visibility = Visibility.Visible; SettingsHost.Visibility = Visibility.Collapsed;
        if (key is not ("all" or "none" or "trash") && !app.Store.Groups.Any(g => g.Id.ToString() == key)) key = "all";
        GroupKey = key;
        CloseCaptures();
        updatingGroups = true;
        GroupList.SelectedValue = SelectedGroupId?.ToString();
        updatingGroups = false;
        SearchBox.Text = ""; Refresh([]);
    }
    private void AllNotesClick(object sender, RoutedEventArgs e) => SelectView("all");
    private void UngroupedClick(object sender, RoutedEventArgs e) => SelectView("none");
    private void TrashClick(object sender, RoutedEventArgs e) => SelectView("trash");
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        HideGallery();
        SettingsHost.Child = new SettingsView(app);
        SettingsHost.Visibility = Visibility.Visible; NotesContent.Visibility = Visibility.Collapsed;
        NoteList.UnselectAll();
        RefreshGroups();
    }
    private void HomeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            if (!e.IsRepeat) NewClick(sender, e);
        }
        else if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None && !e.IsRepeat)
        {
            var target = RenameTarget(Keyboard.FocusedElement as DependencyObject);
            if (target.GroupId is { } groupId) { e.Handled = true; RenameGroup(groupId); }
            else if (target.Note is { } note) { e.Handled = true; app.RenameNote(note, this); }
        }
    }
    public (Guid? GroupId, Note? Note) RenameTarget(DependencyObject? focus)
    {
        for (var node = focus; node != null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node == GroupList && GroupList.SelectedItem is GroupChoice group) return (Guid.Parse(group.Key), null);
            if (node == NoteList && !InTrash && Selected() is { Count: 1 } selection) return (null, selection[0]);
        }
        return (null, null);
    }
    private void EmptyActionClick(object sender, RoutedEventArgs e) { if (SearchBox.Text.Length > 0) SearchBox.Clear(); else NewClick(sender, e); }
    private void RestoreClick(object sender, RoutedEventArgs e) { app.RestoreNotes(Selected()); Refresh([]); }
    private void PermanentDeleteClick(object sender, RoutedEventArgs e) { app.ConfirmPermanentDeletion(Selected()); Refresh([]); }
    private void EmptyTrashClick(object sender, RoutedEventArgs e) { app.ConfirmEmptyTrash(); Refresh(); }
    private void SearchChanged(object sender, TextChangedEventArgs e) { if (initialized) Refresh([]); }
    private void SelectAllClick(object sender, RoutedEventArgs e) { NoteList.SelectAll(); }
    private void ClearClick(object sender, RoutedEventArgs e) { NoteList.UnselectAll(); }
    public Guid? NewNoteGroupId => SettingsHost.Visibility == Visibility.Visible || GalleryVisible ? null : SelectedGroupId;
    private void NewClick(object sender, RoutedEventArgs e) => app.NewNote(groupId: NewNoteGroupId);
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
    private void RefreshGroups()
    {
        var choices = app.Store.Groups.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).Select(g => new GroupChoice(g.Id.ToString(), g.Name, app.Store.Notes.Count(n => !n.Deleted && n.GroupId == g.Id), g.Color)).ToList();
        AllCount.Text = app.Store.Notes.Count(n => !n.Deleted).ToString();
        UngroupedCount.Text = app.Store.Notes.Count(n => !n.Deleted && n.GroupId == null).ToString();
        TrashCount.Text = app.Store.Notes.Count(n => n.Deleted).ToString();
        GalleryCount.Text = app.Store.Notes.Where(n => !n.Deleted && !n.IsProtected).Sum(n => n.Captures.Count).ToString();
        if (SelectedGroupId != null && !choices.Any(g => g.Key == GroupKey)) GroupKey = "none";
        var settingsActive = SettingsHost.Visibility == Visibility.Visible;
        foreach (var (button, key) in new[] { (AllNotesButton, "all"), (UngroupedButton, "none"), (TrashButton, "trash"), (SettingsButton, "settings"), (GalleryButton, "gallery") })
        {
            var selected = settingsActive ? key == "settings" : GalleryVisible ? key == "gallery" : GroupKey == key;
            button.Background = selected ? new SolidColorBrush(Color.FromRgb(234, 241, 255)) : Brushes.Transparent;
            button.Foreground = selected ? new SolidColorBrush(Color.FromRgb(36, 91, 196)) : new SolidColorBrush(Color.FromRgb(36, 38, 41));
        }
        updatingGroups = true;
        if (GroupList.ItemsSource is not List<GroupChoice> current || !current.SequenceEqual(choices)) GroupList.ItemsSource = choices;
        GroupList.SelectedValue = settingsActive || GalleryVisible ? null : SelectedGroupId?.ToString();
        updatingGroups = false;
    }

    private void GroupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (initialized && !updatingGroups && GroupList.SelectedItem is GroupChoice group) SelectView(group.Key);
    }

    private void CreateGroupClick(object sender, RoutedEventArgs e)
    {
        var dialog = new GroupNameDialog(this, "Nouveau groupe", "", name => {
            var group = app.CreateGroup(name); RefreshGroups(); SelectView(group.Id.ToString());
        });
        dialog.ShowDialog();
    }

    private void RenameGroup(Guid id)
    {
        if (app.Store.Groups.FirstOrDefault(g => g.Id == id) is not { } group) return;
        if (new GroupNameDialog(this, "Renommer le groupe", group.Name, name => app.RenameGroup(group.Id, name)).ShowDialog() == true) Refresh();
    }

    private void DeleteGroup(Guid id)
    {
        if (app.Store.Groups.FirstOrDefault(g => g.Id == id) is not { } group) return;
        if (AppDialog.Show(this, $"Supprimer le groupe « {group.Name} » ?\nSes notes seront déplacées vers Sans groupe, sans être supprimées.",
            "Supprimer le groupe", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        app.DeleteGroup(group.Id); Refresh();
    }

    private void ShowGroupClick(object sender, RoutedEventArgs e) => app.ShowNotes(app.Store.Notes.Where(n => !n.Deleted && MatchesGroup(n)).ToList());
    private void HideGroupClick(object sender, RoutedEventArgs e) => app.HideNotes(app.Store.Notes.Where(n => !n.Deleted && MatchesGroup(n)).ToList());
    private void MoveGroupClick(object sender, RoutedEventArgs e)
    {
        var submenu = app.BuildGroupMenu(Selected());
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender };
        while (submenu.Items.Count > 0) { var item = submenu.Items[0]; submenu.Items.RemoveAt(0); menu.Items.Add(item); }
        menu.IsOpen = true;
    }

    public ContextMenu BuildGroupContextMenu(Guid? id)
    {
        var menu = new ContextMenu();
        void Add(string label, Action action)
        {
            var item = new MenuItem { Header = label, InputGestureText = label.StartsWith("Renommer") ? "F2" : "", Icon = NoteIcons.Action(label) };
            item.Click += (_, _) => { if (id != null && !app.Store.Groups.Any(g => g.Id == id)) return; action(); };
            menu.Items.Add(item);
        }
        List<Note> Members() => app.Store.Notes.Where(n => !n.Deleted && n.GroupId == id).ToList();
        Add("Afficher les notes du groupe", () => app.ShowNotes(Members()));
        Add("Masquer les notes du groupe", () => app.HideNotes(Members()));
        if (id is { } groupId && app.Store.Groups.Any(g => g.Id == groupId))
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(BuildGroupColorMenu(groupId));
            Add("Renommer…", () => RenameGroup(groupId));
            menu.Items.Add(new Separator());
            Add("Supprimer le groupe…", () => DeleteGroup(groupId));
        }
        return menu;
    }

    private Guid? contextGroup;
    private static GroupChoice? GroupAt(DependencyObject? node)
    {
        while (node != null && node is not ListBoxItem)
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        return (node as ListBoxItem)?.DataContext as GroupChoice;
    }
    private void GroupsMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right) return;
        contextGroup = Guid.TryParse(GroupAt(e.OriginalSource as DependencyObject)?.Key, out var id) ? id : null;
        e.Handled = true; // Do not let ListBox change the current view or note selection.
    }
    private void GroupsContextOpening(object sender, ContextMenuEventArgs e)
    {
        var id = contextGroup;
        if (e.CursorLeft == -1)
            id = Guid.TryParse(GroupAt(Keyboard.FocusedElement as DependencyObject)?.Key, out var focused) ? focused : null;
        var menu = id != null ? BuildGroupContextMenu(id) : new ContextMenu();
        if (id == null)
        {
            var create = new MenuItem { Header = "Nouveau groupe…", Icon = NoteIcons.Action("Nouveau groupe…") }; create.Click += CreateGroupClick; menu.Items.Add(create);
        }
        GroupList.ContextMenu.Items.Clear();
        while (menu.Items.Count > 0) { var item = menu.Items[0]; menu.Items.RemoveAt(0); GroupList.ContextMenu.Items.Add(item); }
    }
    public MenuItem BuildGroupColorMenu(Guid id)
    {
        var menu = new MenuItem { Header = "Couleur du dossier", Icon = NoteIcons.Action("Couleur du dossier") };
        var current = app.Store.Groups.First(g => g.Id == id).Color;
        foreach (var (name, color) in new[] { ("Gris", "#73777E"), ("Bleu", "#245BC4"), ("Vert", "#35804B"), ("Jaune", "#B8860B"), ("Orange", "#C86523"), ("Rouge", "#BD4040"), ("Rose", "#BA4D8A"), ("Violet", "#8054BA") })
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = string.Equals(current, color, StringComparison.OrdinalIgnoreCase),
                Icon = NoteIcons.FolderIcon(color) };
            item.Click += (_, _) => { if (!app.Store.Groups.Any(g => g.Id == id)) return; app.SetGroupColor(id, color); Refresh(); };
            menu.Items.Add(item);
        }
        return menu;
    }

    public ContextMenu BuildTrashMenu()
    {
        var menu = new ContextMenu();
        var empty = new MenuItem { Header = "Vider la corbeille…", Icon = NoteIcons.Action("Vider la corbeille…"), IsEnabled = app.Store.Notes.Any(n => n.Deleted) };
        empty.Click += EmptyTrashClick;
        menu.Items.Add(empty);
        var purge = new MenuItem { Header = "Purge automatique…", Icon = NoteIcons.Action("Purge automatique…") };
        purge.Click += (_, _) => { new PurgeSettingsDialog(this, app).ShowDialog(); Refresh(); };
        menu.Items.Add(purge);
        return menu;
    }

    private void TrashContextOpening(object sender, ContextMenuEventArgs e)
    {
        var source = BuildTrashMenu();
        TrashButton.ContextMenu.Items.Clear();
        while (source.Items.Count > 0) { var item = source.Items[0]; source.Items.RemoveAt(0); TrashButton.ContextMenu.Items.Add(item); }
    }

    public ContextMenu BuildNotesMenu()
    {
        var menu = new ContextMenu();
        var selection = Selected();
        void Add(string title, Action action, bool enabled)
        {
            var item = new MenuItem { Header = title, InputGestureText = title.StartsWith("Renommer") ? "F2" : title == "Tout sélectionner" ? "Ctrl+A" : title == "Mettre à la corbeille" ? "Suppr" : "", Icon = NoteIcons.Action(title), IsEnabled = enabled };
            item.Click += (_, _) => { action(); Refresh(); }; menu.Items.Add(item);
        }
        if (InTrash)
        {
            Add("Restaurer", () => app.RestoreNotes(selection), selection.Count > 0);
            Add("Supprimer définitivement la sélection…", () => app.ConfirmPermanentDeletion(selection), selection.Count > 0);
        }
        else
        {
            Add("Renommer…", () => app.RenameNote(selection[0], this), selection.Count == 1);
            Add("Afficher", () => app.ShowNotes(selection), selection.Count > 0);
            Add("Masquer", () => app.HideNotes(selection), selection.Count > 0);
            var groups = app.BuildGroupMenu(selection); groups.Header = "Déplacer vers"; groups.IsEnabled = selection.Count > 0; menu.Items.Add(groups);
            Add("Réorganiser en cascade", () => app.ShowNotes(selection, rearrange: true), selection.Count > 0);
            menu.Items.Add(new Separator());
            Add("Mettre à la corbeille", () => app.DeleteNotes(selection), selection.Count > 0);
        }
        menu.Items.Add(new Separator());
        Add("Captures…", () => ShowCaptures(selection[0]), selection.Count == 1 && !selection[0].IsProtected);
        Add("Tout sélectionner", () => NoteList.SelectAll(), rows.Count > 0);
        Add("Désélectionner", () => NoteList.UnselectAll(), selection.Count > 0);
        Add("Verrouiller toutes les notes protégées", app.LockProtectedNotes, app.Store.Notes.Any(n => n.IsProtected && !n.IsLocked));
        return menu;
    }

    private void NotesContextOpening(object sender, ContextMenuEventArgs e)
    {
        var source = BuildNotesMenu();
        NoteList.ContextMenu.Items.Clear();
        while (source.Items.Count > 0) { var item = source.Items[0]; source.Items.RemoveAt(0); NoteList.ContextMenu.Items.Add(item); }
    }

    private void NotesRightClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right) return;
        var node = e.OriginalSource as DependencyObject;
        while (node != null && node is not DataGridRow && node != NoteList)
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        if (node is DataGridRow { Item: Row row })
        {
            if (!NoteList.SelectedItems.Contains(row)) { NoteList.UnselectAll(); NoteList.SelectedItem = row; }
        }
        else NoteList.UnselectAll();
        e.Handled = true;
    }
}
