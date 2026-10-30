using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shell;
using BetterStickyNotes;

internal static class NoteToolsTests
{
    public static void Run(App app, string folder)
    {
        TestCollapse(app, folder);
        TestFind(app);
    }

    public static void RunWindows(App app)
    {
        var note = new Note { Title = "Test des outils", Text = string.Join("\n\n", Enumerable.Range(0, 80).Select(i => $"Paragraphe {i}")) + "\n\nCible finale", Width = 380, Height = 470, HasPlacement = true };
        app.Store.Notes.Add(note);
        app.Open(note); PerformanceChecks.Pump();
        var window = app.Windows.OfType<NoteWindow>().Single(w => w.Title == note.Title);
        try
        {
            var height = note.Height; var width = note.Width;
            for (var i = 0; i < 4; i++)
            {
                window.SetCollapsed(true); PerformanceChecks.Pump(); window.Left += 2;
                Check(window.ActualHeight == 50 && note.Height == height && note.Width == width, "Fenêtre native repliée : déplacement sans perdre la taille normale");
                window.SetCollapsed(false); PerformanceChecks.Pump();
                Check(window.ActualHeight == height && note.Height == height && window.ActualWidth == width, "Fenêtre native dépliée : dimensions stables après plusieurs cycles");
            }
            window.SetCollapsed(true); PerformanceChecks.Pump();
            var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
            var bottom = screen.WorkingArea.Bottom / dpi.DpiScaleY;
            window.Top = bottom - window.ActualHeight; PerformanceChecks.Pump();
            window.SetCollapsed(false); PerformanceChecks.Pump();
            Check(window.Top + window.ActualHeight <= bottom + 1 && note.Height == height, "Dépliage au bord inférieur : note replacée dans la zone utilisable sans perdre sa taille");
            window.SetCollapsed(true); app.HideNotes([note]); app.TrimHiddenWindows(expireAll: true);
            app.Open(note); PerformanceChecks.Pump();
            window = app.Windows.OfType<NoteWindow>().Single(w => w.Title == note.Title);
            Check(note.Collapsed && window.ActualHeight == 50 && note.Height == height, "Recréation après expiration du cache : repli et hauteur normale conservés");
            app.FocusNote(note); PerformanceChecks.Pump();
            Check(!note.Collapsed && window.ActualHeight == height && !note.Pinned, "Mes notes déplie la note ciblée sans changer l’épinglage");
            Invoke(window, "OpenFind");
            var query = (TextBox)window.FindName("FindBox");
            query.Text = "Cible finale"; Invoke(window, "RefreshFind"); PerformanceChecks.Pump();
            var preview = (FlowDocumentScrollViewer)window.FindName("Previewer");
            var rectangles = (IEnumerable<(Rect Rect, bool Active)>)Invoke(window, "FindRectangles")!;
            Check(rectangles.Any(r => r.Active && r.Rect.IntersectsWith(new Rect(preview.RenderSize))), "Recherche en aperçu : défilement jusqu’au résultat éloigné");
            Invoke(window, "SetPreview", false); PerformanceChecks.Pump();
            var editor = (TextBox)window.FindName("Editor");
            rectangles = (IEnumerable<(Rect Rect, bool Active)>)Invoke(window, "FindRectangles")!;
            Check(editor.VerticalOffset > 0 && rectangles.Any(r => r.Active && r.Rect.IntersectsWith(new Rect(editor.RenderSize))), "Recherche en édition : défilement et surlignage du résultat éloigné");
        }
        finally
        {
            app.DeleteNotes([note]); app.Store.Notes.Remove(note);
        }
    }

    private static void TestCollapse(App app, string folder)
    {
        var note = new Note { Title = "À garder", Text = "Contenu", Width = 380, Height = 470, Pinned = true, HasPlacement = true };
        var window = new NoteWindow(app, note);
        try
        {
            window.SetCollapsed(true);
            Layout(window);
            Check(note.Collapsed && window.Height == 50 && note.Height == 470 && note.Width == 380, "Repli : bandeau seul, dimensions normales conservées");
            Check(window.ResizeMode == ResizeMode.NoResize && WindowChrome.GetWindowChrome(window).ResizeBorderThickness == new Thickness(0), "Repli : redimensionnement natif désactivé");
            Check(((Control)window.FindName("Editor")).Visibility == Visibility.Collapsed && ((Control)window.FindName("Previewer")).Visibility == Visibility.Collapsed && ((Control)window.FindName("CornerGrip")).Visibility == Visibility.Collapsed, "Repli : contenu et poignée masqués");
            window.Left = -250; window.Top = 150;
            var store = new NoteStore(Path.Combine(folder, "note-tools"));
            store.Notes.Add(note); store.Save(); store.Load();
            var saved = store.Notes.Single();
            Check(saved.Collapsed && saved.Height == 470 && saved.Width == 380, "Repli : état et taille normale persistés après rechargement");
            var restored = new NoteWindow(app, saved);
            try { Check(restored.Height == 50 && restored.Topmost, "Repli restauré à la création sans changer l’épinglage"); }
            finally { restored.ClosePermanently(); }
            window.SetCollapsed(false); Layout(window);
            Check(window.Height == 470 && window.Width == 380 && window.Topmost && window.ResizeMode == ResizeMode.CanResize, "Dépliage : taille, largeur et épinglage restaurés");
            Check(WindowChrome.GetWindowChrome(window).ResizeBorderThickness == new Thickness(9), "Dépliage : bordure native de nouveau disponible");
            window.SetCollapsed(true);
            Invoke(window, "OpenFind");
            Check(!note.Collapsed && window.Height == 470 && ((Grid)window.FindName("FindBar")).Visibility == Visibility.Visible, "Ctrl+F déplie la note pour rechercher");
        }
        finally { window.ClosePermanently(); }
    }

    private static void TestFind(App app)
    {
        Check(NoteSearch.Find("Été été ETE", "ÉTÉ").Select(m => m.Start).SequenceEqual(new[] { 0, 4 }), "Recherche littérale sans distinction de casse");
        Check(NoteSearch.Find("aaaa", "aa").Count == 2 && NoteSearch.Find("abc", "").Count == 0 && NoteSearch.Find("abc", "absent").Count == 0, "Recherche vide, absente et occurrences adjacentes");
        var note = new Note { Text = "mot **composé**\n\n[mot](https://cache.example)\n\n```cs\nmot\n```", Preview = true };
        var window = new NoteWindow(app, note);
        try
        {
            Invoke(window, "OpenFind");
            var query = (TextBox)window.FindName("FindBox");
            var counter = (TextBlock)window.FindName("FindCounter");
            var editor = (TextBox)window.FindName("Editor");
            var preview = (FlowDocumentScrollViewer)window.FindName("Previewer");
            void Search(string value) { query.Text = value; Invoke(window, "RefreshFind"); Layout(window); }
            Search("mot");
            Check(counter.Text == "1 / 3" && note.Preview, "Aperçu : recherche dans le texte et le code sans changer de mode");
            ((Button)window.FindName("FindPreviousButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(counter.Text == "3 / 3", "Recherche précédente : retour au dernier résultat");
            ((Button)window.FindName("FindNextButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(counter.Text == "1 / 3", "Recherche suivante : retour au premier résultat");
            Search("mot composé");
            Check(counter.Text == "1 / 1", "Aperçu : correspondance traversant plusieurs fragments Markdown");
            var rectangles = (IEnumerable<(Rect Rect, bool Active)>)Invoke(window, "FindRectangles")!;
            Check(rectangles.Any(r => !r.Rect.IsEmpty && r.Rect.Width > 1 && r.Active), "Aperçu : géométrie de surlignage calculée sur le document rendu");
            Search("cache.example");
            Check(counter.Text == "0 / 0", "Aperçu : une URL masquée par son libellé ne participe pas à la recherche");
            Check(note.Text == editor.Text && new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("mot composé"), "Recherche : source et rendu Markdown conservés");
            Invoke(window, "SetPreview", false);
            Search("cache.example");
            Check(counter.Text == "1 / 1" && !note.Preview, "Édition : recherche dans la source Markdown complète");
            var originalEditorText = editor.Text;
            editor.AppendText(" mot");
            Search("mot");
            Check(counter.Text == "1 / 4", "Édition : résultats actualisés après modification du contenu");
            var before = editor.Text;
            var canUndo = editor.CanUndo;
            Search("composé");
            Check(canUndo && editor.Text == before && editor.CanUndo && note.Text == before, "Surlignage : aucun changement du texte ou de l’historique d’annulation");
            rectangles = (IEnumerable<(Rect Rect, bool Active)>)Invoke(window, "FindRectangles")!;
            Check(rectangles.Any(r => r.Rect.Width > 1), "Édition : géométrie de surlignage disponible");
            editor.Undo();
            Check(editor.Text == originalEditorText, "Ctrl+Z après recherche annule la dernière saisie, pas le surlignage");
            editor.Select(0, 3); Invoke(window, "OpenFind");
            Check(query.Text == "mot", "Ouverture de la recherche : reprise du texte sélectionné");
            window.HideForCache();
            Check(query.Text == "" && !query.CanUndo && ((Grid)window.FindName("FindBar")).Visibility == Visibility.Collapsed, "Masquage : recherche et historique du champ effacés");
            Invoke(window, "OpenFind"); Search("mot"); window.SetCollapsed(true);
            Check(query.Text == "" && counter.Text == "0 / 0", "Repli : nettoyage des résultats et de la requête");
            var key = RandomNumberGenerator.GetBytes(32);
            try
            {
                NoteProtection.Protect(note, key, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));
                Invoke(window, "OpenFind"); Search("composé"); query.Text = "Requête privée en attente";
                NoteProtection.Lock(note); window.ClearSensitiveContent();
                Check(query.Text == "" && !query.CanUndo && counter.Text == "0 / 0" && preview.Document == null && editor.Text == "", "Verrouillage : requête, résultats, document et texte sensible effacés");
                Invoke(window, "OpenFind");
                Check(((Grid)window.FindName("FindBar")).Visibility == Visibility.Collapsed, "Une note verrouillée ne peut pas ouvrir la recherche");
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        finally { window.ClosePermanently(); }
        PerformanceChecks.Pump();
    }

    private static object? Invoke(NoteWindow window, string method, params object[] args) =>
        typeof(NoteWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    private static void Layout(NoteWindow window)
    {
        var content = (FrameworkElement)window.Content;
        var size = new Size(window.Width, window.Height);
        content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("OK : " + message);
    }
}
