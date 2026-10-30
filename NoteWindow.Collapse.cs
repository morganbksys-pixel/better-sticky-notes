using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Shell;

namespace BetterStickyNotes;

public partial class NoteWindow
{
    private bool adjustingBounds;

    private void ToggleCollapsed(object sender, RoutedEventArgs e) => SetCollapsed(!note.Collapsed);

    public void SetCollapsed(bool collapsed)
    {
        if (note.Collapsed == collapsed) return;
        if (collapsed)
        {
            SaveBounds();
            CloseFind();
            EndTitleEdit();
        }
        note.Collapsed = collapsed;
        ApplyCollapsed();
        EnsureVisibleOnScreen();
        SaveBounds();
        app.Changed(listChanged: false);
    }

    private void ApplyCollapsed()
    {
        adjustingBounds = true;
        try
        {
            MinHeight = note.Collapsed ? 50 : 220;
            Height = note.Collapsed ? 50 : note.Height;
            ResizeMode = note.Collapsed ? ResizeMode.NoResize : ResizeMode.CanResize;
            WindowChrome.GetWindowChrome(this).ResizeBorderThickness = new Thickness(note.Collapsed ? 0 : 9);
            CornerGrip.Visibility = note.Collapsed ? Visibility.Collapsed : Visibility.Visible;
            HeaderSurface.CornerRadius = note.Collapsed ? new CornerRadius(13) : new CornerRadius(13, 13, 0, 0);
            CollapseButton.Content = NoteIcons.Glyph(note.Collapsed ? "\uE70D" : "\uE70E", 12);
            var label = note.Collapsed ? "Déplier la note (Ctrl+Maj+M)" : "Replier la note (Ctrl+Maj+M)";
            CollapseButton.ToolTip = label;
            AutomationProperties.SetName(CollapseButton, label);
            ApplyPreview();
        }
        finally { adjustingBounds = false; }
    }

    private bool HandleNoteToolsKey(KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.M && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (!e.IsRepeat) SetCollapsed(!note.Collapsed);
        }
        else if (e.Key == Key.F && modifiers == ModifierKeys.Control) OpenFind();
        else if (e.Key == Key.S && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (!e.IsRepeat) CaptureScreenClick(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible) CloseFind(restoreFocus: true);
        else if (FindBar.Visibility == Visibility.Visible &&
            ((e.Key == Key.Enter && FindBox.IsKeyboardFocusWithin) || e.Key == Key.F3) &&
            modifiers is ModifierKeys.None or ModifierKeys.Shift)
            MoveFind(modifiers == ModifierKeys.Shift ? -1 : 1);
        else return false;
        e.Handled = true;
        return true;
    }
}
