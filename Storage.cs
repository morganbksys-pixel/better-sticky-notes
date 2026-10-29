using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BetterStickyNotes;

public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? GroupId { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Color { get; set; } = "#FFF1A8";
    public double Left { get; set; } = 120;
    public double Top { get; set; } = 120;
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 360;
    public bool Pinned { get; set; }
    public bool Hidden { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public bool Preview { get; set; } = true;
    public string? WindowsStickyNoteId { get; set; }
    public bool? HasPlacement { get; set; }
    public EncryptedNote? Protection { get; set; }
    [JsonIgnore] public byte[]? ProtectionKey { get; set; }
    [JsonIgnore] public bool IsProtected => Protection != null;
    [JsonIgnore] public bool IsLocked => IsProtected && ProtectionKey == null;
    [JsonIgnore] public string DisplayTitle => IsLocked ? "Note protégée 🔒" : string.IsNullOrWhiteSpace(Title) ? "Sans titre" : Title;
    public Note Snapshot()
    {
        var snapshot = (Note)MemberwiseClone();
        if (IsProtected)
        {
            if (!IsLocked) snapshot.Protection = NoteProtection.Encrypt(this, ProtectionKey!, Protection!.Salt);
            snapshot.Title = ""; snapshot.Text = ""; snapshot.ProtectionKey = null;
        }
        return snapshot;
    }
}

public sealed record NoteGroup(Guid Id, string Name, string Color = "#73777E")
{
    public static bool IsValidColor(string? color) => color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit);
}

public sealed class NoteStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterStickyNotes");
    private readonly string path;
    private readonly object writeLock = new();
    private Task writeTail = Task.CompletedTask;
    // Kept on after sensitive changes so retries also sanitize the recovery copy.
    public bool SanitizeBackup { get; set; }
    public NoteStore(string? folder = null) => path = Path.Combine(folder ?? Folder, "notes.json");
    public List<Note> Notes { get; private set; } = [];
    public List<NoteGroup> Groups { get; private set; } = [];
    private sealed record Document(int Version, List<Note> Notes, List<NoteGroup> Groups);
    public string? LoadWarning { get; private set; }
    public int? TrashRetentionDays { get; private set; }
    private string SettingsPath => Path.Combine(Path.GetDirectoryName(path)!, "settings.json");
    private sealed record Settings(int? TrashRetentionDays);
    public void SaveTrashRetention(int? days)
    {
        if (days is < 1 or > 36500) throw new ArgumentOutOfRangeException(nameof(days));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(SettingsPath + ".tmp", JsonSerializer.Serialize(new Settings(days)));
        File.Move(SettingsPath + ".tmp", SettingsPath, true);
        TrashRetentionDays = days;
    }

    public void Load()
    {
        TrashRetentionDays = null;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                if (settings?.TrashRetentionDays is >= 1 and <= 36500) TrashRetentionDays = settings.TrashRetentionDays;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { LoadWarning = "Réglage de purge illisible : la purge automatique est désactivée."; }
        if (!File.Exists(path)) return;
        try { Apply(Read(path)); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Never overwrite the only surviving copy when recovery fails.
            var backup = path + ".bak";
            if (!File.Exists(backup)) throw new IOException("Impossible de lire les notes. Le fichier original a été conservé : " + path, ex);
            Apply(Read(backup));
            File.Copy(path, path + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
            File.Copy(backup, path, true);
            LoadWarning = "Les notes ont été récupérées depuis la sauvegarde précédente.";
        }
        var migrated = false;
        foreach (var n in Notes)
        {
            if (n.GroupId != null && !Groups.Any(g => g.Id == n.GroupId)) { n.GroupId = null; migrated = true; }
            if (n.Deleted && n.DeletedAtUtc == null) { n.DeletedAtUtc = DateTimeOffset.UtcNow; migrated = true; }
            if (!n.Deleted) n.DeletedAtUtc = null;
            if (n.IsProtected) { n.Title = ""; n.Text = ""; n.Hidden = true; }
            n.Title ??= ""; n.Text ??= "";
            n.Width = double.IsFinite(n.Width) ? Math.Clamp(n.Width, 260, 3000) : 340;
            n.Height = double.IsFinite(n.Height) ? Math.Clamp(n.Height, 220, 3000) : 360;
            if (!double.IsFinite(n.Left)) n.Left = 120;
            if (!double.IsFinite(n.Top)) n.Top = 120;
            // Old imports shared the default coordinates; preserve other existing placements.
            n.HasPlacement ??= !(n.WindowsStickyNoteId != null && n.Left == 120 && n.Top == 120);
        }
        if (migrated) Save();
    }
    private void Apply(Document document)
    {
        Notes = document.Notes;
        Groups = document.Groups.Select(g => NoteGroup.IsValidColor(g.Color) ? g : g with { Color = "#73777E" }).ToList();
    }
    private static Document Read(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        if (json.RootElement.ValueKind == JsonValueKind.Array)
        {
            var notes = json.RootElement.Deserialize<List<Note>>() ?? throw new JsonException("Fichier vide.");
            if (notes.Any(n => n == null)) throw new JsonException("Note invalide.");
            return new Document(1, notes, []);
        }
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("Version", out _))
            throw new JsonException("Format de notes invalide.");
        var document = json.RootElement.Deserialize<Document>() ?? throw new JsonException("Fichier vide.");
        if (document.Version != 1) throw new NotSupportedException("Version du fichier de notes non prise en charge : " + document.Version);
        if (document.Notes == null || document.Groups == null || document.Notes.Any(n => n == null) ||
            document.Groups.Any(g => g == null || g.Id == Guid.Empty || string.IsNullOrWhiteSpace(g.Name)) ||
            document.Groups.Select(g => g.Id).Distinct().Count() != document.Groups.Count)
            throw new JsonException("Structure des notes ou groupes invalide.");
        return document;
    }
    public Task SaveAsync()
    {
        // Capture immutable values on the UI thread, serialize and write on a worker.
        var snapshot = new Document(1, Notes.Select(n => n.Snapshot()).ToList(), Groups.ToList());
        var sanitize = SanitizeBackup || snapshot.Notes.Any(n => n.IsProtected);
        lock (writeLock)
            return writeTail = writeTail.ContinueWith(_ => Write(snapshot, sanitize), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
    public void Save() => SaveAsync().GetAwaiter().GetResult();
    private void Write(Document snapshot, bool sanitize)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        if (sanitize)
        {
            // Replace backup first: after success neither app-managed copy contains old secrets.
            var backupTemp = path + ".bak.tmp";
            File.Copy(temp, backupTemp, true);
            File.Move(backupTemp, path + ".bak", true);
            File.Move(temp, path, true);
        }
        else if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
}
