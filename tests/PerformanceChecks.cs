using BetterStickyNotes;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

internal static class PerformanceChecks
{
    public static void Run(int count)
    {
        var folder = Path.Combine(Path.GetTempPath(), "BetterStickyNotes-perf-" + Guid.NewGuid());
        var app = new App(false, folder); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var body = string.Join("\n", Enumerable.Repeat("## Développement\n- [ ] Vérifier la requête\n```sql\nSELECT * FROM Orders;\n```\n", 12));
        for (int i = 0; i < count; i++) app.Store.Notes.Add(new Note { Title = $"Projet {i}", Text = body, Hidden = true, Preview = true, HasPlacement = true });
        var manager = new NotesWindow(app);
        var search = (TextBox)manager.FindName("SearchBox");
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) search.Text = i % 2 == 0 ? "requete" : "projet 1";
        Console.WriteLine($"{count} notes / 100 recherches : {watch.Elapsed.TotalMilliseconds:F1} ms");
        watch.Restart(); Wait(app.ShowNotesAsync(app.Store.Notes));
        Pump();
        Console.WriteLine($"{count} notes / affichage : {watch.Elapsed.TotalMilliseconds:F1} ms");
        watch.Restart(); app.HideNotes(app.Store.Notes); Pump();
        Console.WriteLine($"{count} notes / masquage : {watch.Elapsed.TotalMilliseconds:F1} ms");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Console.WriteLine($"{count} notes / fenêtres retenues : {app.Windows.OfType<NoteWindow>().Count()}; mémoire managée : {GC.GetTotalMemory(false) / 1048576.0:F1} Mo");
        Wait(app.SaveNowAsync());
        manager.Close();
        foreach (var w in app.Windows.OfType<NoteWindow>().ToList()) w.ClosePermanently();
        app.Shutdown();
        // Preserve only synthetic temporary output if the process still holds a writer.
        Console.WriteLine("Données synthétiques uniquement.");
    }
    public static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    public static void Wait(Task task)
    {
        while (!task.IsCompleted) { Pump(); Thread.Sleep(1); }
        task.GetAwaiter().GetResult();
    }
}
