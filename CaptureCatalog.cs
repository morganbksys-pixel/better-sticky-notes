namespace BetterStickyNotes;

public sealed record CaptureReference(Note Note, NoteCapture Capture);

public static class CaptureCatalog
{
    public static List<CaptureReference> ForNote(Note note) => note.IsProtected ? [] : note.Captures.Select(c => new CaptureReference(note, c)).ToList();

    public static List<CaptureReference> ForGallery(NoteStore store, bool trash = false) => store.Notes
        .Where(n => !n.IsProtected && n.Deleted == trash)
        .SelectMany(ForNote)
        .OrderByDescending(c => c.Capture.CreatedAtUtc)
        .ThenBy(c => c.Note.Id).ThenBy(c => c.Capture.Id).ToList();
}
