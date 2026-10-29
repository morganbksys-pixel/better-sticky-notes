using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BetterStickyNotes;

internal static class ContextMenuTests
{
    public static void Run(App app, string data)
    {
        var group = app.CreateGroup("Projet");
        var one = new Note { Title = "Un", Hidden = true };
        var two = new Note { Title = "Deux", Hidden = true };
        app.Store.Notes.AddRange([one, two]);
        var manager = new NotesWindow(app);
        var grid = (DataGrid)manager.FindName("NoteList");
        var content = (FrameworkElement)manager.Content;
        content.Measure(new Size(980, 620)); content.Arrange(new Rect(0, 0, 980, 620)); content.UpdateLayout();
        grid.SelectAll();
        var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(grid.Items[0]);
        row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.PreviewMouseDownEvent });
        Check(grid.SelectedItems.Count == 2, "Clic droit sur une ligne sélectionnée conserve la sélection multiple");
        grid.UnselectAll(); grid.SelectedItem = grid.Items[1];
        row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.PreviewMouseDownEvent });
        Check(grid.SelectedItems.Count == 1 && ((NotesWindow.Row)grid.SelectedItem).Note == one, "Clic droit sur une autre ligne cible uniquement cette note");
        grid.SelectAll();
        var move = Item(manager.BuildNotesMenu(), "Déplacer vers");
        ((MenuItem)move.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(one.GroupId == group.Id && two.GroupId == group.Id, "Menu contextuel affecte toute la sélection");
        manager.SelectView(group.Id.ToString());
        var palette = Item(manager.BuildGroupContextMenu(group.Id), "Couleur du dossier");
        palette.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Violet")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        app.RenameGroup(group.Id, "Renommé"); manager.RefreshPrivacy();
        Check(app.Store.Groups.Single().Color == "#8054BA", "Renommer conserve la couleur du dossier");
        Check(((ListBox)manager.FindName("GroupList")).Items.Cast<NotesWindow.GroupChoice>().Single().Color == "#8054BA", "Couleur transmise à la navigation");
        app.Store.Save(); var restored = new NoteStore(data); restored.Load();
        Check(restored.Groups.Single().Color == "#8054BA", "Couleur persistée au rechargement");
        var file = Path.Combine(data, "notes.json");
        File.WriteAllText(file, File.ReadAllText(file).Replace("#8054BA", "invalide")); restored.Load();
        Check(restored.Groups.Single().Color == "#73777E", "Couleur invalide remplacée par le gris");
        app.Store.Save();
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!;
        json["Groups"]![0]!.AsObject().Remove("Color"); File.WriteAllText(file, json.ToJsonString()); restored.Load();
        Check(restored.Groups.Single().Color == "#73777E", "Ancien groupe sans couleur chargé en gris");
        grid.SelectAll(); Item(manager.BuildNotesMenu(), "Mettre à la corbeille").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(one.Deleted && two.Deleted, "Corbeille depuis le menu de sélection");
        manager.SelectView("trash"); grid.SelectAll(); Item(manager.BuildNotesMenu(), "Restaurer").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(!one.Deleted && !two.Deleted && one.Hidden && two.Hidden, "Restauration contextuelle sans ouverture de fenêtre");
        manager.SelectView("all"); grid.UnselectAll();
        Check(!Item(manager.BuildNotesMenu(), "Afficher").IsEnabled, "Actions désactivées sans sélection");
        Check(manager.FindName("SelectionBar") == null, "Aucune barre de boutons en bas");
        manager.Close(); app.Store.Notes.Clear(); app.Store.Groups.Clear();
    }
    private static MenuItem Item(ContextMenu menu, string header) => menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, header));
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
