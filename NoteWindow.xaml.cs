using System.ComponentModel;
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
    private bool allowClose;
    public NoteWindow(App app, Note note)
    {
        this.app = app; this.note = note;
        InitializeComponent();
        Left = note.Left; Top = note.Top; Width = note.Width; Height = note.Height;
        TitleBox.Text = note.Title; Editor.Text = note.Text;
        Title = string.IsNullOrWhiteSpace(note.Title) ? "Sans titre — Better Sticky Notes" : note.Title;
        ApplyColor(note.Color); ApplyPin(); ApplyPreview();
        TitleBox.TextChanged += (_, _) => { note.Title = TitleBox.Text; Title = string.IsNullOrWhiteSpace(note.Title) ? "Sans titre — Better Sticky Notes" : note.Title; app.Changed(); };
        Editor.TextChanged += (_, _) => { note.Text = Editor.Text; app.Changed(); };
        LocationChanged += (_, _) => SaveBounds(); SizeChanged += (_, _) => SaveBounds();
        Loaded += (_, _) => { ready = true; EnsureVisibleOnScreen(); SaveBounds(); if (!note.Preview) Editor.Focus(); };
        Closing += OnClosing;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F2) { BeginTitleEdit(); e.Handled = true; return; }
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key == Key.Enter) { TogglePreview(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.N) { app.NewNote(); e.Handled = true; }
            else if (e.Key == Key.S) { app.SaveNow(); e.Handled = true; }
        };
    }
    private void SaveBounds()
    {
        if (!ready || WindowState != WindowState.Normal) return;
        note.Left = Left; note.Top = Top; note.Width = ActualWidth; note.Height = ActualHeight; app.Changed();
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
    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        var title = string.IsNullOrWhiteSpace(note.Title) ? "Sans titre" : note.Title;
        if (System.Windows.MessageBox.Show(this, $"Mettre la note « {title} » à la corbeille ?\n\nVous pourrez la restaurer depuis le menu de l’icône système.", "Supprimer la note", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            app.Delete(note);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    public void PlaceBottomLeft(Forms.Screen screen)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var area = screen.WorkingArea;
        // Native coordinates preserve monitor origins with mixed Windows DPI scaling.
        for (var pass = 0; pass < 2; pass++)
        {
            if (!GetWindowRect(handle, out var bounds)) return;
            var margin = (int)Math.Round(16 * VisualTreeHelper.GetDpi(this).DpiScaleY);
            SetWindowPos(handle, IntPtr.Zero, area.Left + margin, Math.Max(area.Top, area.Bottom - (bounds.Bottom - bounds.Top) - margin), 0, 0, 0x0015);
        }
        SaveBounds();
    }
    public void HideNote() { note.Hidden = true; Hide(); app.SaveNow(); }
    public void ClosePermanently() { allowClose = true; Close(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!allowClose) { e.Cancel = true; HideNote(); } }
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        // Leave text selection and buttons alone; the surrounding header moves the note.
        for (var element = e.OriginalSource as DependencyObject; element != null && element != sender; element = VisualTreeHelper.GetParent(element))
            if (element is TextBox || element is System.Windows.Controls.Primitives.ButtonBase) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }
    private void TogglePreview(object sender, RoutedEventArgs e) { note.Preview = !note.Preview; ApplyPreview(); app.Changed(); }
    private void ApplyPreview()
    {
        Editor.Visibility = note.Preview ? Visibility.Collapsed : Visibility.Visible;
        Previewer.Visibility = note.Preview ? Visibility.Visible : Visibility.Collapsed;
        PreviewButton.Content = note.Preview ? "\uE70F" : "\uE890";
        var action = note.Preview ? "Modifier la note" : "Afficher l’aperçu";
        PreviewButton.ToolTip = action + " (Ctrl+Entrée)";
        System.Windows.Automation.AutomationProperties.SetName(PreviewButton, action);
        if (note.Preview) Previewer.Document = MarkdownView.Render(note.Text, ToggleTask);
        else Editor.Focus();
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
        menu.Items.Add(new Separator());
        var hide = new MenuItem { Header = "Masquer la note", Icon = new Viewbox {
            Width = 16, Height = 16, Child = new Canvas { Width = 24, Height = 24,
                Children = { new System.Windows.Shapes.Path {
                    Data = Geometry.Parse("M2,12 C7,4 17,4 22,12 C17,20 7,20 2,12 M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12 M3,3 L21,21"),
                    Stroke = Foreground, StrokeThickness = 1.7, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                } }
            }
        } }; hide.Click += HideClick; menu.Items.Add(hide);
        var delete = new MenuItem { Header = "Mettre à la corbeille…", Icon = MenuIcon("\uE74D") }; delete.Click += DeleteClick; menu.Items.Add(delete);
        menu.PlacementTarget = (UIElement)sender; menu.IsOpen = true;
    }
    private static TextBlock MenuIcon(string glyph) => new() { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
}
