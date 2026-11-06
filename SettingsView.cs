using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace BetterStickyNotes;

public sealed class SettingsView : ScrollViewer
{
    public SettingsView(App app)
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(new TextBlock { Text = "Paramètres", FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 24) });
        var startup = new CheckBox { Content = "Démarrer avec Windows", Margin = new Thickness(0, 0, 0, 10) };
        panel.Children.Add(startup);
        panel.Children.Add(new TextBlock { Text = "Les post-it précédemment visibles sont restaurés à leur emplacement, sans ouvrir Mes notes. Les notes protégées restent verrouillées.", Foreground = System.Windows.Media.Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = $"Version active : {AppInfo.Version}", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBox { Text = AppInfo.ExecutablePath, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 10) });
        panel.Children.Add(new TextBlock { Text = "Commande enregistrée au démarrage Windows :" });
        var startupCommand = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
        panel.Children.Add(startupCommand);
        var repairStartup = new Button { Content = "Utiliser cette version au démarrage", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 4, 0, 8) };
        panel.Children.Add(repairStartup);
        void RefreshStartup()
        {
            try
            {
                var command = App.GetStartupRegistration();
                startup.IsEnabled = true; startup.ToolTip = null;
                startup.IsChecked = command is not null;
                startupCommand.Text = command ?? "Désactivé";
                repairStartup.IsEnabled = command is not null && Environment.ProcessPath is string executable && !App.StartupTargetsExecutable(command, executable);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                startup.IsEnabled = false; startup.ToolTip = ex.Message;
                repairStartup.IsEnabled = false;
                startupCommand.Text = "Impossible de lire le démarrage Windows : " + ex.Message;
            }
        }
        startup.Click += (_, _) => { App.ToggleStartup(); RefreshStartup(); };
        repairStartup.Click += (_, _) =>
        {
            try { App.UseCurrentVersionForStartup(); }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            { AppDialog.Show("Impossible de modifier le démarrage avec Windows.\n" + ex.Message, "Better Sticky Notes"); }
            RefreshStartup();
        };
        RefreshStartup();
        panel.Children.Add(new TextBlock { Text = "Pour changer de version, quittez d’abord l’application depuis son icône de notification, puis ouvrez le nouvel exécutable. Si une version tourne déjà, un autre lancement ouvre Mes notes dans cette instance.", Foreground = System.Windows.Media.Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 24) });
        void Add(string label, Action action)
        {
            var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 4, 0, 4) };
            button.Click += (_, _) => action(); panel.Children.Add(button);
        }
        panel.Children.Add(new TextBlock { Text = "Retrouver vos notes Windows", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "Importez vos notes Sticky Notes ou les post-it de OneNote. Les originales resteront intactes.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        panel.Children.Add(new TextBlock { Text = "1. Fermez Sticky Notes ou OneNote avant de commencer.\n2. Lancez l’import : une base locale sera proposée si elle est détectée.\n3. Sélectionnez les notes dans l’aperçu, puis confirmez.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        Add("Importer depuis Sticky Notes…", app.ImportStickyNotes);
        panel.Children.Add(new TextBlock { Text = "Le texte, les listes et les couleurs sont récupérés. Les images et la mise en forme enrichie ne sont pas importées. Après l’import, retrouvez la sélection dans Mes notes ; les post-it restent masqués.", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 8, 0, 12) });
        var help = new StackPanel { Margin = new Thickness(0, 10, 0, 6) };
        help.Children.Add(new TextBlock { Text = "Si aucun fichier n’est proposé, collez l’un des chemins ci-dessous dans la barre d’adresse du sélecteur de fichiers. Choisissez plum.sqlite pour Sticky Notes, ou un fichier notes.sdk_….db pour les post-it OneNote. En présence de plusieurs comptes, choisissez la base correspondant à vos notes.", TextWrapping = TextWrapping.Wrap });
        foreach (var location in new[] {
            "%LocalAppData%\\Packages\\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe\\LocalState",
            "%LocalAppData%\\Microsoft\\OneNote\\16.0\\Memory" })
            help.Children.Add(new TextBox { Text = location, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4), Padding = new Thickness(6) });
        help.Children.Add(new TextBlock { Text = "L’import lit les fichiers locaux .sqlite et .db. Les blocs-notes .one, les anciennes bases .snt et les notes uniquement dans le cloud ne sont pas pris en charge. Si le fichier reste verrouillé, quittez Sticky Notes / OneNote et réessayez. Pour importer une copie de la base, conservez aussi ses fichiers -wal et -shm à côté s’ils existent.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(new Expander { Header = "Où trouver mes notes ?", Content = help, Margin = new Thickness(0, 0, 0, 24) });
        panel.Children.Add(new TextBlock { Text = "Données locales", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        Add("Ouvrir le dossier des données", () => {
            try { Directory.CreateDirectory(NoteStore.Folder); Process.Start(new ProcessStartInfo(NoteStore.Folder) { UseShellExecute = true }); }
            catch (Exception ex) { AppDialog.Show(ex.Message, "Dossier des données"); }
        });
        Content = panel;
    }
}
