using System.IO;
using System.Windows.Controls;
using BetterStickyNotes;

internal static class OptimizationTests
{
    public static void Run(App app, string folder)
    {
        var store = new NoteStore(Path.Combine(folder, "async-save"));
        store.Notes.Add(new Note { Title = "capture initiale" });
        var first = store.SaveAsync(); store.Notes[0].Title = "modification après capture";
        first.GetAwaiter().GetResult();
        var read = new NoteStore(Path.Combine(folder, "async-save")); read.Load();
        Check(read.Notes[0].Title == "capture initiale", "Sauvegarde : instantané indépendant des modifications suivantes");
        var writes = new List<Task>();
        for (var i = 0; i < 20; i++) { store.Notes[0].Title = "version " + i; writes.Add(store.SaveAsync()); }
        Task.WhenAll(writes).GetAwaiter().GetResult(); read.Load();
        Check(read.Notes[0].Title == "version 19", "Sauvegardes concurrentes sérialisées, dernière version conservée");
        using (var locked = new FileStream(Path.Combine(folder, "async-save", "notes.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            try { store.SaveAsync().GetAwaiter().GetResult(); throw new Exception("Le verrou aurait dû refuser l’écriture"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        store.Notes[0].Title = "récupération"; store.SaveAsync().GetAwaiter().GetResult(); read.Load();
        Check(read.Notes[0].Title == "récupération", "Une erreur disque ne bloque pas les sauvegardes suivantes");

        var samples = Enumerable.Range(0, 8).Select(i => new Note { Title = "Test " + i, Text = "0123456789 contenu", Hidden = true, HasPlacement = true }).ToList();
        app.Store.Notes.AddRange(samples);
        var manager = new NotesWindow(app);
        var list = (DataGrid)manager.FindName("NoteList");
        var original = list.Items.Cast<NotesWindow.Row>().ToArray();
        int changed = 0, untouched = 0, events = 0;
        original[0].PropertyChanged += (_, _) => changed++;
        original[1].PropertyChanged += (_, _) => untouched++;
        app.NotesChanged += Observe;
        app.Changed(listChanged: false);
        Check(events == 0, "Déplacement : aucune notification à la liste");
        samples[0].Title = "Modifiée"; app.Changed();
        Thread.Sleep(300); PerformanceChecks.Pump();
        Check(ReferenceEquals(original[0], list.Items[0]) && ReferenceEquals(original[1], list.Items[1]) && changed == 1 && untouched == 0, "Liste : lignes conservées et seule la ligne modifiée est notifiée");
        app.NotesChanged -= Observe;
        manager.Close();

        var opening = app.ShowNotesAsync(samples);
        app.HideNotes(samples); PerformanceChecks.Wait(opening); PerformanceChecks.Pump();
        Check(samples.All(n => n.Hidden) && !app.Windows.OfType<NoteWindow>().Any(w => w.IsVisible), "Masquer interrompt les ouvertures encore en attente");
        PerformanceChecks.Wait(app.ShowNotesAsync(samples));
        var firstWindow = app.Windows.OfType<NoteWindow>().Single(w => w.Title == samples[0].Title);
        ((TextBox)firstWindow.FindName("Editor")).Select(4, 3);
        app.HideNotes(samples); PerformanceChecks.Pump();
        Check(app.Windows.OfType<NoteWindow>().Count() <= 5, "Au plus cinq fenêtres masquées en cache");
        app.TrimHiddenWindows(expireAll: true); PerformanceChecks.Pump();
        Check(!app.Windows.OfType<NoteWindow>().Any(), "Expiration : toutes les fenêtres masquées sont libérées");
        app.Open(samples[0]); PerformanceChecks.Pump();
        var restored = (TextBox)app.Windows.OfType<NoteWindow>().Single().FindName("Editor");
        Check(restored.SelectionStart == 4 && restored.SelectionLength == 3 && restored.Text == samples[0].Text, "Réouverture après libération : contenu, curseur et sélection conservés");
        app.HideNotes(samples); app.TrimHiddenWindows(true); PerformanceChecks.Wait(app.SaveNowAsync());
        app.Store.Notes.Clear();
        void Observe() => events++;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
