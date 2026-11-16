using System.Windows;
using System.Windows.Controls;

namespace BetterStickyNotes;

public sealed class PasswordDialog : Window
{
    private const int MinimumPasswordLength = 8;
    public static byte[]? Request(bool create, Func<string, byte[]> validate)
    {
        byte[]? key = null;
        var dialog = new PasswordDialog { Title = create ? "Protéger la note" : "Mot de passe des notes", Width = 400,
            SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false, Topmost = true };
        var panel = new StackPanel(); panel.SetResourceReference(FrameworkElement.StyleProperty, "DialogBodyStyle");
        var heading = new TextBlock { Text = create ? "Protéger la note" : "Déverrouiller les notes" };
        heading.SetResourceReference(FrameworkElement.StyleProperty, "DialogHeadingStyle"); panel.Children.Add(heading);
        var description = new TextBlock { Text = create
            ? $"Choisissez un mot de passe commun aux notes protégées ({MinimumPasswordLength} caractères minimum). En cas d’oubli, leur contenu ne pourra pas être récupéré."
            : "Saisissez le mot de passe commun aux notes protégées." };
        description.SetResourceReference(FrameworkElement.StyleProperty, "DialogDescriptionStyle"); panel.Children.Add(description);
        panel.Children.Add(new TextBlock { Text = "Mot de passe", Margin = new Thickness(0, 16, 0, 6) });
        var password = new PasswordBox();
        System.Windows.Automation.AutomationProperties.SetName(password, "Mot de passe");
        panel.Children.Add(password);
        var confirm = new PasswordBox();
        if (create)
        {
            panel.Children.Add(new TextBlock { Text = "Confirmer le mot de passe", Margin = new Thickness(0, 16, 0, 6) }); panel.Children.Add(confirm);
            System.Windows.Automation.AutomationProperties.SetName(confirm, "Confirmer le mot de passe");
        }
        var error = new TextBlock(); error.SetResourceReference(FrameworkElement.StyleProperty, "DialogErrorStyle");
        panel.Children.Add(error);
        var buttons = new StackPanel(); buttons.SetResourceReference(FrameworkElement.StyleProperty, "DialogActionsStyle");
        var cancel = new Button { Content = "Annuler", IsCancel = true }; cancel.SetResourceReference(FrameworkElement.StyleProperty, "DialogButtonStyle");
        var ok = new Button { Content = create ? "Protéger" : "Valider", IsDefault = true };
        ok.SetResourceReference(FrameworkElement.StyleProperty, "DialogPrimaryButtonStyle");
        ok.Click += (_, _) =>
        {
            if (create && password.Password.Length < MinimumPasswordLength) { error.Text = $"Utilisez au moins {MinimumPasswordLength} caractères."; return; }
            if (create && password.Password != confirm.Password) { error.Text = "Les mots de passe ne correspondent pas."; return; }
            try { key = validate(password.Password); dialog.DialogResult = true; }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException or System.Text.Json.JsonException or ArgumentException)
            { error.Text = "Mot de passe incorrect ou données protégées endommagées."; password.SelectAll(); password.Focus(); }
        };
        buttons.Children.Add(cancel); buttons.Children.Add(ok); panel.Children.Add(buttons); dialog.Content = panel; WindowAppearance.Apply(dialog);
        dialog.Loaded += (_, _) => password.Focus();
        dialog.ShowDialog(); password.Clear(); confirm.Clear();
        return key;
    }
}
