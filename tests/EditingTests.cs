using System.Windows.Controls;
using BetterStickyNotes;

internal static class EditingTests
{
    public static void Run()
    {
        var editor = new TextBox { AcceptsReturn = true, IsUndoEnabled = true, Text = "avant mot après" };
        var host = new System.Windows.Window { Content = editor, Width = 400, Height = 300, Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
        host.Show();
        try
        {
        editor.IsUndoEnabled = false; editor.IsUndoEnabled = true;
        editor.Select(6, 3); MarkdownEditing.Apply(editor, MarkdownTool.Bold);
        Check(editor.Text == "avant **mot** après" && editor.SelectedText == "mot", "Gras sur sélection et conservation du texte autour");
        editor.Undo(); Check(editor.Text == "avant mot après", "Annulation en une opération");
        editor.Redo(); Check(editor.Text == "avant **mot** après", "Rétablissement de l’édition");
        editor.Text = "un\r\ndeux\r\ntrois"; editor.Select(0, 9); MarkdownEditing.Apply(editor, MarkdownTool.Checklist);
        Check(editor.Text == "- [ ] un\r\n- [ ] deux\r\ntrois", "Checklist multiligne sans modifier la ligne après la sélection");
        editor.Text = "- [x] fini"; editor.Select(0, editor.Text.Length); MarkdownEditing.Apply(editor, MarkdownTool.List);
        Check(editor.Text == "- fini", "Conversion de checklist en liste sans préfixes empilés");
        editor.Text = "a\nb"; editor.SelectAll(); MarkdownEditing.Apply(editor, MarkdownTool.Code);
        Check(editor.Text == "```\na\nb\n```", "Sélection multiligne transformée en bloc de code");
        editor.Text = "`x`"; editor.SelectAll(); MarkdownEditing.Apply(editor, MarkdownTool.Code);
        Check(editor.Text == "`` `x` ``", "Code contenant déjà des accents graves");
        editor.Text = "documentation"; editor.SelectAll(); MarkdownEditing.Apply(editor, MarkdownTool.Link);
        Check(editor.Text == "[documentation](https://example.com)" && editor.SelectedText == "https://example.com", "Lien avec URL sélectionnée pour remplacement");
        editor.Text = ""; MarkdownEditing.Apply(editor, MarkdownTool.Italic);
        Check(editor.Text == "*texte en italique*" && editor.SelectedText == "texte en italique", "Exemple remplaçable sans sélection");
        }
        finally { host.Close(); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
