using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace BetterStickyNotes;

public sealed class SettingsView : ScrollViewer
{
    public SettingsView(App app)
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        var panel = new StackPanel { Margin = new Thickness(28) };
        var title = new TextBlock { Text = "Paramètres", Margin = new Thickness(0, 0, 0, 24) };
        title.SetResourceReference(FrameworkElement.StyleProperty, "PageHeadingStyle"); panel.Children.Add(title);

        var launch = Section(panel, "Démarrage");
        var startup = new CheckBox { Content = "Démarrer avec Windows", Margin = new Thickness(0, 0, 0, 8) };
        launch.Children.Add(startup);
        launch.Children.Add(Description("Les post-it visibles retrouvent leur emplacement, sans ouvrir Mes notes. Les notes protégées restent verrouillées."));
        var startupError = Description("Impossible de lire le réglage de démarrage. Consultez Diagnostic.");
        startupError.SetResourceReference(TextBlock.ForegroundProperty, "AppDangerBrush");
        startupError.Visibility = Visibility.Collapsed;
        startupError.Margin = new Thickness(0, 8, 0, 0);
        AutomationProperties.SetLiveSetting(startupError, System.Windows.Automation.AutomationLiveSetting.Polite);
        launch.Children.Add(startupError);

        var import = Section(panel, "Importation");
        import.Children.Add(Description("Fermez Sticky Notes ou OneNote, puis choisissez les notes à importer. Les originales resteront intactes."));
        var importButton = ActionButton(import, "Importer depuis Sticky Notes…", app.ImportStickyNotes);
        importButton.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButtonStyle");
        var importHint = Description("Le texte, les listes et les couleurs sont récupérés. Les images et la mise en forme enrichie ne sont pas importées. Les notes importées restent masquées dans Mes notes.");
        importHint.FontSize = 12;
        importHint.Margin = new Thickness(0, 8, 0, 0);
        import.Children.Add(importHint);

        var data = Section(panel, "Données");
        data.Children.Add(Description("Vos notes et captures restent sur cet ordinateur. Pour les sauvegarder, quittez l’application puis copiez le dossier entier."));
        ActionButton(data, "Ouvrir le dossier des données", () => {
            try { Directory.CreateDirectory(NoteStore.Folder); Process.Start(new ProcessStartInfo(NoteStore.Folder) { UseShellExecute = true }); }
            catch (Exception ex) { AppDialog.Show(ex.Message, "Dossier des données"); }
        });

        var details = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        details.Children.Add(Heading("Application"));
        details.Children.Add(Description($"Version active : {AppInfo.Version}"));
        Field(details, "Exécutable actif", AppInfo.ExecutablePath);
        details.Children.Add(Description("Pour changer de version, quittez d’abord l’application depuis son icône de notification, puis ouvrez le nouvel exécutable. Si une version tourne déjà, un autre lancement ouvre Mes notes dans cette instance."));
        details.Children.Add(Heading("Démarrage Windows", top: 20));
        var startupCommand = Field(details, "Commande enregistrée au démarrage Windows", "");
        var repairStartup = new Button { Content = "Utiliser cette version au démarrage", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 0, 0) };
        details.Children.Add(repairStartup);
        details.Children.Add(Heading("Aide à l’import", top: 20));
        details.Children.Add(Description("Une base locale est proposée si elle est détectée. Sinon, collez l’un des chemins ci-dessous dans le sélecteur de fichiers : plum.sqlite pour Sticky Notes, ou notes.sdk_….db pour OneNote. En présence de plusieurs comptes, choisissez la base correspondant à vos notes."));
        Field(details, "Dossier Sticky Notes", "%LocalAppData%\\Packages\\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe\\LocalState");
        Field(details, "Dossier OneNote", "%LocalAppData%\\Microsoft\\OneNote\\16.0\\Memory");
        details.Children.Add(Description("L’import lit les fichiers locaux .sqlite et .db. Les blocs-notes .one, les anciennes bases .snt et les notes uniquement dans le cloud ne sont pas pris en charge. Si le fichier reste verrouillé, quittez Sticky Notes / OneNote et réessayez. Pour importer une copie de la base, conservez aussi ses fichiers -wal et -shm à côté s’ils existent."));
        var diagnostic = new Expander {
            Header = new TextBlock { Text = "Diagnostic", FontSize = 16, FontWeight = FontWeights.SemiBold },
            Content = details, IsExpanded = false, HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(diagnostic, "Diagnostic");
        panel.Children.Add(diagnostic);
        startup.Click += (_, _) => { App.ToggleStartup(); RefreshStartup(); };
        repairStartup.Click += (_, _) =>
        {
            try { App.UseCurrentVersionForStartup(); }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            { AppDialog.Show("Impossible de modifier le démarrage avec Windows.\n" + ex.Message, "Better Sticky Notes"); }
            RefreshStartup();
        };
        RefreshStartup();
        Content = panel;

        void RefreshStartup()
        {
            try
            {
                var command = App.GetStartupRegistration();
                startup.IsEnabled = true;
                startup.IsChecked = command is not null;
                startupCommand.Text = command ?? "Désactivé";
                repairStartup.IsEnabled = command is not null && Environment.ProcessPath is string executable && !App.StartupTargetsExecutable(command, executable);
                startupError.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                startup.IsEnabled = false;
                repairStartup.IsEnabled = false;
                startupCommand.Text = "Impossible de lire le démarrage Windows : " + ex.Message;
                startupError.Visibility = Visibility.Visible;
            }
        }
    }

    private static StackPanel Section(Panel parent, string title)
    {
        var content = new StackPanel();
        content.Children.Add(Heading(title));
        var section = new Border { Child = content, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 20), Margin = new Thickness(0, 0, 0, 20) };
        section.SetResourceReference(Border.BorderBrushProperty, "AppBorderBrush");
        AutomationProperties.SetName(section, title);
        parent.Children.Add(section);
        return content;
    }

    private static TextBlock Heading(string title, double top = 0) => new() {
        Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, top, 0, 10)
    };

    private static TextBlock Description(string text)
    {
        var description = new TextBlock { Text = text };
        description.SetResourceReference(FrameworkElement.StyleProperty, "AppDescriptionStyle");
        return description;
    }

    private static TextBox Field(Panel parent, string label, string value)
    {
        var caption = Description(label);
        caption.Margin = new Thickness(0, 8, 0, 0);
        parent.Children.Add(caption);
        var field = new TextBox { Text = value, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12) };
        AutomationProperties.SetName(field, label);
        parent.Children.Add(field);
        return field;
    }

    private static Button ActionButton(Panel parent, string label, Action action)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 12, 0, 0) };
        button.Click += (_, _) => action();
        parent.Children.Add(button);
        return button;
    }
}
