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
    public NoteStore Store { get; } = new();
    private readonly Dictionary<Guid, NoteWindow> windows = [];
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer activationTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private EventWaitHandle? activation;
    private Mutex? mutex;
    private Forms.NotifyIcon? tray;
    private Drawing.Icon? icon;
    private Window? menuHost;
    private ContextMenu? trayMenu;
    private bool saveErrorShown;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, @"Local\BetterStickyNotes.Mvp", out var first);
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BetterStickyNotes.Show");
        if (!first) { activation.Set(); Shutdown(); return; }
        try { Store.Load(); }
        catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Chargement impossible", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(); return; }
        saveTimer.Tick += (_, _) => SaveNow();
        activationTimer.Tick += (_, _) => { if (activation.WaitOne(0)) ShowAll(); };
        activationTimer.Start();
        icon = MakeIcon();
        tray = new Forms.NotifyIcon { Icon = icon, Text = "Better Sticky Notes — afficher / masquer", Visible = true };
        var clickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime + 30) };
        clickTimer.Tick += (_, _) => { clickTimer.Stop(); if (windows.Values.Any(w => w.IsVisible)) HideAll(); else ShowAll(); };
        // Coalesce single and double clicks into one visibility toggle.
        tray.MouseClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) { clickTimer.Stop(); clickTimer.Start(); } };
        tray.MouseDoubleClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) { clickTimer.Stop(); clickTimer.Start(); } };
        tray.MouseUp += (_, args) => { if (args.Button == Forms.MouseButtons.Right) { clickTimer.Stop(); ShowTrayMenu(); } };
        if (Store.Notes.Count == 0)
        {
            NewNote("Bienvenue 👋", "# Vos idées, à portée de main\n\nÉcrivez ici en **Markdown**.\n\n- [ ] Créer ma première note\n- [ ] Épingler une note au premier plan\n\nUtilisez **Aperçu** pour lire votre note.\n\nL’icône jaune près de l’horloge permet de retrouver toutes vos notes.");
        }
        else foreach (var note in Store.Notes.Where(n => !n.Deleted && !n.Hidden)) Open(note);
        if (Store.LoadWarning is not null) System.Windows.MessageBox.Show(Store.LoadWarning, "Récupération des notes");
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        SessionEnding += (_, _) => SaveNow();
    }

    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { foreach (var w in windows.Values) w.EnsureVisibleOnScreen(); });
    public void Changed() { saveTimer.Stop(); saveTimer.Start(); }
    public bool SaveNow()
    {
        saveTimer.Stop();
        try { Store.Save(); saveErrorShown = false; return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!saveErrorShown) System.Windows.MessageBox.Show("Les modifications restent en mémoire mais n’ont pas pu être enregistrées.\n\n" + ex.Message, "Sauvegarde impossible", MessageBoxButton.OK, MessageBoxImage.Error);
            saveErrorShown = true;
            saveTimer.Interval = TimeSpan.FromSeconds(5); saveTimer.Start();
            return false;
        }
    }
    public void NewNote(string title = "", string text = "")
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var note = new Note { Title = title, Text = text };
        Store.Notes.Add(note); Open(note);
        windows[note.Id].PlaceBottomLeft(screen);
        Changed();
    }
    public void Open(Note note)
    {
        note.Hidden = false;
        if (!windows.TryGetValue(note.Id, out var window)) { window = new NoteWindow(this, note); windows.Add(note.Id, window); }
        window.Show(); window.WindowState = WindowState.Normal; window.EnsureVisibleOnScreen(); window.Activate(); Changed();
    }
    private void ShowAll()
    {
        var notes = Store.Notes.Where(n => !n.Deleted).ToList();
        foreach (var note in notes) Open(note);
    }
    private void HideAll() { foreach (var w in windows.Values) w.HideNote(); SaveNow(); }
    public void Delete(Note note)
    {
        note.Deleted = true; note.Hidden = true;
        if (windows.Remove(note.Id, out var window)) window.ClosePermanently();
        SaveNow();
    }
    private static string Label(Note n) => string.IsNullOrWhiteSpace(n.Title) ? "Sans titre" : n.Title;
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
        var visible = windows.Values.Any(w => w.IsVisible);
        menu.Items.Add(ActionItem(visible ? "Masquer les notes" : "Afficher les notes", "\uE890", () => { if (visible) HideAll(); else ShowAll(); }));
        var notesMenu = new MenuItem { Header = "Mes notes", Icon = Glyph("\uE8A5") };
        foreach (var note in Store.Notes.Where(n => !n.Deleted)) notesMenu.Items.Add(ActionItem(Label(note), note.Pinned ? "\uE718" : "\uE8A5", () => Open(note)));
        notesMenu.IsEnabled = notesMenu.Items.Count > 0; menu.Items.Add(notesMenu);
        menu.Items.Add(new Separator());
        var more = new MenuItem { Header = "Options", Icon = Glyph("\uE713") };
        var trash = new MenuItem { Header = "Corbeille — restaurer", Icon = Glyph("\uE74D") };
        foreach (var note in Store.Notes.Where(n => n.Deleted)) trash.Items.Add(ActionItem(Label(note), "\uE777", () => { note.Deleted = false; Open(note); }));
        trash.IsEnabled = trash.Items.Count > 0; more.Items.Add(trash);
        more.Items.Add(new Separator());
        var startup = ActionItem("Démarrer avec Windows", "\uE7E8", ToggleStartup);
        startup.IsCheckable = true; startup.IsChecked = IsStartupEnabled(); more.Items.Add(startup);
        more.Items.Add(ActionItem("Ouvrir le dossier des données", "\uE8B7", () => { Directory.CreateDirectory(NoteStore.Folder); Process.Start(new ProcessStartInfo(NoteStore.Folder) { UseShellExecute = true }); }));
        menu.Items.Add(more);
        menu.Items.Add(ActionItem("Quitter", "\uE7E8", Quit));
        return menu;
    }
    private static TextBlock Glyph(string glyph) => new() { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
    private static MenuItem ActionItem(string label, string glyph, Action action)
    {
        var item = new MenuItem { Header = label, Icon = Glyph(glyph) };
        item.Click += (_, _) => action(); return item;
    }
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("BetterStickyNotes") is string;
    }
    private static void ToggleStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (IsStartupEnabled()) key.DeleteValue("BetterStickyNotes", false);
            else key.SetValue("BetterStickyNotes", "\"" + Environment.ProcessPath + "\"");
        }
        catch (Exception ex) { System.Windows.MessageBox.Show("Impossible de modifier le démarrage avec Windows.\n" + ex.Message, "Better Sticky Notes"); }
    }
    private void Quit()
    {
        if (!SaveNow()) return;
        foreach (var w in windows.Values) w.ClosePermanently();
        Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        saveTimer.Stop(); activationTimer.Stop();
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
