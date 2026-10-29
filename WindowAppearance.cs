using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace BetterStickyNotes;

public static class WindowAppearance
{
    public static void Apply(Window window)
    {
        if (window.Content is not UIElement content) return;
        window.Content = null;
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = true;
        window.Background = Brushes.Transparent;
        window.FontFamily = new FontFamily("Segoe UI"); window.FontSize = 13;
        WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = window.ResizeMode == ResizeMode.NoResize ? new Thickness(0) : new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0) });
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = new Grid { Background = new SolidColorBrush(Color.FromRgb(246, 246, 245)) };
        header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock { Margin = new Thickness(16, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(Window.Title)) { Source = window }); header.Children.Add(title);
        header.MouseLeftButtonDown += (_, e) => {
            for (var node = e.OriginalSource as DependencyObject; node != null && node != header; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
                if (node is Button) return;
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2 && window.ResizeMode == ResizeMode.CanResize) window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else if (e.ButtonState == MouseButtonState.Pressed) window.DragMove();
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 5, 3) };
        void Add(string text, string label, Action click)
        {
            var button = new Button { Content = NoteIcons.Glyph(text, 12), Width = 32, Padding = new Thickness(0), ToolTip = label, Background = Brushes.Transparent };
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += (_, _) => click(); buttons.Children.Add(button);
        }
        if (window.ResizeMode != ResizeMode.NoResize)
        {
            Add("\uE921", "Réduire", () => window.WindowState = WindowState.Minimized);
            Add("\uE922", "Agrandir / Restaurer", () => window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        }
        Add("\uE711", "Fermer", window.Close);
        Grid.SetColumn(buttons, 1); header.Children.Add(buttons); layout.Children.Add(header);
        Grid.SetRow(content, 1); layout.Children.Add(content);
        var surface = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(210, 212, 215)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = layout };
        layout.SizeChanged += (_, e) => layout.Clip = new RectangleGeometry(new Rect(e.NewSize), 11, 11);
        window.Content = surface;
    }
}
