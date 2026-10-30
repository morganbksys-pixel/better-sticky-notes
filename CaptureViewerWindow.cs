using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace BetterStickyNotes;

public sealed class CaptureViewerWindow : Window
{
    private readonly NoteStore store;
    private readonly bool trashScope;
    private List<CaptureReference> sequence;
    private int index;
    private readonly ScrollViewer viewport;
    private readonly Grid stage;
    private readonly TextBlock message;
    private readonly TextBlock counter;
    private readonly TextBlock zoomLabel;
    private readonly Button previous;
    private readonly Button next;
    private bool fitting = true;
    private double zoom = 1;
    private Point? dragStart;
    private Point dragOffset;
    private bool updating;
    public Image CaptureImage { get; }
    public double ZoomFactor => zoom;
    public bool IsFitting => fitting;
    public string CounterText => counter.Text;
    public CaptureReference? Current => sequence.Count == 0 ? null : sequence[index];

    public CaptureViewerWindow(NoteStore store, Note note, NoteCapture capture, Window owner, IEnumerable<CaptureReference>? captures = null)
    {
        this.store = store;
        trashScope = note.Deleted;
        sequence = (captures ?? CaptureCatalog.ForNote(note)).Where(Available).ToList();
        index = Math.Max(0, sequence.FindIndex(c => c.Note.Id == note.Id && c.Capture.Id == capture.Id));
        if (owner.IsVisible) Owner = owner;
        ShowInTaskbar = false; Topmost = owner.Topmost;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = Forms.Screen.FromHandle(new WindowInteropHelper(owner).Handle);
        var dpi = VisualTreeHelper.GetDpi(owner);
        var area = new Rect(screen.WorkingArea.Left / dpi.DpiScaleX, screen.WorkingArea.Top / dpi.DpiScaleY,
            screen.WorkingArea.Width / dpi.DpiScaleX, screen.WorkingArea.Height / dpi.DpiScaleY);
        var bounds = FitBounds(area, new Rect(owner.Left, owner.Top, owner.Width, owner.Height));
        Left = bounds.Left; Top = bounds.Top; Width = bounds.Width; Height = bounds.Height;
        MinWidth = Math.Min(320, Width); MinHeight = Math.Min(240, Height);
        CaptureImage = new Image { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stage = new Grid { Children = { CaptureImage } };
        viewport = new ScrollViewer { Content = stage, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, Background = Brushes.Transparent,
            ToolTip = "Ctrl + molette : zoomer · Glisser : déplacer l’image agrandie" };
        message = new TextBlock { TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16), Visibility = Visibility.Collapsed };
        counter = new TextBlock { Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center };
        zoomLabel = new TextBlock { Margin = new Thickness(8, 6, 2, 6), VerticalAlignment = VerticalAlignment.Center };
        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        Button Tool(object content, string label, Action action)
        {
            var button = new Button { Content = content, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 2, 4, 2), ToolTip = label };
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += (_, _) => action(); tools.Children.Add(button); return button;
        }
        previous = Tool(NoteIcons.Glyph("\uE72B"), "Capture précédente (←)", () => MoveCapture(-1));
        tools.Children.Add(counter);
        next = Tool(NoteIcons.Glyph("\uE72A"), "Capture suivante (→)", () => MoveCapture(1));
        Tool("Adapter", "Afficher l’image entière", FitImage);
        Tool("100 %", "Afficher à la taille réelle", () => ZoomAt(1, ViewCenter()));
        tools.Children.Add(zoomLabel);
        var surface = new Grid { Margin = new Thickness(12) };
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); surface.RowDefinitions.Add(new RowDefinition());
        surface.Children.Add(tools); Grid.SetRow(viewport, 1); surface.Children.Add(viewport); Grid.SetRow(message, 1); surface.Children.Add(message);
        Content = surface;
        viewport.SizeChanged += (_, _) => RefreshScale();
        viewport.ScrollChanged += (_, e) => { if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) RefreshScale(); };
        PreviewMouseWheel += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || e.Delta == 0) return;
            var anchor = e.GetPosition(viewport);
            if (anchor.X < 0 || anchor.Y < 0 || anchor.X > viewport.ActualWidth || anchor.Y > viewport.ActualHeight) return;
            ZoomAt(zoom * Math.Pow(1.2, e.Delta / 120.0), anchor); e.Handled = true;
        };
        viewport.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (fitting || viewport.ExtentWidth <= viewport.ViewportWidth && viewport.ExtentHeight <= viewport.ViewportHeight) return;
            dragStart = e.GetPosition(viewport); dragOffset = new Point(viewport.HorizontalOffset, viewport.VerticalOffset);
            viewport.CaptureMouse(); viewport.Cursor = Cursors.SizeAll; e.Handled = true;
        };
        viewport.MouseMove += (_, e) =>
        {
            if (dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
            var current = e.GetPosition(viewport);
            viewport.ScrollToHorizontalOffset(dragOffset.X + start.X - current.X);
            viewport.ScrollToVerticalOffset(dragOffset.Y + start.Y - current.Y);
        };
        viewport.PreviewMouseLeftButtonUp += (_, e) => { if (dragStart != null) { viewport.ReleaseMouseCapture(); e.Handled = true; } };
        viewport.LostMouseCapture += (_, _) => { dragStart = null; viewport.Cursor = Cursors.Arrow; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Left or Key.Right)
            { MoveCapture(e.Key == Key.Left ? -1 : 1); e.Handled = true; }
        };
        LoadCurrent();
        WindowAppearance.Apply(this);
    }

    private bool Available(CaptureReference item) => store.Notes.Contains(item.Note) && !item.Note.IsProtected && item.Note.Deleted == trashScope && item.Note.Captures.Contains(item.Capture);

    public void MoveCapture(int direction)
    {
        if (sequence.Count <= 1) return;
        var current = Current;
        sequence = sequence.Where(Available).ToList();
        var position = current == null ? -1 : sequence.IndexOf(current);
        index = sequence.Count == 0 ? 0 : (Math.Max(0, position) + direction % sequence.Count + sequence.Count) % sequence.Count;
        LoadCurrent();
    }

    private void LoadCurrent()
    {
        viewport.ReleaseMouseCapture();
        CaptureImage.Source = null; message.Visibility = Visibility.Collapsed;
        zoomLabel.Text = "—";
        previous.IsEnabled = next.IsEnabled = sequence.Count > 1;
        counter.Text = sequence.Count == 0 ? "0 / 0" : $"{index + 1} / {sequence.Count}";
        if (Current is not { } current) { message.Text = "Aucune capture disponible."; message.Visibility = Visibility.Visible; return; }
        Title = $"Capture du {current.Capture.CreatedAtUtc.ToLocalTime():dd/MM/yyyy à HH:mm:ss} — {current.Note.DisplayTitle}";
        try { CaptureImage.Source = store.LoadCapture(current.Note, current.Capture); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or InvalidOperationException)
        { message.Text = "Cette capture n’est plus disponible."; message.Visibility = Visibility.Visible; }
        FitImage();
    }

    public void FitImage()
    {
        fitting = true;
        viewport.HorizontalScrollBarVisibility = viewport.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        viewport.ScrollToHorizontalOffset(0); viewport.ScrollToVerticalOffset(0);
        RefreshScale();
    }

    private Point ViewCenter() => new(viewport.ViewportWidth / 2, viewport.ViewportHeight / 2);

    public void ZoomAt(double target, Point anchor)
    {
        if (CaptureImage.Source is not BitmapSource image || !double.IsFinite(target)) return;
        viewport.UpdateLayout();
        var oldLeft = Math.Max(0, (stage.Width - image.PixelWidth * zoom) / 2);
        var oldTop = Math.Max(0, (stage.Height - image.PixelHeight * zoom) / 2);
        var pixel = new Point((viewport.HorizontalOffset + anchor.X - oldLeft) / zoom, (viewport.VerticalOffset + anchor.Y - oldTop) / zoom);
        fitting = false; zoom = Math.Clamp(target, 0.01, 8);
        viewport.HorizontalScrollBarVisibility = viewport.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        RefreshScale(); viewport.UpdateLayout(); RefreshScale();
        var newLeft = Math.Max(0, (stage.Width - image.PixelWidth * zoom) / 2);
        var newTop = Math.Max(0, (stage.Height - image.PixelHeight * zoom) / 2);
        viewport.ScrollToHorizontalOffset(Math.Max(0, newLeft + pixel.X * zoom - anchor.X));
        viewport.ScrollToVerticalOffset(Math.Max(0, newTop + pixel.Y * zoom - anchor.Y));
    }

    private void RefreshScale()
    {
        if (updating || CaptureImage.Source is not BitmapSource image) return;
        updating = true;
        try
        {
            var width = Math.Max(1, viewport.ViewportWidth > 0 ? viewport.ViewportWidth : viewport.ActualWidth);
            var height = Math.Max(1, viewport.ViewportHeight > 0 ? viewport.ViewportHeight : viewport.ActualHeight);
            if (fitting) zoom = Math.Min(width / image.PixelWidth, height / image.PixelHeight);
            CaptureImage.Width = image.PixelWidth * zoom; CaptureImage.Height = image.PixelHeight * zoom;
            stage.Width = Math.Max(width, CaptureImage.Width); stage.Height = Math.Max(height, CaptureImage.Height);
            zoomLabel.Text = $"{zoom:P0}";
        }
        finally { updating = false; }
    }

    public static Rect FitBounds(Rect screen, Rect owner)
    {
        var width = Math.Max(1, Math.Min(960, screen.Width - 32));
        var height = Math.Max(1, Math.Min(640, screen.Height - 32));
        var centerX = double.IsFinite(owner.Left) ? owner.Left + owner.Width / 2 : screen.Left + screen.Width / 2;
        var centerY = double.IsFinite(owner.Top) ? owner.Top + owner.Height / 2 : screen.Top + screen.Height / 2;
        return new Rect(Math.Clamp(centerX - width / 2, screen.Left, screen.Right - width), Math.Clamp(centerY - height / 2, screen.Top, screen.Bottom - height), width, height);
    }
}
