using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using BetterStickyNotes;

internal static class VisualRefreshTests
{
    public static void Run(App app)
    {
        TestExcerpts();
        TestHeader(app);
    }

    private static void TestExcerpts()
    {
        var source = "## Ma journée\r\n\r\nDu **gras**, de l’*italique*, ~~barré~~ et `code`.\n\n- [x] Préparer\n- [ ] Relire\n\n[Documentation](https://example.com/prive)";
        var note = new Note { Text = source };
        var row = new NotesWindow.Row(note);
        Check(row.Excerpt == "Ma journée Du gras, de l’italique, barré et code. Préparer Relire Documentation", "Extrait : Markdown lisible, libellés des liens et tâches sans leur syntaxe");
        Check(note.Text == source, "L’extrait conserve intégralement la source Markdown");
        note.Text = "```cs\nvar total = 1;\n```\n\n> Une citation\n\nUn \\*astérisque\\* et ![un schéma](image.png)";
        Check(row.Excerpt == "var total = 1; Une citation Un *astérisque* et un schéma", "Extrait : code, citation, caractères échappés et texte alternatif conservés");
        note.Text = new string('a', 180);
        Check(row.Excerpt == note.Text, "Un extrait de 180 caractères n’est pas tronqué inutilement");
        note.Text = new string('a', 178) + "📝 suite";
        Check(row.Excerpt.Length <= 180 && row.Excerpt.EndsWith('…') && !row.Excerpt.Any(char.IsSurrogate), "La troncature ajoute une ellipse sans couper une paire Unicode");
        note.Text = "   \r\n\t";
        Check(row.Excerpt == "", "Extrait vide sans espaces résiduels");
        note.Text = "**Contenu privé**";
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            NoteProtection.Protect(note, key, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));
            Check(row.Excerpt == "Contenu privé", "Une note protégée déverrouillée bénéficie d’un extrait lisible");
            NoteProtection.Lock(note); row.Refresh();
            Check(row.Excerpt == "Contenu protégé", "Le reverrouillage anonymise immédiatement un extrait déjà calculé");
            NoteProtection.Unlock(note, key);
            Check(row.Excerpt == "Contenu privé", "Le déverrouillage recalcule l’extrait");
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static void TestHeader(App app)
    {
        var note = new Note { Title = "Un titre volontairement long pour les petits post-it" };
        var window = new NoteWindow(app, note);
        try
        {
            var content = (FrameworkElement)window.Content;
            var title = (TextBlock)window.FindName("TitleDisplay");
            foreach (var width in new[] { 260, 340, 420, 260 })
            {
                content.Measure(new Size(width, 360)); content.Arrange(new Rect(0, 0, width, 360)); content.UpdateLayout();
                Check(title.ActualWidth >= 90, "Bandeau : place réservée au titre à " + width);
                var right = title.TransformToAncestor(content).TransformBounds(new Rect(title.RenderSize)).Right;
                foreach (var name in new[] { "OptionsButton", "PinButton", "HideButton" })
                {
                    var button = (Button)window.FindName(name);
                    var bounds = button.TransformToAncestor(content).TransformBounds(new Rect(button.RenderSize));
                    Check(button.Visibility == Visibility.Visible && bounds.Left >= right && bounds.Right <= width, "Bandeau : action essentielle accessible sans chevauchement : " + name);
                }
                Check(((Button)window.FindName("CaptureScreenButton")).Visibility == (width < 360 ? Visibility.Collapsed : Visibility.Visible), "Bandeau : actions secondaires adaptées à la largeur");
            }
            ContextMenu Menu() => (ContextMenu)typeof(NoteWindow).GetMethod("BuildNoteMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            var menu = Menu();
            Check(menu.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Capturer cet écran") && i.InputGestureText == "Ctrl+Maj+S"), "Bandeau compact : capture disponible dans le menu avec son raccourci");
            menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Replier la note")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(note.Collapsed && note.Height == 360, "Le repli par le menu conserve la hauteur normale");
            Menu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Déplier la note")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(!note.Collapsed && window.Height == 360, "Le menu permet également de déplier la note");
        }
        finally { window.ClosePermanently(); }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("OK : " + message);
    }
}
