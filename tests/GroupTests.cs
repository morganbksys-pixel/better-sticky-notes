using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using BetterStickyNotes;

internal static class GroupTests
{
    public static void Run(App app, string folder)
    {
        var data = Path.Combine(folder, "groups-migration");
        Directory.CreateDirectory(data);
        var original = new Note { Title = "Ancienne note", Text = "Texte conservé", Left = -500, Pinned = true };
        File.WriteAllText(Path.Combine(data, "notes.json"), JsonSerializer.Serialize(new[] { original }));
        var migrated = new NoteStore(data); migrated.Load();
        Check(migrated.Groups.Count == 0 && migrated.Notes[0].GroupId == null && migrated.Notes[0].Left == -500 && migrated.Notes[0].Pinned,
            "Ancien format accepté sans perte de position ni d’épinglage");
        var storedGroup = new NoteGroup(Guid.NewGuid(), "Projet");
        migrated.Groups.Add(storedGroup); migrated.Notes[0].GroupId = storedGroup.Id; migrated.Save();
        var loaded = new NoteStore(data); loaded.Load();
        Check(loaded.Groups.Single() == storedGroup && loaded.Notes.Single().GroupId == storedGroup.Id && loaded.Notes[0].Text == original.Text,
            "Migration vers le document versionné : groupes et notes sauvegardés ensemble");
        loaded.Save();
        File.WriteAllText(Path.Combine(data, "notes.json"), "broken");
        var recovered = new NoteStore(data); recovered.Load();
        Check(recovered.Groups.Single() == storedGroup && recovered.Notes[0].GroupId == storedGroup.Id && recovered.LoadWarning != null,
            "Récupération cohérente des groupes et des notes depuis la sauvegarde");
        recovered.Notes[0].GroupId = Guid.NewGuid(); recovered.Save(); recovered.Load();
        Check(recovered.Notes[0].GroupId == null, "Groupe introuvable : note replacée dans Sans groupe");
        var future = "{\"Version\":99,\"Notes\":[],\"Groups\":[]}";
        File.WriteAllText(Path.Combine(data, "notes.json"), future);
        try { new NoteStore(data).Load(); throw new Exception("Version inconnue acceptée"); }
        catch (NotSupportedException) { }
        Check(File.ReadAllText(Path.Combine(data, "notes.json")) == future, "Version future refusée sans remplacer le fichier par une ancienne sauvegarde");

        var group = app.CreateGroup("  WMS  ");
        var other = app.CreateGroup("Perso");
        Check(group.Name == "WMS", "Nom du groupe normalisé");
        foreach (var name in new[] { "wms", " ", "Sans groupe", new string('a', 81) })
        {
            try { app.CreateGroup(name); throw new Exception("Nom invalide accepté : " + name); }
            catch (ArgumentException) { }
        }
        var note = new Note { Title = "Été", Text = "Déploiement", GroupId = group.Id, Hidden = true, Pinned = true, Left = 456 };
        var personal = new Note { Title = "Courses", GroupId = other.Id, Hidden = true };
        var deleted = new Note { Title = "Ancienne", GroupId = group.Id, Deleted = true, Hidden = true };
        var secret = new Note { Title = "Secret", Text = "Confidentiel", GroupId = group.Id, Hidden = true };
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        NoteProtection.Protect(secret, key, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)));
        app.Store.Notes.AddRange([note, personal, deleted, secret]);
        var manager = new NotesWindow(app);
        var groups = (ListBox)manager.FindName("GroupList");
        var list = (DataGrid)manager.FindName("NoteList");
        var search = (TextBox)manager.FindName("SearchBox");
        groups.SelectedValue = group.Id.ToString();
        Check(list.Items.Count == 2 && list.Items.Cast<NotesWindow.Row>().All(r => r.Note.GroupId == group.Id), "Filtre de groupe exclut les autres groupes et la corbeille");
        Check(list.Items.Cast<NotesWindow.Row>().Single(r => r.Note == secret).Title == "Secret", "Groupe : titre déverrouillé visible");
        list.SelectAll(); search.Text = "ETE";
        Check(list.Items.Count == 1 && list.SelectedItems.Count == 0, "Groupe et recherche sans accents se combinent et réinitialisent la sélection");
        search.Text = "secret"; Check(list.Items.Count == 1, "Recherche de groupe inclut les notes déverrouillées");
        search.Text = ""; list.SelectAll(); groups.SelectedValue = other.Id.ToString();
        Check(list.Items.Count == 1 && list.SelectedItems.Count == 0, "Changement de groupe réinitialise la sélection");
        search.Text = "absent";
        manager.BuildGroupContextMenu(other.Id).Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Afficher les notes du groupe")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(!personal.Hidden && note.Hidden && secret.Hidden, "Afficher le groupe ignore la recherche et ne touche pas aux autres groupes");
        manager.BuildGroupContextMenu(other.Id).Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Masquer les notes du groupe")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(personal.Hidden, "Masquer le groupe masque ses notes");
        app.TrimHiddenWindows(expireAll: true); search.Text = "";
        app.RenameGroup(group.Id, "Travail"); manager.RefreshPrivacy();
        Check(app.Store.Groups.Single(g => g.Id == group.Id).Name == "Travail" && note.GroupId == group.Id, "Renommage préserve les associations");
        app.AssignGroup([note, personal], other.Id); manager.RefreshPrivacy();
        Check(list.Items.Count == 2 && note.Pinned && note.Left == 456 && note.Hidden, "Affectation multiple préserve visibilité, épinglage et position");
        var menu = app.BuildGroupMenu([note, personal]);
        Check(menu.Items.Cast<MenuItem>().Count(i => i.IsChecked) == 1, "Menu de groupe indique l’affectation commune");
        ((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(note.GroupId == null && personal.GroupId == null, "Menu permet de retirer le groupe de toute la sélection");
        app.AssignGroup([note], group.Id);
        groups.SelectedValue = group.Id.ToString();
        manager.SelectView("trash");
        Check(list.Items.Count == 1 && ((NotesWindow.Row)list.Items[0]).Note == deleted, "Corbeille accessible depuis la navigation");
        app.RestoreNotes([deleted]); manager.RefreshPrivacy();
        Check(deleted.GroupId == group.Id && deleted.Hidden, "Restauration conserve le groupe et laisse la note masquée");
        app.DeleteNotes([deleted]); app.DeleteGroup(group.Id); manager.RefreshPrivacy();
        Check(new[] { note, deleted, secret }.All(n => n.GroupId == null) && deleted.Deleted && !note.Deleted, "Suppression de groupe détache notes actives, protégées et supprimées sans les effacer");
        groups.SelectedValue = other.Id.ToString(); manager.SelectImported([note.Id]);
        Check(groups.SelectedItem == null && ((TextBlock)manager.FindName("ViewTitle")).Text == "Toutes les notes" && list.SelectedItems.Count == 1, "Import remet le filtre sur Toutes les notes et sélectionne les nouvelles notes");
        groups.SelectedValue = other.Id.ToString();
        ((Button)manager.FindName("NewNoteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var created = app.Store.Notes.Last();
        Check(created.GroupId == other.Id, "Création dans le groupe demandé");
        app.HideNotes([created]);
        PerformanceChecks.Wait(app.SaveNowAsync());
        var saved = new NoteStore(Path.Combine(folder, "app")); saved.Load();
        Check(saved.Groups.Single() == other && saved.Notes.Single(n => n.Id == secret.Id).IsLocked,
            "Persistance des groupes préserve le chiffrement et le verrouillage");
        NoteProtection.Lock(secret); System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        app.TrimHiddenWindows(expireAll: true);
        manager.Close(); app.Store.Notes.Clear(); app.Store.Groups.Clear();
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
