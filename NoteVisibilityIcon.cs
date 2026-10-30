using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BetterStickyNotes;

public sealed class NoteVisibilityIcon : Control
{
    private static readonly Geometry visibleEye = Shape("M2 12 C7 2.8 17 2.8 22 12 C17 21.2 7 21.2 2 12 Z M15 12 A3 3 0 1 1 9 12 A3 3 0 1 1 15 12");
    private static readonly Geometry hiddenEye = Shape("M9.5 5.4 C14.9 3.9 19.3 7.3 22 12 C21.5 13 20.8 14 20.1 14.8 M17.6 17.6 C11.6 21.1 5.5 17.9 2 12 C3 10.1 4.3 8.6 6.3 7.2 M9.9 9.9 A3 3 0 0 0 14.1 14.1 M3 3 L21 21");

    public static readonly DependencyProperty IsHiddenProperty = DependencyProperty.Register(nameof(IsHidden), typeof(bool), typeof(NoteVisibilityIcon), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsHidden { get => (bool)GetValue(IsHiddenProperty); set => SetValue(IsHiddenProperty, value); }

    static NoteVisibilityIcon() => ForegroundProperty.OverrideMetadata(typeof(NoteVisibilityIcon), new FrameworkPropertyMetadata(SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public NoteVisibilityIcon()
    {
        Width = Height = 18;
        Focusable = false;
        IsHitTestVisible = false;
        VerticalAlignment = VerticalAlignment.Center;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        var pen = new Pen(Foreground, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        drawingContext.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        drawingContext.PushTransform(new ScaleTransform(size / 24, size / 24));
        drawingContext.DrawGeometry(null, pen, IsHidden ? hiddenEye : visibleEye);
        drawingContext.Pop(); drawingContext.Pop();
    }

    private static Geometry Shape(string data)
    {
        var shape = Geometry.Parse(data); shape.Freeze(); return shape;
    }
}
