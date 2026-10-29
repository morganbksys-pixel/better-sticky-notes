using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using BetterStickyNotes;

internal static class FocusTests
{
    public static void Run(App app)
    {
        var note = new Note { Text = "Initial" };
        var window = new NoteWindow(app, note) { Opacity = 0 };
        var other = new Window { Width = 100, Height = 100, Opacity = 0, ShowInTaskbar = false };
        var second = new NoteWindow(app, new Note()) { Opacity = 0 };
        var mode = typeof(NoteWindow).GetMethod("SetPreview", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var editor = (TextBox)window.FindName("Editor");
        var preview = (FlowDocumentScrollViewer)window.FindName("Previewer");
        void Edit()
        {
            window.Activate(); PerformanceChecks.Pump();
            Check(window.IsActive, "Fenêtre de test active");
            mode.Invoke(window, [false]); editor.Focus();
        }
        try
        {
            window.Show(); other.Show(); second.Show();
            Edit(); editor.Text = "**Dernière saisie**";
            other.Activate(); PerformanceChecks.Pump();
            Check(note.Preview && preview.Visibility == Visibility.Visible &&
                new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("Dernière saisie"),
                "Autre fenêtre : aperçu Markdown de la dernière saisie");
            window.Activate(); PerformanceChecks.Pump();
            Check(note.Preview, "Retour sur la note : reste en aperçu");
            Edit(); second.Activate(); PerformanceChecks.Pump();
            Check(note.Preview, "Autre note : passage en aperçu");

            Edit();
            var menu = new ContextMenu { PlacementTarget = editor };
            var command = new MenuItem { Header = "Mettre en gras" };
            command.Click += (_, _) => MarkdownEditing.Apply(editor, MarkdownTool.Bold);
            menu.Items.Add(command); editor.ContextMenu = menu;
            editor.Text = "texte"; editor.SelectAll(); menu.IsOpen = true; PerformanceChecks.Pump();
            Check(!note.Preview && SpellCheck.GetIsEnabled(editor), "Menu du texte : édition et correction conservées");
            command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); menu.IsOpen = false; PerformanceChecks.Pump();
            Check(!note.Preview && editor.Text == "**texte**", "Action de menu applicable à la sélection");
            editor.ContextMenu = null;

            var helpButton = ((StackPanel)window.FindName("EditingBar")).Children.OfType<Button>().Single(b => Equals(b.Content, "?"));
            helpButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); PerformanceChecks.Pump();
            var help = (Popup)typeof(NoteWindow).GetField("markdownHelp", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Check(help.IsOpen && !note.Preview, "Aide Markdown : édition conservée");
            other.Activate(); PerformanceChecks.Pump();
            Check(note.Preview && !help.IsOpen, "Sortie depuis l’aide : aperçu et fermeture de l’aide");
        }
        finally { window.ClosePermanently(); second.ClosePermanently(); other.Close(); }
    }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
