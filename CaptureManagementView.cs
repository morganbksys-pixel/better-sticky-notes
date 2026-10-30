using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace BetterStickyNotes;

public sealed class CaptureManagementView : ScrollViewer
{
    private readonly App app;
    private readonly Window owner;
    private readonly Action changed;
    private Note? note;
    private string? renderedState;
    private CaptureViewerWindow? viewer;

    public Note? CurrentNote => note;

    public CaptureManagementView(App app, Window owner, Action changed)
    {
        this.app = app; this.owner = owner; this.changed = changed;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    public void ShowNote(Note selected)
    {
        if (note != selected) { Clear(); note = selected; }
        Refresh();
    }

    public void Refresh()
    {
        if (note == null || note.IsProtected) { Clear(); return; }
        var state = $"{note.Id}|{note.DisplayTitle}|{note.Deleted}|{string.Join(',', note.Captures.Select(c => c.Id))}";
        if (state == renderedState) return;
        renderedState = state;
        viewer?.Close(); viewer = null;
        var target = note;
        var panel = new StackPanel { Margin = new Thickness(0, 8, 8, 0) };
        panel.Children.Add(new TextBlock { Text = target.DisplayTitle, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = target.Captures.Count == 1 ? "1 capture" : $"{target.Captures.Count} captures", Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 10) });
        if (target.Deleted)
            panel.Children.Add(new TextBlock { Text = "Dans la corbeille, vous pouvez consulter et enregistrer les images. Restaurez la note pour modifier ses captures.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        if (target.Captures.Count == 0)
            panel.Children.Add(new TextBlock { Text = target.Deleted ? "Cette note ne contient aucune capture." : "Aucune capture dans cette note. Pour en ajouter, affichez le post-it puis choisissez ⋯ → Capturer cet écran.", TextWrapping = TextWrapping.Wrap });
        foreach (var capture in target.Captures)
        {
            var item = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var preview = new Button { Height = 115, Padding = new Thickness(3), HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = "Ouvrir la capture" };
            try
            {
                preview.Content = new Image { Source = app.Store.LoadCapture(target, capture, thumbnail: true), Stretch = Stretch.Uniform };
                preview.Click += (_, _) => Open(target, capture);
            }
            catch (Exception ex) when (ImageError(ex))
            {
                preview.Content = new TextBlock { Text = "Image indisponible", TextWrapping = TextWrapping.Wrap };
                preview.IsEnabled = false;
            }
            item.Children.Add(preview);
            item.Children.Add(new TextBlock { Text = $"{capture.CreatedAtUtc.ToLocalTime():dd/MM/yyyy à HH:mm:ss}\n{capture.Width} × {capture.Height} pixels", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(2, 6, 0, 4) });
            var actions = new WrapPanel();
            var export = new Button { Content = "Enregistrer sous…", Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 2, 4, 2) };
            export.Click += (_, _) => Export(target, capture);
            actions.Children.Add(export);
            if (!target.Deleted)
            {
                var remove = new Button { Content = "Supprimer", Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 2, 0, 2), ToolTip = "Supprimer uniquement cette capture" };
                remove.Click += async (_, _) => { await app.RemoveCapturesAsync(target, [capture]); Refresh(); changed(); };
                actions.Children.Add(remove);
            }
            item.Children.Add(actions); panel.Children.Add(item);
        }
        if (!target.Deleted && target.Captures.Count > 0)
        {
            var removeAll = new Button { Content = "Supprimer toutes les captures…", Padding = new Thickness(8, 6, 8, 6), HorizontalAlignment = HorizontalAlignment.Left };
            removeAll.Click += async (_, _) =>
            {
                var captures = target.Captures.ToArray();
                if (captures.Length == 0) return;
                var question = captures.Length == 1 ? $"Supprimer la capture de « {target.DisplayTitle} » ?" : $"Supprimer les {captures.Length} captures de « {target.DisplayTitle} » ?";
                if (AppDialog.Show(owner, question + " Le texte de la note sera conservé.",
                    "Supprimer les captures", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No,
                    destructiveAction: captures.Length == 1 ? "Supprimer la capture" : $"Supprimer les {captures.Length} captures") != MessageBoxResult.Yes) return;
                await app.RemoveCapturesAsync(target, captures); Refresh(); changed();
            };
            panel.Children.Add(removeAll);
        }
        Content = panel;
    }

    private void Open(Note target, NoteCapture capture)
    {
        try
        {
            viewer?.Close();
            var window = new CaptureViewerWindow(app.Store, target, capture, owner);
            window.Closed += (_, _) => { if (viewer == window) viewer = null; };
            viewer = window; window.Show();
        }
        catch (Exception ex) when (ImageError(ex))
        { AppDialog.Show(owner, "Impossible d’ouvrir cette capture.\n\n" + ex.Message, "Capture d’écran"); }
    }

    private void Export(Note target, NoteCapture capture)
    {
        var picker = new SaveFileDialog { Title = "Enregistrer la capture", FileName = $"Capture-{capture.CreatedAtUtc.ToLocalTime():yyyyMMdd-HHmmss}.png", Filter = "Image PNG (*.png)|*.png", DefaultExt = ".png", AddExtension = true, OverwritePrompt = true };
        if (picker.ShowDialog(owner) != true) return;
        try { app.Store.ExportCapture(target, capture, picker.FileName); }
        catch (Exception ex) when (ImageError(ex))
        { AppDialog.Show(owner, "Impossible d’enregistrer cette capture.\n\n" + ex.Message, "Capture d’écran"); }
    }

    private static bool ImageError(Exception ex) => ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or InvalidOperationException;

    public void Clear()
    {
        viewer?.Close(); viewer = null;
        note = null; renderedState = null; Content = null;
    }
}
