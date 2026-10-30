using System.Windows;
using BetterStickyNotes;

internal static class NavigationTests
{
    public static void Run(App app)
    {
        var note = new Note { Title = "Navigation", Left = 300, Top = 220, HasPlacement = true };
        app.Store.Notes.Add(note);
        app.PrepareLaunch(background: true);
        Check(note.Hidden && !app.Windows.OfType<NotesWindow>().Any() && !app.Windows.OfType<NoteWindow>().Any(), "Démarrage discret : aucune fenêtre");
        app.PrepareLaunch(background: false);
        var manager = app.Windows.OfType<NotesWindow>().Single(); manager.Opacity = 0;
        Check(manager.IsVisible && manager.ShowInTaskbar && note.Hidden && note.Left == 300 && note.Top == 220, "Lancement normal : Mes notes dans la barre des tâches, positions conservées");
        manager.WindowState = WindowState.Minimized;
        app.ShowManager();
        Check(manager.WindowState == WindowState.Normal && app.Windows.OfType<NotesWindow>().Count() == 1 && note.Hidden, "Relance : fenêtre de gestion réutilisée et restaurée sans afficher les notes");
        app.FocusNote(note); PerformanceChecks.Pump();
        var window = app.Windows.OfType<NoteWindow>().Single();
        Check(window.IsActive && !window.Topmost && !note.Pinned, $"Retrouver une note sans activer l’épinglage (active={window.IsActive}, premier plan permanent={window.Topmost}, épinglée={note.Pinned})");
        window.WindowState = WindowState.Minimized;
        app.FocusNote(note); PerformanceChecks.Pump();
        Check(window.WindowState == WindowState.Normal && window.IsActive, "Note réduite restaurée au premier plan");
        app.DeleteNotes([note]); app.FocusNote(note);
        Check(note.Deleted && note.Hidden && !app.Windows.OfType<NoteWindow>().Any(), "Note dans la corbeille : aucune ouverture");
        Check(App.StartupCommand(@"C:\Mes applications\BetterStickyNotes.exe") == "\"C:\\Mes applications\\BetterStickyNotes.exe\" --background", "Commande de démarrage : chemin cité et mode discret");
        manager.Close(); app.Store.Notes.Remove(note); app.Changed();
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
