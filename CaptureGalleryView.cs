using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace BetterStickyNotes;

public sealed class CaptureGalleryView : Grid
{
    private readonly App app;
    private readonly Window owner;
    private readonly Action<Note> locateNote;
    private readonly ListBox cards = new() { SelectionMode = SelectionMode.Extended, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly TextBlock counter = new() { Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock empty = new() { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 20, 8, 8), IsHitTestVisible = false };
    private readonly RadioButton activeFilter, trashFilter;
    private readonly Button open, copy, export, locate, remove;
    private string? renderedState;
    private CaptureViewerWindow? viewer;
    private bool updating;
    public IReadOnlyList<CaptureReference> Entries { get; private set; } = [];
    public IReadOnlyList<CaptureReference> SelectedEntries
    {
        get
        {
            var selected = cards.SelectedItems.Cast<ListBoxItem>().Select(i => (CaptureReference)i.Tag).ToHashSet();
            return Entries.Where(selected.Contains).ToArray();
        }
    }
    public bool InTrash => trashFilter.IsChecked == true;

    public CaptureGalleryView(App app, Window owner, Action<Note> locateNote)
    {
        this.app = app; this.owner = owner; this.locateNote = locateNote;
        Margin = new Thickness(28, 28, 28, 18);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel();
        activeFilter = new RadioButton { Content = "Notes actives", IsChecked = true, ToolTip = "Captures des notes actives, même masquées sur le bureau" };
        trashFilter = new RadioButton { Content = "Corbeille", ToolTip = "Captures des notes supprimées : consultation et export" };
        activeFilter.SetResourceReference(StyleProperty, "CaptureGalleryFilterStyle");
        trashFilter.SetResourceReference(StyleProperty, "CaptureGalleryFilterStyle");
        var filters = new Border { Background = new SolidColorBrush(Color.FromRgb(246, 246, 245)), BorderBrush = new SolidColorBrush(Color.FromRgb(227, 229, 232)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(2), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { activeFilter, trashFilter } } };
        System.Windows.Automation.AutomationProperties.SetName(filters, "Captures des notes actives ou de la corbeille");
        DockPanel.SetDock(filters, Dock.Right); header.Children.Add(filters);
        header.Children.Add(new TextBlock { Text = "Captures", FontSize = 26, FontWeight = FontWeights.SemiBold });
        Children.Add(header);
        var tools = new WrapPanel();
        Button Action(string label, string hint, Action action)
        {
            var button = new Button { Content = label, ToolTip = hint, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 6, 6) };
            button.Click += (_, _) => action(); tools.Children.Add(button); return button;
        }
        open = Action("Ouvrir", "Entrée · Double-clic", OpenSelected);
        copy = Action("Copier", "Ctrl+C · Une image ou plusieurs fichiers", CopySelected);
        export = Action("Enregistrer…", "Ctrl+S · Une image ou un dossier pour la sélection", ExportSelected);
        locate = Action("Retrouver la note", "Sélectionner la note d’origine dans Mes notes", LocateSelected);
        remove = Action("Supprimer…", "Suppr · Supprimer uniquement les captures sélectionnées", async () => await DeleteSelectedAsync());
        var commands = new StackPanel { Children = { counter, tools } }; SetRow(commands, 1); Children.Add(commands);
        var panel = new FrameworkElementFactory(typeof(WrapPanel));
        cards.ItemsPanel = new ItemsPanelTemplate(panel);
        ScrollViewer.SetHorizontalScrollBarVisibility(cards, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(cards, ScrollBarVisibility.Auto);
        ScrollViewer.SetCanContentScroll(cards, false);
        KeyboardNavigation.SetDirectionalNavigation(cards, KeyboardNavigationMode.Contained);
        System.Windows.Automation.AutomationProperties.SetName(cards, "Galerie des captures · sélection multiple");
        cards.SelectionChanged += (_, _) => { if (!updating) UpdateActions(); };
        cards.PreviewKeyDown += async (_, e) =>
        {
            if (e.IsRepeat && e.Key is Key.Enter or Key.Delete) { e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A) cards.SelectAll();
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C) CopySelected();
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) ExportSelected();
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape) cards.UnselectAll();
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Enter) OpenSelected();
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Delete) { e.Handled = true; await DeleteSelectedAsync(); return; }
            else return; // Native ListBox navigation, Ctrl+click and Shift ranges.
            e.Handled = true;
        };
        cards.ContextMenu = new ContextMenu();
        cards.ContextMenuOpening += (_, _) => BuildMenu();
        var body = new Grid { Children = { cards, empty } }; SetRow(body, 2); Children.Add(body);
        var hint = new TextBlock { Text = "Clic : sélectionner · Ctrl/Maj+clic : sélection multiple · Flèches : naviguer\nEntrée : ouvrir · Ctrl+A : tout sélectionner · Échap : désélectionner · Ctrl+C : copier · Ctrl+S : enregistrer · Suppr : supprimer", FontSize = 11, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
        SetRow(hint, 3); Children.Add(hint); UpdateActions();
        activeFilter.Checked += FilterChanged;
        trashFilter.Checked += FilterChanged;
    }

    private void FilterChanged(object sender, RoutedEventArgs e) { Suspend(); Refresh(); }

    public void ShowTrash(bool trash)
    {
        var filter = trash ? trashFilter : activeFilter;
        if (filter.IsChecked != true) filter.IsChecked = true;
        else Refresh();
    }

    public void Refresh()
    {
        var entries = CaptureCatalog.ForGallery(app.Store, InTrash);
        var state = string.Join('|', entries.Select(c => $"{c.Note.Id}:{c.Capture.Id}:{c.Note.DisplayTitle}"));
        if (state == renderedState) return;
        var selected = SelectedEntries.Select(c => (c.Note.Id, c.Capture.Id)).ToHashSet();
        var focusId = cards.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.IsKeyboardFocusWithin)?.Tag as CaptureReference;
        var scroll = Descendants(cards).OfType<ScrollViewer>().FirstOrDefault();
        var offset = scroll?.VerticalOffset ?? 0;
        viewer?.Close(); viewer = null;
        renderedState = state; Entries = entries; updating = true;
        cards.Items.Clear();
        empty.Text = InTrash ? "Aucune capture dans la corbeille." : "Aucune capture pour le moment. Utilisez le bouton appareil photo en haut d’un post-it.";
        empty.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var entry in entries)
        {
            var content = new StackPanel();
            var preview = new Border { Height = 112, Background = new SolidColorBrush(Color.FromRgb(244, 245, 247)), CornerRadius = new CornerRadius(4), Padding = new Thickness(3) };
            try { preview.Child = new Image { Source = app.Store.LoadCapture(entry.Note, entry.Capture, thumbnail: true), Stretch = Stretch.Uniform }; }
            catch (Exception ex) when (ImageError(ex)) { preview.Child = new TextBlock { Text = "Image indisponible", VerticalAlignment = VerticalAlignment.Center }; }
            content.Children.Add(preview);
            content.Children.Add(new TextBlock { Text = entry.Note.DisplayTitle, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 8, 0, 4), ToolTip = entry.Note.DisplayTitle });
            content.Children.Add(new TextBlock { Text = $"{entry.Capture.CreatedAtUtc.ToLocalTime():dd/MM/yyyy à HH:mm:ss}\n{entry.Capture.Width} × {entry.Capture.Height} pixels", Foreground = Brushes.DimGray, FontSize = 11 });
            var item = new ListBoxItem { Tag = entry, Content = content, Width = 210, Padding = new Thickness(10), Margin = new Thickness(0, 0, 10, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            item.SetResourceReference(StyleProperty, "CaptureGalleryCardStyle");
            System.Windows.Automation.AutomationProperties.SetName(item, $"{entry.Note.DisplayTitle}, capture du {entry.Capture.CreatedAtUtc.ToLocalTime():g}");
            item.MouseDoubleClick += (_, e) => { if (e.ChangedButton == MouseButton.Left) { Open(entry); e.Handled = true; } };
            item.PreviewMouseRightButtonDown += (_, _) => { if (!item.IsSelected) { cards.UnselectAll(); item.IsSelected = true; } item.Focus(); };
            cards.Items.Add(item); item.IsSelected = selected.Contains((entry.Note.Id, entry.Capture.Id));
            if (focusId?.Note.Id == entry.Note.Id && focusId.Capture.Id == entry.Capture.Id) item.Focus();
        }
        updating = false; UpdateActions();
        cards.UpdateLayout(); scroll?.ScrollToVerticalOffset(offset);
    }

    private void UpdateActions()
    {
        var count = SelectedEntries.Count;
        counter.Text = $"{Entries.Count} capture(s) · {count} sélectionnée(s) · Plus récentes en premier" + (InTrash ? " · Corbeille : consultation uniquement" : "");
        open.IsEnabled = locate.IsEnabled = count == 1;
        copy.IsEnabled = export.IsEnabled = count > 0;
        remove.IsEnabled = count > 0 && !InTrash;
        remove.Visibility = InTrash ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BuildMenu()
    {
        var menu = cards.ContextMenu; menu.Items.Clear();
        void Add(string label, string gesture, bool enabled, Action action)
        {
            var item = new MenuItem { Header = label, InputGestureText = gesture, IsEnabled = enabled };
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        Add("Ouvrir", "Entrée", open.IsEnabled, OpenSelected);
        Add("Copier", "Ctrl+C", copy.IsEnabled, CopySelected);
        Add("Enregistrer…", "Ctrl+S", export.IsEnabled, ExportSelected);
        Add("Retrouver la note", "", locate.IsEnabled, LocateSelected);
        if (!InTrash) Add("Supprimer les captures…", "Suppr", remove.IsEnabled, async () => await DeleteSelectedAsync());
    }

    private void OpenSelected() { if (SelectedEntries is { Count: 1 } selected) Open(selected[0]); }
    private void LocateSelected() { if (SelectedEntries is { Count: 1 } selected) locateNote(selected[0].Note); }
    private void Open(CaptureReference entry)
    {
        try
        {
            viewer?.Close();
            var window = new CaptureViewerWindow(app.Store, entry.Note, entry.Capture, owner, Entries);
            window.Closed += (_, _) => { if (viewer == window) viewer = null; };
            viewer = window; window.Show();
        }
        catch (Exception ex) when (ImageError(ex)) { Error("Impossible d’ouvrir cette capture.", ex); }
    }

    private void CopySelected()
    {
        var selected = SelectedEntries;
        if (selected.Count == 0) return;
        try
        {
            if (selected.Count == 1) Clipboard.SetImage(app.Store.LoadCapture(selected[0].Note, selected[0].Capture));
            else
            {
                var paths = selected.Select(c => app.Store.CapturePath(c.Note, c.Capture)).ToArray();
                if (paths.Any(path => !File.Exists(path))) throw new FileNotFoundException("Une capture sélectionnée est introuvable.");
                var files = new StringCollection(); files.AddRange(paths);
                Clipboard.SetFileDropList(files);
            }
        }
        catch (Exception ex) when (ImageError(ex) || ex is ExternalException) { Error("Impossible de copier la sélection.", ex); }
    }

    private void ExportSelected()
    {
        var selected = SelectedEntries;
        if (selected.Count == 0) return;
        try
        {
            if (selected.Count == 1)
            {
                var entry = selected[0];
                var picker = new SaveFileDialog { Title = "Enregistrer la capture", FileName = FileName(entry), Filter = "Image PNG (*.png)|*.png", DefaultExt = ".png", AddExtension = true, OverwritePrompt = true };
                if (picker.ShowDialog(owner) == true) app.Store.ExportCapture(entry.Note, entry.Capture, picker.FileName);
            }
            else
            {
                var picker = new OpenFolderDialog { Title = $"Enregistrer les {selected.Count} captures dans un dossier" };
                if (picker.ShowDialog(owner) == true) ExportSelectionTo(picker.FolderName);
            }
        }
        catch (Exception ex) when (ImageError(ex)) { Error("Impossible d’enregistrer la sélection. Certains fichiers peuvent déjà avoir été copiés.", ex); }
    }

    public void ExportSelectionTo(string folder)
    {
        foreach (var entry in SelectedEntries)
        {
            var name = FileName(entry); var destination = Path.Combine(folder, name); var suffix = 2;
            while (File.Exists(destination) || Directory.Exists(destination)) destination = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + $" ({suffix++}).png");
            app.Store.ExportCapture(entry.Note, entry.Capture, destination, overwrite: false);
        }
    }

    public async Task DeleteSelectedAsync(Func<int, bool>? confirm = null)
    {
        if (InTrash) return;
        var selected = SelectedEntries.ToArray();
        if (selected.Length == 0) return;
        var accepted = confirm?.Invoke(selected.Length) ?? AppDialog.Show(owner, $"Supprimer définitivement {selected.Length} capture(s) sélectionnée(s) ?\n\nLes notes seront conservées. Cette action est irréversible.", "Supprimer les captures", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, "Supprimer les captures") == MessageBoxResult.Yes;
        if (!accepted) return;
        foreach (var group in selected.GroupBy(c => c.Note))
            await Dispatcher.InvokeAsync(() => app.RemoveCapturesAsync(group.Key, group.Select(c => c.Capture))).Task.Unwrap();
        await Dispatcher.InvokeAsync(Refresh);
    }

    private static string FileName(CaptureReference entry) => $"Capture-{entry.Capture.CreatedAtUtc.ToLocalTime():yyyyMMdd-HHmmss}-{entry.Capture.Id:N}.png";
    private void Error(string message, Exception ex) => AppDialog.Show(owner, message + "\n\n" + ex.Message, "Captures d’écran");
    private static bool ImageError(Exception ex) => ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or InvalidOperationException;
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    public void Suspend()
    {
        viewer?.Close(); viewer = null;
        cards.Items.Clear(); Entries = []; renderedState = null;
    }
}
