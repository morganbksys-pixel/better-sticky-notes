using System.IO;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using BetterStickyNotes;

internal static class ProtectionTests
{
    public static void Run(App app, string folder)
    {
        var data = Path.Combine(folder, "protection");
        var store = new NoteStore(data);
        var note = new Note { Title = "SecretTitleMarker", Text = "SecretBodyMarker" };
        store.Notes.Add(note); store.Save(); store.Save();
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var key = NoteProtection.DeriveKey("long password for tests", salt);
        var wrongKey = NoteProtection.DeriveKey("wrong password", salt);
        try
        {
            // A queued old snapshot must finish before the sensitive transition's write.
            var pending = store.SaveAsync();
            NoteProtection.Protect(note, key, salt);
            note.Text += "EditedMarker";
            store.Save(); pending.GetAwaiter().GetResult();
            foreach (var file in Directory.GetFiles(data))
            {
                var text = File.ReadAllText(file);
                Check(!text.Contains("SecretTitleMarker") && !text.Contains("SecretBodyMarker") && !text.Contains("EditedMarker"), "Titre et contenu absents des fichiers, sauvegarde comprise");
            }
            var restored = new NoteStore(data); restored.Load();
            var locked = restored.Notes.Single();
            Check(locked.IsLocked && locked.Hidden && locked.Title == "" && locked.Text == "", "Rechargement verrouillé et sans texte en clair");
            Check(!NoteManagement.Matches(locked, "Secret") && NoteManagement.Matches(locked, ""), "Recherche sans fuite des notes verrouillées");
            try { NoteProtection.Unlock(locked, wrongKey); throw new Exception("Mot de passe incorrect accepté"); }
            catch (CryptographicException) { Check(locked.IsLocked, "Mauvais mot de passe : note inchangée"); }
            NoteProtection.Unlock(locked, key);
            Check(locked.Title == note.Title && locked.Text == note.Text, "Déchiffrement de la dernière édition");
            NoteProtection.Lock(locked);
            var original = locked.Protection!;
            var bytes = Convert.FromBase64String(original.Ciphertext); bytes[0] ^= 1;
            locked.Protection = original with { Ciphertext = Convert.ToBase64String(bytes) };
            try { NoteProtection.Unlock(locked, key); throw new Exception("Altération acceptée"); }
            catch (CryptographicException) { Check(locked.IsLocked, "Altération du contenu détectée"); }
            locked.Protection = original;
            NoteProtection.Unlock(locked, key); NoteProtection.Remove(locked);
            Check(!locked.IsProtected && locked.Text == note.Text, "Retrait de protection conserve le contenu");

            app.Store.Notes.Add(note); app.Open(note);
            var window = app.Windows.OfType<NoteWindow>().Single(w => ((TextBox)w.FindName("Editor")).Text == note.Text);
            var editor = (TextBox)window.FindName("Editor");
            editor.Text += "LatestEdit";
            var manager = new NotesWindow(app);
            app.HideNotes([note]);
            Check(note.IsLocked && note.Hidden && editor.Text == "" && !editor.CanUndo, "Masquage : verrouillage, éditeur et annulation vidés");
            NoteProtection.Unlock(note, key);
            Check(note.Text.EndsWith("LatestEdit"), "Verrouillage conserve la dernière saisie");
            app.Open(note); window = app.Windows.OfType<NoteWindow>().Single(w => ((TextBox)w.FindName("Editor")).Text == note.Text);
            window.Close(); PerformanceChecks.Pump();
            Check(note.IsLocked && !window.IsVisible, "Fermeture native d’une note protégée sans fermeture réentrante");
            NoteProtection.Unlock(note, key); app.Open(note); app.LockProtectedNotes();
            Check(note.IsLocked, "Verrouillage global");
            app.DeleteNotes([note]);
            Check(note.Deleted && note.IsLocked, "Corbeille conserve la protection");
            manager.SelectView("trash");
            var trash = (DataGrid)manager.FindName("NoteList");
            Check(trash.Items.Cast<NotesWindow.Row>().Any(row => row.Note == note && row.Title == "Note protégée"), "Corbeille masque le titre protégé");
            app.PermanentlyDeleteNotes([note]);
            PerformanceChecks.Wait(app.SaveNowAsync());
            var appData = Path.Combine(folder, "app");
            foreach (var file in new[] { "notes.json", "notes.json.bak" })
                Check(!File.ReadAllText(Path.Combine(appData, file)).Contains(note.Id.ToString()), "Suppression définitive dans le fichier et la sauvegarde");
            manager.Close();

            File.WriteAllText(Path.Combine(data, "notes.json"), "broken");
            var recovered = new NoteStore(data); recovered.Load();
            Check(recovered.Notes.Single().IsLocked, "Récupération depuis une sauvegarde chiffrée");
            NoteProtection.Unlock(recovered.Notes.Single(), key);
            Check(recovered.Notes.Single().Text.EndsWith("EditedMarker"), "Sauvegarde chiffrée récupérable avec le mot de passe");
            TestPasswordFlows(app);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(wrongKey); }
    }
    private static void TestPasswordFlows(App app)
    {
        const string password = "test common password";
        var first = new Note { Title = "First private note", Text = "first" };
        var second = new Note { Title = "Second private note", Text = "second" };
        app.Store.Notes.AddRange([first, second]);
        SubmitPassword(app, password, () => app.ProtectNote(first), create: true);
        Check(first.IsLocked, "Dialogue de création : mot de passe confirmé et note verrouillée");
        SubmitPassword(app, password, () => app.ProtectNote(second), tryWrongFirst: true);
        Check(second.IsLocked && first.Protection!.Salt == second.Protection!.Salt, "La seconde protection vérifie le mot de passe commun");
        SubmitPassword(app, password, () => app.Open(first));
        Check(!first.IsLocked && first.Text == "first", "Ouverture depuis une note verrouillée : dialogue puis contenu");
        SubmitPassword(app, password, () => app.RemoveProtection(first));
        Check(!first.IsProtected && first.Text == "first", "Retrait de protection authentifié depuis le dialogue");
        app.DeleteNotes([first, second]); app.PermanentlyDeleteNotes([first, second]);
        PerformanceChecks.Wait(app.SaveNowAsync());
    }

    private static void SubmitPassword(App app, string password, Action action, bool create = false, bool tryWrongFirst = false)
    {
        app.Dispatcher.BeginInvoke(() =>
        {
            var dialog = app.Windows.OfType<PasswordDialog>().Single();
            dialog.Opacity = 0;
            var panel = ((Grid)((Border)dialog.Content).Child).Children.OfType<StackPanel>().Single();
            var fields = panel.Children.OfType<PasswordBox>().ToList();
            var submit = panel.Children.OfType<StackPanel>().Single().Children.OfType<Button>().Single(b => b.IsDefault);
            if (tryWrongFirst)
            {
                fields[0].Password = "incorrect password";
                submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(dialog.IsVisible && panel.Children.OfType<TextBlock>().Any(t => t.Text.Contains("incorrect")), "Dialogue : erreur de mot de passe sans fermeture");
            }
            fields[0].Password = password;
            if (create) fields[1].Password = password;
            submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }, DispatcherPriority.Loaded);
        action();
    }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("OK : " + message); }
}
