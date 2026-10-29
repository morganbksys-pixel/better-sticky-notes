using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace BetterStickyNotes;

public partial class App : System.Windows.Application
{
    private readonly bool startNormally;
    public App() : this(true) { }
    // Off-screen WPF tests pump the Dispatcher without loading the user's notes or tray.
    public App(bool startNormally, string? dataFolder = null)
    {
        this.startNormally = startNormally; Store = new NoteStore(dataFolder);
        saveTimer.Tick += async (_, _) => { saveTimer.Stop(); if (revision != savedRevision && saveGate.CurrentCount != 0) await SaveNowAsync(); };
        cacheTimer.Tick += (_, _) => TrimHiddenWindows();
    }
    public NoteStore Store { get; }
    private readonly Dictionary<Guid, NoteWindow> windows = [];
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer cacheTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private RegisteredWaitHandle? activationWait;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private long revision, savedRevision;
    private bool quitting;
    private readonly Dictionary<Guid, DateTime> hiddenSince = [];
    private readonly Dictionary<Guid, NoteWindow.EditorState> editorStates = [];
    private readonly Dictionary<Guid, long> visibilityRequests = [];
    private long visibilitySerial;
    private DispatcherOperation? trimOperation;
    private EventWaitHandle? activation;
    private Mutex? mutex;
    private Forms.NotifyIcon? tray;
    private Drawing.Icon? icon;
    private Window? menuHost;
    private ContextMenu? trayMenu;
    private bool saveErrorShown;
    private bool importing;
    private NotesWindow? notesWindow;
    private int cascadeIndex;
    public event Action? NotesChanged;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!startNormally) return;
        base.OnStartup(e);
        SetCurrentProcessExplicitAppUserModelID("BetterStickyNotes.Desktop");
        var background = e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        mutex = new Mutex(true, @"Local\BetterStickyNotes.Mvp", out var first);
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BetterStickyNotes.Show");
        if (!first)
        {
            if (!background) { AllowSetForegroundWindow(uint.MaxValue); activation.Set(); }
            Shutdown(); return;
        }
        try { Store.Load(); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Chargement impossible", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(); return; }
        RunTrashPurge();
        purgeTimer.Tick += (_, _) => RunTrashPurge(); purgeTimer.Start();
        activationWait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => {
            if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(() => { if (!quitting) ShowManager(); });
        }, null, Timeout.Infinite, false);
        icon = MakeIcon();
        tray = new Forms.NotifyIcon { Icon = icon, Text = AppInfo.DisplayName, Visible = true };
        tray.MouseDoubleClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) ShowManager(); };
        tray.MouseUp += (_, args) => { if (args.Button == Forms.MouseButtons.Right) ShowTrayMenu(); };
        UpgradeStartupRegistration();
        PrepareLaunch(background);
        if (Store.LoadWarning is not null) AppDialog.Show(Store.LoadWarning, "Récupération des notes");
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        SystemEvents.SessionSwitch += SessionChanged;
        SessionEnding += (_, e) => { quitting = true; if (!SaveNow()) { e.Cancel = true; quitting = false; } };
    }

    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { foreach (var w in windows.Values.Where(w => w.IsVisible)) w.EnsureVisibleOnScreen(); });
    public void Changed(bool listChanged = true)
    {
        revision++;
        saveTimer.Stop(); saveTimer.Interval = TimeSpan.FromMilliseconds(450); saveTimer.Start();
        if (listChanged) NotesChanged?.Invoke();
    }
    // Used only for Windows session shutdown, where we must finish before returning.
    public bool SaveNow()
    {
        saveTimer.Stop();
        try { Store.Save(); savedRevision = revision; return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ReportSaveError(ex); return false; }
    }
    public async Task<bool> SaveNowAsync()
    {
        saveTimer.Stop();
        await saveGate.WaitAsync();
        try
        {
            do
            {
                var target = revision;
                await Store.SaveAsync();
                savedRevision = target;
            } while (savedRevision != revision);
            saveErrorShown = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ReportSaveError(ex); return false; }
        finally { saveGate.Release(); }
    }
    private void ReportSaveError(Exception ex)
    {
        if (!saveErrorShown) AppDialog.Show("Les modifications restent en mémoire mais n’ont pas pu être enregistrées.\n\n" + ex.Message, "Sauvegarde impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        saveErrorShown = true;
        saveTimer.Interval = TimeSpan.FromSeconds(5); saveTimer.Start();
    }
    public void NewNote(string title = "", string text = "", Guid? groupId = null)
    {
        var note = new Note { Title = title, Text = text, GroupId = Store.Groups.Any(g => g.Id == groupId) ? groupId : null };
        Store.Notes.Add(note); Open(note);
        Changed();
    }
    public void Open(Note note, Forms.Screen? screen = null, bool activate = true)
    {
        if (note.Deleted || quitting) return;
        if (!UnlockNote(note)) return;
        var place = note.HasPlacement != true;
        screen ??= Forms.Screen.FromPoint(Forms.Cursor.Position);
        note.Hidden = false;
        if (!windows.TryGetValue(note.Id, out var window))
        {
            window = new NoteWindow(this, note);
            if (!startNormally) { window.Opacity = 0; window.ShowActivated = false; }
            if (editorStates.Remove(note.Id, out var state)) window.RestoreEditorState(state);
            windows.Add(note.Id, window);
        }
        hiddenSince.Remove(note.Id);
        if (hiddenSince.Count == 0) cacheTimer.Stop();
        window.ShowActivated = activate && startNormally;
        window.Show(); window.WindowState = WindowState.Normal; window.EnsureVisibleOnScreen(); if (activate && startNormally) window.Activate();
        Changed();
        if (place) window.PlaceInCascade(screen, cascadeIndex++);
    }
    public void ShowManager(IEnumerable<Guid>? selected = null)
    {
        if (quitting) return;
        if (notesWindow == null)
        {
            notesWindow = new NotesWindow(this);
            notesWindow.Closed += (_, _) => notesWindow = null;
        }
        notesWindow.Show(); notesWindow.WindowState = WindowState.Normal; notesWindow.Activate();
        SetForegroundWindow(new WindowInteropHelper(notesWindow).Handle);
        if (selected != null) notesWindow.SelectImported(selected);
    }
    public void ShowNotes(IEnumerable<Note> notes, bool rearrange = false) => _ = ShowNotesAsync(notes, rearrange);
    public async Task ShowNotesAsync(IEnumerable<Note> notes, bool rearrange = false)
    {
        var selection = notes.Where(n => !n.Deleted).DistinctBy(n => n.Id).ToList();
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        if (rearrange) cascadeIndex = 0;
        var request = ++visibilitySerial;
        foreach (var note in selection) visibilityRequests[note.Id] = request;
        var placed = new List<Note>();
        foreach (var note in selection)
        {
            if (quitting || note.Deleted || visibilityRequests.GetValueOrDefault(note.Id) != request) continue;
            if (rearrange) note.HasPlacement = false;
            if (note.HasPlacement != true) placed.Add(note);
            Open(note, screen, activate: selection.Count == 1);
            if (selection.Count > 1) await Dispatcher.Yield(DispatcherPriority.Background);
        }
        // Lower notes go in front, leaving the headers of the notes above exposed.
        foreach (var note in placed.AsEnumerable().Reverse())
            if (visibilityRequests.GetValueOrDefault(note.Id) == request && windows.TryGetValue(note.Id, out var window) && window.IsVisible) window.RaiseWithoutActivation();
        if (!quitting) Changed();
    }
    public void HideNotes(IEnumerable<Note> notes)
    {
        foreach (var note in notes.Where(n => !n.Deleted).ToList())
        {
            if (note.IsProtected) { LockNote(note); continue; }
            note.Hidden = true;
            visibilityRequests[note.Id] = ++visibilitySerial;
            if (windows.TryGetValue(note.Id, out var window)) { window.HideForCache(); hiddenSince[note.Id] = DateTime.UtcNow; }
        }
        ScheduleCacheTrim();
        Changed();
    }
    private void ScheduleCacheTrim()
    {
        if (hiddenSince.Count == 0) return;
        cacheTimer.Start();
        if (trimOperation?.Status != DispatcherOperationStatus.Pending)
            trimOperation = Dispatcher.BeginInvoke(() => TrimHiddenWindows(), DispatcherPriority.Background);
    }
    public void TrimHiddenWindows(bool expireAll = false)
    {
        var entries = hiddenSince.OrderByDescending(pair => pair.Value).ToList();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!expireAll && i < 5 && DateTime.UtcNow - entry.Value < TimeSpan.FromMinutes(1)) continue;
            hiddenSince.Remove(entry.Key);
            if (!windows.TryGetValue(entry.Key, out var window) || window.IsVisible) continue;
            editorStates[entry.Key] = window.CaptureEditorState();
            windows.Remove(entry.Key);
            window.ClosePermanently();
        }
        if (hiddenSince.Count == 0) cacheTimer.Stop();
    }
    private void ShowAll()
    {
        var notes = Store.Notes.Where(n => !n.Deleted && !n.IsLocked).ToList();
        ShowNotes(notes);
    }
    private void HideAll() => HideNotes(Store.Notes);
    public void Delete(Note note)
        => DeleteNotes([note]);
    public void DeleteNotes(IEnumerable<Note> notes)
    {
        foreach (var note in notes.ToList())
        {
            if (!note.Deleted) note.DeletedAtUtc = DateTimeOffset.UtcNow;
            note.Deleted = true;
            if (note.IsProtected) { LockNote(note); continue; }
            note.Hidden = true;
            visibilityRequests[note.Id] = ++visibilitySerial;
            hiddenSince.Remove(note.Id); editorStates.Remove(note.Id);
            if (windows.Remove(note.Id, out var window)) window.ClosePermanently();
        }
        Changed();
    }
    private static string Label(Note n) => n.DisplayTitle;
    private void ShowTrayMenu()
    {
        if (trayMenu?.IsOpen == true) { trayMenu.IsOpen = false; return; }
        // Invisible foreground owner: dismiss on outside clicks, including when notes are hidden.
        menuHost ??= new Window { Width = 1, Height = 1, WindowStyle = WindowStyle.None,
            AllowsTransparency = true, Background = Brushes.Transparent, Opacity = 0,
            ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize };
        menuHost.Show();
        SetForegroundWindow(new WindowInteropHelper(menuHost).Handle);
        var menu = BuildTrayMenu();
        trayMenu = menu;
        menu.PlacementTarget = menuHost;
        menu.Placement = PlacementMode.MousePoint;
        menu.Closed += (_, _) => { if (!Dispatcher.HasShutdownStarted && menuHost.IsLoaded) menuHost.Hide(); };
        menu.IsOpen = true;
    }
    public ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(ActionItem("Nouvelle note", "\uE710", () => NewNote()));
        menu.Items.Add(ActionItem("Mes notes", "\uE8A5", () => ShowManager()));
        menu.Items.Add(new Separator());
        menu.Items.Add(ActionItem("Quitter", "\uE7E8", Quit));
        return menu;
    }
    private static TextBlock Glyph(string glyph) => new() { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
    public async void ImportStickyNotes()
    {
        if (importing) return;
        importing = true;
        try
        {
            var detectedPath = StickyNotesImporter.DetectPath();
            var picker = new OpenFileDialog {
                Title = "Choisir la base Sticky Notes (Windows ou OneNote)",
                Filter = "Base Sticky Notes (*.sqlite;*.db)|*.sqlite;*.db|Tous les fichiers (*.*)|*.*",
                CheckFileExists = true,
                FileName = detectedPath ?? "plum.sqlite"
            };
            if (picker.ShowDialog() != true) return;
            var existing = Store.Notes.ToList();
            var result = await Task.Run(() => StickyNotesImporter.Read(picker.FileName, existing));
            var importedIds = new List<Guid>();
            var preview = new ImportWindow(result, async selected =>
            {
                var known = Store.Notes.Where(n => n.WindowsStickyNoteId != null).Select(n => n.WindowsStickyNoteId!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var additions = selected.Where(n => known.Add(n.WindowsStickyNoteId!)).ToList();
                Store.Notes.AddRange(additions);
                Changed();
                if (!await SaveNowAsync())
                {
                    foreach (var note in additions) Store.Notes.Remove(note);
                    Changed();
                    return false;
                }
                importedIds = additions.Select(n => n.Id).ToList();
                return true;
            });
            if (preview.ShowDialog() == true)
                ShowManager(importedIds);
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            AppDialog.Show("Impossible d’importer ce fichier. Fermez Sticky Notes / OneNote puis réessayez avec plum.sqlite ou notes.sdk_….db. Pour une copie, conservez aussi les fichiers -wal et -shm à côté s’ils existent.\n\n" + ex.Message, "Import Sticky Notes", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { importing = false; }
    }
    private static MenuItem ActionItem(string label, string glyph, Action action)
    {
        var item = new MenuItem { Header = label, Icon = Glyph(glyph) };
        item.Click += (_, _) => action(); return item;
    }
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("BetterStickyNotes") is string;
    }
    public static void ToggleStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (IsStartupEnabled()) key.DeleteValue("BetterStickyNotes", false);
            else key.SetValue("BetterStickyNotes", StartupCommand(Environment.ProcessPath!));
        }
        catch (Exception ex) { AppDialog.Show("Impossible de modifier le démarrage avec Windows.\n" + ex.Message, "Better Sticky Notes"); }
    }
    private async void Quit()
    {
        if (quitting) return;
        quitting = true;
        if (!await SaveNowAsync()) { quitting = false; return; }
        foreach (var w in windows.Values) w.ClosePermanently();
        Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        SystemEvents.SessionSwitch -= SessionChanged;
        LockProtectedNotes();
        purgeTimer.Stop();
        saveTimer.Stop(); cacheTimer.Stop(); activationWait?.Unregister(null); trimOperation?.Abort();
        if (trayMenu is not null) trayMenu.IsOpen = false;
        tray?.Dispose(); icon?.Dispose(); activation?.Dispose(); mutex?.Dispose();
        base.OnExit(e);
    }
    private static Drawing.Icon MakeIcon()
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/BetterStickyNotes;component/Assets/BetterStickyNotes.ico")).Stream;
        using var source = new Drawing.Icon(stream, 32, 32);
        return (Drawing.Icon)source.Clone();
    }
}
