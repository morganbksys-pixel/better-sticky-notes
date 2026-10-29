using System.IO;
using System.Windows;
using System.Windows.Controls;
using BetterStickyNotes;

internal static class ManagementTests
{
    public static void Run(App app, string folder)
    {
        var notes = new[] {
            new Note { Title = "Été et déploiement", Text = "API à vérifier", Hidden = true },
            new Note { Title = "SQL", Text = "Réparer la requête", Hidden = true },
            new Note { Title = "Ancienne note", Deleted = true }
        };
        Check(NoteManagement.Matches(notes[0], "ETE") && NoteManagement.Matches(notes[1], "reparer") && !NoteManagement.Matches(notes[2], ""), "Recherche titre/contenu sans casse ni accents, hors corbeille");
        app.Store.Notes.AddRange(notes);
        var manager = new NotesWindow(app);
        manager.SelectImported([notes[0].Id, notes[1].Id]);
        var list = (DataGrid)manager.FindName("NoteList");
        Check(list.SelectedItems.Count == 2, "Sélection automatique des notes importées");
        ((TextBox)manager.FindName("SearchBox")).Text = "DEPLOIEMENT";
        Check(list.Items.Count == 1 && list.SelectedItems.Count == 0, "Recherche réinitialise la sélection et limite les résultats");
        app.HideNotes(notes.Take(2)); app.DeleteNotes([notes[0]]);
        PerformanceChecks.Wait(app.SaveNowAsync());
        Check(notes[0].Deleted && notes[0].Hidden && !notes[1].Deleted, "Actions groupées limitées aux notes ciblées");
        var loaded = new NoteStore(Path.Combine(folder, "app")); loaded.Load();
        Check(loaded.Notes.Single(n => n.Id == notes[0].Id).Deleted, "Suppression vers la corbeille persistée");
        manager.SelectView("trash");
        Check(list.Items.Count == 2 && list.Items.Cast<NotesWindow.Row>().All(r => r.Note.Deleted) && list.SelectedItems.Count == 0, "Onglet corbeille : uniquement les notes supprimées, sélection remise à zéro");
        Check(manager.BuildTrashMenu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Vider la corbeille…")).IsEnabled && !manager.BuildNotesMenu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Restaurer")).IsEnabled,
            "Vider la corbeille disponible sans sélection, restaurer désactivé");
        ((TextBox)manager.FindName("SearchBox")).Text = "ancienne";
        Check(list.Items.Count == 1, "Recherche limitée à la corbeille");
        list.SelectedItem = list.Items[0];
        manager.BuildNotesMenu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Restaurer")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(!notes[2].Deleted && notes[2].Hidden && list.Items.Count == 0, "Restauration depuis l’onglet sans ouvrir la note");
        manager.SelectView("all");
        Check(list.Items.Count == 2 && list.SelectedItems.Count == 0, "Retour aux notes actives : recherche et sélection réinitialisées");
        var secret = new Note { Title = "Confidentiel", Text = "Secret introuvable" };
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        NoteProtection.Protect(secret, key, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)));
        var secretRow = new NotesWindow.Row(secret);
        Check(!secret.IsLocked && secretRow.Title == "Confidentiel" && secretRow.Excerpt == "Secret introuvable" && NoteManagement.Matches(secret, "secret"),
            "Note protégée déverrouillée : titre, extrait et recherche disponibles");
        secret.Deleted = true;
        Check(NoteManagement.Matches(secret, "", true) && NoteManagement.Matches(secret, "confidentiel", true), "Note déverrouillée recherchable dans la corbeille");
        NoteProtection.Lock(secret); System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        manager.Close(); app.Store.Notes.Clear();
        foreach (var area in new[] { new Rect(0, 0, 1920, 1040), new Rect(-2560, 0, 2560, 1400), new Rect(0, 0, 800, 600) })
        {
            var positions = Enumerable.Range(0, 20).Select(i => NoteManagement.Cascade(area, new Size(340, 360), i)).ToList();
            Check(positions.All(p => area.Contains(new Rect(p, new Size(340, 360)))), "Cascade dans la zone utilisable, y compris écran à coordonnées négatives");
            Check(positions[0].X == area.Right - 340 - 16 && positions[0].Y == area.Bottom - 360 - 16, "Première position en bas à droite de la zone utilisable");
            Check(positions[1].X < positions[0].X && positions[1].Y < positions[0].Y, "Décalage visible vers le haut et la gauche");
        }
        var migration = new NoteStore(Path.Combine(folder, "migration"));
        migration.Notes.Add(new Note { WindowsStickyNoteId = "old-import", Left = 120, Top = 120 });
        migration.Notes.Add(new Note { WindowsStickyNoteId = "positioned", Left = 350, Top = 400 });
        migration.Save(); migration.Load();
        Check(migration.Notes[0].HasPlacement == false && migration.Notes[1].HasPlacement == true, "Anciennes notes importées à répartir, positions personnalisées préservées");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
