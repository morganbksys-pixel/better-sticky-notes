using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace BetterStickyNotes;

public static class NoteIcons
{
    public static TextBlock Glyph(string glyph) => new() { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
    public static FrameworkElement? Action(string label)
    {
        if (label.StartsWith("Afficher", StringComparison.Ordinal)) return Glyph("\uE890");
        if (label.StartsWith("Masquer", StringComparison.Ordinal)) return Glyph("\uED1A");
        if (label.StartsWith("Supprimer", StringComparison.Ordinal) || label.StartsWith("Vider", StringComparison.Ordinal) || label == "Mettre à la corbeille") return Glyph("\uE74D");
        if (label.StartsWith("Renommer", StringComparison.Ordinal)) return Glyph("\uE70F");
        return label switch
        {
            "Restaurer" => Glyph("\uE7A7"),
            "Tout sélectionner" => Glyph("\uE8B3"),
            "Désélectionner" => Glyph("\uE894"),
            "Réorganiser en cascade" => Glyph("\uE8B6"),
            "Couleur du dossier" => Glyph("\uE790"),
            "Purge automatique…" => Glyph("\uE713"),
            "Verrouiller toutes les notes protégées" => Glyph("\uE72E"),
            "Nouveau groupe…" => Glyph("\uE710"),
            _ => null
        };
    }
    public static Geometry Folder { get; } = Frozen("M2,4 L9,4 L12,7 L22,7 L22,20 L2,20 Z");
    public static Geometry Pin { get; } = Frozen("M8,2 L16,2 L15,4 L15,9 L18,13 L18,15 L6,15 L6,13 L9,9 L9,4 Z M12,15 L12,22");

    private static Geometry Frozen(string data) { var geometry = Geometry.Parse(data); geometry.Freeze(); return geometry; }
    public static Path FolderIcon(string color) => new()
    {
        Data = Folder, Fill = (Brush)new BrushConverter().ConvertFromString(color)!,
        Width = 16, Height = 14, Stretch = Stretch.Fill, VerticalAlignment = VerticalAlignment.Center
    };
}
