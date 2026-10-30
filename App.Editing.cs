namespace BetterStickyNotes;

public partial class App
{
    public void UpdateNoteTitle(Note note, string title)
    {
        if (note.IsLocked || note.Deleted || note.Title == title) return;
        note.Title = title;
        note.ModifiedAtUtc = DateTimeOffset.UtcNow;
        Changed();
    }

    public void UpdateNoteText(Note note, string text)
    {
        if (note.IsLocked || note.Deleted || note.Text == text) return;
        note.Text = text;
        note.ModifiedAtUtc = DateTimeOffset.UtcNow;
        Changed();
    }
}
