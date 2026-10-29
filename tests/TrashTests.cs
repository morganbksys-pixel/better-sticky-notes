using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BetterStickyNotes;

internal static class TrashTests
{
    public static void Run(App app, string folder)
    {
        var migration = new NoteStore(Path.Combine(folder, "trash-migration"));
        migration.Notes.Add(new Note { Deleted = true }); migration.Save(); migration.Load();
        var date = migration.Notes.Single().DeletedAtUtc;
        Check(date != null && date > DateTimeOffset.UtcNow.AddMinutes(-1), "Ancienne corbeille : date de départ récente");
        migration.Load(); Check(migration.Notes.Single().DeletedAtUtc == date, "Date migrée persistée entre les démarrages");
        Check(migration.TrashRetentionDays == null, "Purge désactivée par défaut");
        migration.SaveTrashRetention(7); migration.Load(); Check(migration.TrashRetentionDays == 7, "Délai de purge persisté");
        File.WriteAllText(Path.Combine(folder, "trash-migration", "settings.json"), "invalid");
        migration.Load(); Check(migration.TrashRetentionDays == null, "Réglage endommagé : purge désactivée");

        var now = DateTimeOffset.UtcNow;
        var expired = new Note { Deleted = true, DeletedAtUtc = now.AddDays(-8) };
        var boundary = new Note { Deleted = true, DeletedAtUtc = now.AddDays(-7) };
        var recent = new Note { Deleted = true, DeletedAtUtc = now.AddDays(-6) };
        var active = new Note { Title = "Active" };
        app.Store.Notes.AddRange([expired, boundary, recent, active]);
        app.RunTrashPurge(now); Check(app.Store.Notes.Contains(expired), "Purge désactivée : aucune suppression");
        app.Store.SaveTrashRetention(7); app.RunTrashPurge(now);
        Check(!app.Store.Notes.Contains(expired) && !app.Store.Notes.Contains(boundary) && app.Store.Notes.Contains(recent) && app.Store.Notes.Contains(active), "Purge limitée aux notes expirées, borne incluse");
        foreach (var file in new[] { "notes.json", "notes.json.bak" })
            Check(!File.ReadAllText(Path.Combine(folder, "app", file)).Contains(expired.Id.ToString()), "Purge effective dans le fichier et la sauvegarde");
        app.RestoreNotes([recent]); Check(recent.DeletedAtUtc == null && !recent.Deleted, "Restauration annule le délai");
        app.DeleteNotes([recent]); var deletedAt = recent.DeletedAtUtc;
        Check(deletedAt > now.AddSeconds(-1), "Nouvelle mise à la corbeille : nouveau délai");
        app.DeleteNotes([recent]); Check(recent.DeletedAtUtc == deletedAt, "Action répétée sur une note supprimée : date conservée");
        app.Store.SaveTrashRetention(null);
        app.PermanentlyDeleteNotes([recent]); app.Store.Notes.Remove(active);

        var first = new Note { Title = "Sélection un", Hidden = true };
        var second = new Note { Title = "Sélection deux", Hidden = true };
        app.Store.Notes.AddRange([first, second]);
        var manager = new NotesWindow(app) { Opacity = 0 };
        manager.Show(); manager.Activate(); PerformanceChecks.Pump();
        var list = (DataGrid)manager.FindName("NoteList");
        var search = (TextBox)manager.FindName("SearchBox");
        search.Text = "Sélection";
        Check(list.SelectionMode == DataGridSelectionMode.Extended && list.SelectionUnit == DataGridSelectionUnit.FullRow && list.IsReadOnly, "Sélection native étendue par lignes");
        list.Focus(); ApplicationCommands.SelectAll.Execute(null, list);
        Check(list.SelectedItems.Count == 2 && ((TextBlock)manager.FindName("SelectionInfo")).Text.StartsWith("2"), "Sélection native et compteur synchronisés");
        first.Color = "#D9EEC4"; app.Changed(); manager.RefreshPrivacy();
        Check(list.SelectedItems.Count == 2, "Actualisation conserve la sélection native");
        list.UpdateLayout();
        var firstRow = (DataGridRow)list.ItemContainerGenerator.ContainerFromItem(list.Items[0]);
        var visibility = Descendants(firstRow).OfType<Button>().First();
        visibility.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        Check(!first.Hidden && second.Hidden && list.SelectedItems.Count == 2, "Bouton de visibilité : une seule note, sélection multiple conservée");
        firstRow.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
        PerformanceChecks.Pump();
        Check(!first.Hidden && second.Hidden && !first.Pinned && app.Windows.OfType<NoteWindow>().Any(w => w.IsActive), "Double-clic : note au premier plan sans masquer ni épingler");
        app.HideNotes([first]);
        manager.Activate();
        firstRow.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
        Check(!first.Hidden && second.Hidden, "Double-clic sur une note masquée : ouverture de cette note uniquement");
        app.HideNotes([first]); manager.Activate();
        search.Focus();
        search.RaiseEvent(DeleteKey(search));
        Check(!first.Deleted && !second.Deleted, "Suppr dans la recherche ne supprime aucune note");
        list.Focus(); list.SelectAll();
        list.RaiseEvent(DeleteKey(list));
        Check(first.Deleted && second.Deleted && first.DeletedAtUtc != null && list.Items.Count == 0, "Suppr dans la liste : sélection envoyée à la corbeille sans confirmation");
        ((TabControl)manager.FindName("NoteTabs")).SelectedIndex = 1;
        var enabled = (CheckBox)manager.FindName("AutoPurge");
        var days = (TextBox)manager.FindName("PurgeDays");
        var apply = Descendants((StackPanel)manager.FindName("PurgeSettings")).OfType<Button>().Single();
        enabled.IsChecked = true; days.Text = "0"; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(app.Store.TrashRetentionDays == null, "Réglage de purge invalide refusé");
        days.Text = "2"; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(app.Store.TrashRetentionDays == 2 && app.Store.Notes.Contains(first), "Activation de purge depuis l’interface sans supprimer les notes récentes");
        enabled.IsChecked = false; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(app.Store.TrashRetentionDays == null, "Désactivation de purge depuis l’interface");
        manager.Close(); app.PermanentlyDeleteNotes([first, second]); PerformanceChecks.Wait(app.SaveNowAsync());
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static KeyEventArgs DeleteKey(Visual target) => new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, Environment.TickCount, Key.Delete) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
