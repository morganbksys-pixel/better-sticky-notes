using System.Windows;

namespace BetterStickyNotes;

public partial class NotesWindow
{
    private CaptureGalleryView? gallery;
    private bool GalleryVisible => GalleryHost.Visibility == Visibility.Visible;

    public void ShowGallery(bool trash = false)
    {
        NoteList.UnselectAll(); CloseCaptures();
        NotesContent.Visibility = Visibility.Collapsed; SettingsHost.Visibility = Visibility.Collapsed;
        gallery ??= new CaptureGalleryView(app, this, LocateCaptureNote);
        GalleryHost.Child = gallery; GalleryHost.Visibility = Visibility.Visible;
        gallery.ShowTrash(trash); RefreshGroups();
    }

    private void GalleryClick(object sender, RoutedEventArgs e) => ShowGallery();

    private void HideGallery()
    {
        gallery?.Suspend(); GalleryHost.Visibility = Visibility.Collapsed;
    }

    private void LocateCaptureNote(Note note)
    {
        if (!app.Store.Notes.Contains(note)) return;
        SelectView(note.Deleted ? "trash" : "all");
        Refresh([note.Id]);
        if (NoteList.SelectedItem is Row row) NoteList.ScrollIntoView(row);
    }
}
