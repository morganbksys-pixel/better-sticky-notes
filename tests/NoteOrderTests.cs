using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BetterStickyNotes;
using Microsoft.Data.Sqlite;

internal static class NoteOrderTests
{
    public static void Run(App app, string folder)
    {
        var group = app.CreateGroup("Ordre chronologique");
        var first = new Note { Title = "Première", Hidden = true, GroupId = group.Id, CreatedAtUtc = DateTimeOffset.Parse("2026-01-01T12:00:00Z") };
        var last = new Note { Title = "Dernière", Hidden = true, GroupId = group.Id, CreatedAtUtc = first.CreatedAtUtc.Value.AddDays(1) };
        var legacy = new Note { Title = "Historique sans date", Hidden = true, GroupId = group.Id };
        app.Store.Notes.AddRange([last, legacy, first]);
        var home = new NotesWindow(app);
        try
        {
            home.SelectView(group.Id.ToString());
            var list = (DataGrid)home.FindName("NoteList");
            Check(list.Items.Cast<NotesWindow.Row>().Select(r => r.Note).SequenceEqual(new[] { last, first, legacy }), "Notes : plus récentes en premier, historique sans date après les notes datées");
            list.SelectedItem = list.Items[1]; first.Hidden = false; home.RefreshPrivacy();
            Check(((NotesWindow.Row)list.Items[1]).Note == first && ((NotesWindow.Row)list.SelectedItem).Note == first, "Notes : masquer/afficher et actualiser ne modifient ni ordre ni sélection");
            var newest = new Note { Title = "30/10", GroupId = group.Id, Hidden = true, CreatedAtUtc = DateTimeOffset.UtcNow };
            app.Store.Notes.Add(newest); home.RefreshPrivacy();
            Check(((NotesWindow.Row)list.Items[0]).Note == newest && ((NotesWindow.Row)list.SelectedItem).Note == first, "Nouvelle note 30/10 : ajout en haut sans modifier la sélection existante");
            newest.Title = "ZZZ"; home.RefreshPrivacy();
            Check(((NotesWindow.Row)list.Items[0]).Note == newest, "Le titre ne sert pas de critère alphabétique de tri");
            var store = new NoteStore(Path.Combine(folder, "note-order")); store.Notes.AddRange([last, legacy, first, newest]); store.Save();
            var loaded = new NoteStore(Path.Combine(folder, "note-order")); loaded.Load();
            Check(loaded.Notes.OrderByDescending(n => n.CreatedAtUtc ?? DateTimeOffset.MinValue).Select(n => n.Id).SequenceEqual(new[] { newest.Id, last.Id, first.Id, legacy.Id }), "Notes : dates et tri conservés après rechargement");
            File.WriteAllText(Path.Combine(folder, "note-order", "notes.json"), "[{\"Title\":\"Ancienne A\"},{\"Title\":\"Ancienne B\"}]");
            loaded.Load();
            Check(loaded.Notes.All(n => n.CreatedAtUtc == null && n.ModifiedAtUtc == null) && loaded.Notes.Select(n => n.Title).SequenceEqual(new[] { "Ancienne A", "Ancienne B" }), "Notes historiques : aucune date inventée ni inversion au chargement");
            TestImportDates(folder);
            TestEdits(app, folder);
        }
        finally { home.Close(); app.Store.Notes.RemoveAll(n => n.GroupId == group.Id); app.Store.Groups.Remove(group); }
    }

    private static void TestEdits(App app, string folder)
    {
        var group = app.CreateGroup("Dernières modifications");
        var created = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var old = new Note { Title = "Ordre ancienne", CreatedAtUtc = created, GroupId = group.Id, Hidden = true };
        var recent = new Note { Title = "Ordre récente", CreatedAtUtc = created.AddDays(1), GroupId = group.Id, Hidden = true };
        var legacy = new Note { Title = "Ordre historique", Text = "- [ ] Action ordre", GroupId = group.Id, Hidden = true };
        app.Store.Notes.AddRange([old, legacy, recent]);
        var home = new NotesWindow(app);
        var oldWindow = new NoteWindow(app, old);
        var recentWindow = new NoteWindow(app, recent);
        var legacyWindow = new NoteWindow(app, legacy);
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            home.SelectView(group.Id.ToString());
            var list = (DataGrid)home.FindName("NoteList");
            var search = (TextBox)home.FindName("SearchBox");
            Note First() => ((NotesWindow.Row)list.Items[0]).Note;
            Check(old.ModifiedAtUtc == null && recent.ModifiedAtUtc == null && legacy.ModifiedAtUtc == null, "Ouverture de l’éditeur : aucune modification inventée");
            list.SelectedItems.Add(list.Items.Cast<NotesWindow.Row>().Single(r => r.Note == old));
            list.SelectedItems.Add(list.Items.Cast<NotesWindow.Row>().Single(r => r.Note == legacy));
            oldWindow.SetTitle("Ordre titre modifié");
            // Exercise the existing deferred NotesChanged refresh, without forcing RefreshPrivacy.
            PerformanceChecks.Wait(Task.Delay(350));
            Check(First() == old && old.ModifiedAtUtc != null && old.CreatedAtUtc == created, "Renommer le titre du post-it fait remonter la note sans changer sa création");
            Check(list.SelectedItems.Cast<NotesWindow.Row>().Select(r => r.Note.Id).ToHashSet().SetEquals(new[] { old.Id, legacy.Id }), "Réordonner conserve la sélection multiple par identité");
            var edited = old.ModifiedAtUtc;
            app.UpdateNoteTitle(old, old.Title); app.UpdateNoteText(old, old.Text);
            oldWindow.SetCollapsed(true); oldWindow.SetCollapsed(false);
            oldWindow.Left += 10; oldWindow.Width += 10;
            app.HideNotes([old]); home.RefreshPrivacy();
            Check(old.ModifiedAtUtc == edited && First() == old, "Texte identique, repli, déplacement, taille et masquage ne changent pas la date");

            // Use older known dates between actions to avoid relying on timer precision.
            old.ModifiedAtUtc = created.AddDays(2);
            ((TextBox)recentWindow.FindName("Editor")).Text = "Contenu ordre modifié";
            home.SelectView("all"); search.Text = "ordre";
            Check(First() == recent, "Modifier le texte remonte la note dans toutes les notes et la recherche");
            home.SelectView(group.Id.ToString()); search.Text = "ordre";
            Check(First() == recent, "Les recherches dans un groupe utilisent aussi la dernière modification");
            recent.ModifiedAtUtc = created.AddDays(3);
            typeof(NoteWindow).GetMethod("ToggleTask", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(legacyWindow, [legacy.Text.IndexOf("[ ]") + 1, true]);
            home.RefreshPrivacy();
            Check(First() == legacy && legacy.Text.Contains("[x]") && legacy.CreatedAtUtc == null && legacy.ModifiedAtUtc != null, "Cocher une tâche fait remonter une note historique sans inventer sa création");

            legacy.ModifiedAtUtc = created.AddDays(4);
            var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
            var capture = app.Store.AddCapture(old, image); home.RefreshPrivacy();
            Check(First() == old, "Ajouter une capture fait remonter la note");
            old.ModifiedAtUtc = created.AddDays(5);
            PerformanceChecks.Wait(app.RemoveCapturesAsync(recent, [capture]));
            Check(recent.ModifiedAtUtc == created.AddDays(3), "Retirer une capture absente ne modifie pas la note");
            PerformanceChecks.Wait(app.RemoveCapturesAsync(old, [capture])); home.RefreshPrivacy();
            Check(First() == old && old.ModifiedAtUtc > created.AddDays(5), "Retirer une capture fait remonter la note");

            old.ModifiedAtUtc = created.AddDays(6);
            NoteProtection.Protect(recent, key, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));
            app.UpdateNoteText(recent, "Contenu ordre confidentiel"); home.RefreshPrivacy();
            Check(First() == recent, "Modifier une note protégée déverrouillée la fait remonter");
            var protectedDate = recent.ModifiedAtUtc;
            recentWindow.ClearSensitiveContent(); app.LockNote(recent); home.RefreshPrivacy();
            Check(recent.ModifiedAtUtc == protectedDate && list.Items.Cast<NotesWindow.Row>().All(r => r.Note != recent), "Verrouiller conserve la date et retire la note des résultats de recherche");
            search.Text = "";
            Check(First() == recent && ((NotesWindow.Row)list.Items[0]).Title == "Note protégée", "La note verrouillée reste anonyme dans le tri par modification");
            NoteProtection.Unlock(recent, key); home.RefreshPrivacy();
            Check(recent.ModifiedAtUtc == protectedDate && First() == recent, "Déverrouiller conserve la date et l’ordre");

            var store = new NoteStore(Path.Combine(folder, "note-edits")); store.Notes.AddRange([old, legacy, recent]); store.Groups.Add(group); store.Save();
            var loaded = new NoteStore(Path.Combine(folder, "note-edits")); loaded.Load();
            Check(loaded.Notes.Single(n => n.Id == recent.Id).ModifiedAtUtc == protectedDate && loaded.Notes.Single(n => n.Id == old.Id).CreatedAtUtc == created, "Création et dernière modification sont persistées, y compris pour une note protégée");
            Check(!File.ReadAllText(Path.Combine(folder, "note-edits", "notes.json")).Contains("confidentiel"), "Le contenu modifié reste chiffré à la sauvegarde");
            app.Store.Notes.RemoveAll(n => n.GroupId == group.Id); app.Store.Notes.AddRange(loaded.Notes); home.RefreshPrivacy();
            Check(list.Items.Cast<NotesWindow.Row>().Select(r => r.Note.Id).SequenceEqual(new[] { recent.Id, old.Id, legacy.Id }), "Le même ordre est retrouvé après rechargement des notes");
        }
        finally
        {
            home.Close(); oldWindow.ClosePermanently(); recentWindow.ClosePermanently(); legacyWindow.ClosePermanently();
            NoteProtection.Lock(recent); CryptographicOperations.ZeroMemory(key);
            app.Store.Notes.RemoveAll(n => n.GroupId == group.Id); app.Store.Groups.Remove(group);
        }
    }

    private static void TestImportDates(string folder)
    {
        var file = Path.Combine(folder, "dated-import.sqlite");
        using (var db = new SqliteConnection($"Data Source={file};Pooling=False"))
        {
            db.Open(); using var sql = db.CreateCommand();
            sql.CommandText = "CREATE TABLE Note (Id TEXT, Text TEXT, CreationDate TEXT); INSERT INTO Note VALUES ('recent','Récente','2026-02-01T12:00:00Z'), ('old','Ancienne','2026-01-01T12:00:00Z'), ('invalid','Sans date','inconnue');"; sql.ExecuteNonQuery();
        }
        var notes = StickyNotesImporter.Read(file, []).Notes;
        Check(notes.Select(n => n.WindowsStickyNoteId).SequenceEqual(new[] { "invalid", "old", "recent" }) && notes[0].CreatedAtUtc == null && notes[1].CreatedAtUtc < notes[2].CreatedAtUtc,
            "Import : dates source utilisées, ordre chronologique et date illisible tolérée");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
