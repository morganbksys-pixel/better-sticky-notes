using BetterStickyNotes;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Security.Cryptography;

internal static class ImportTests
{
    public static void Run(string folder)
    {
        var database = Path.Combine(folder, "plum.sqlite");
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Note (Id TEXT, Text TEXT, Theme TEXT, IsDeleted INTEGER);";
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO Note VALUES ($id,$text,$theme,$deleted)";
            foreach (var row in new[] {
                ("11111111-1111-1111-1111-111111111111", "\\id=aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa Bonjour été 📝\r\n- Une tâche", "Blue", 0),
                ("22222222-2222-2222-2222-222222222222", "Même texte", "Green", 0),
                ("33333333-3333-3333-3333-333333333333", "Même texte", "Pink", 0),
                ("44444444-4444-4444-4444-444444444444", "Supprimée", "Yellow", 1),
                ("55555555-5555-5555-5555-555555555555", "", "Yellow", 0) })
            {
                command.Parameters.Clear(); command.Parameters.AddWithValue("$id", row.Item1); command.Parameters.AddWithValue("$text", row.Item2);
                command.Parameters.AddWithValue("$theme", row.Item3); command.Parameters.AddWithValue("$deleted", row.Item4); command.ExecuteNonQuery();
            }
        }
        var before = SHA256.HashData(File.ReadAllBytes(database));
        var result = StickyNotesImporter.Read(database, []);
        Check(result.Notes.Count == 3 && result.Deleted == 1 && result.Empty == 1, "Import des notes actives, exclusion corbeille et notes vides");
        Check(result.Notes[0].Text == "Bonjour été 📝\n- Une tâche" && result.Notes[0].Color == "#CDE9FA", "Texte Unicode, listes, identifiants de paragraphe et couleurs");
        Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(database))), "La base source reste intacte");
        var store = new NoteStore(Path.Combine(folder, "imported")); store.Notes.AddRange(result.Notes); store.Save();
        var reloaded = new NoteStore(Path.Combine(folder, "imported")); reloaded.Load();
        reloaded.Notes[0].Text = "Modifiée localement"; reloaded.Notes[1].Deleted = true;
        var again = StickyNotesImporter.Read(database, reloaded.Notes);
        Check(again.Notes.Count == 0 && again.AlreadyImported == 3, "Réimport sans doublons, même après modification ou suppression locale");
        using (var writer = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            writer.Open(); using var sql = writer.CreateCommand();
            sql.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; INSERT INTO Note VALUES ('wal-note','Note dans le journal','Purple',0);"; sql.ExecuteNonQuery();
            Check(StickyNotesImporter.Read(database, reloaded.Notes).Notes.Single().Text == "Note dans le journal", "Lecture des notes validées dans le journal WAL");
        }
        var unsupported = Path.Combine(folder, "other.sqlite");
        using (var c = new SqliteConnection($"Data Source={unsupported};Pooling=False")) { c.Open(); using var sql = c.CreateCommand(); sql.CommandText = "CREATE TABLE Note (Text TEXT)"; sql.ExecuteNonQuery(); }
        try { StickyNotesImporter.Read(unsupported, []); throw new Exception("Un format incompatible a été accepté."); }
        catch (InvalidDataException) { Console.WriteLine("OK : Format incompatible rejeté sans import partiel"); }
        var oneNote = Path.Combine(folder, "notes.sdk_test.db");
        using (var c = new SqliteConnection($"Data Source={oneNote};Pooling=False"))
        {
            c.Open(); using var sql = c.CreateCommand();
            sql.CommandText = "CREATE TABLE notes (key TEXT, value TEXT)"; sql.ExecuteNonQuery();
            sql.CommandText = "INSERT INTO notes VALUES ($key, $value)";
            for (var i = 0; i < 3; i++)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new {
                    id = "onenote-" + i, deleted = i == 2, color = "Purple", title = "Titre conservé",
                    document = new { content = new[] { new { type = "paragraph", content = new { text = "Été : \"bonjour\" 📝" }, blockStyle = new { bullet = true } } } },
                    serverShadowNote = new { title = "Ancienne copie serveur à ignorer" }
                });
                sql.Parameters.Clear(); sql.Parameters.AddWithValue("$key", i.ToString());
                sql.Parameters.AddWithValue("$value", i == 0 ? json : json.Replace("\"", "\"\"")); sql.ExecuteNonQuery();
            }
        }
        var oneNoteHash = SHA256.HashData(File.ReadAllBytes(oneNote));
        var imported = StickyNotesImporter.Read(oneNote, []);
        Check(imported.Notes.Count == 2 && imported.Deleted == 1 && imported.Notes.All(n => n.Text == "- Été : \"bonjour\" 📝" && n.Title == "Titre conservé" && n.Color == "#E6DDF7"), "OneNote : JSON normal et échappé, Unicode, guillemets, listes, couleurs et titres");
        Check(StickyNotesImporter.Read(oneNote, imported.Notes).AlreadyImported == 2, "OneNote : réimport sans doublons ni copie du serverShadowNote");
        Check(oneNoteHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(oneNote))), "OneNote : base source inchangée");
    }
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("OK : " + label); }
}
