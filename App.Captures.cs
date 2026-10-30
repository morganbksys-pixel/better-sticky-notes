using System.Drawing;
using System.Windows.Media.Imaging;

namespace BetterStickyNotes;

public partial class App
{
    public bool IsCapturing { get; private set; }

    public async Task<bool> CaptureNoteScreenAsync(Note note, NoteWindow source, Func<Rectangle, BitmapSource>? readPixels = null)
    {
        if (IsCapturing || quitting || note.IsProtected || note.Deleted || !Store.Notes.Contains(note)) return false;
        IsCapturing = true;
        try
        {
            var image = await ScreenCapture.CaptureAsync(source, Windows.OfType<System.Windows.Window>().ToArray(), readPixels);
            return await Dispatcher.InvokeAsync(async () =>
            {
                if (quitting || note.IsProtected || note.Deleted || note.Hidden || !Store.Notes.Contains(note)) return false;
                Store.AddCapture(note, image);
                NotifyCapturesChanged(note);
                return await SaveNowAsync();
            }).Task.Unwrap();
        }
        finally { await Dispatcher.InvokeAsync(() => IsCapturing = false); }
    }

    public async Task<bool> RemoveCapturesAsync(Note note, IEnumerable<NoteCapture> captures)
    {
        if (quitting || note.Deleted || note.IsProtected || !Store.Notes.Contains(note)) return false;
        foreach (var capture in captures.ToArray()) Store.RemoveCapture(note, capture);
        NotifyCapturesChanged(note);
        return await SaveNowAsync();
    }

    private void NotifyCapturesChanged(Note note)
    {
        if (windows.TryGetValue(note.Id, out var window)) window.RefreshCaptures();
        Changed();
        notesWindow?.RefreshCaptureState();
    }
}
