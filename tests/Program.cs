using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using BetterStickyNotes;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.FirstOrDefault() == "--benchmark") { PerformanceChecks.Run(int.Parse(args[1])); return; }
        if (args.FirstOrDefault() == "--inspect-import")
        {
            var source = StickyNotesImporter.DetectPath() ?? throw new Exception("Aucune base détectée.");
            var result = StickyNotesImporter.Read(source, []);
            Console.WriteLine($"Lecture seule : {result.Notes.Count} importables, {result.Deleted} supprimées, {result.Empty} vides, {result.AlreadyImported} doublons.");
            return;
        }
        var folder = Path.Combine(Path.GetTempPath(), "BetterStickyNotes-tests-" + Guid.NewGuid());
        try
        {
            var store = new NoteStore(folder);
            store.Notes.Add(new Note { Title = "Été 📝", Text = "# Bonjour\n- [ ] Une tâche", Pinned = true, Color = "#CDE9FA", Hidden = true, Left = -900 });
            store.Save();
            var loaded = new NoteStore(folder); loaded.Load();
            Assert(loaded.Notes.Single().Title == "Été 📝" && loaded.Notes[0].Pinned && loaded.Notes[0].Hidden && loaded.Notes[0].Left == -900, "Persistance des notes et états");
            loaded.Notes[0].Title = "Deuxième version"; loaded.Save();
            File.WriteAllText(Path.Combine(folder, "notes.json"), "invalid json");
            var recovered = new NoteStore(folder); recovered.Load();
            Assert(recovered.Notes[0].Title == "Été 📝" && recovered.LoadWarning != null, "Récupération depuis la sauvegarde");
            var markdown = "# Titre\n\nDu **gras**, de l’*italique*, ~~barré~~ et `code`.\n\n- [ ] Même tâche\n- [x] Même tâche\n\n```cs\nvar x = 1;\n```\n\n[lien](https://example.com)\n\n[dangereux](file:///C:/Windows/notepad.exe)";
            var toggles = new List<(int, bool)>();
            var doc = MarkdownView.Render(markdown, (offset, value) => toggles.Add((offset, value)));
            var inlines = Flatten(doc.Blocks).ToList();
            var boxes = inlines.OfType<InlineUIContainer>().Select(i => i.Child).OfType<CheckBox>().ToList();
            Assert(boxes.Count == 2 && boxes.All(b => b.IsEnabled), "Cases à cocher Markdown interactives");
            boxes[0].IsChecked = true; boxes[1].IsChecked = false;
            Assert(toggles.Count == 2 && toggles[0].Item1 == markdown.IndexOf("[ ]") + 1 && toggles[1].Item1 == markdown.IndexOf("[x]") + 1, "Chaque case modifie sa propre position source");
            Assert(inlines.OfType<Hyperlink>().Count() == 1, "Seuls les protocoles de liens autorisés sont cliquables");
            var app = new App(startNormally: false, dataFolder: Path.Combine(folder, "app")); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ManagementTests.Run(app, folder);
            NavigationTests.Run(app);
            TrashTests.Run(app, folder);
            OptimizationTests.Run(app, folder);
            ProtectionTests.Run(app, folder);
            FocusTests.Run(app);
            EditingTests.Run();
            ImportTests.Run(folder);
            var trayMenu = app.BuildTrayMenu();
            Assert(trayMenu.Items.OfType<MenuItem>().Count() == 7, "Menu système : essentiels, verrouillage et À propos");
            Assert(trayMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Options")).Items.OfType<MenuItem>().Count() == 4, "Import, corbeille et réglages disponibles dans Options");
            var window = new NoteWindow(app, new Note { Title = "Mes idées du jour", Text = markdown, Preview = true, Pinned = true });
            window.Opacity = 0; window.ShowActivated = false; window.Show();
            var handle = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
            Assert(handle != IntPtr.Zero, "Création de la fenêtre native transparente avec redimensionnement");
            window.Measure(new Size(340, 360)); window.Arrange(new Rect(0, 0, 340, 360)); window.UpdateLayout();
            Assert(window.Topmost && window.Background != null, "Construction de la fenêtre WPF avec aperçu et épinglage");
            var mode = typeof(NoteWindow).GetMethod("SetPreview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var editor = (TextBox)window.FindName("Editor");
            var preview = (FlowDocumentScrollViewer)window.FindName("Previewer");
            mode.Invoke(window, [false]);
            editor.Text = "bonjor";
            var spellingError = editor.GetSpellingError(0);
            Assert(spellingError != null, "Correcteur français : faute détectée en édition");
            editor.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, 0, editor, new MenuItem()) { RoutedEvent = System.Windows.Input.Keyboard.LostKeyboardFocusEvent });
            PerformanceChecks.Pump();
            Assert(SpellCheck.GetIsEnabled(editor), "Perte de focus : le correcteur reste actif sans événement d’ouverture du menu");
            spellingError!.Correct("bonjour");
            Assert(editor.Text == "bonjour", "La correction reste applicable après le transfert de focus");
            mode.Invoke(window, [true]);
            Assert(!SpellCheck.GetIsEnabled(editor), "Correcteur désactivé en aperçu");
            mode.Invoke(window, [false]);
            editor.Text = "**Dernière saisie**";
            window.Left += 1;
            Assert(editor.Visibility == Visibility.Visible, "Un repositionnement extérieur au glissement conserve l’édition");
            var dragging = typeof(NoteWindow).GetField("draggingHeader", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            dragging.SetValue(window, true); window.Left += 1; dragging.SetValue(window, false);
            Assert(preview.Visibility == Visibility.Visible && new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Dernière saisie"), "Déplacement par le bandeau : aperçu du dernier texte saisi");
            mode.Invoke(window, [false]); window.HideForCache(); window.Show();
            Assert(preview.Visibility == Visibility.Visible && ((FrameworkElement)window.FindName("EditingBar")).Visibility == Visibility.Collapsed, "Réouverture en aperçu et outils d’édition masqués");
            Assert(window.FindName("PreviewButton") == null && window.FindName("HideButton") is Button, "Masquer remplace le bouton édition dans le bandeau");
            editor.Text = markdown; mode.Invoke(window, [true]);
            if (args.Length > 0)
            {
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(340, 360)); content.Arrange(new Rect(0, 0, 340, 360)); content.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(340, 360, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(content);
                var pixels = new byte[340 * 360 * 4]; bitmap.CopyPixels(pixels, 340 * 4, 0);
                Assert(new[] { 0, 339, 340 * 359, 340 * 360 - 1 }.All(p => pixels[p * 4 + 3] == 0), "Coins extérieurs transparents sans débordement de couleur");
                var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var output = File.Create(args[0]); png.Save(output);
                typeof(NoteWindow).GetMethod("SetPreview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [false]);
                content.Measure(new Size(340, 360)); content.Arrange(new Rect(0, 0, 340, 360)); content.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var editImage = new System.Windows.Media.Imaging.RenderTargetBitmap(340, 360, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); editImage.Render(content);
                var editPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); editPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(editImage));
                using var editOutput = File.Create(Path.Combine(Path.GetDirectoryName(args[0])!, "editing-preview.png")); editPng.Save(editOutput);
                trayMenu.Measure(new Size(400, 600));
                trayMenu.Arrange(new Rect(new Point(), trayMenu.DesiredSize)); trayMenu.UpdateLayout();
                var menuImage = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(trayMenu.ActualWidth), (int)Math.Ceiling(trayMenu.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                menuImage.Render(trayMenu);
                var menuPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); menuPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(menuImage));
                using var menuOutput = File.Create(Path.Combine(Path.GetDirectoryName(args[0])!, "menu-preview.png")); menuPng.Save(menuOutput);
                var importWindow = new ImportWindow(new StickyImportResult([
                    new Note { Title = "Courses de la semaine", Text = "Courses de la semaine\n- Café\n- Fruits", Color = "#FFF1A8" },
                    new Note { Title = "Idées pour le projet", Text = "Idées pour le projet\nPréparer la prochaine démo", Color = "#CDE9FA" }
                ], 3, 1, 0), _ => Task.FromResult(false));
                importWindow.Measure(new Size(680, 500)); importWindow.Arrange(new Rect(0, 0, 680, 500)); importWindow.UpdateLayout();
                var importContent = (FrameworkElement)importWindow.Content;
                importContent.Measure(new Size(680, 500)); importContent.Arrange(new Rect(0, 0, 680, 500)); importContent.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var importImage = new System.Windows.Media.Imaging.RenderTargetBitmap(680, 500, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); importImage.Render(importContent);
                var importPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); importPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(importImage));
                using var importOutput = File.Create(Path.Combine(Path.GetDirectoryName(args[0])!, "import-preview.png")); importPng.Save(importOutput);
                importWindow.Close();
                app.Store.Notes.AddRange([
                    new Note { Title = "Déploiement API", Text = "Vérifier les migrations avant la mise en production", Hidden = true, Color = "#CDE9FA" },
                    new Note { Title = "Requêtes SQL", Text = "Optimiser la recherche dans les commandes", Hidden = false, Pinned = true },
                    new Note { Title = "Idées du sprint", Text = "Ajouter les raccourcis et améliorer les imports", Hidden = true, Color = "#D9EEC4" }
                ]);
                var notesWindow = new NotesWindow(app); notesWindow.SelectImported(app.Store.Notes.Take(2).Select(n => n.Id));
                var notesContent = (FrameworkElement)notesWindow.Content;
                notesContent.Measure(new Size(900, 580)); notesContent.Arrange(new Rect(0, 0, 900, 580)); notesContent.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var notesImage = new System.Windows.Media.Imaging.RenderTargetBitmap(900, 580, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); notesImage.Render(notesContent);
                var notesPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); notesPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(notesImage));
                using var notesOutput = File.Create(Path.Combine(Path.GetDirectoryName(args[0])!, "notes-preview.png")); notesPng.Save(notesOutput);
                ((TabControl)notesWindow.FindName("NoteTabs")).SelectedIndex = 1;
                notesContent.UpdateLayout();
                var trashImage = new System.Windows.Media.Imaging.RenderTargetBitmap(900, 580, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); trashImage.Render(notesContent);
                var trashPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); trashPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(trashImage));
                using var trashOutput = File.Create(Path.Combine(Path.GetDirectoryName(args[0])!, "trash-preview.png")); trashPng.Save(trashOutput);
                notesWindow.Close(); app.Store.Notes.Clear();
            }
            window.ClosePermanently();
            PerformanceChecks.Wait(app.SaveNowAsync()); app.Shutdown();
            Console.WriteLine("Toutes les vérifications ont réussi.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    private static IEnumerable<Inline> Flatten(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph p) foreach (var i in FlattenInlines(p.Inlines)) yield return i;
            else if (block is List list) foreach (var item in list.ListItems) foreach (var i in Flatten(item.Blocks)) yield return i;
            else if (block is Section section) foreach (var i in Flatten(section.Blocks)) yield return i;
        }
    }
    private static IEnumerable<Inline> FlattenInlines(InlineCollection inlines)
    {
        foreach (var i in inlines) { yield return i; if (i is Span span) foreach (var child in FlattenInlines(span.Inlines)) yield return child; }
    }
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception("ÉCHEC : " + name);
        Console.WriteLine("OK : " + name);
    }
}
