using System.Windows;
namespace BetterStickyNotes;

public partial class ImportWindow : Window
{
    public sealed class Row(Note note) { public Note Note { get; } = note; public bool Selected { get; set; } = true; }
    private readonly List<Row> rows;
    private readonly Func<List<Note>, Task<bool>> import;
    private bool saving;
    public ImportWindow(StickyImportResult result, Func<List<Note>, Task<bool>> import)
    {
        InitializeComponent(); WindowAppearance.Apply(this);
        this.import = import;
        rows = result.Notes.Select(n => new Row(n)).ToList();
        NoteList.ItemsSource = rows;
        Summary.Text = $"{rows.Count} note(s) à importer · {result.AlreadyImported} déjà importée(s) · {result.Deleted} supprimée(s) ignorée(s) · {result.Empty} vide(s) ignorée(s).";
        ImportButton.IsEnabled = rows.Count > 0;
        Closing += (_, e) => { if (saving) e.Cancel = true; };
    }
    private async void ImportClick(object sender, RoutedEventArgs e)
    {
        var selected = rows.Where(r => r.Selected).Select(r => r.Note).ToList();
        if (selected.Count == 0) { AppDialog.Show(this, "Sélectionnez au moins une note.", "Import Sticky Notes"); return; }
        saving = true; ImportButton.IsEnabled = false;
        bool success;
        try { success = await import(selected); }
        finally { saving = false; ImportButton.IsEnabled = true; }
        if (success) DialogResult = true;
    }
}
