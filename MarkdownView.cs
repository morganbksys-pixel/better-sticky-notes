using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.TaskLists;

namespace BetterStickyNotes;

public static class MarkdownView
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseTaskLists().UseEmphasisExtras().UsePreciseSourceLocation().Build();
    public static FlowDocument Render(string markdown, Action<int, bool> toggleTask)
    {
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 14, PagePadding = new Thickness(6), Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(48, 45, 35)) };
        var ast = Markdown.Parse(markdown, Pipeline);
        AddBlocks(document.Blocks, ast, markdown, toggleTask);
        return document;
    }
    private static void AddBlocks(BlockCollection target, ContainerBlock source, string text, Action<int, bool> toggle)
    {
        foreach (var block in source)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    var h = new Paragraph { FontSize = heading.Level switch { 1 => 25, 2 => 21, _ => 17 }, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 7) };
                    AddInlines(h.Inlines, heading.Inline, text, toggle); target.Add(h); break;
                case ParagraphBlock paragraph:
                    var p = new Paragraph { Margin = new Thickness(0, 3, 0, 8) };
                    AddInlines(p.Inlines, paragraph.Inline, text, toggle); target.Add(p); break;
                case ListBlock list:
                    var output = new System.Windows.Documents.List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 2, 0, 6), Padding = new Thickness(20, 0, 0, 0) };
                    if (list.IsOrdered && int.TryParse(list.OrderedStart, out var start) && start > 0) output.StartIndex = start;
                    foreach (var child in list.OfType<ListItemBlock>())
                    {
                        var item = new ListItem(); AddBlocks(item.Blocks, child, text, toggle); output.ListItems.Add(item);
                    }
                    target.Add(output); break;
                case CodeBlock code:
                    target.Add(new Paragraph(new Run(code.Lines.ToString())) { FontFamily = new FontFamily("Consolas"), FontSize = 12, Background = new SolidColorBrush(Color.FromArgb(18, 0, 0, 0)), Padding = new Thickness(8), Margin = new Thickness(0, 5, 0, 8) }); break;
                case QuoteBlock quote:
                    var section = new Section { BorderBrush = Brushes.DarkGray, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 0, 0, 0), Margin = new Thickness(0, 6, 0, 6) };
                    AddBlocks(section.Blocks, quote, text, toggle); target.Add(section); break;
                case ThematicBreakBlock:
                    target.Add(new Paragraph { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 8, 0, 8) }); break;
                case HtmlBlock html:
                    target.Add(new Paragraph(new Run(html.Lines.ToString()))); break;
            }
        }
    }
    private static void AddInlines(InlineCollection target, ContainerInline? source, string text, Action<int, bool> toggle)
    {
        if (source is null) return;
        foreach (var inline in source)
        {
            switch (inline)
            {
                case LiteralInline literal: target.Add(new Run(literal.Content.ToString())); break;
                case LineBreakInline: target.Add(new LineBreak()); break;
                case CodeInline code: target.Add(new Run(code.Content) { FontFamily = new FontFamily("Consolas"), Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)) }); break;
                case TaskList task:
                    var check = new CheckBox { IsChecked = task.Checked, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0), ToolTip = "Cocher / décocher" };
                    var offset = task.Span.Start + 1;
                    // Use the source span so identical tasks and nested lists update the correct character.
                    if (offset > 0 && offset + 1 < text.Length && text[offset - 1] == '[' && text[offset + 1] == ']')
                    {
                        check.Checked += (_, _) => toggle(offset, true); check.Unchecked += (_, _) => toggle(offset, false);
                    }
                    else check.IsEnabled = false;
                    target.Add(new InlineUIContainer(check)); break;
                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterChar == '~') span.TextDecorations = TextDecorations.Strikethrough;
                    else if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold;
                    else span.FontStyle = FontStyles.Italic;
                    AddInlines(span.Inlines, emphasis, text, toggle); target.Add(span); break;
                case LinkInline link:
                    if (!link.IsImage && Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https" or "mailto"))
                    {
                        var anchor = new Hyperlink { NavigateUri = uri };
                        AddInlines(anchor.Inlines, link, text, toggle);
                        anchor.RequestNavigate += (_, args) => { try { Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true }); } catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Impossible d’ouvrir le lien"); } args.Handled = true; };
                        target.Add(anchor);
                    }
                    else AddInlines(target, link, text, toggle);
                    break;
                case HtmlInline html: target.Add(new Run(html.Tag)); break;
                case AutolinkInline auto:
                    target.Add(new Run(auto.Url)); break;
                case ContainerInline container: AddInlines(target, container, text, toggle); break;
            }
        }
    }
}
