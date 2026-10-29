using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace BetterStickyNotes;

public static class NoteIcons
{
    public static TextBlock Glyph(string glyph, double size = 14) => new() { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
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
    public static TextBlock FolderIcon(string color) => new()
    {
        Text = "\uE8B7", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16,
        Foreground = (Brush)new BrushConverter().ConvertFromString(color)!,
        LayoutTransform = new RotateTransform(-90), VerticalAlignment = VerticalAlignment.Center
    };
}
