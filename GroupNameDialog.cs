using System.Windows;
using System.Windows.Controls;

namespace BetterStickyNotes;

public sealed class GroupNameDialog : Window
{
    public GroupNameDialog(Window owner, string title, string initialName, Action<string> save, string label = "Nom du groupe", int maxLength = 80)
    {
        if (owner.IsVisible) Owner = owner;
        Title = title; Width = 400; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 8) });
        var input = new TextBox { Text = initialName, MaxLength = maxLength, Padding = new Thickness(6) };
        System.Windows.Automation.AutomationProperties.SetName(input, label);
        panel.Children.Add(input);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Annuler", IsCancel = true };
        var confirm = new Button { Content = "Enregistrer", IsDefault = true };
        confirm.Click += (_, _) => {
            try { save(input.Text); DialogResult = true; }
            catch (ArgumentException ex) { error.Text = ex.Message; }
        };
        buttons.Children.Add(cancel); buttons.Children.Add(confirm); panel.Children.Add(buttons); Content = panel; WindowAppearance.Apply(this);
        Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
    }
}
