using System.Windows;
using System.IO;
using System.Security.Cryptography;
using BetterStickyNotes;

internal static class NavigationTests
{
    public static void Run(App app)
    {
        CheckRestoration(app);
        var note = new Note { Title = "Navigation", Left = 300, Top = 220, HasPlacement = true, Hidden = true };
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
        var executable = @"C:\Mes applications\BetterStickyNotes.exe";
        Check(App.StartupTargetsExecutable(App.StartupCommand(executable.ToUpperInvariant()), executable), "Démarrage : comparaison des chemins sans distinction de casse");
        Check(App.StartupTargetsExecutable($"  \"{executable}\" --BACKGROUND  ", executable), "Démarrage : espaces externes et argument sans distinction de casse");
        Check(App.StartupTargetsExecutable($"\"{executable}\"", executable) && App.StartupTargetsExecutable($"{executable} --background", executable), "Démarrage : anciennes commandes reconnues");
        Check(!App.StartupTargetsExecutable(App.StartupCommand(@"C:\Ancienne version\BetterStickyNotes.exe"), executable), "Démarrage : autre version détectée");
        Check(!App.StartupTargetsExecutable(null, executable) && !App.StartupTargetsExecutable("BetterStickyNotes.exe --background", executable) && !App.StartupTargetsExecutable($"\"{executable}\" --autre", executable) && !App.StartupTargetsExecutable($"\"{executable} --background", executable), "Démarrage : commande absente, relative ou invalide rejetée sans accès au registre");
        manager.Close(); app.Store.Notes.Remove(note); app.Changed();
    }

    private static void CheckRestoration(App app)
    {
        var folder = Path.Combine(Path.GetTempPath(), "BetterStickyNotes-restoration-" + Guid.NewGuid());
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var visible = new Note { Title = "À restaurer", Left = 120, Top = 120, Width = 340, Height = 360, HasPlacement = true, Pinned = true, Collapsed = true };
            var hidden = new Note { Title = "Masquée", Hidden = true };
            var deleted = new Note { Title = "Supprimée", Deleted = true };
            var locked = new Note { Title = "Verrouillée" };
            var unlocked = new Note { Title = "Protégée" };
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            NoteProtection.Protect(locked, key, salt); NoteProtection.Lock(locked);
            NoteProtection.Protect(unlocked, key, salt); NoteProtection.Lock(unlocked);
            var store = new NoteStore(folder);
            store.Notes.AddRange([visible, hidden, deleted, locked, unlocked]); store.Save();
            foreach (var background in new[] { true, false })
            {
                var loaded = new NoteStore(folder); loaded.Load();
                visible = loaded.Notes.Single(n => n.Id == visible.Id);
                unlocked = loaded.Notes.Single(n => n.Id == unlocked.Id);
                NoteProtection.Unlock(unlocked, key); unlocked.Hidden = false;
                app.Store.Notes.AddRange(loaded.Notes);
                try
                {
                    app.PrepareLaunch(background); PerformanceChecks.Pump();
                    var window = app.Windows.OfType<NoteWindow>().Single();
                    Check(!visible.Hidden && window.Title == "À restaurer" && window.IsVisible && !window.ShowActivated && window.Topmost && visible.Pinned && visible.Collapsed, "Restauration : seule la note visible non protégée est ouverte, épinglée et repliée sans activation demandée");
                    Check(visible.Left == 120 && visible.Top == 120 && visible.Width == 340 && visible.Height == 360 && window.Height == 50 && visible.HasPlacement == true, "Restauration : placement, dimensions dépliées et repli conservés après sauvegarde/rechargement");
                    Check(app.Windows.OfType<NotesWindow>().Count() == (background ? 0 : 1), "Restauration : Mes notes uniquement au lancement manuel");
                    Check(unlocked.IsProtected && !unlocked.IsLocked, "Restauration : une protection déverrouillée en mémoire reste exclue");
                    var bounds = (visible.Left, visible.Top, visible.Width, visible.Height);
                    app.ShowManager();
                    Check(app.Windows.OfType<NoteWindow>().Single() == window && !visible.Hidden && bounds == (visible.Left, visible.Top, visible.Width, visible.Height), "Relance : Mes notes sans modifier les post-it restaurés");
                }
                finally
                {
                    app.DeleteNotes([visible]);
                    foreach (var manager in app.Windows.OfType<NotesWindow>().ToList()) manager.Close();
                    foreach (var note in loaded.Notes) { if (note.IsProtected) NoteProtection.Lock(note); app.Store.Notes.Remove(note); }
                    app.Changed();
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
