using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BetterStickyNotes;

internal static class HomeTests
{
    public static void Run(App app)
    {
        Check(app.BuildTrayMenu().Items.OfType<MenuItem>().Select(i => i.Header.ToString()).SequenceEqual(new[] { "Nouvelle note", "Mes notes", "Quitter" }), "Menu système limité à trois commandes");
        var group = app.CreateGroup("Travail");
        var one = new Note { Title = "Titre", GroupId = group.Id }; var two = new Note { Title = "Autre", GroupId = group.Id };
        app.Store.Notes.AddRange([one, two]);
        var home = new NotesWindow(app); home.SelectView(group.Id.ToString());
        var groups = (ListBox)home.FindName("GroupList"); var list = (DataGrid)home.FindName("NoteList");
        var search = (TextBox)home.FindName("SearchBox"); list.SelectedItem = list.Items[0];
        Check(home.RenameTarget(groups).GroupId == group.Id && home.RenameTarget(list).Note == one, "F2 cible le groupe ou la note selon le focus");
        Check(home.RenameTarget(search) == (null, null), "F2 ne détourne pas la recherche");
        list.SelectAll(); Check(home.RenameTarget(list) == (null, null), "F2 refuse un renommage multiple");
        Check(home.NewNoteGroupId == group.Id, "Ctrl+N reprend le groupe affiché");
        var content = (FrameworkElement)home.Content;
        content.Measure(new Size(724, 440)); content.Arrange(new Rect(0, 0, 724, 440)); content.UpdateLayout();
        var settingsButton = (Button)home.FindName("SettingsButton");
        var bounds = settingsButton.TransformToAncestor(content).TransformBounds(new Rect(settingsButton.RenderSize));
        Check(bounds.Bottom <= 440, "Paramètres accessibles dans la fenêtre compacte");
        settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((Border)home.FindName("SettingsHost")).Child is SettingsView && home.NewNoteGroupId == null, "Accueil : paramètres intégrés, nouvelles notes sans groupe depuis cette vue");
        Check(groups.SelectedItem == null && settingsButton.Background is SolidColorBrush { Color.R: 234 }, "Paramètres seuls sélectionnés dans la navigation");
        home.SelectView(group.Id.ToString()); Check(groups.SelectedItem != null, "Retour au groupe après les paramètres");
        home.SelectView("all"); Check(((Border)home.FindName("SettingsHost")).Visibility == Visibility.Collapsed, "Retour aux notes depuis les paramètres");
        Check(home.WindowStyle == WindowStyle.None && home.AllowsTransparency && home.Content is Border { CornerRadius.TopLeft: 12 }, "Accueil avec cadre arrondi partagé");
        var rename = new GroupNameDialog(home, "Renommer", "Initial", _ => { });
        var purge = new PurgeSettingsDialog(home, app);
        Check(rename.Content is Border { CornerRadius.TopLeft: 12 } && purge.Content is Border { CornerRadius.TopLeft: 12 }, "Dialogues de renommage et purge avec mêmes arrondis");
        rename.Close(); purge.Close();
        var noteWindow = new NoteWindow(app, one);
        var editor = (TextBox)noteWindow.FindName("Editor");
        var preview = (FlowDocumentScrollViewer)noteWindow.FindName("Previewer");
        var editorStyle = (Style)editor.FindResource(typeof(ScrollBar));
        Check(ReferenceEquals(editorStyle, preview.FindResource(typeof(ScrollBar))), "Édition et aperçu partagent le même défilement discret");
        var scroll = new ScrollBar { Style = editorStyle, Maximum = 100, ViewportSize = 20, Value = 40 };
        scroll.Measure(new Size(12, 200)); scroll.Arrange(new Rect(0, 0, 12, 200)); scroll.ApplyTemplate();
        var track = (Track)scroll.Template.FindName("PART_Track", scroll);
        Check(scroll.Width == 12 && track.Thumb.MinHeight == 24 && track.Value == 40, "Barre fine avec zone de prise élargie et suivi de la position");
        var begin = typeof(NoteWindow).GetMethod("BeginTitleEdit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var cancel = typeof(NoteWindow).GetMethod("CancelTitleEdit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        begin.Invoke(noteWindow, null); ((TextBox)noteWindow.FindName("TitleBox")).Text = "Modifié"; cancel.Invoke(noteWindow, null);
        Check(one.Title == "Titre", "Échap restaure le titre avant le renommage");
        noteWindow.ClosePermanently(); home.Close(); app.Store.Notes.Clear(); app.Store.Groups.Clear();
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
