using System.Windows;
using System.Windows.Controls;

namespace BetterStickyNotes;

public static class AppDialog
{
    public static MessageBoxResult Show(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None, string? destructiveAction = null)
        => Show(Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive), text, title, buttons, image, defaultResult, destructiveAction);

    public static MessageBoxResult Show(Window? owner, string text, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None, string? destructiveAction = null)
    {
        var result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.OK;
        var dialog = new Window { Title = title, Width = 460, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, WindowStartupLocation = owner?.IsVisible == true ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen };
        if (owner?.IsVisible == true) dialog.Owner = owner;
        var panel = new StackPanel(); panel.SetResourceReference(FrameworkElement.StyleProperty, "DialogBodyStyle");
        var heading = new TextBlock { Text = title }; heading.SetResourceReference(FrameworkElement.StyleProperty, "DialogHeadingStyle"); panel.Children.Add(heading);
        var message = new TextBlock { Text = text }; message.SetResourceReference(FrameworkElement.StyleProperty, "DialogDescriptionStyle");
        panel.Children.Add(new ScrollViewer { MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = message });
        var actions = new StackPanel(); actions.SetResourceReference(FrameworkElement.StyleProperty, "DialogActionsStyle");
        void Add(string label, MessageBoxResult value, bool cancel, bool primary, bool destructive = false)
        {
            var button = new Button { Content = label, IsCancel = cancel, IsDefault = primary };
            button.SetResourceReference(FrameworkElement.StyleProperty, destructive ? "DialogDangerButtonStyle" : value is MessageBoxResult.OK or MessageBoxResult.Yes ? "DialogPrimaryButtonStyle" : "DialogButtonStyle");
            if (cancel && destructiveAction != null) dialog.Loaded += (_, _) => button.Focus();
            button.Click += (_, _) => { result = value; dialog.Close(); }; actions.Children.Add(button);
        }
        if (buttons == MessageBoxButton.YesNo)
        {
            Add(destructiveAction != null ? "Annuler" : "Non", MessageBoxResult.No, true, destructiveAction != null || defaultResult != MessageBoxResult.Yes);
            Add(destructiveAction ?? "Oui", MessageBoxResult.Yes, false, destructiveAction == null && defaultResult == MessageBoxResult.Yes, destructiveAction != null);
        }
        else Add("OK", MessageBoxResult.OK, true, true);
        panel.Children.Add(actions); dialog.Content = panel; WindowAppearance.Apply(dialog); dialog.ShowDialog(); return result;
    }
}
