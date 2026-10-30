using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BetterStickyNotes;

internal static class CaptureManagementTests
{
    public static void Run(App app, string folder)
    {
        TestViewer(app);
        TestManagement(app, folder);
        TestSettings(app);
    }

    public static void RunWindows(App app)
    {
        var note = new Note { Title = "Suppression des captures", Text = "Texte à conserver", Hidden = true };
        app.Store.Notes.Add(note);
        var first = app.Store.AddCapture(note, Image(32, 16));
        var second = app.Store.AddCapture(note, Image(16, 32));
        app.ShowManager([note.Id]);
        var manager = app.Windows.OfType<NotesWindow>().Single(); manager.Opacity = 0;
        try
        {
            manager.ShowCaptures(note);
            var panel = (CaptureManagementView)((Border)manager.FindName("CapturePanelHost")).Child;
            void RemoveAll(bool confirm)
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                var handled = false;
                timer.Tick += (_, _) =>
                {
                    var dialog = app.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w.Title == "Supprimer les captures");
                    if (dialog == null) return;
                    timer.Stop(); dialog.Opacity = 0;
                    var buttons = Visuals((DependencyObject)dialog.Content).OfType<Button>().ToArray();
                    var cancel = buttons.Single(b => Equals(b.Content, "Annuler"));
                    var remove = buttons.Single(b => Equals(b.Content, "Supprimer les 2 captures"));
                    Check(cancel.IsDefault && Visuals((DependencyObject)dialog.Content).OfType<TextBlock>().Any(t => t.Text.Contains(note.Title) && t.Text.Contains("Le texte de la note sera conservé")), "Captures : confirmation nomme la note et sélectionne Annuler par défaut");
                    handled = true;
                    (confirm ? remove : cancel).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                timer.Start();
                try
                {
                    var remove = Visuals(panel).OfType<Button>().Single(b => Equals(b.Content, "Supprimer toutes les captures…"));
                    PerformanceChecks.Wait(app.Dispatcher.InvokeAsync(() => remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))).Task);
                    PerformanceChecks.Wait(app.SaveNowAsync()); PerformanceChecks.Pump();
                    Check(handled, "Captures : dialogue de suppression globale présenté");
                }
                finally { timer.Stop(); }
            }
            RemoveAll(false);
            Check(note.Captures.Count == 2 && File.Exists(app.Store.CapturePath(note, first)), "Captures : annuler conserve toutes les images");
            RemoveAll(true);
            var list = (DataGrid)manager.FindName("NoteList");
            var row = list.Items.Cast<NotesWindow.Row>().Single(r => r.Note == note);
            Check(note.Captures.Count == 0 && note.Text == "Texte à conserver" && row.CaptureSummary == "" && note.Hidden &&
                !File.Exists(app.Store.CapturePath(note, first)) && !File.Exists(app.Store.CapturePath(note, second)), "Captures : confirmation supprime les PNG et synchronise Mes notes sans toucher au texte ni afficher le post-it");
        }
        finally
        {
            manager.Close(); app.DeleteNotes([note]); app.PermanentlyDeleteNotes([note]); PerformanceChecks.Wait(app.SaveNowAsync());
        }
    }

    private static void TestViewer(App app)
    {
        foreach (var screen in new[] { new Rect(0, 0, 800, 600), new Rect(-2560, -200, 2560, 1440) })
            Check(screen.Contains(CaptureViewerWindow.FitBounds(screen, new Rect(screen.Right - 50, screen.Bottom - 50, 340, 360))), "Aperçu : fenêtre contenue dans l’écran, y compris à coordonnées négatives");
        var note = new Note { Title = "Formats d’image" }; app.Store.Notes.Add(note);
        var owner = new NoteWindow(app, note);
        try
        {
            foreach (var imageSize in new[] { (3840, 2160), (1200, 2400), (3000, 600) })
            {
                var capture = app.Store.AddCapture(note, Image(imageSize.Item1, imageSize.Item2));
                var file = app.Store.CapturePath(note, capture);
                var original = File.ReadAllBytes(file);
                var viewer = new CaptureViewerWindow(app.Store, note, capture, owner);
                try
                {
                    foreach (var size in new[] { new Size(440, 320), new Size(900, 600) })
                    {
                        Layout(viewer, size);
                        var image = viewer.CaptureImage;
                        var bounds = image.TransformToAncestor((Visual)viewer.Content).TransformBounds(new Rect(image.RenderSize));
                        Check(image.ActualWidth > 0 && image.ActualHeight > 0 && bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width + 1 && bounds.Bottom <= size.Height + 1,
                            $"Aperçu : image {imageSize} entière dans une fenêtre {size}");
                        Check(Math.Abs(image.ActualWidth / image.ActualHeight - (double)capture.Width / capture.Height) < 0.01, "Aperçu : proportions conservées au redimensionnement");
                    }
                    Check(File.ReadAllBytes(file).SequenceEqual(original), "Aperçu : résolution et PNG d’origine inchangés");
                }
                finally { viewer.Close(); }
            }
        }
        finally
        {
            owner.ClosePermanently();
            app.DeleteNotes([note]); app.PermanentlyDeleteNotes([note]); PerformanceChecks.Wait(app.SaveNowAsync());
        }
    }

    private static void TestManagement(App app, string folder)
    {
        var one = new Note { Title = "Images masquées", Hidden = true };
        var two = new Note { Title = "Autre note", Hidden = true };
        app.Store.Notes.AddRange([one, two]);
        var first = app.Store.AddCapture(one, Image(40, 20));
        var manager = new NotesWindow(app);
        try
        {
            var list = (DataGrid)manager.FindName("NoteList");
            var row = list.Items.Cast<NotesWindow.Row>().Single(r => r.Note == one);
            list.SelectedItem = row;
            manager.BuildNotesMenu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Captures…")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var panel = (CaptureManagementView)((Border)manager.FindName("CapturePanelHost")).Child;
            Check(panel.CurrentNote == one && one.Hidden && !app.Windows.OfType<NoteWindow>().Any(), "Mes notes : consulter les captures d’une note masquée sans ouvrir le post-it");
            Check(row.CaptureSummary == "1 capture", "Mes notes : compteur de captures dans la ligne");
            foreach (var size in new[] { new Size(980, 620), new Size(724, 440), new Size(724, 620) })
            {
                Layout(manager, size);
                var host = (Border)manager.FindName("CapturesHost");
                var bounds = host.TransformToAncestor((Visual)manager.Content).TransformBounds(new Rect(host.RenderSize));
                Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width + 1 && bounds.Bottom <= size.Height + 1 && host.ActualWidth >= 200 && host.ActualHeight >= 70,
                    "Mes notes : volet de captures utilisable et contenu dans la fenêtre compacte");
                Check(list.SelectedItems.Count == 1 && list.SelectedItem == row, "Mes notes : adaptation du volet conserve la sélection native");
            }
            list.SelectAll();
            Check(list.SelectedItems.Count == 2 && ((Border)manager.FindName("CapturesHost")).Visibility == Visibility.Collapsed &&
                !manager.BuildNotesMenu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Captures…")).IsEnabled, "Mes notes : sélection multiple préservée, gestion de captures limitée à une note");
            list.UnselectAll(); list.SelectedItem = row; manager.ShowCaptures(one);
            app.Store.AddCapture(one, Image(20, 40)); manager.RefreshCaptureState();
            Check(row.CaptureSummary == "2 captures" && Visuals(panel).OfType<Image>().Count() == 2, "Mes notes : compteur et vignettes actualisés après ajout");
            var export = Path.Combine(folder, "capture-export.png");
            app.Store.ExportCapture(one, first, export);
            Check(File.ReadAllBytes(export).SequenceEqual(File.ReadAllBytes(app.Store.CapturePath(one, first))) && one.Captures.Count == 2, "Enregistrer sous : copie PNG identique sans modifier la note");
            var remove = Visuals(panel).OfType<Button>().First(b => Equals(b.Content, "Supprimer"));
            PerformanceChecks.Wait(app.Dispatcher.InvokeAsync(() => remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))).Task);
            PerformanceChecks.Wait(app.SaveNowAsync()); PerformanceChecks.Pump();
            Check(one.Captures.Count == 1 && row.CaptureSummary == "1 capture" && Visuals(panel).OfType<Image>().Count() == 1 && !File.Exists(app.Store.CapturePath(one, first)), "Mes notes : suppression ciblée synchronise compteur, vignettes et fichier");
            app.DeleteNotes([one]); manager.SelectView("trash");
            list.SelectedItem = list.Items.Cast<NotesWindow.Row>().Single(r => r.Note == one); manager.ShowCaptures(one);
            var buttons = Visuals(panel).OfType<Button>().ToList();
            Check(buttons.Any(b => Equals(b.Content, "Enregistrer sous…")) && buttons.All(b => !Equals(b.Content, "Supprimer") && !Equals(b.Content, "Supprimer toutes les captures…")), "Corbeille : consultation et export disponibles, suppression d’images désactivée");
            var captureCount = one.Captures.Count;
            var forbidden = app.RemoveCapturesAsync(one, one.Captures.ToArray()); PerformanceChecks.Wait(forbidden);
            Check(!forbidden.Result && one.Captures.Count == captureCount, "Corbeille : action périmée de suppression refusée");
            app.RestoreNotes([one]); manager.SelectView("all");
            list.SelectedItem = list.Items.Cast<NotesWindow.Row>().Single(r => r.Note == one); manager.ShowCaptures(one);
            Check(Visuals(panel).OfType<Button>().Any(b => Equals(b.Content, "Supprimer toutes les captures…")), "Restauration : gestion des captures rétablie");
            ((Button)manager.FindName("CloseCapturesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((Grid)manager.FindName("NotesListHost")).Visibility == Visibility.Visible && list.SelectedItems.Count == 1, "Mes notes : fermeture du volet restitue la liste et sa sélection");
        }
        finally
        {
            manager.Close(); app.DeleteNotes([one, two]); app.PermanentlyDeleteNotes([one, two]); PerformanceChecks.Wait(app.SaveNowAsync());
        }
    }

    private static void TestSettings(App app)
    {
        var settings = new SettingsView(app);
        var texts = Visuals(settings.Content as DependencyObject ?? settings).OfType<TextBlock>().Select(t => t.Text).ToArray();
        Check(!texts.Contains("À propos") && !texts.Contains("Exécutable actif") && !texts.Contains(AppInfo.DisplayName), "Paramètres : bloc À propos et chemin de l’exécutable retirés");
        Check(texts.Any(t => t.Contains("Fermez Sticky Notes")) && texts.Any(t => t.Contains("originales resteront intactes")) && texts.Any(t => t.Contains("Les images et la mise en forme enrichie")), "Import : étapes et contenu récupéré expliqués avant de choisir un fichier");
        var help = Visuals(settings.Content as DependencyObject ?? settings).OfType<Expander>().Single();
        var locations = Visuals((DependencyObject)help.Content).OfType<TextBox>().Select(t => t.Text).ToArray();
        Check(Equals(help.Header, "Où trouver mes notes ?") && locations.Length == 2 && locations.All(t => t.StartsWith("%LocalAppData%")), "Import : aide dépliable et chemins locaux copiables");
    }

    private static BitmapSource Image(int width, int height)
    {
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, new byte[width * height * 4], width * 4);
        image.Freeze(); return image;
    }

    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        yield return root;
        // Logical traversal includes collapsed import help and content before a window is shown.
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var nested in Visuals(child)) yield return nested;
    }

    private static void Layout(Window window, Size size)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("OK : " + message);
    }
}
