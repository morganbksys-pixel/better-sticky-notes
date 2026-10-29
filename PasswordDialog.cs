using System.Windows;
using System.Windows.Controls;

namespace BetterStickyNotes;

public sealed class PasswordDialog : Window
{
    public static byte[]? Request(bool create, Func<string, byte[]> validate)
    {
        byte[]? key = null;
        var dialog = new PasswordDialog { Title = create ? "Protéger la note" : "Mot de passe des notes", Width = 400,
            SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false, Topmost = true };
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = create
            ? "Choisissez un mot de passe commun aux notes protégées (12 caractères minimum). En cas d’oubli, leur contenu ne pourra pas être récupéré."
            : "Saisissez le mot de passe commun aux notes protégées.", TextWrapping = TextWrapping.Wrap });
        var password = new PasswordBox { Margin = new Thickness(0, 14, 0, 10) };
        System.Windows.Automation.AutomationProperties.SetName(password, "Mot de passe");
        panel.Children.Add(password);
        var confirm = new PasswordBox { Margin = new Thickness(0, 5, 0, 10) };
        if (create)
        {
            panel.Children.Add(new TextBlock { Text = "Confirmer le mot de passe" }); panel.Children.Add(confirm);
            System.Windows.Automation.AutomationProperties.SetName(confirm, "Confirmer le mot de passe");
        }
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick };
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Annuler", IsCancel = true, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        var ok = new Button { Content = create ? "Protéger" : "Valider", IsDefault = true, Padding = new Thickness(12, 6, 12, 6) };
        ok.Click += (_, _) =>
        {
            if (create && password.Password.Length < 12) { error.Text = "Utilisez au moins 12 caractères."; return; }
            if (create && password.Password != confirm.Password) { error.Text = "Les mots de passe ne correspondent pas."; return; }
            try { key = validate(password.Password); dialog.DialogResult = true; }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException or System.Text.Json.JsonException or ArgumentException)
            { error.Text = "Mot de passe incorrect ou données protégées endommagées."; password.SelectAll(); password.Focus(); }
        };
        buttons.Children.Add(cancel); buttons.Children.Add(ok); panel.Children.Add(buttons); dialog.Content = panel;
        dialog.Loaded += (_, _) => password.Focus();
        dialog.ShowDialog(); password.Clear(); confirm.Clear();
        return key;
    }
}
