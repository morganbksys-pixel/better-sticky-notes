using System.Windows.Controls;
using System.Text.RegularExpressions;

namespace BetterStickyNotes;

public enum MarkdownTool { Bold, Italic, List, Checklist, Code, Link }

public static class MarkdownEditing
{
    public static void Apply(TextBox editor, MarkdownTool tool)
    {
        var start = editor.SelectionStart;
        var length = editor.SelectionLength;
        var selected = editor.SelectedText;
        var text = editor.Text;
        string replacement;
        int selectOffset, selectLength;
        if (tool is MarkdownTool.List or MarkdownTool.Checklist)
        {
            var first = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
            var end = start + length;
            // A selection ending at the start of the next line excludes that line.
            if (length > 0 && end > first && text[end - 1] == '\n') end--;
            var last = text.IndexOf('\n', end);
            if (last < 0) last = text.Length;
            if (last > first && text[last - 1] == '\r') last--;
            var content = text[first..last];
            var prefix = tool == MarkdownTool.List ? "- " : "- [ ] ";
            replacement = Regex.Replace(content, @"(?m)^([ \t]*)(?:[-+*] (?:\[[ xX]\] )?|\d+\. )?", "$1" + prefix);
            if (content.Length == 0) replacement = prefix + (tool == MarkdownTool.List ? "élément" : "À faire");
            start = first; length = last - first;
            selectOffset = content.Length == 0 ? prefix.Length : 0;
            selectLength = replacement.Length - selectOffset;
        }
        else
        {
            var body = selected.Length > 0 ? selected : tool switch { MarkdownTool.Code => "variable", MarkdownTool.Link => "texte du lien", MarkdownTool.Bold => "texte en gras", _ => "texte en italique" };
            var prefix = tool switch { MarkdownTool.Bold => "**", MarkdownTool.Italic => "*", MarkdownTool.Link => "[", _ => "`" };
            var suffix = tool == MarkdownTool.Link ? "](https://example.com)" : prefix;
            if (tool == MarkdownTool.Code)
            {
                var maxTicks = Regex.Matches(body, "`+").Select(m => m.Length).DefaultIfEmpty(0).Max();
                if (body.Contains('\n') || body.Contains('\r'))
                {
                    var fence = new string('`', Math.Max(3, maxTicks + 1));
                    prefix = (start > 0 && text[start - 1] != '\n' ? "\n" : "") + fence + "\n";
                    suffix = (body.EndsWith('\n') ? "" : "\n") + fence + (start + length < text.Length && text[start + length] != '\n' ? "\n" : "");
                }
                else
                {
                    var fence = new string('`', Math.Max(1, maxTicks + 1));
                    var pad = body.StartsWith('`') || body.EndsWith('`') ? " " : "";
                    prefix = fence + pad; suffix = pad + fence;
                }
            }
            replacement = prefix + body + suffix;
            selectOffset = prefix.Length; selectLength = body.Length;
            if (tool == MarkdownTool.Link && selected.Length > 0) { selectOffset = prefix.Length + body.Length + 2; selectLength = "https://example.com".Length; }
        }
        editor.BeginChange();
        try { editor.Select(start, length); editor.SelectedText = replacement; editor.Select(start + selectOffset, selectLength); }
        finally { editor.EndChange(); }
        editor.Focus();
    }
}
