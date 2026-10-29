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
        manager.SelectView("trash");
        var settings = new PurgeSettingsDialog(manager, app);
        var enabled = (CheckBox)settings.FindName("AutoPurge");
        var days = (TextBox)settings.FindName("PurgeDays");
        var apply = (Button)settings.FindName("ApplyButton");
        enabled.IsChecked = true; days.Text = "0"; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(app.Store.TrashRetentionDays == null, "Réglage de purge invalide refusé");
        days.Text = "2"; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(app.Store.TrashRetentionDays == 2 && app.Store.Notes.Contains(first), "Activation de purge depuis l’interface sans supprimer les notes récentes");
        enabled.IsChecked = false; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(app.Store.TrashRetentionDays == null, "Désactivation de purge depuis l’interface");
        settings.Close();
        var emptyButton = (Button)manager.FindName("EmptyTrashButton");
        Check(emptyButton.Visibility == Visibility.Visible && emptyButton.IsEnabled, "Vider visible sans sélection dans la corbeille");
        Check(!manager.BuildNotesMenu().Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Vider la corbeille…") || Equals(i.Header, "Purge automatique…")), "Menu de sélection sans actions globales de corbeille");
        var pluralMessage = Confirm(() => app.ConfirmPermanentDeletion([first, second]), "Annuler", "Supprimer 2 notes");
        Check(pluralMessage.Contains("les 2 notes sélectionnées"), "Suppression au pluriel");
        search.Text = "Sélection un"; list.SelectAll();
        var message = Confirm(() => emptyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), "Annuler", "Vider la corbeille (2 notes)");
        Check(message.Contains("2 notes") && message.Contains("même si une recherche est active") && app.Store.Notes.Contains(first) && app.Store.Notes.Contains(second), "Vider : confirmation du total filtré et annulation sans suppression");
        message = Confirm(() => manager.BuildNotesMenu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Supprimer définitivement la sélection…")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)), "Supprimer 1 note", "Supprimer 1 note");
        Check(message.Contains("Supprimer définitivement cette note ?") && !app.Store.Notes.Contains(first) && app.Store.Notes.Contains(second), "Suppression définitive limitée à la sélection filtrée");
        message = Confirm(app.ConfirmEmptyTrash, "Annuler", "Vider la corbeille (1 note)");
        Check(message.Contains("la note de la corbeille") && app.Store.Notes.Contains(second), "Vidage au singulier et annulation");
        var secret = new Note { Title = "Secret", Deleted = true, Hidden = true };
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        NoteProtection.Protect(secret, key, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)));
        NoteProtection.Lock(secret); System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        var kept = new Note { Title = "À conserver", Hidden = true };
        app.Store.Notes.AddRange([secret, kept]);
        manager.SelectView("all"); list.SelectAll();
        Check(emptyButton.Visibility == Visibility.Collapsed && ((Button)manager.FindName("TrashButton")).ContextMenu != null, "Menu corbeille disponible hors de sa vue et bouton masqué");
        message = Confirm(() => manager.BuildTrashMenu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Vider la corbeille…")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)), "Vider la corbeille (2 notes)", "Vider la corbeille (2 notes)");
        Check(message.Contains("2 notes") && !message.Contains("Secret") && !app.Store.Notes.Contains(second) && !app.Store.Notes.Contains(secret) && app.Store.Notes.Contains(kept), "Vider depuis une autre vue inclut les notes verrouillées et préserve les notes actives");
        Check(list.SelectedItems.Count == 1, "Vider la corbeille préserve la sélection active");
        manager.SelectView("trash");
        Check(!emptyButton.IsEnabled && !manager.BuildTrashMenu().Items.OfType<MenuItem>().First().IsEnabled && manager.BuildTrashMenu().Items.OfType<MenuItem>().Last().IsEnabled, "Corbeille vide : vidage désactivé et purge accessible");
        manager.Close(); app.Store.Notes.Remove(kept); PerformanceChecks.Wait(app.SaveNowAsync());
    }
    private static string Confirm(Action action, string answer, string expectedAction)
    {
        var message = "";
        var safeButtons = false;
        Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "Suppression définitive");
            var buttons = Descendants(dialog).OfType<Button>().ToList();
            var cancel = buttons.Single(b => Equals(b.Content, "Annuler"));
            var confirm = buttons.Single(b => Equals(b.Content, expectedAction));
            safeButtons = cancel.IsDefault && cancel.IsCancel && !confirm.IsDefault && confirm.Background is SolidColorBrush background && background.Color == Color.FromRgb(180, 35, 24) && confirm.Foreground == Brushes.White;
            message = string.Join(" ", Descendants(dialog).OfType<TextBlock>().Select(t => t.Text));
            Descendants(dialog).OfType<Button>().Single(b => Equals(b.Content, answer)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        action();
        Check(safeButtons && !message.Contains("note(s)"), "Confirmation : action chiffrée rouge, annulation par défaut et texte accordé");
        return message;
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
