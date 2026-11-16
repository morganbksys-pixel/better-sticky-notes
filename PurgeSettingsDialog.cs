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
        var panel = new StackPanel(); panel.SetResourceReference(FrameworkElement.StyleProperty, "DialogBodyStyle");
        var heading = new TextBlock { Text = "Purge automatique" }; heading.SetResourceReference(FrameworkElement.StyleProperty, "DialogHeadingStyle"); panel.Children.Add(heading);
        var enabled = new CheckBox { Content = "Activer la purge automatique", IsChecked = app.Store.TrashRetentionDays != null, Margin = new Thickness(0, 4, 0, 0) };
        RegisterName("AutoPurge", enabled); panel.Children.Add(enabled);
        panel.Children.Add(new TextBlock { Text = "Supprimer les notes après ce nombre de jours :", Margin = new Thickness(0, 18, 0, 8) });
        var days = new TextBox { Text = (app.Store.TrashRetentionDays ?? 30).ToString(), MaxLength = 5, Padding = new Thickness(8), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
        RegisterName("PurgeDays", days); System.Windows.Automation.AutomationProperties.SetName(days, "Délai de purge en jours"); panel.Children.Add(days);
        var warning = new TextBlock { Text = "Suppression définitive sans confirmation des notes arrivées à échéance, y compris dès l’activation.", Margin = new Thickness(0, 16, 0, 0) };
        warning.SetResourceReference(FrameworkElement.StyleProperty, "DialogDescriptionStyle"); panel.Children.Add(warning);
        var status = new TextBlock(); status.SetResourceReference(FrameworkElement.StyleProperty, "DialogErrorStyle"); RegisterName("PurgeStatus", status); panel.Children.Add(status);
        var buttons = new StackPanel(); buttons.SetResourceReference(FrameworkElement.StyleProperty, "DialogActionsStyle");
        var close = new Button { Content = "Fermer", IsCancel = true }; close.SetResourceReference(FrameworkElement.StyleProperty, "DialogButtonStyle"); close.Click += (_, _) => Close();
        var apply = new Button { Content = "Appliquer", IsDefault = true }; apply.SetResourceReference(FrameworkElement.StyleProperty, "DialogPrimaryButtonStyle"); RegisterName("ApplyButton", apply);
        apply.Click += (_, _) => {
            int? retention = null;
            if (enabled.IsChecked == true)
            {
                if (!int.TryParse(days.Text, out var value) || value is < 1 or > 36500)
                { status.SetResourceReference(TextBlock.ForegroundProperty, "AppDangerBrush"); status.Text = "Saisissez un nombre de jours entre 1 et 36 500."; return; }
                retention = value;
            }
            var saved = app.ConfigureTrashRetention(retention);
            status.SetResourceReference(TextBlock.ForegroundProperty, saved ? "AppFocusBrush" : "AppDangerBrush");
            status.Text = saved
                ? retention == null ? "Purge automatique désactivée." : $"Purge activée après {retention} jours."
                : "Réglage non enregistré. Réessayez.";
        };
        buttons.Children.Add(close); buttons.Children.Add(apply); panel.Children.Add(buttons); Content = panel; WindowAppearance.Apply(this);
    }
}
