using System.Security.Cryptography;
using System.Windows;
using Microsoft.Win32;

namespace BetterStickyNotes;

public partial class App
{
    private byte[]? RequestProtectionKey(Note? target, out string salt)
    {
        var sample = target?.IsProtected == true ? target : Store.Notes.FirstOrDefault(n => n.IsProtected);
        salt = sample?.Protection?.Salt ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var selectedSalt = salt;
        return PasswordDialog.Request(sample == null, password =>
        {
            var key = NoteProtection.DeriveKey(password, selectedSalt);
            try { if (sample != null) NoteProtection.Decrypt(sample, key); return key; }
            catch { CryptographicOperations.ZeroMemory(key); throw; }
        });
    }

    public void ProtectNote(Note note)
    {
        if (note.IsProtected) return;
        if (note.Captures.Count > 0)
        {
            AppDialog.Show("Retirez les captures avant de protéger cette note. Les images ne sont pas chiffrées dans cette version.", "Protection indisponible");
            return;
        }
        var key = RequestProtectionKey(null, out var salt);
        if (key == null) return;
        try { NoteProtection.Protect(note, key, salt); }
        finally { CryptographicOperations.ZeroMemory(key); }
        Store.SanitizeBackup = true;
        LockNote(note);
        SaveNow();
    }

    private bool UnlockNote(Note note)
    {
        if (!note.IsLocked) return true;
        var key = RequestProtectionKey(note, out _);
        if (key == null) return false;
        try { NoteProtection.Unlock(note, key); notesWindow?.RefreshPrivacy(); return true; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public void RemoveProtection(Note note)
    {
        if (!note.IsProtected) return;
        var key = RequestProtectionKey(note, out _);
        if (key == null) return;
        try
        {
            if (note.IsLocked) NoteProtection.Unlock(note, key);
            NoteProtection.Remove(note);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
        Changed(); SaveNow();
    }

    public void LockNote(Note note)
    {
        if (!note.IsProtected) return;
        NoteProtection.Lock(note);
        note.Hidden = true;
        visibilityRequests[note.Id] = ++visibilitySerial;
        hiddenSince.Remove(note.Id); editorStates.Remove(note.Id);
        if (windows.Remove(note.Id, out var window)) { window.ClearSensitiveContent(); window.ClosePermanently(); }
        notesWindow?.RefreshPrivacy();
        Changed();
    }

    public void LockProtectedNotes()
    {
        if (trayMenu != null) trayMenu.IsOpen = false;
        foreach (var note in Store.Notes.Where(n => n.IsProtected).ToList()) LockNote(note);
    }

    private void SessionChanged(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or SessionSwitchReason.RemoteDisconnect)
            Dispatcher.Invoke(LockProtectedNotes);
    }

    public void ConfirmEmptyTrash() => ConfirmPermanentDeletion(Store.Notes.Where(n => n.Deleted), entireTrash: true);

    public void ConfirmPermanentDeletion(IEnumerable<Note> notes) => ConfirmPermanentDeletion(notes, entireTrash: false);

    private void ConfirmPermanentDeletion(IEnumerable<Note> notes, bool entireTrash)
    {
        var selected = notes.Where(n => n.Deleted).ToList();
        if (selected.Count == 0) return;
        var countLabel = selected.Count == 1 ? "1 note" : $"{selected.Count} notes";
        var message = entireTrash
            ? (selected.Count == 1 ? "Supprimer définitivement la note de la corbeille ?" : $"Supprimer définitivement les {countLabel} de la corbeille ?")
                + "\n\nToute la corbeille sera vidée, même si une recherche est active."
            : selected.Count == 1 ? "Supprimer définitivement cette note ?" : $"Supprimer définitivement les {countLabel} sélectionnées ?";
        var actionLabel = entireTrash ? $"Vider la corbeille ({countLabel})" : $"Supprimer {countLabel}";
        if (AppDialog.Show(message + "\n\nCette action est irréversible.",
            "Suppression définitive", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, destructiveAction: actionLabel) != MessageBoxResult.Yes) return;
        PermanentlyDeleteNotes(selected);
        SaveNow();
    }

    public void RestoreNotes(IEnumerable<Note> notes)
    {
        foreach (var note in notes.Where(n => n.Deleted).ToList())
        {
            note.Deleted = false; note.DeletedAtUtc = null; note.Hidden = true;
        }
        Changed();
    }

    public void PermanentlyDeleteNotes(IEnumerable<Note> notes)
    {
        foreach (var note in notes.Where(n => n.Deleted).ToList())
        {
            if (note.IsProtected) LockNote(note);
            Store.DeleteCaptureFiles(note);
            Store.Notes.Remove(note);
        }
        Store.SanitizeBackup = true;
        Changed();
    }
}
