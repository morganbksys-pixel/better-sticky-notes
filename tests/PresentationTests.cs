using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BetterStickyNotes;

internal static class PresentationTests
{
    public static void Run(App app, string data)
    {
        var note = new Note { Title = "Titre confidentiel", Text = "Détail confidentiel", Hidden = false };
        var key = RandomNumberGenerator.GetBytes(32);
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        NoteProtection.Protect(note, key, salt); app.Store.Notes.Add(note);
        var manager = new NotesWindow(app);
        var list = (DataGrid)manager.FindName("NoteList");
        var row = (NotesWindow.Row)list.Items[0];
        Check(row.Title == note.Title && row.Excerpt == note.Text && row.ProtectionMarker == "\uE785", "Déverrouillée : titre, contenu et cadenas ouvert à la place de l’œil");
        var search = (TextBox)manager.FindName("SearchBox"); search.Text = "DETAIL";
        Check(list.Items.Count == 1, "Recherche sans accents dans une note déverrouillée");
        app.Store.Save();
        Check(!File.ReadAllText(Path.Combine(data, "notes.json")).Contains("confidentiel"), "Déverrouillée : contenu toujours chiffré sur disque");
        app.LockNote(note); manager.RefreshPrivacy();
        Check(list.Items.Count == 0 && row.Title == "Note protégée" && row.Excerpt == "Contenu protégé" && row.ProtectionMarker == "\uE72E", "Reverrouillage : résultats retirés et ligne mise en cache anonymisée");
        search.Clear(); Check(list.Items.Count == 1, "Note verrouillée accessible hors recherche");
        NoteProtection.Unlock(note, key); manager.RefreshPrivacy();
        Check(row.Title == "Titre confidentiel", "Déverrouillage rétablit le titre");
        note.Deleted = true; manager.SelectView("trash"); search.Text = "confidentiel";
        Check(list.Items.Count == 1, "Même règle de recherche dans la corbeille");
        app.LockNote(note); manager.RefreshPrivacy(); Check(list.Items.Count == 0, "Corbeille anonymisée au reverrouillage");
        note.Deleted = false; NoteProtection.Unlock(note, key);
        var window = new NoteWindow(app, note);
        var close = (Button)window.FindName("HideButton");
        Check(close.ToolTip.ToString()!.StartsWith("Masquer la note"), "Œil barré du post-it : action de masquage explicite");
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(note.Hidden && note.IsLocked && !note.Deleted && app.Store.Notes.Contains(note), "Fermer masque et verrouille sans supprimer");
        window.ClosePermanently();
        var plain = new Note(); var plainWindow = new NoteWindow(app, plain);
        var pin = (Button)plainWindow.FindName("PinButton");
        Check(pin.Content is TextBlock { Text: "\uE77A" }, "Punaise inactive Windows");
        pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(plain.Pinned && plainWindow.Topmost && pin.Content is TextBlock { Text: "\uE718" }, "Punaise épinglée Windows et premier plan conservé");
        plainWindow.ClosePermanently();
        var group = app.CreateGroup("Groupe"); app.SetGroupColor(group.Id, "#8054BA");
        var menu = app.BuildGroupMenu([note]); var entry = (MenuItem)menu.Items[1];
        Check(entry.Header is Border { Background: SolidColorBrush, Child: TextBlock } && entry.Icon is TextBlock { Foreground: SolidColorBrush, Text: "\uE8B7" }, "Libellé surligné et dossier Windows coloré dans le menu");
        Check(((MenuItem)menu.Items[0]).Header is TextBlock, "Sans groupe reste neutre");
        app.AssignGroup([note], group.Id); menu = app.BuildGroupMenu([note]);
        Check(((MenuItem)menu.Items[1]).IsChecked, "Coche du groupe actuel préservée");
        Check(!app.BuildGroupMenu([note, plain]).Items.OfType<MenuItem>().Any(i => i.IsChecked), "Sélection mixte sans coche commune");
        CryptographicOperations.ZeroMemory(key); manager.Close(); app.Store.Notes.Clear(); app.Store.Groups.Clear();
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
