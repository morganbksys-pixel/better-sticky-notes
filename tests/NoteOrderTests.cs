using System.IO;
using System.Windows.Controls;
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
            Check(((NotesWindow.Row)list.Items[0]).Note == newest, "Renommer conserve le tri par création, indépendamment du titre");
            var store = new NoteStore(Path.Combine(folder, "note-order")); store.Notes.AddRange([last, legacy, first, newest]); store.Save();
            var loaded = new NoteStore(Path.Combine(folder, "note-order")); loaded.Load();
            Check(loaded.Notes.OrderByDescending(n => n.CreatedAtUtc ?? DateTimeOffset.MinValue).Select(n => n.Id).SequenceEqual(new[] { newest.Id, last.Id, first.Id, legacy.Id }), "Notes : dates et tri conservés après rechargement");
            File.WriteAllText(Path.Combine(folder, "note-order", "notes.json"), "[{\"Title\":\"Ancienne A\"},{\"Title\":\"Ancienne B\"}]");
            loaded.Load();
            Check(loaded.Notes.All(n => n.CreatedAtUtc == null) && loaded.Notes.Select(n => n.Title).SequenceEqual(new[] { "Ancienne A", "Ancienne B" }), "Notes historiques : aucune date inventée ni inversion au chargement");
            TestImportDates(folder);
        }
        finally { home.Close(); app.Store.Notes.RemoveAll(n => n.GroupId == group.Id); app.Store.Groups.Remove(group); }
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
