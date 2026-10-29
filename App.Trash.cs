using System.IO;
using System.Windows.Threading;

namespace BetterStickyNotes;

public partial class App
{
    private readonly DispatcherTimer purgeTimer = new() { Interval = TimeSpan.FromHours(1) };

    public bool ConfigureTrashRetention(int? days)
    {
        // Persist dates before enabling an irreversible automatic action.
        if (!SaveNow()) return false;
        try { Store.SaveTrashRetention(days); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ReportSaveError(ex); return false; }
        RunTrashPurge();
        return true;
    }

    public void RunTrashPurge(DateTimeOffset? now = null)
    {
        if (Store.TrashRetentionDays is not int days || quitting) return;
        var cutoff = (now ?? DateTimeOffset.UtcNow).AddDays(-days);
        var expired = Store.Notes.Where(n => n.Deleted && n.DeletedAtUtc is { } date && date <= cutoff).ToList();
        if (expired.Count == 0) return;
        PermanentlyDeleteNotes(expired);
        SaveNow();
    }
}
