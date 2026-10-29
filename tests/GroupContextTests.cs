using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BetterStickyNotes;

internal static class GroupContextTests
{
    public static void Run(App app)
    {
        var first = app.CreateGroup("Affiché"); var target = app.CreateGroup("Ciblé");
        var one = new Note { Title = "Un", GroupId = first.Id, Hidden = false };
        var two = new Note { Title = "Deux", GroupId = target.Id, Hidden = false };
        var loose = new Note { Title = "Libre", Hidden = false };
        app.Store.Notes.AddRange([one, two, loose]);
        var manager = new NotesWindow(app); manager.SelectView(first.Id.ToString());
        var list = (DataGrid)manager.FindName("NoteList"); list.SelectAll();
        var groups = (ListBox)manager.FindName("GroupList");
        var content = (FrameworkElement)manager.Content;
        content.Measure(new Size(980, 620)); content.Arrange(new Rect(0, 0, 980, 620)); content.UpdateLayout();
        var choice = groups.Items.Cast<NotesWindow.GroupChoice>().Single(g => g.Key == target.Id.ToString());
        var item = (ListBoxItem)groups.ItemContainerGenerator.ContainerFromItem(choice);
        item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.PreviewMouseDownEvent });
        Check((string)groups.SelectedValue == first.Id.ToString() && list.SelectedItems.Count == 1, "Clic droit sur un autre groupe préserve la vue et la sélection");
        var menu = manager.BuildGroupContextMenu(target.Id);
        Item(menu, "Masquer les notes du groupe").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(two.Hidden && !one.Hidden && !loose.Hidden, "Masquage limité au groupe ciblé");
        Item(menu, "Couleur du dossier").Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Vert")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(app.Store.Groups.Single(g => g.Id == target.Id).Color == "#35804B" && app.Store.Groups.Single(g => g.Id == first.Id).Color == "#73777E", "Couleur appliquée au groupe ciblé, pas au groupe affiché");
        Check((string)groups.SelectedValue == first.Id.ToString() && list.SelectedItems.Count == 1, "Personnalisation conserve la sélection de notes");
        Check(Item(manager.BuildGroupContextMenu(target.Id), "Couleur du dossier").Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Vert")).IsChecked, "Palette indique la couleur courante");
        Check(Item(menu, "Renommer…").IsEnabled && Item(menu, "Supprimer le groupe…").IsEnabled, "Gestion disponible dans le menu du dossier");
        Check(manager.FindName("ViewMenuButton") == null, "Aucun menu superflu dans l’en-tête");
        var ungrouped = manager.BuildGroupContextMenu(null);
        Check(ungrouped.Items.Count == 2, "Sans groupe : uniquement afficher et masquer");
        Item(ungrouped, "Masquer les notes du groupe").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(loose.Hidden && !one.Hidden, "Sans groupe cible seulement les notes non classées");
        // A menu may outlive its group if another window removes it.
        app.DeleteGroup(target.Id);
        Item(menu, "Couleur du dossier").Items.OfType<MenuItem>().First().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(!app.Store.Groups.Any(g => g.Id == target.Id), "Menu périmé sans recréation ni erreur");
        manager.Close(); app.Store.Notes.Clear(); app.Store.Groups.Clear();
    }
    private static MenuItem Item(ContextMenu menu, string label) => menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, label));
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
