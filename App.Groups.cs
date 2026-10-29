using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace BetterStickyNotes;

public partial class App
{
    public NoteGroup CreateGroup(string name)
    {
        name = ValidateGroupName(name);
        var group = new NoteGroup(Guid.NewGuid(), name);
        Store.Groups.Add(group); Changed();
        return group;
    }

    public void RenameGroup(Guid id, string name)
    {
        var index = Store.Groups.FindIndex(g => g.Id == id);
        if (index < 0) throw new ArgumentException("Ce groupe n’existe plus.");
        name = ValidateGroupName(name, id);
        Store.Groups[index] = Store.Groups[index] with { Name = name }; Changed();
    }

    public void SetGroupColor(Guid id, string color)
    {
        if (!NoteGroup.IsValidColor(color)) throw new ArgumentException("Couleur de groupe invalide.");
        var index = Store.Groups.FindIndex(g => g.Id == id);
        if (index < 0) throw new ArgumentException("Ce groupe n’existe plus.");
        Store.Groups[index] = Store.Groups[index] with { Color = color.ToUpperInvariant() }; Changed();
    }

    private string ValidateGroupName(string name, Guid? except = null)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80) throw new ArgumentException("Saisissez un nom de 1 à 80 caractères.");
        if (new[] { "Toutes les notes", "Sans groupe" }.Contains(name, StringComparer.CurrentCultureIgnoreCase) ||
            Store.Groups.Any(g => g.Id != except && string.Equals(g.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            throw new ArgumentException("Ce nom de groupe est déjà utilisé.");
        return name;
    }

    public void DeleteGroup(Guid id)
    {
        if (Store.Groups.RemoveAll(g => g.Id == id) == 0) return;
        foreach (var note in Store.Notes.Where(n => n.GroupId == id)) note.GroupId = null;
        Changed();
    }

    public void AssignGroup(IEnumerable<Note> notes, Guid? id)
    {
        if (id != null && !Store.Groups.Any(g => g.Id == id)) throw new ArgumentException("Ce groupe n’existe plus.");
        foreach (var note in notes.Where(Store.Notes.Contains)) note.GroupId = id;
        Changed();
    }

    public MenuItem BuildGroupMenu(IEnumerable<Note> notes)
    {
        var selection = notes.ToList();
        var menu = new MenuItem { Header = "Groupe", Icon = NoteIcons.FolderIcon("#73777E") };
        void Add(Guid? id, string name, string? color = null)
        {
            var label = new TextBlock { Text = name, Foreground = Brushes.Black };
            object header = label;
            if (color != null)
            {
                var baseColor = (Color)ColorConverter.ConvertFromString(color);
                // Blend with white to keep black labels readable for every stored group color.
                byte Tint(byte channel) => (byte)(255 * 0.78 + channel * 0.22);
                header = new Border { Background = new SolidColorBrush(Color.FromRgb(Tint(baseColor.R), Tint(baseColor.G), Tint(baseColor.B))),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(7, 3, 7, 3), Child = label };
            }
            var item = new MenuItem { Header = header, Icon = color != null ? NoteIcons.FolderIcon(color) : null, IsCheckable = true,
                IsChecked = selection.Count > 0 && selection.All(n => n.GroupId == id) };
            item.Click += (_, _) => AssignGroup(selection, id);
            menu.Items.Add(item);
        }
        Add(null, "Sans groupe");
        foreach (var group in Store.Groups.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)) Add(group.Id, group.Name, group.Color);
        return menu;
    }
}
