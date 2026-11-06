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

    /// <summary>Reads this application's Windows startup command without changing the registration.</summary>
    public static string? GetStartupRegistration()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("BetterStickyNotes") as string;
    }

    private static string? StartupExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var executable = command.Trim();
        if (executable.EndsWith(" --background", StringComparison.OrdinalIgnoreCase)) executable = executable[..^13].TrimEnd();
        if (executable.StartsWith('"'))
        {
            if (executable.Length < 2 || !executable.EndsWith('"')) return null;
            executable = executable[1..^1];
        }
        return Path.IsPathFullyQualified(executable) && executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !executable.Contains('"') ? executable : null;
    }

    /// <summary>Compares the registered executable path with a candidate, ignoring Windows path casing.</summary>
    /// <param name="command">The registered startup command, or null when disabled.</param>
    /// <param name="executable">The full candidate executable path.</param>
    public static bool StartupTargetsExecutable(string? command, string executable)
    {
        var registered = StartupExecutable(command);
        if (registered is null || !Path.IsPathFullyQualified(executable)) return false;
        try { return string.Equals(Path.GetFullPath(registered), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>Rebinds an enabled startup registration to the running application, preserving opt-in.</summary>
    public static void UseCurrentVersionForStartup()
    {
        var executable = Environment.ProcessPath ?? throw new IOException("Le chemin de l’application est indisponible.");
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue("BetterStickyNotes") is not string) return;
        key.SetValue("BetterStickyNotes", StartupCommand(executable));
    }

    private static void UpgradeStartupRegistration()
    {
        // Preserve opt-in, but update this app's existing entry after an upgrade/move.
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue("BetterStickyNotes") is not string command) return;
            var executable = StartupExecutable(command);
            if (string.Equals(Path.GetFileName(executable), "BetterStickyNotes.exe", StringComparison.OrdinalIgnoreCase))
                key.SetValue("BetterStickyNotes", StartupCommand(Environment.ProcessPath!));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        { AppDialog.Show("Impossible de mettre à jour le démarrage discret avec Windows.\n" + ex.Message, "Better Sticky Notes"); }
    }

    public void PrepareLaunch(bool background)
    {
        // Restore only previously visible, unprotected notes without taking focus.
        foreach (var note in Store.Notes.Where(n => !n.Hidden && !n.Deleted && !n.IsProtected).ToList())
            Open(note, activate: false);
        if (!background) ShowManager();
    }

    public void FocusNote(Note note)
    {
        if (note.Deleted || quitting) return;
        Open(note, activate: false);
        // A cancelled password prompt must not expose or activate the note.
        if (note.IsLocked || note.Hidden || !windows.TryGetValue(note.Id, out var window)) return;
        window.SetCollapsed(false);
        window.Activate();
        SetForegroundWindow(new WindowInteropHelper(window).Handle);
    }
}
