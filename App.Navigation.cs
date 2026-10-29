using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace BetterStickyNotes;

public partial class App
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);

    public static string StartupCommand(string executable) => $"\"{executable}\" --background";

    private static void UpgradeStartupRegistration()
    {
        // Preserve opt-in, but update this app's existing entry after an upgrade/move.
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue("BetterStickyNotes") is not string command) return;
            var executable = command.Trim();
            if (executable.EndsWith(" --background", StringComparison.OrdinalIgnoreCase)) executable = executable[..^13];
            executable = executable.Trim('"');
            if (string.Equals(Path.GetFileName(executable), "BetterStickyNotes.exe", StringComparison.OrdinalIgnoreCase))
                key.SetValue("BetterStickyNotes", StartupCommand(Environment.ProcessPath!));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        { MessageBox.Show("Impossible de mettre à jour le démarrage discret avec Windows.\n" + ex.Message, "Better Sticky Notes"); }
    }

    public void PrepareLaunch(bool background)
    {
        // Startup never restores floating windows or rearranges saved positions.
        foreach (var note in Store.Notes) note.Hidden = true;
        Changed();
        if (!background) ShowManager();
    }

    public void FocusNote(Note note)
    {
        if (note.Deleted || quitting) return;
        Open(note, activate: false);
        // A cancelled password prompt must not expose or activate the note.
        if (note.IsLocked || note.Hidden || !windows.TryGetValue(note.Id, out var window)) return;
        window.Activate();
        SetForegroundWindow(new WindowInteropHelper(window).Handle);
    }
}
