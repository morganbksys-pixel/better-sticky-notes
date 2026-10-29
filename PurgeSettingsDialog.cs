using System.Windows;
using System.Windows.Controls;

namespace BetterStickyNotes;

public sealed class PurgeSettingsDialog : Window
{
    public PurgeSettingsDialog(Window owner, App app)
    {
        NameScope.SetNameScope(this, new NameScope());
        if (owner.IsVisible) Owner = owner;
        Title = "Purge automatique"; Width = 430; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); FontSize = 13;
        var panel = new StackPanel { Margin = new Thickness(24) };
        var enabled = new CheckBox { Content = "Activer la purge automatique", IsChecked = app.Store.TrashRetentionDays != null };
        RegisterName("AutoPurge", enabled); panel.Children.Add(enabled);
        panel.Children.Add(new TextBlock { Text = "Supprimer les notes après ce nombre de jours :", Margin = new Thickness(0, 18, 0, 8) });
        var days = new TextBox { Text = (app.Store.TrashRetentionDays ?? 30).ToString(), MaxLength = 5, Padding = new Thickness(8), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
        RegisterName("PurgeDays", days); System.Windows.Automation.AutomationProperties.SetName(days, "Délai de purge en jours"); panel.Children.Add(days);
        panel.Children.Add(new TextBlock { Text = "Suppression définitive sans confirmation des notes arrivées à échéance, y compris dès l’activation.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 12) });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap }; RegisterName("PurgeStatus", status); panel.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var close = new Button { Content = "Fermer", IsCancel = true }; close.Click += (_, _) => Close();
        var apply = new Button { Content = "Appliquer", IsDefault = true }; RegisterName("ApplyButton", apply);
        apply.Click += (_, _) => {
            int? retention = null;
            if (enabled.IsChecked == true)
            {
                if (!int.TryParse(days.Text, out var value) || value is < 1 or > 36500)
                { status.Text = "Saisissez un nombre de jours entre 1 et 36 500."; return; }
                retention = value;
            }
            status.Text = app.ConfigureTrashRetention(retention)
                ? retention == null ? "Purge automatique désactivée." : $"Purge activée après {retention} jours."
                : "Réglage non enregistré. Réessayez.";
        };
        buttons.Children.Add(close); buttons.Children.Add(apply); panel.Children.Add(buttons); Content = panel; WindowAppearance.Apply(this);
    }
}
