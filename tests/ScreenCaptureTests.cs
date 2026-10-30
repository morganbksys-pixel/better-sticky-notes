using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BetterStickyNotes;

internal static class ScreenCaptureTests
{
    public static void Run(App app, string folder)
    {
        var left = new Rectangle(-2560, -200, 2560, 1440);
        var right = new Rectangle(0, 0, 1920, 1080);
        Check(ScreenCapture.SelectScreen(new Rectangle(-500, 200, 340, 360), [left, right]) == left, "Capture : écran à coordonnées négatives choisi depuis la position de la note");
        Check(ScreenCapture.SelectScreen(new Rectangle(-100, 200, 340, 360), [left, right]) == right, "Capture : écran contenant la plus grande partie d’une note à cheval");
        Check(ScreenCapture.SelectScreen(new Rectangle(2200, 200, 340, 360), [left, right]) == right, "Capture : écran le plus proche pour une position hors écran");
        var store = new NoteStore(Path.Combine(folder, "captures"));
        var note = new Note { Title = "Avec capture", Text = "Texte inchangé" };
        store.Notes.Add(note);
        var first = store.AddCapture(note, Image());
        var snapshot = note.Snapshot();
        var second = store.AddCapture(note, Image());
        Check(snapshot.Captures.Count == 1 && note.Captures.Count == 2, "Capture : instantané de sauvegarde indépendant des ajouts suivants");
        store.Save();
        var firstFile = store.CapturePath(note, first);
        Check(File.Exists(firstFile) && File.ReadAllBytes(firstFile).Take(4).SequenceEqual(new byte[] { 137, 80, 78, 71 }), "Capture : PNG local écrit avant sa référence dans la note");
        Check(File.ReadAllText(Path.Combine(folder, "captures", "notes.json")).Contains("\"Version\": 2"), "Capture : nouveau format distingué pour éviter la perte des pièces jointes avec une ancienne version");
        store.Load(); note = store.Notes.Single();
        var image = store.LoadCapture(note, first);
        var pixels = new byte[16 * 10 * 4]; image.CopyPixels(pixels, 16 * 4, 0);
        Check(note.Captures.SequenceEqual(new[] { first, second }) && image.PixelWidth == 16 && image.PixelHeight == 10 && pixels[0] == 45 && pixels[3] == 255 && note.Text == "Texte inchangé", "Capture : rechargement des références et des pixels sans modifier le texte");
        var thumbnail = store.LoadCapture(note, first, thumbnail: true);
        Check(thumbnail.IsFrozen && thumbnail.PixelWidth <= 180, "Capture : vignette chargée sans garder le fichier ouvert");
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            try { NoteProtection.Protect(note, key, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))); throw new Exception("Protection avec image acceptée"); }
            catch (InvalidOperationException) { Check(!note.IsProtected, "Capture : protection refusée avant toute modification d’une note avec image"); }
            var secret = new Note(); store.Notes.Add(secret);
            NoteProtection.Protect(secret, key, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));
            try { store.AddCapture(secret, Image()); throw new Exception("Capture protégée acceptée"); }
            catch (InvalidOperationException) { Check(secret.Captures.Count == 0, "Capture : ajout refusé dans une note déjà protégée"); }
            store.Notes.Remove(secret);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
        store.RemoveCapture(note, first);
        Check(File.Exists(firstFile), "Capture : suppression du fichier différée jusqu’à une sauvegarde réussie");
        var blockedWrite = Path.Combine(folder, "captures", "notes.json.tmp");
        Directory.CreateDirectory(blockedWrite);
        try
        {
            try { store.Save(); throw new Exception("Erreur d’écriture ignorée"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            Check(File.Exists(firstFile) && File.ReadAllText(Path.Combine(folder, "captures", "notes.json")).Contains(first.Id.ToString()), "Capture : échec de sauvegarde conserve le PNG et sa référence enregistrée");
        }
        finally { Directory.Delete(blockedWrite); }
        store.Save();
        Check(!File.Exists(firstFile) && File.Exists(store.CapturePath(note, second)), "Capture : suppression limitée à l’image retirée");
        foreach (var file in new[] { "notes.json", "notes.json.bak" })
            Check(!File.ReadAllText(Path.Combine(folder, "captures", file)).Contains(first.Id.ToString()), "Capture : référence retirée du fichier et de sa sauvegarde");
        File.WriteAllText(Path.Combine(folder, "captures", "notes.json"), "broken");
        store.Load();
        Check(store.Notes.Single().Captures.Single() == second && File.Exists(store.CapturePath(store.Notes.Single(), second)), "Capture : récupération depuis la sauvegarde avec son image");

        var displayed = new Note { Title = "Vignette" }; app.Store.Notes.Add(displayed);
        var capture = app.Store.AddCapture(displayed, Image());
        var window = new NoteWindow(app, displayed);
        try
        {
            Check(((StackPanel)window.FindName("CaptureItems")).Children.OfType<Button>().Single().Content is System.Windows.Controls.Image, "Capture : vignette présente dans le post-it");
            window.SetCollapsed(true);
            Check(((StackPanel)window.FindName("CaptureStrip")).Visibility == Visibility.Collapsed, "Capture : vignettes masquées dans une note repliée");
            window.SetCollapsed(false);
            Check(((StackPanel)window.FindName("CaptureStrip")).Visibility == Visibility.Visible, "Capture : vignettes rétablies au dépliage");
            File.Delete(app.Store.CapturePath(displayed, capture));
            window.HideForCache(); window.RefreshCaptures();
            Check(((StackPanel)window.FindName("CaptureItems")).Children.OfType<Button>().Single().Content is TextBlock, "Capture : image manquante signalée sans empêcher l’ouverture de la note");
            File.WriteAllText(app.Store.CapturePath(displayed, capture), "PNG invalide");
            window.HideForCache(); window.RefreshCaptures();
            Check(((StackPanel)window.FindName("CaptureItems")).Children.OfType<Button>().Single().Content is TextBlock, "Capture : image endommagée signalée sans faire échouer la note");
        }
        finally { window.ClosePermanently(); }
        app.DeleteNotes([displayed]); app.RestoreNotes([displayed]);
        Check(displayed.Captures.Single() == capture, "Capture : association conservée par la corbeille et la restauration");
        var replacement = app.Store.AddCapture(displayed, Image());
        var replacementFile = app.Store.CapturePath(displayed, replacement);
        app.DeleteNotes([displayed]); app.PermanentlyDeleteNotes([displayed]);
        Check(File.Exists(replacementFile), "Capture : suppression définitive attend l’enregistrement des notes");
        PerformanceChecks.Wait(app.SaveNowAsync());
        Check(!File.Exists(replacementFile), "Capture : suppression définitive nettoie les PNG après sauvegarde");
    }

    public static void RunWindows(App app)
    {
        var note = new Note { Title = "Capture synthétique", Text = "Texte conservé", Pinned = true };
        app.Store.Notes.Add(note); app.Open(note); app.FocusNote(note); PerformanceChecks.Pump();
        var window = app.Windows.OfType<NoteWindow>().Single(w => w.Title == note.Title);
        try
        {
            typeof(NoteWindow).GetMethod("SetPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [false]);
            var editor = (TextBox)window.FindName("Editor"); editor.Select(2, 4);
            var opacity = window.Opacity;
            var read = false;
            var task = app.CaptureNoteScreenAsync(note, window, bounds =>
            {
                read = true;
                Check(bounds.Width > 0 && bounds.Height > 0 && window.Opacity == 0 && !window.IsHitTestVisible && !note.Hidden, "Capture : fenêtres masquées visuellement sans masquer la note dans les données");
                return Image();
            });
            Check(app.IsCapturing && !app.CaptureNoteScreenAsync(note, window, _ => throw new Exception("Double capture")).Result, "Capture : déclenchements concurrents ignorés");
            PerformanceChecks.Wait(task);
            Check(task.Result && read && note.Captures.Count == 1 && window.Opacity == opacity && window.IsHitTestVisible && !app.IsCapturing, "Capture : image attachée et fenêtres restaurées après succès");
            Check(!note.Preview && note.Pinned && !note.Collapsed && editor.SelectionStart == 2 && editor.SelectionLength == 4, "Capture : édition, sélection, épinglage et repli conservés");
            window.SetCollapsed(true);
            PerformanceChecks.Wait(app.CaptureNoteScreenAsync(note, window, _ => Image()));
            Check(note.Collapsed && window.ActualHeight == 50 && note.Captures.Count == 2, "Capture : ajout depuis une note repliée sans la déplier");
            var failure = app.CaptureNoteScreenAsync(note, window, _ => throw new IOException("Échec synthétique"));
            try { PerformanceChecks.Wait(failure); throw new Exception("Erreur de capture ignorée"); }
            catch (IOException) { }
            Check(window.Opacity == opacity && window.IsHitTestVisible && !app.IsCapturing && note.Captures.Count == 2, "Capture : restauration garantie après erreur, aucune image ajoutée");
            window.SetCollapsed(false);
            ((StackPanel)window.FindName("CaptureItems")).Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var viewer = window.OwnedWindows.OfType<Window>().Single(); viewer.Opacity = 0;
            Check(viewer.IsVisible && viewer.Title.StartsWith("Capture du"), "Capture : clic sur la vignette ouvre l’image dans une fenêtre dédiée");
            var hidden = app.CaptureNoteScreenAsync(note, window, _ => { app.HideNotes([note]); return Image(); });
            PerformanceChecks.Wait(hidden);
            Check(!hidden.Result && note.Hidden && !window.IsVisible && window.Opacity == opacity && window.IsHitTestVisible && note.Captures.Count == 2, "Capture : masquage pendant l’opération respecté, état visuel restauré sans rouvrir la note");
            Check(!viewer.IsVisible, "Capture : masquer la note ferme aussi son aperçu agrandi");
        }
        finally
        {
            app.DeleteNotes([note]); app.PermanentlyDeleteNotes([note]); PerformanceChecks.Wait(app.SaveNowAsync());
        }
    }

    private static BitmapSource Image()
    {
        var pixels = Enumerable.Range(0, 16 * 10).SelectMany(_ => new byte[] { 45, 120, 200, 255 }).ToArray();
        var image = BitmapSource.Create(16, 10, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
        image.Freeze(); return image;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("OK : " + message);
    }
}
