using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BetterStickyNotes;

public sealed class NotePinIcon : Control
{
    private static readonly Geometry body = CreateBody();

    public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(NotePinIcon), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsPinned { get => (bool)GetValue(IsPinnedProperty); set => SetValue(IsPinnedProperty, value); }

    static NotePinIcon() => ForegroundProperty.OverrideMetadata(typeof(NotePinIcon), new FrameworkPropertyMetadata(SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public NotePinIcon()
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
        var pen = new Pen(Foreground, 1.65) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        drawingContext.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        drawingContext.PushTransform(new ScaleTransform(size / 24, size / 24));
        drawingContext.PushTransform(new RotateTransform(35, 12, 12));
        drawingContext.DrawGeometry(IsPinned ? Foreground : null, pen, body);
        drawingContext.DrawLine(pen, new Point(12, 15), new Point(12, 22));
        drawingContext.Pop(); drawingContext.Pop(); drawingContext.Pop();
    }

    private static Geometry CreateBody()
    {
        var shape = Geometry.Parse("M8 3 L16 3 L16 5 L15 6 L15 10 L19 14 L19 15 L5 15 L5 14 L9 10 L9 6 L8 5 Z");
        shape.Freeze(); return shape;
    }
}
