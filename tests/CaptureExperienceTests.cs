using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BetterStickyNotes;

internal static class CaptureExperienceTests
{
    public static void RunWindows(App app)
    {
        var note = new Note { Title = "Navigation clavier", Hidden = true };
        app.Store.Notes.Add(note);
        var first = app.Store.AddCapture(note, Image(900, 600));
        var second = app.Store.AddCapture(note, Image(600, 900));
        app.Open(note);
        var owner = app.Windows.OfType<NoteWindow>().Single(w => w.Title == note.Title);
        var viewer = new CaptureViewerWindow(app.Store, note, first, owner) { Opacity = 0, ShowActivated = false };
        try
        {
            viewer.Show(); PerformanceChecks.Pump();
            var source = PresentationSource.FromVisual(viewer)!;
            void Arrow(Key key) => viewer.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Arrow(Key.Right);
            Check(viewer.Current?.Capture == second && viewer.CounterText == "2 / 2", "Visio native : flèche droite passe à la capture suivante");
            Arrow(Key.Right);
            Check(viewer.Current?.Capture == first, "Visio native : flèche droite boucle vers la première");
            Arrow(Key.Left);
            Check(viewer.Current?.Capture == second, "Visio native : flèche gauche boucle vers la dernière");
            TestGalleryKeyboard(app);
        }
        finally
        {
            viewer.Close(); app.DeleteNotes([note]); app.PermanentlyDeleteNotes([note]); PerformanceChecks.Wait(app.SaveNowAsync());
        }
    }

    private static void TestGalleryKeyboard(App app)
    {
        var manager = new NotesWindow(app) { Opacity = 0 };
        try
        {
            manager.Show(); manager.ShowGallery(); PerformanceChecks.Pump();
            var gallery = (CaptureGalleryView)((Border)manager.FindName("GalleryHost")).Child;
            var cards = All(gallery).OfType<ListBox>().Single();
            var first = (ListBoxItem)cards.Items[0]; cards.SelectedItem = first; first.Focus(); PerformanceChecks.Pump();
            var source = PresentationSource.FromVisual(manager)!;
            var right = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent };
            first.RaiseEvent(right); PerformanceChecks.Pump();
            Check(cards.SelectedIndex == 1, "Galerie native : flèche droite sélectionne la capture suivante");
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            ((ListBoxItem)cards.Items[1]).RaiseEvent(escape);
            Check(gallery.SelectedEntries.Count == 0 && escape.Handled, "Galerie native : Échap désélectionne sans fermer Mes notes");
        }
        finally { manager.Close(); }
    }

    public static void Run(App app)
    {
        var note = new Note { Title = "Trois captures", Hidden = true };
        var other = new Note { Title = "Autre origine", Hidden = true };
        var trash = new Note { Title = "Images à la corbeille", Hidden = true };
        app.Store.Notes.AddRange([note, other, trash]);
        var first = app.Store.AddCapture(note, Image(1200, 600));
        var second = app.Store.AddCapture(note, Image(300, 800));
        var third = app.Store.AddCapture(note, Image(1600, 300));
        app.Store.AddCapture(other, Image(60, 40));
        app.Store.AddCapture(trash, Image(40, 60)); app.DeleteNotes([trash]);
        var owner = new NoteWindow(app, note);
        try
        {
            TestZoomAndNavigation(app, owner, note, first, second, third);
            TestSection(app, owner, note);
            TestGallery(app, note, other, trash);
        }
        finally
        {
            owner.ClosePermanently(); app.DeleteNotes([note, other, trash]); app.PermanentlyDeleteNotes([note, other, trash]);
            PerformanceChecks.Wait(app.SaveNowAsync());
        }
    }

    private static void TestZoomAndNavigation(App app, NoteWindow owner, Note note, NoteCapture first, NoteCapture second, NoteCapture third)
    {
        var viewer = new CaptureViewerWindow(app.Store, note, first, owner);
        try
        {
            Layout(viewer, new Size(640, 460));
            var viewport = All(viewer.Content as DependencyObject ?? viewer).OfType<ScrollViewer>().Single();
            Check(viewer.CounterText == "1 / 3" && viewer.IsFitting, "Visio : première image ajustée et compteur de la note");
            viewer.MoveCapture(-1); Layout(viewer, new Size(640, 460));
            Check(viewer.Current?.Capture == third && viewer.CounterText == "3 / 3", "Visio : précédente depuis la première boucle vers la dernière");
            viewer.MoveCapture(1); Layout(viewer, new Size(640, 460));
            Check(viewer.Current?.Capture == first, "Visio : suivante depuis la dernière boucle vers la première");
            viewer.ZoomAt(1, new Point(viewport.ViewportWidth / 2, viewport.ViewportHeight / 2)); Layout(viewer, new Size(640, 460));
            Check(!viewer.IsFitting && viewer.ZoomFactor == 1 && viewer.CaptureImage.ActualWidth == first.Width, "Zoom : 100 % conserve la taille réelle des pixels");
            var anchor = new Point(150, 110);
            Point PixelAt()
            {
                var local = viewer.CaptureImage.TransformToAncestor(viewport).Inverse!.Transform(anchor);
                return new Point(local.X / viewer.ZoomFactor, local.Y / viewer.ZoomFactor);
            }
            var before = PixelAt();
            viewer.ZoomAt(2, anchor); Layout(viewer, new Size(640, 460));
            var after = PixelAt();
            Check((after - before).Length < 2, "Zoom : le pixel sous le pointeur reste au même endroit");
            Layout(viewer, new Size(800, 540));
            Check(viewer.ZoomFactor == 2, "Zoom manuel conservé pendant le redimensionnement");
            viewer.ZoomAt(999, anchor); Layout(viewer, new Size(640, 460));
            Check(viewer.ZoomFactor == 8, "Zoom : limite haute à 800 %");
            viewer.ZoomAt(0.0001, anchor); Layout(viewer, new Size(640, 460));
            Check(viewer.ZoomFactor == 0.01, "Zoom : limite basse à 1 %");
            viewer.FitImage(); Layout(viewer, new Size(440, 320));
            Check(viewer.IsFitting && viewer.CaptureImage.ActualWidth <= viewport.ViewportWidth + 1 && viewer.CaptureImage.ActualHeight <= viewport.ViewportHeight + 1, "Adapter : retour à l’image entière dans la fenêtre");
            viewer.ZoomAt(2, new Point(100, 100)); viewer.MoveCapture(1); Layout(viewer, new Size(640, 460));
            Check(viewer.Current?.Capture == second && viewer.IsFitting && viewer.CounterText == "2 / 3", "Navigation : chaque nouvelle image repart en affichage ajusté");
        }
        finally { viewer.Close(); }
        var single = new CaptureViewerWindow(app.Store, note, first, owner, [new CaptureReference(note, first)]);
        try
        {
            Check(All((DependencyObject)single.Content).OfType<Button>().Where(b => b.ToolTip?.ToString()?.StartsWith("Capture ") == true).All(b => !b.IsEnabled), "Visio : navigation désactivée pour une seule capture");
            Layout(single, new Size(640, 460)); single.ZoomAt(2, new Point(100, 100)); single.MoveCapture(1);
            Check(single.ZoomFactor == 2 && !single.IsFitting, "Visio : une flèche sans autre capture conserve le zoom");
        }
        finally { single.Close(); }
    }

    private static void TestSection(App app, NoteWindow owner, Note note)
    {
        Layout(owner, new Size(340, 400));
        var preview = (FrameworkElement)owner.FindName("Previewer");
        var height = preview.ActualHeight; var windowHeight = owner.Height;
        owner.SetCapturesCollapsed(true); Layout(owner, new Size(340, 400));
        Check(note.CapturesCollapsed && !note.Collapsed && preview.ActualHeight > height + 50 && owner.Height == windowHeight, "Section Captures : repli vers le bas rend l’espace au texte sans redimensionner la note");
        Check(((ScrollViewer)owner.FindName("CaptureThumbnails")).Visibility == Visibility.Collapsed && ((TextBlock)owner.FindName("CaptureSectionLabel")).Text == "3", "Section Captures : seule la poignée centrale avec compteur reste visible");
        owner.SetCollapsed(true); owner.SetCollapsed(false);
        Check(note.CapturesCollapsed, "Section Captures : état indépendant du repli du post-it");
        app.Store.AddCapture(note, Image(60, 30)); owner.RefreshCaptures();
        Check(note.CapturesCollapsed && ((TextBlock)owner.FindName("CaptureSectionLabel")).Text == "4" && ((ScrollViewer)owner.FindName("CaptureThumbnails")).Visibility == Visibility.Collapsed, "Section Captures : ajout actualise le compteur sans déplier");
        PerformanceChecks.Wait(app.SaveNowAsync());
        var loaded = new NoteStore(Path.GetDirectoryName(app.Store.CaptureFolder)); loaded.Load();
        var saved = loaded.Notes.Single(n => n.Id == note.Id);
        Check(saved.CapturesCollapsed && saved.Captures.Count == 4, "Section Captures : repli et images conservés après rechargement");
        var restored = new NoteWindow(app, saved);
        try { Check(((ScrollViewer)restored.FindName("CaptureThumbnails")).Visibility == Visibility.Collapsed, "Section Captures : réouverture avec les vignettes repliées"); }
        finally { restored.ClosePermanently(); }
        owner.SetCapturesCollapsed(false); Layout(owner, new Size(340, 400));
        Check(((StackPanel)owner.FindName("CaptureItems")).Children.Count == 4, "Section Captures : dépliage recharge toutes les vignettes");
        Layout(owner, new Size(340, 220));
        var thumbnails = (ScrollViewer)owner.FindName("CaptureThumbnails");
        Check(thumbnails.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden && preview.ActualHeight >= 55,
            "Note compacte : bande de captures sans barre visible, espace conservé pour le texte");
        Check(((FlowDocumentScrollViewer)preview).VerticalScrollBarVisibility == ScrollBarVisibility.Hidden, "Note vide : aucun ascenseur parasite dans le texte");
        Layout(owner, new Size(340, 400));
    }

    private static void TestGallery(App app, Note note, Note other, Note trash)
    {
        var isolated = new NoteStore(Path.Combine(app.Store.CaptureFolder, "catalog-test"));
        var secret = new Note { Title = "Métadonnées protégées", Protection = new EncryptedNote(1, "", "", "", ""), Captures = note.Captures.ToList() };
        isolated.Notes.AddRange([note, secret]);
        Check(CaptureCatalog.ForNote(secret).Count == 0 && CaptureCatalog.ForGallery(isolated).All(c => c.Note != secret), "Galerie : notes protégées exclues même en présence de métadonnées d’images");
        var manager = new NotesWindow(app);
        try
        {
            ((Button)manager.FindName("GalleryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var gallery = (CaptureGalleryView)((Border)manager.FindName("GalleryHost")).Child;
            Check(gallery.Entries.Count == 5 && gallery.Entries.All(c => !c.Note.Deleted) && note.Hidden && other.Hidden, "Galerie : captures de toutes les notes actives, y compris masquées, sans ouvrir les post-it");
            Check(gallery.Entries.Select(c => c.Capture.CreatedAtUtc).SequenceEqual(gallery.Entries.Select(c => c.Capture.CreatedAtUtc).OrderByDescending(t => t)), "Galerie : captures les plus récentes en premier");
            Check(((TextBlock)manager.FindName("GalleryCount")).Text == "5" && manager.NewNoteGroupId == null, "Galerie : compteur global et créations sans groupe");
            foreach (var size in new[] { new Size(724, 440), new Size(980, 620) })
            {
                Layout(manager, size);
                Check(gallery.ActualWidth > 300 && gallery.ActualHeight > 200, "Galerie : disposition utilisable en fenêtre compacte");
            }
            var ordered = gallery.Entries.ToArray();
            var viewer = new CaptureViewerWindow(app.Store, ordered[0].Note, ordered[0].Capture, manager, ordered);
            try
            {
                viewer.MoveCapture(-1);
                Check(viewer.Current == ordered[^1], "Galerie : navigation en boucle sur la liste affichée, entre notes différentes");
                viewer.MoveCapture(1);
                viewer.MoveCapture(Array.FindIndex(ordered, c => c.Note != ordered[0].Note));
                Check(viewer.Current?.Note != ordered[0].Note, "Galerie : la navigation atteint aussi les captures d’une autre note");
            }
            finally { viewer.Close(); }
            manager.ShowGallery(trash: true);
            var cards = All(gallery).OfType<ListBox>().Single();
            cards.SelectAll();
            PerformanceChecks.Wait(gallery.DeleteSelectedAsync(_ => throw new Exception("La corbeille ne doit pas proposer de suppression")));
            Check(gallery.Entries.Count == 1 && gallery.Entries[0].Note == trash && trash.Captures.Count == 1, "Galerie Corbeille : consultation sans suppression des images");
            manager.ShowGallery();
            Check(gallery.SelectedEntries.Count == 0 && cards.SelectionMode == SelectionMode.Extended, "Galerie : changement de filtre efface la sélection, sélection multiple native");
            cards.SelectedItem = cards.Items[0];
            All(gallery).OfType<Button>().First(b => Equals(b.Content, "Retrouver la note")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var list = (DataGrid)manager.FindName("NoteList");
            Check(((Border)manager.FindName("GalleryHost")).Visibility == Visibility.Collapsed && list.SelectedItems.Count == 1 && note.Hidden && other.Hidden, "Galerie : retrouver la note sélectionne sa ligne sans afficher ni épingler le post-it");
            manager.ShowGallery();
            cards.SelectedItems.Add(cards.Items[0]); cards.SelectedItems.Add(cards.Items[1]);
            var selected = gallery.SelectedEntries.ToArray();
            selected[0].Note.Title += " renommée"; gallery.Refresh();
            Check(gallery.SelectedEntries.Select(c => c.Capture.Id).SequenceEqual(selected.Select(c => c.Capture.Id)), "Galerie : sélection conservée après actualisation des titres");
            var folder = Path.Combine(app.Store.CaptureFolder, "export-gallery"); Directory.CreateDirectory(folder);
            gallery.ExportSelectionTo(folder); gallery.ExportSelectionTo(folder);
            Check(Directory.GetFiles(folder).Length == 4 && selected.All(c => File.Exists(app.Store.CapturePath(c.Note, c.Capture))), "Galerie : export multiple sans écraser les fichiers existants ni déplacer les originaux");
            PerformanceChecks.Wait(gallery.DeleteSelectedAsync(count => { Check(count == 2, "Galerie : confirmation porte sur le nombre d’images"); return false; }));
            Check(gallery.Entries.Count == 5, "Galerie : annuler conserve toutes les captures");
            PerformanceChecks.Wait(gallery.DeleteSelectedAsync(_ => true));
            PerformanceChecks.Wait(app.SaveNowAsync()); PerformanceChecks.Pump();
            Check(gallery.Entries.Count == 3 && selected.All(c => !c.Note.Captures.Contains(c.Capture)) && app.Store.Notes.Contains(note) && app.Store.Notes.Contains(other), "Galerie : suppression multiple actualise les images sans supprimer les notes");
        }
        finally { manager.Close(); }
    }

    private static BitmapSource Image(int width, int height)
    {
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, new byte[width * height * 4], width * 4);
        image.Freeze(); return image;
    }

    private static IEnumerable<DependencyObject> All(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var nested in All(child)) yield return nested;
    }

    private static void Layout(Window window, Size size)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
        PerformanceChecks.Pump(); content.UpdateLayout();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("OK : " + message);
    }
}
