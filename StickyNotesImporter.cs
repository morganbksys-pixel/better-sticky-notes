using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace BetterStickyNotes;

public sealed record StickyImportResult(List<Note> Notes, int AlreadyImported, int Deleted, int Empty);

public static partial class StickyNotesImporter
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe", "LocalState", "plum.sqlite");

    public static string? DetectPath()
    {
        var candidates = new List<string>();
        if (File.Exists(DefaultPath)) candidates.Add(DefaultPath);
        var memory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneNote", "16.0", "Memory");
        if (Directory.Exists(memory)) candidates.AddRange(Directory.EnumerateFiles(memory, "notes.sdk_*.db"));
        return candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    public static StickyImportResult Read(string path, IEnumerable<Note> existing)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Le fichier Sticky Notes est introuvable.", path);
        var seen = existing.Where(n => n.WindowsStickyNoteId != null).Select(n => n.WindowsStickyNoteId!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = Path.GetFullPath(path), Mode = SqliteOpenMode.ReadOnly, Pooling = false, DefaultTimeout = 3
        }.ToString());
        connection.Open();
        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND lower(name)='notes'";
            if (Convert.ToInt32(schema.ExecuteScalar()) > 0) return ReadOneNote(connection, seen);
        }
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM \"Note\"";
        using var reader = command.ExecuteReader();
        var columns = Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => i, StringComparer.OrdinalIgnoreCase);
        if (!columns.ContainsKey("Id") || !columns.ContainsKey("Text"))
            throw new InvalidDataException("Ce fichier n’utilise pas le format Sticky Notes attendu (table Note, colonnes Id et Text).");
        string Value(string name) => columns.TryGetValue(name, out var index) && !reader.IsDBNull(index) ? Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture) ?? "" : "";
        var notes = new List<Note>();
        int duplicates = 0, deleted = 0, empty = 0;
        while (reader.Read())
        {
            var removed = Value("IsDeleted");
            var deletedAt = Value("DeletedAt");
            if (removed is "1" || removed.Equals("true", StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(deletedAt) && deletedAt != "0")) { deleted++; continue; }
            var id = Value("Id").Trim();
            if (string.IsNullOrEmpty(id)) throw new InvalidDataException("Une note ne possède pas d’identifiant : import annulé pour éviter les doublons.");
            id = Guid.TryParse(id, out var guid) ? guid.ToString("D") : id;
            if (!seen.Add(id)) { duplicates++; continue; }
            var text = CleanText(Value("Text"));
            if (string.IsNullOrWhiteSpace(text)) { empty++; continue; }
            var title = text.Split('\n').FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? "Note importée";
            if (title.Length > 80) title = title[..80] + "…";
            notes.Add(new Note { Title = title, Text = text, Color = MapColor(Value("Theme")), WindowsStickyNoteId = id, Hidden = true,
                CreatedAtUtc = ParseCreationDate(Value("CreationDate")) ?? ParseCreationDate(Value("CreatedAt")) ?? ParseCreationDate(Value("CreatedAtTimestamp")) });
        }
        return new(notes.OrderBy(n => n.CreatedAtUtc ?? DateTimeOffset.MinValue).ToList(), duplicates, deleted, empty);
    }

    private static StickyImportResult ReadOneNote(SqliteConnection connection, HashSet<string> seen)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM notes";
        using var reader = command.ExecuteReader();
        var notes = new List<Note>();
        int duplicates = 0, deleted = 0, empty = 0;
        while (reader.Read())
        {
            if (reader.IsDBNull(0)) throw new InvalidDataException("Une note OneNote est illisible. Aucun import n’a été effectué.");
            using var document = ParseOneNote(reader.GetString(0));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Format de note OneNote non reconnu.");
            if (root.TryGetProperty("deleted", out var removed) && removed.ValueKind == JsonValueKind.True) { deleted++; continue; }
            var id = String(root, "id");
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Identifiant de note OneNote manquant.");
            id = Guid.TryParse(id, out var guid) ? guid.ToString("D") : id;
            if (!seen.Add(id)) { duplicates++; continue; }
            if (!root.TryGetProperty("document", out var content) || content.ValueKind != JsonValueKind.Object ||
                !content.TryGetProperty("content", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Le contenu d’une note OneNote utilise un format non pris en charge.");
            var lines = new List<string>();
            foreach (var block in blocks.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object || String(block, "type") != "paragraph")
                    throw new InvalidDataException("Un bloc OneNote n’est pas un paragraphe pris en charge. Import annulé pour ne pas perdre de texte.");
                if (!block.TryGetProperty("content", out var paragraph) || paragraph.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Paragraphe OneNote illisible.");
                var text = String(paragraph, "text");
                if (block.TryGetProperty("blockStyle", out var style) && style.ValueKind == JsonValueKind.Object &&
                    style.TryGetProperty("bullet", out var bullet) && bullet.ValueKind == JsonValueKind.True && text.Length > 0) text = "- " + text;
                lines.Add(text);
            }
            var body = string.Join("\n", lines).TrimEnd('\n');
            var title = String(root, "title").Trim();
            if (string.IsNullOrWhiteSpace(body) && title.Length == 0) { empty++; continue; }
            if (title.Length == 0) title = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "Note importée";
            if (title.Length > 80) title = title[..80] + "…";
            notes.Add(new Note { Title = title, Text = body, Color = MapColor(String(root, "color")), WindowsStickyNoteId = id, Hidden = true,
                CreatedAtUtc = ParseCreationDate(DateValue(root, "createdAt")) ?? ParseCreationDate(DateValue(root, "createdTime"))
                    ?? ParseCreationDate(DateValue(root, "creationDate")) ?? ParseCreationDate(DateValue(root, "creationDateTime")) });
        }
        return new(notes.OrderBy(n => n.CreatedAtUtc ?? DateTimeOffset.MinValue).ToList(), duplicates, deleted, empty);
    }
    private static DateTimeOffset? ParseCreationDate(string raw)
    {
        DateTimeOffset date;
        if (long.TryParse(raw, out var number))
        {
            try { date = number >= DateTime.UnixEpoch.Ticks ? new DateTimeOffset(number, TimeSpan.Zero) : DateTimeOffset.FromUnixTimeMilliseconds(number); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        else if (!DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out date)) return null;
        return date >= DateTimeOffset.UnixEpoch && date <= DateTimeOffset.UtcNow.AddDays(1) ? date.ToUniversalTime() : null;
    }
    private static string DateValue(JsonElement element, string name) => element.TryGetProperty(name, out var value)
        ? value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "" : "";
    private static string String(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    private static JsonDocument ParseOneNote(string raw)
    {
        try { return JsonDocument.Parse(raw); }
        catch (JsonException)
        {
            // The OneNote SDK stores JSON with every quote doubled (CSV-style escaping).
            try { return JsonDocument.Parse(raw.Replace("\"\"", "\"")); }
            catch (JsonException ex) { throw new InvalidDataException("Le contenu d’une note OneNote est illisible. Aucun import n’a été effectué.", ex); }
        }
    }

    // Windows stores paragraph identifiers alongside the visible text. Only remove
    // the known metadata prefix; preserve user text, URLs, Unicode and list markers.
    public static string CleanText(string text) => ParagraphId().Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'), "").TrimEnd('\0', '\n');
    [GeneratedRegex(@"(?m)^\\id=\{?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}?[ \t]?")]
    private static partial Regex ParagraphId();

    public static string MapColor(string theme) => theme.Trim().ToLowerInvariant() switch
    {
        "blue" => "#CDE9FA", "green" => "#D9EEC4", "pink" => "#FAD8E3", "purple" => "#E6DDF7",
        "gray" or "grey" or "charcoal" => "#E2E2E2", "white" => "#F7F7F2", _ => "#FFF1A8"
    };
}
