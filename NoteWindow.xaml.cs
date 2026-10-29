using System.ComponentModel;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace BetterStickyNotes;

public partial class NoteWindow : Window
{
    private readonly App app;
    private readonly Note note;
    private bool ready;
    private bool clearingSensitiveContent;
    private bool allowClose;
    private bool draggingHeader;
    private readonly List<(Button Button, MarkdownTool Tool, string Label)> editingButtons = [];
    private Button overflowButton = null!;
    private System.Windows.Controls.Primitives.Popup? markdownHelp;
    private string? renderedText;
    public sealed record EditorState(int Start, int Length, double Scroll);
    private EditorState? pendingEditorState;
    public NoteWindow(App app, Note note)
    {
        this.app = app; this.note = note;
        InitializeComponent();
        BuildEditingBar();
        Left = note.Left; Top = note.Top; Width = note.Width; Height = note.Height;
        TitleBox.Text = note.Title; Editor.Text = note.Text;
        Title = string.IsNullOrWhiteSpace(note.Title) ? "Sans titre — Better Sticky Notes" : note.Title;
        ApplyColor(note.Color); ApplyPin(); ApplyPreview();
        TitleBox.TextChanged += (_, _) => { if (clearingSensitiveContent) return; note.Title = TitleBox.Text; Title = string.IsNullOrWhiteSpace(note.Title) ? "Sans titre — Better Sticky Notes" : note.Title; app.Changed(); };
        Editor.TextChanged += (_, _) => { if (clearingSensitiveContent) return; note.Text = Editor.Text; app.Changed(); };
        LocationChanged += (_, _) => { if (draggingHeader && !note.Preview) SetPreview(true); SaveBounds(); }; SizeChanged += (_, _) => SaveBounds();
        Loaded += (_, _) => {
            ready = true; EnsureVisibleOnScreen(); SaveBounds();
            if (pendingEditorState is { } state) { Editor.Select(Math.Min(state.Start, Editor.Text.Length), Math.Min(state.Length, Math.Max(0, Editor.Text.Length - state.Start))); Editor.ScrollToVerticalOffset(state.Scroll); pendingEditorState = null; }
            if (!note.Preview && IsActive) Editor.Focus();
        };
        IsVisibleChanged += (_, _) => { if (IsVisible) SetPreview(true); };
        Deactivated += (_, _) => ScheduleInactivePreview();
        // Include the TextBox's built-in spelling menu, which handles routed events.
        AddHandler(ContextMenuService.ContextMenuClosingEvent, new ContextMenuEventHandler((_, _) => ScheduleInactivePreview()), true);
        Closing += OnClosing;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F2) { BeginTitleEdit(); e.Handled = true; return; }
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (!note.Preview && Editor.IsKeyboardFocusWithin && e.Key is Key.B or Key.I or Key.K)
            {
                MarkdownEditing.Apply(Editor, e.Key == Key.B ? MarkdownTool.Bold : e.Key == Key.I ? MarkdownTool.Italic : MarkdownTool.Link);
                e.Handled = true; return;
            }
            if (e.Key == Key.Enter) { TogglePreview(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.N) { app.NewNote(); e.Handled = true; }
            else if (e.Key == Key.S) { _ = app.SaveNowAsync(); e.Handled = true; }
        };
    }
    private void ScheduleInactivePreview()
    {
        // Let menu actions and activation changes finish before replacing the editor.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (!ready || !IsVisible || clearingSensitiveContent || note.Preview || IsActive) return;
            var foreground = GetForegroundWindow();
            var handle = new WindowInteropHelper(this).Handle;
            // Menus/popups belong to this native window. A different app or note does not.
            if (foreground != IntPtr.Zero && GetAncestor(foreground, 3 /* GA_ROOTOWNER */) == handle) return;
            SetPreview(true);
        });
    }
    private void SaveBounds()
    {
        if (!ready || WindowState != WindowState.Normal) return;
        if (note.HasPlacement == true && note.Left == Left && note.Top == Top && note.Width == ActualWidth && note.Height == ActualHeight) return;
        note.HasPlacement = true;
        note.Left = Left; note.Top = Top; note.Width = ActualWidth; note.Height = ActualHeight; app.Changed(listChanged: false);
    }
    public void EnsureVisibleOnScreen()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var screen = Forms.Screen.FromHandle(handle);
        var scale = VisualTreeHelper.GetDpi(this);
        var area = screen.WorkingArea;
        var left = area.Left / scale.DpiScaleX; var top = area.Top / scale.DpiScaleY;
        var width = area.Width / scale.DpiScaleX; var height = area.Height / scale.DpiScaleY;
        Width = Math.Min(Width, width); Height = Math.Min(Height, height);
        Left = Math.Clamp(Left, left, Math.Max(left, left + width - Width));
        Top = Math.Clamp(Top, top, Math.Max(top, top + height - Height));
    }
    private void ApplyColor(string color)
    {
        try { NoteSurface.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); note.Color = color; }
        catch { NoteSurface.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF1A8")); note.Color = "#FFF1A8"; }
    }
    private void ClipSurface(object sender, SizeChangedEventArgs e) => ((UIElement)sender).Clip = new RectangleGeometry(new Rect(e.NewSize), 13, 13);
    private void BeginTitleEdit()
    {
        TitleDisplay.Visibility = Visibility.Collapsed;
        TitleBox.Visibility = Visibility.Visible;
        TitleBox.Focus(); TitleBox.SelectAll();
    }
    private void EndTitleEdit()
    {
        TitleBox.Visibility = Visibility.Collapsed;
        TitleDisplay.Visibility = Visibility.Visible;
    }
    private void TitleDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { BeginTitleEdit(); e.Handled = true; }
    }
    private void TitleKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape) { EndTitleEdit(); e.Handled = true; }
    }
    private void TitleLostFocus(object sender, KeyboardFocusChangedEventArgs e) => EndTitleEdit();
    private void ApplyPin()
    {
        Topmost = note.Pinned;
        PinButton.Content = new Viewbox
        {
            Width = 20, Height = 20,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            RenderTransform = new RotateTransform(note.Pinned ? 0 : 35),
            Child = new Canvas
            {
                Width = 24, Height = 24,
                Children = { new System.Windows.Shapes.Path {
                    Data = Geometry.Parse("M8,2 L16,2 M9,2 L9,9 L6,13 L6,15 L18,15 L18,13 L15,9 L15,2 M12,15 L12,22"),
                    Stroke = Foreground, StrokeThickness = 1.7,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
                } }
            }
        };
        PinButton.Background = note.Pinned ? new SolidColorBrush(Color.FromArgb(50, 90, 65, 10)) : Brushes.Transparent;
        PinButton.Opacity = note.Pinned ? 1 : 0.6;
        var action = note.Pinned ? "Désépingler — actuellement au premier plan" : "Épingler au premier plan";
        PinButton.ToolTip = action;
        System.Windows.Automation.AutomationProperties.SetName(PinButton, action);
    }
    private void TogglePin(object sender, RoutedEventArgs e) { note.Pinned = !note.Pinned; ApplyPin(); app.Changed(); }
    private void NewNote(object sender, RoutedEventArgs e) => app.NewNote();
    private void HideClick(object sender, RoutedEventArgs e) => HideNote();
    private void DeleteClick(object sender, RoutedEventArgs e) => app.Delete(note);
    public void ClearSensitiveContent()
    {
        clearingSensitiveContent = true;
        Editor.IsUndoEnabled = false; TitleBox.IsUndoEnabled = false;
        Editor.Clear(); TitleBox.Clear(); Title = "Note protégée";
        Previewer.Document = null; renderedText = null; pendingEditorState = null;
        if (markdownHelp != null) markdownHelp.IsOpen = false;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr handle, uint flags);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    public void PlaceBottomLeft(Forms.Screen screen) => PlaceInCascade(screen, 0);
    public void PlaceInCascade(Forms.Screen screen, int index)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var area = screen.WorkingArea;
        // Native coordinates preserve monitor origins with mixed Windows DPI scaling.
        for (var pass = 0; pass < 2; pass++)
        {
            if (!GetWindowRect(handle, out var bounds)) return;
            var width = Math.Min(bounds.Right - bounds.Left, area.Width);
            var height = Math.Min(bounds.Bottom - bounds.Top, area.Height);
            var point = NoteManagement.Cascade(new Rect(area.Left, area.Top, area.Width, area.Height), new Size(width, height), index, 48 * VisualTreeHelper.GetDpi(this).DpiScaleY);
            SetWindowPos(handle, IntPtr.Zero, (int)point.X, (int)point.Y, width, height, 0x0014);
        }
        SaveBounds();
    }
    public void RaiseWithoutActivation() => SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0013);
    public EditorState CaptureEditorState() => new(Editor.SelectionStart, Editor.SelectionLength, Editor.VerticalOffset);
    public void RestoreEditorState(EditorState state) => pendingEditorState = state;
    public void HideNote() => app.HideNotes([note]);
    public void HideForCache()
    {
        note.Hidden = true;
        if (markdownHelp != null) markdownHelp.IsOpen = false;
        Hide(); Previewer.Document = null; renderedText = null;
        SpellCheck.SetIsEnabled(Editor, false);
    }
    public void ClosePermanently() { ready = false; allowClose = true; if (markdownHelp != null) markdownHelp.IsOpen = false; Close(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        // A protected window is destroyed on hide; wait until Closing has returned.
        if (note.IsProtected) Dispatcher.BeginInvoke(HideNote); else HideNote();
    }
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        // Leave text selection and buttons alone; the surrounding header moves the note.
        for (var element = e.OriginalSource as DependencyObject; element != null && element != sender; element = VisualTreeHelper.GetParent(element))
            if (element is TextBox || element is System.Windows.Controls.Primitives.ButtonBase) return;
        draggingHeader = true;
        try { DragMove(); } catch (InvalidOperationException) { }
        finally { draggingHeader = false; }
    }
    private void TogglePreview(object sender, RoutedEventArgs e) => SetPreview(!note.Preview);
    private void SetPreview(bool preview)
    {
        var changed = note.Preview != preview;
        note.Preview = preview; ApplyPreview();
        if (changed) app.Changed(listChanged: false);
    }
    private static DependencyObject? ParentOf(DependencyObject element) => element is FrameworkContentElement content
        ? content.Parent : element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
    private void PreviewDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || !note.Preview) return;
        int? offset = null;
        for (var element = e.OriginalSource as DependencyObject; element != null && element != Previewer; element = ParentOf(element))
        {
            if (element is Hyperlink or ButtonBase or ScrollBar or Thumb) return;
            if (offset == null && element is FrameworkContentElement { Tag: int position }) offset = position;
        }
        SetPreview(false);
        if (offset is int start)
        {
            Editor.Select(Math.Clamp(start, 0, Editor.Text.Length), 0);
            Editor.UpdateLayout();
            var line = Editor.GetLineIndexFromCharacterIndex(Editor.SelectionStart);
            if (line >= 0) Editor.ScrollToLine(line);
        }
        e.Handled = true;
    }
    private void ApplyPreview()
    {
        EditingBar.Visibility = note.Preview ? Visibility.Collapsed : Visibility.Visible;
        if (markdownHelp != null) markdownHelp.IsOpen = false;
        Editor.Visibility = note.Preview ? Visibility.Collapsed : Visibility.Visible;
        Previewer.Visibility = note.Preview ? Visibility.Visible : Visibility.Collapsed;
        // The built-in spelling menu takes focus and handles its own opening event.
        // Keep spelling errors valid for the entire editing session.
        SpellCheck.SetIsEnabled(Editor, IsVisible && !note.Preview);
        if (note.Preview && IsVisible && renderedText != note.Text) { Previewer.Document = MarkdownView.Render(note.Text, ToggleTask); renderedText = note.Text; }
        else if (!note.Preview && IsActive) Editor.Focus();
    }
    private void ToggleTask(int offset, bool check)
    {
        if (offset < 0 || offset >= note.Text.Length) return;
        note.Text = note.Text[..offset] + (check ? "x" : " ") + note.Text[(offset + 1)..];
        Editor.Text = note.Text; app.Changed();
    }
    private void OpenMenu(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var create = new MenuItem { Header = "Nouvelle note", InputGestureText = "Ctrl+N", Icon = MenuIcon("\uE710") };
        create.Click += NewNote; menu.Items.Add(create);
        var rename = new MenuItem { Header = "Renommer la note", InputGestureText = "F2", Icon = MenuIcon("\uE70F") };
        rename.Click += (_, _) => Dispatcher.BeginInvoke(BeginTitleEdit); menu.Items.Add(rename);
        var colors = new MenuItem { Header = "Couleur", Icon = MenuIcon("\uE790") };
        foreach (var (name, color) in new[] { ("Jaune", "#FFF1A8"), ("Bleu", "#CDE9FA"), ("Vert", "#D9EEC4"), ("Rose", "#FAD8E3"), ("Violet", "#E6DDF7") })
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = note.Color == color };
            item.Click += (_, _) => { ApplyColor(color); app.Changed(); }; colors.Items.Add(item);
        }
        menu.Items.Add(colors);
        var protect = new MenuItem { Header = note.IsProtected ? "Verrouiller" : "Protéger par mot de passe…", Icon = MenuIcon("\uE72E") };
        protect.Click += (_, _) => { if (note.IsProtected) app.LockNote(note); else app.ProtectNote(note); };
        menu.Items.Add(protect);
        if (note.IsProtected)
        {
            var unprotect = new MenuItem { Header = "Retirer la protection…" };
            unprotect.Click += (_, _) => app.RemoveProtection(note); menu.Items.Add(unprotect);
        }

        menu.Items.Add(new Separator());
        var delete = new MenuItem { Header = "Mettre à la corbeille", Icon = MenuIcon("\uE74D") }; delete.Click += DeleteClick; menu.Items.Add(delete);
        menu.Closed += (_, _) => ScheduleInactivePreview();
        menu.PlacementTarget = (UIElement)sender; menu.IsOpen = true;
    }
    private static TextBlock MenuIcon(string glyph) => new() { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
    private void BuildEditingBar()
    {
        foreach (var (tool, glyph, label) in new[] {
            (MarkdownTool.Bold, "B", "Gras (Ctrl+B)"), (MarkdownTool.Italic, "I", "Italique (Ctrl+I)"),
            (MarkdownTool.List, "≡", "Liste à puces"), (MarkdownTool.Checklist, "☑", "Checklist"),
            (MarkdownTool.Code, "</>", "Code — sélection multiligne : bloc de code"), (MarkdownTool.Link, "↗", "Lien (Ctrl+K)") })
        {
            var button = EditingButton(glyph, label);
            if (tool == MarkdownTool.Bold) button.FontWeight = FontWeights.Bold;
            if (tool == MarkdownTool.Italic) button.FontStyle = FontStyles.Italic;
            button.Click += (_, _) => MarkdownEditing.Apply(Editor, tool);
            editingButtons.Add((button, tool, label)); EditingBar.Children.Add(button);
        }
        overflowButton = EditingButton("⋯", "Autres outils d’édition");
        overflowButton.Click += (_, _) => {
            var menu = new ContextMenu();
            foreach (var entry in editingButtons.Where(b => b.Button.Visibility == Visibility.Collapsed))
            {
                var item = new MenuItem { Header = entry.Label };
                item.Click += (_, _) => MarkdownEditing.Apply(Editor, entry.Tool); menu.Items.Add(item);
            }
            menu.Closed += (_, _) => ScheduleInactivePreview();
            menu.PlacementTarget = overflowButton; menu.IsOpen = true;
        };
        EditingBar.Children.Add(overflowButton);
        var help = EditingButton("?", "Aide Markdown");
        help.Click += (_, _) => ShowMarkdownHelp(help); EditingBar.Children.Add(help);
    }
    private static Button EditingButton(string text, string label)
    {
        var button = new Button { Content = text, ToolTip = label, Width = 32, Height = 28, Padding = new Thickness(0), Background = Brushes.Transparent, Focusable = false, FontSize = 13 };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        return button;
    }
    private void EditingBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (overflowButton == null) return;
        var slots = Math.Max(2, (int)(e.NewSize.Width / 36));
        var count = slots >= editingButtons.Count + 1 ? editingButtons.Count : Math.Max(0, slots - 2);
        for (int i = 0; i < editingButtons.Count; i++) editingButtons[i].Button.Visibility = i < count ? Visibility.Visible : Visibility.Collapsed;
        overflowButton.Visibility = count < editingButtons.Count ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ShowMarkdownHelp(Button anchor)
    {
        if (markdownHelp?.IsOpen == true) { markdownHelp.IsOpen = false; return; }
        var panel = new StackPanel { Margin = new Thickness(14), Width = 270 };
        panel.Children.Add(new TextBlock { Text = "Markdown · l’essentiel", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var (label, syntax) in new[] {
            ("Titres", "# Titre  /  ## Sous-titre"), ("Mise en valeur", "**gras**  *italique*  ~~barré~~"),
            ("Listes", "- élément\n1. élément"), ("Tâches", "- [ ] À faire\n- [x] Terminé"),
            ("Code en ligne", "`variable`"), ("Bloc de code", "```csharp\nvar valeur = 42;\n```"),
            ("Lien", "[texte](https://exemple.com)"), ("Citation", "> remarque") })
        {
            panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 2) });
            panel.Children.Add(new TextBlock { Text = syntax, FontFamily = new FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        }
        panel.Children.Add(new TextBlock { Text = "Blocs de code sans coloration syntaxique.\nÉchap ou clic ailleurs pour fermer.", FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
        var border = new Border { Background = Brushes.WhiteSmoke, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Focusable = true,
            Child = new ScrollViewer { Content = panel, MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        markdownHelp = new System.Windows.Controls.Primitives.Popup { Child = border, PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Top, StaysOpen = false, AllowsTransparency = true };
        border.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { markdownHelp.IsOpen = false; Editor.Focus(); e.Handled = true; } };
        markdownHelp.Opened += (_, _) => border.Focus();
        markdownHelp.Closed += (_, _) => ScheduleInactivePreview();
        markdownHelp.IsOpen = true;
    }
}
