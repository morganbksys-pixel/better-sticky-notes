using System.Windows;

namespace BetterStickyNotes;

public partial class NotesWindow
{
    private CaptureManagementView? capturePanel;

    public void ShowCaptures(Note note)
    {
        var selected = Selected();
        if (selected.Count != 1 || selected[0] != note || note.IsProtected) return;
        capturePanel ??= new CaptureManagementView(app, this, RefreshCaptureState);
        CapturePanelHost.Child = capturePanel;
        CapturesHost.Visibility = Visibility.Visible;
        capturePanel.ShowNote(note);
        ArrangeCapturePanel();
    }

    public void RefreshCaptureState()
    {
        refresh.Stop(); Refresh();
    }

    private void RefreshCapturePanel()
    {
        if (CapturesHost.Visibility != Visibility.Visible) return;
        var selected = Selected();
        if (selected.Count != 1 || selected[0].IsProtected) { CloseCaptures(); return; }
        capturePanel?.ShowNote(selected[0]);
    }

    private void CloseCapturesClick(object sender, RoutedEventArgs e) => CloseCaptures();

    private void CloseCaptures()
    {
        capturePanel?.Clear();
        CapturesHost.Visibility = Visibility.Collapsed;
        ArrangeCapturePanel();
    }

    private void CaptureWorkspaceSizeChanged(object sender, SizeChangedEventArgs e) => ArrangeCapturePanel();

    private void ArrangeCapturePanel()
    {
        if (NotesWorkspace == null || CapturesHost == null) return;
        var visible = CapturesHost.Visibility == Visibility.Visible;
        var side = visible && NotesWorkspace.ActualWidth >= 680;
        var overlay = visible && !side && NotesWorkspace.ActualHeight < 260;
        NotesListHost.Visibility = overlay ? Visibility.Collapsed : Visibility.Visible;
        NotesWorkspace.ColumnDefinitions[1].Width = new GridLength(side ? 300 : 0);
        NotesWorkspace.RowDefinitions[1].Height = new GridLength(visible && !side && !overlay ? Math.Min(220, Math.Max(0, NotesWorkspace.ActualHeight - 110)) : 0);
        System.Windows.Controls.Grid.SetColumn(CapturesHost, side ? 1 : 0);
        System.Windows.Controls.Grid.SetRow(CapturesHost, side || overlay ? 0 : 1);
        CapturesHost.Margin = side ? new Thickness(12, 0, 0, 0) : overlay ? new Thickness(0) : new Thickness(0, 12, 0, 0);
    }
}
