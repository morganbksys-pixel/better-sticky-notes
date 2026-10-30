using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace BetterStickyNotes;

public sealed record NoteCapture(Guid Id, DateTimeOffset CreatedAtUtc, int Width, int Height);

public sealed partial class NoteStore
{
    private readonly HashSet<string> pendingCaptureDeletes = [];
    public string CaptureFolder => Path.Combine(Path.GetDirectoryName(path)!, "captures");

    public string CapturePath(Note note, NoteCapture capture) => Path.Combine(CaptureFolder, $"{note.Id:N}-{capture.Id:N}.png");

    private static void ValidateCaptures(Note note)
    {
        if (note.Captures == null || note.Captures.Any(c => c == null || c.Id == Guid.Empty || c.Width <= 0 || c.Height <= 0) ||
            note.Captures.Select(c => c.Id).Distinct().Count() != note.Captures.Count || note.IsProtected && note.Captures.Count > 0)
            throw new JsonException("Captures de note invalides.");
    }

    public NoteCapture AddCapture(Note note, BitmapSource image)
    {
        if (!Notes.Contains(note) || note.Deleted || note.IsProtected)
            throw new InvalidOperationException("Les captures sont disponibles uniquement dans une note active non protégée.");
        var capture = new NoteCapture(Guid.NewGuid(), DateTimeOffset.UtcNow, image.PixelWidth, image.PixelHeight);
        var file = CapturePath(note, capture);
        Directory.CreateDirectory(CaptureFolder);
        try
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (var output = File.Create(file + ".tmp")) encoder.Save(output);
            File.Move(file + ".tmp", file);
        }
        finally { File.Delete(file + ".tmp"); }
        note.Captures.Add(capture);
        return capture;
    }

    public BitmapSource LoadCapture(Note note, NoteCapture capture, bool thumbnail = false)
    {
        if (note.IsProtected || !note.Captures.Contains(capture)) throw new InvalidOperationException("Capture indisponible.");
        using var input = File.OpenRead(CapturePath(note, capture));
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        if (thumbnail) image.DecodePixelWidth = Math.Min(180, capture.Width);
        image.StreamSource = input; image.EndInit(); image.Freeze();
        return image;
    }

    public void RemoveCapture(Note note, NoteCapture capture)
    {
        if (!note.Captures.Remove(capture)) return;
        pendingCaptureDeletes.Add(CapturePath(note, capture));
        SanitizeBackup = true;
    }

    public void ExportCapture(Note note, NoteCapture capture, string destination, bool overwrite = true)
    {
        if (note.IsProtected || !note.Captures.Contains(capture)) throw new InvalidOperationException("Capture indisponible.");
        var source = CapturePath(note, capture);
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return;
        File.Copy(source, destination, overwrite);
    }

    public void DeleteCaptureFiles(Note note)
    {
        foreach (var capture in note.Captures) pendingCaptureDeletes.Add(CapturePath(note, capture));
        if (note.Captures.Count > 0) SanitizeBackup = true;
    }
}
