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
            var app = new App(); app.InitializeComponent();
            var trayMenu = app.BuildTrayMenu();
            Assert(trayMenu.Items.OfType<MenuItem>().Count() == 5, "Menu système limité aux cinq entrées essentielles");
            Assert(trayMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Options")).Items.OfType<MenuItem>().Count() == 3, "Corbeille et réglages disponibles dans Options");
            var window = new NoteWindow(app, new Note { Title = "Mes idées du jour", Text = markdown, Preview = true, Pinned = true });
            var handle = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
            Assert(handle != IntPtr.Zero, "Création de la fenêtre native transparente avec redimensionnement");
            window.Measure(new Size(340, 360)); window.Arrange(new Rect(0, 0, 340, 360)); window.UpdateLayout();
            Assert(window.Topmost && window.Background != null, "Construction de la fenêtre WPF avec aperçu et épinglage");
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
                trayMenu.Measure(new Size(400, 600));
                trayMenu.Arrange(new Rect(new Point(), trayMenu.DesiredSize)); trayMenu.UpdateLayout();
                var menuImage = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(trayMenu.ActualWidth), (int)Math.Ceiling(trayMenu.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                menuImage.Render(trayMenu);
                var menuPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); menuPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(menuImage));
                using var menuOutput = File.Create(Path.Combine(Path.GetDirectoryName(args[0])!, "menu-preview.png")); menuPng.Save(menuOutput);
            }
            window.ClosePermanently();
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
