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
        try { startup.IsChecked = App.IsStartupEnabled(); }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { startup.IsEnabled = false; startup.ToolTip = ex.Message; }
        startup.Click += (_, _) => { App.ToggleStartup(); try { startup.IsChecked = App.IsStartupEnabled(); } catch { startup.IsEnabled = false; } };
        panel.Children.Add(startup);
        panel.Children.Add(new TextBlock { Text = "Le démarrage Windows reste discret, sans ouvrir de fenêtre.", Foreground = System.Windows.Media.Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 24) });
        void Add(string label, Action action)
        {
            var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 4, 0, 4) };
            button.Click += (_, _) => action(); panel.Children.Add(button);
        }
        Add("Importer depuis Sticky Notes…", app.ImportStickyNotes);
        Add("Ouvrir le dossier des données", () => {
            try { Directory.CreateDirectory(NoteStore.Folder); Process.Start(new ProcessStartInfo(NoteStore.Folder) { UseShellExecute = true }); }
            catch (Exception ex) { AppDialog.Show(ex.Message, "Dossier des données"); }
        });
        panel.Children.Add(new TextBlock { Text = "À propos", FontSize = 18, Margin = new Thickness(0, 28, 0, 10) });
        panel.Children.Add(new TextBlock { Text = AppInfo.DisplayName, Margin = new Thickness(0, 0, 0, 10) });
        panel.Children.Add(new TextBlock { Text = "Exécutable actif", Foreground = System.Windows.Media.Brushes.DimGray });
        panel.Children.Add(new TextBox { Text = AppInfo.ExecutablePath, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent, Margin = new Thickness(0, 6, 0, 0) });
        Content = panel;
    }
}
