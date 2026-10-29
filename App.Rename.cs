using System.Windows;

namespace BetterStickyNotes;

public partial class App
{
    public void RenameNote(Note note, Window owner)
    {
        if (note.Deleted || !Store.Notes.Contains(note)) return;
        var wasLocked = note.IsLocked;
        if (!UnlockNote(note)) return;
        try
        {
            new GroupNameDialog(owner, "Renommer la note", note.Title, title => {
                note.Title = title;
                if (windows.TryGetValue(note.Id, out var window)) window.SetTitle(title);
                Changed();
            }, "Titre de la note", 120).ShowDialog();
        }
        finally { if (wasLocked) LockNote(note); }
    }
}
