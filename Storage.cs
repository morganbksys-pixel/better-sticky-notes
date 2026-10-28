using System.IO;
using System.Text.Json;

namespace BetterStickyNotes;

public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
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
    public bool Preview { get; set; } = true;
    public string? WindowsStickyNoteId { get; set; }
    public bool? HasPlacement { get; set; }
    public Note Snapshot() => (Note)MemberwiseClone();
}

public sealed class NoteStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterStickyNotes");
    private readonly string path;
    private readonly object writeLock = new();
    private Task writeTail = Task.CompletedTask;
    public NoteStore(string? folder = null) => path = Path.Combine(folder ?? Folder, "notes.json");
    public List<Note> Notes { get; private set; } = [];
    public string? LoadWarning { get; private set; }

    public void Load()
    {
        if (!File.Exists(path)) return;
        try { Notes = Read(path); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Never overwrite the only surviving copy when recovery fails.
            var backup = path + ".bak";
            if (!File.Exists(backup)) throw new IOException("Impossible de lire les notes. Le fichier original a été conservé : " + path, ex);
            Notes = Read(backup);
            File.Copy(path, path + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
            File.Copy(backup, path, true);
            LoadWarning = "Les notes ont été récupérées depuis la sauvegarde précédente.";
        }
        foreach (var n in Notes)
        {
            n.Title ??= ""; n.Text ??= "";
            n.Width = double.IsFinite(n.Width) ? Math.Clamp(n.Width, 260, 3000) : 340;
            n.Height = double.IsFinite(n.Height) ? Math.Clamp(n.Height, 220, 3000) : 360;
            if (!double.IsFinite(n.Left)) n.Left = 120;
            if (!double.IsFinite(n.Top)) n.Top = 120;
            // Old imports shared the default coordinates; preserve other existing placements.
            n.HasPlacement ??= !(n.WindowsStickyNoteId != null && n.Left == 120 && n.Top == 120);
        }
    }
    private static List<Note> Read(string file) => JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(file)) ?? throw new JsonException("Fichier vide.");
    public Task SaveAsync()
    {
        // Capture immutable values on the UI thread, serialize and write on a worker.
        var snapshot = Notes.Select(n => n.Snapshot()).ToList();
        lock (writeLock)
            return writeTail = writeTail.ContinueWith(_ => Write(snapshot), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
    public void Save() => SaveAsync().GetAwaiter().GetResult();
    private void Write(List<Note> snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
}
