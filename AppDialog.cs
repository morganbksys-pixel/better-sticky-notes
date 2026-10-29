using System.Windows;
using System.Windows.Controls;

namespace BetterStickyNotes;

public static class AppDialog
{
    public static MessageBoxResult Show(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
        => Show(Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive), text, title, buttons, image, defaultResult);

    public static MessageBoxResult Show(Window? owner, string text, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        var result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.OK;
        var dialog = new Window { Title = title, Width = 460, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, WindowStartupLocation = owner?.IsVisible == true ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen };
        if (owner?.IsVisible == true) dialog.Owner = owner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new ScrollViewer { MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        void Add(string label, MessageBoxResult value, bool cancel, bool primary)
        {
            var button = new Button { Content = label, IsCancel = cancel, IsDefault = primary, MinWidth = 78, Padding = new Thickness(12, 8, 12, 8) };
            button.Click += (_, _) => { result = value; dialog.Close(); }; actions.Children.Add(button);
        }
        if (buttons == MessageBoxButton.YesNo) { Add("Non", MessageBoxResult.No, true, defaultResult != MessageBoxResult.Yes); Add("Oui", MessageBoxResult.Yes, false, defaultResult == MessageBoxResult.Yes); }
        else Add("OK", MessageBoxResult.OK, true, true);
        panel.Children.Add(actions); dialog.Content = panel; WindowAppearance.Apply(dialog); dialog.ShowDialog(); return result;
    }
}
