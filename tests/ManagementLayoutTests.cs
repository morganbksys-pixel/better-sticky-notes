using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BetterStickyNotes;

internal static class ManagementLayoutTests
{
    public static void Run(App app)
    {
        var group = app.CreateGroup("Un projet avec un nom volontairement très long pour vérifier la navigation");
        var note = new Note { Title = "Une note de projet", Text = "Un extrait", GroupId = group.Id, Hidden = true };
        var deleted = new Note { Title = "Note supprimée", Deleted = true, Hidden = true };
        app.Store.Notes.AddRange([note, deleted]);
        var manager = new NotesWindow(app);
        var list = (DataGrid)manager.FindName("NoteList");
        var selection = (TextBlock)manager.FindName("SelectionInfo");
        var search = (TextBox)manager.FindName("SearchBox");
        Check(list.SelectedItems.Count == 0, "Gestion : actions masquées sans sélection");
        Check(list.Items.Cast<NotesWindow.Row>().Single().Detail.Contains(group.Name), "Toutes les notes : groupe visible dans le détail");
        ((Button)manager.FindName("UngroupedButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(list.Items.Count == 0 && ((StackPanel)manager.FindName("EmptyState")).Visibility == Visibility.Visible, "Sans groupe : message de vue vide");
        ((ListBox)manager.FindName("GroupList")).SelectedValue = group.Id.ToString();
        Check(((TextBlock)manager.FindName("ViewTitle")).Text == group.Name && list.Items.Cast<NotesWindow.Row>().Single().Detail == "Un extrait", "Navigation de groupe : titre et extrait adaptés");
        list.SelectAll(); Check(list.SelectedItems.Count == 1, "Sélection : compteur mis à jour");
        var content = (FrameworkElement)manager.Content;
        foreach (var size in new[] { new Size(724, 440), new Size(980, 620) })
        {
            content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
            Check(list.ActualHeight >= 100 && list.ActualWidth >= 300, "Disposition compacte : liste de notes utilisable");
            foreach (var name in new[] { "GroupList", "NewNoteButton", "SelectionInfo", "SearchBox", "TrashButton" })
            {
                var element = (FrameworkElement)manager.FindName(name);
                var bounds = element.TransformToAncestor(content).TransformBounds(new Rect(element.RenderSize));
                Check(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= size.Width + 1 && bounds.Bottom <= size.Height + 1,
                    "Disposition sans débordement : " + name);
            }
        }
        search.Text = "introuvable";
        Check(list.SelectedItems.Count == 0 && ((Button)manager.FindName("EmptyAction")).Content.Equals("Effacer la recherche"), "Recherche vide : sélection annulée et action pour effacer");
        ((Button)manager.FindName("EmptyAction")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(search.Text == "" && list.Items.Count == 1, "Effacer la recherche conserve le groupe");
        ((Button)manager.FindName("TrashButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((Button)manager.FindName("NewNoteButton")).Visibility == Visibility.Collapsed && list.Items.Cast<NotesWindow.Row>().Single().Note == deleted, "Corbeille autonome : toutes les notes supprimées et aucune création");
        Check(manager.BuildNotesMenu().Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Purge automatique…")), "Réglage de purge accessible depuis le menu de la corbeille");
        list.SelectAll();
        Check(manager.BuildNotesMenu().Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Restaurer")) && !manager.BuildNotesMenu().Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Afficher")), "Corbeille : actions de sélection adaptées");
        ((Button)manager.FindName("AllNotesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(list.SelectedItems.Count == 0 && list.Items.Count == 1, "Changer de vue remet la sélection à zéro");
        manager.Close(); app.Store.Notes.Clear(); app.Store.Groups.Clear(); app.Changed();
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
