using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BetterStickyNotes;

public partial class NoteWindow
{
    private Guid[] renderedCaptures = [];
    private Window? captureViewer;

    private async void CaptureScreenClick(object sender, RoutedEventArgs e)
    {
        if (markdownHelp != null) markdownHelp.IsOpen = false;
        try { await app.CaptureNoteScreenAsync(note, this); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or ExternalException or InvalidOperationException or ArgumentException)
        { AppDialog.Show(this, "La capture n’a pas pu être ajoutée.\n\n" + ex.Message, "Capture d’écran"); }
    }

    public void RefreshCaptures()
    {
        CaptureScreenButton.IsEnabled = !note.IsProtected;
        CaptureScreenButton.ToolTip = note.IsProtected ? "Les captures ne sont pas disponibles dans les notes protégées." : "Capturer cet écran (Ctrl+Maj+S)";
        CaptureStrip.Visibility = !note.Collapsed && !note.IsProtected && note.Captures.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CaptureSectionLabel.Text = note.Captures.Count.ToString();
        var action = note.CapturesCollapsed ? "Déplier les captures" : "Replier les captures vers le bas";
        var actionLabel = action + (note.Captures.Count == 1 ? " · 1 capture" : $" · {note.Captures.Count} captures");
        CaptureSectionButton.ToolTip = actionLabel;
        AutomationProperties.SetName(CaptureSectionButton, actionLabel);
        CaptureThumbnails.Visibility = note.CapturesCollapsed ? Visibility.Collapsed : Visibility.Visible;
        if (note.IsProtected || note.Captures.Count == 0) { ClearCaptureViews(); return; }
        if (note.Collapsed || note.CapturesCollapsed || renderedCaptures.SequenceEqual(note.Captures.Select(c => c.Id))) return;
        ClearCaptureViews();
        foreach (var capture in note.Captures)
        {
            var label = $"Capture du {capture.CreatedAtUtc.ToLocalTime():dd/MM/yyyy à HH:mm:ss} · {capture.Width} × {capture.Height}";
            var button = new Button { Width = 132, Height = 80, Margin = new Thickness(0, 3, 8, 3), Padding = new Thickness(2), Background = Brushes.Transparent, ToolTip = label };
            AutomationProperties.SetName(button, label);
            try
            {
                button.Content = new Image { Source = app.Store.LoadCapture(note, capture, thumbnail: true), Stretch = Stretch.Uniform };
                button.Click += (_, _) => OpenCapture(capture);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
            {
                button.Content = new TextBlock { Text = "Image indisponible", TextWrapping = TextWrapping.Wrap };
                button.ToolTip = label + "\n" + ex.Message;
            }
            var menu = new ContextMenu();
            var remove = new MenuItem { Header = "Supprimer la capture", Icon = NoteIcons.Glyph("\uE74D") };
            remove.Click += async (_, _) =>
            {
                await app.RemoveCapturesAsync(note, [capture]);
                RefreshCaptures();
            };
            menu.Items.Add(remove); button.ContextMenu = menu;
            CaptureItems.Children.Add(button);
        }
        renderedCaptures = note.Captures.Select(c => c.Id).ToArray();
        UpdateCaptureLayout();
    }

    private void ScrollCaptures(object sender, MouseWheelEventArgs e)
    {
        CaptureThumbnails.ScrollToHorizontalOffset(CaptureThumbnails.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void CaptureThumbnailKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End)) return;
        var buttons = CaptureItems.Children.OfType<Button>().ToArray();
        if (buttons.Length == 0) return;
        var current = Array.FindIndex(buttons, b => b.IsKeyboardFocusWithin);
        var next = e.Key switch { Key.Home => 0, Key.End => buttons.Length - 1, Key.Left => Math.Max(0, current - 1), _ => Math.Min(buttons.Length - 1, current + 1) };
        buttons[next].Focus(); buttons[next].BringIntoView(); e.Handled = true;
    }

    private void UpdateCaptureLayout()
    {
        // Keep a usable text viewport even with the editor/search tools visible.
        var surface = (FrameworkElement)CaptureStrip.Parent;
        var available = surface.ActualHeight - 48 - (FindBar.Visibility == Visibility.Visible ? FindBar.ActualHeight + 5 : 0)
            - (EditingBar.Visibility == Visibility.Visible ? EditingBar.ActualHeight + 7 : 0) - 43 - 90;
        var height = Math.Clamp(available, 32, 80);
        foreach (var button in CaptureItems.Children.OfType<Button>()) if (button.Height != height) button.Height = height;
        var hasText = !string.IsNullOrWhiteSpace(note.Text);
        Editor.VerticalScrollBarVisibility = hasText && Editor.ActualHeight >= 55 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
        Previewer.VerticalScrollBarVisibility = hasText && Previewer.ActualHeight >= 55 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
    }

    private void ToggleCaptureSection(object sender, RoutedEventArgs e) => SetCapturesCollapsed(!note.CapturesCollapsed);

    public void SetCapturesCollapsed(bool collapsed)
    {
        if (note.CapturesCollapsed == collapsed) return;
        note.CapturesCollapsed = collapsed;
        RefreshCaptures();
        app.Changed(listChanged: false);
    }

    private void OpenCapture(NoteCapture capture)
    {
        try
        {
            captureViewer?.Close();
            var viewer = new CaptureViewerWindow(app.Store, note, capture, this);
            viewer.Closed += (_, _) => { if (captureViewer == viewer) captureViewer = null; };
            captureViewer = viewer; viewer.Show();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or InvalidOperationException)
        { AppDialog.Show(this, "Impossible d’ouvrir cette capture.\n\n" + ex.Message, "Capture d’écran"); }
    }

    private void ClearCaptureViews()
    {
        captureViewer?.Close();
        CaptureItems.Children.Clear(); renderedCaptures = [];
    }
}
