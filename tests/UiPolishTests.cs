using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using BetterStickyNotes;

internal static class UiPolishTests
{
    public static void Run(App app)
    {
        var note = new Note(); var window = new NoteWindow(app, note);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(340, 360)); content.Arrange(new Rect(0, 0, 340, 360)); content.UpdateLayout();
        var grip = (ResizeGrip)window.FindName("CornerGrip");
        Check(WindowChrome.GetWindowChrome(window).ResizeBorderThickness == new Thickness(9), "Bords de redimensionnement élargis à 9 unités WPF");
        Check(grip.ActualWidth == 24 && grip.ActualHeight == 24 && WindowChrome.GetResizeGripDirection(grip) == ResizeGripDirection.BottomRight, "Poignée 24 × 24 reliée au redimensionnement natif diagonal");
        var hit = VisualTreeHelper.HitTest(content, new Point(327, 347))?.VisualHit;
        while (hit != null && hit != grip) hit = VisualTreeHelper.GetParent(hit);
        Check(hit == grip, "Zone saisissable à 13 unités du bord, au-delà de la bordure native");
        var curve = (System.Windows.Shapes.Path)grip.Template.FindName("Curve", grip);
        Check(curve.Data.ToString().Contains("A") && curve.Opacity == 0.3, "Trait courbe discret dans la poignée");
        Check(((TextBox)window.FindName("Editor")).Margin.Right >= 24 && ((StackPanel)window.FindName("EditingBar")).Margin.Right >= 24, "Texte et outils dégagés de la poignée");
        window.ClosePermanently();
        var group = app.CreateGroup("Test"); app.Store.Notes.Add(note);
        var manager = new NotesWindow(app);
        var create = (Button)manager.FindName("NewNoteButton");
        Check(Equals(create.Content, "＋ Nouvelle note") && ((SolidColorBrush)create.Background).Color == (Color)ColorConverter.ConvertFromString("#FFF1A8"), "Création : libellé Nouvelle note sur fond jaune");
        Check(manager.FindName("ViewMenuButton") == null, "Bouton de menu supprimé de l’en-tête");
        manager.SelectView("trash");
        Check(manager.BuildNotesMenu().Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Purge automatique…")).IsEnabled, "Purge accessible au clic droit même dans une corbeille vide");
        foreach (var item in manager.BuildNotesMenu().Items.OfType<MenuItem>()) Check(item.Icon != null, "Icône dans le menu : " + item.Header);
        manager.SelectView("all");
        foreach (var item in manager.BuildNotesMenu().Items.OfType<MenuItem>()) Check(item.Icon != null, "Icône dans le menu : " + item.Header);
        Check(app.BuildGroupMenu([note]).Icon is System.Windows.Shapes.Path, "Entrée Groupe avec dossier plein");
        Check(((TextBlock)NoteIcons.Action("Supprimer définitivement…")!).Text == "\uE74D", "Suppression : même corbeille que dans le post-it");
        manager.Close(); app.Store.Notes.Clear(); app.Store.Groups.Clear();
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
