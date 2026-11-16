using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace BetterStickyNotes;

internal static class MarkdownExcerpt
{
    private const int MaxLength = 180;
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseTaskLists().UseEmphasisExtras().Build();

    internal static string Create(string markdown)
    {
        var text = new StringBuilder(MaxLength + 1);
        var pendingSpace = false;
        AddBlocks(Markdown.Parse(markdown, Pipeline));
        if (text.Length <= MaxLength) return text.ToString().TrimEnd();
        var length = MaxLength - 1;
        if (char.IsHighSurrogate(text[length - 1])) length--;
        return text.ToString(0, length).TrimEnd() + "…";

        void Append(string value)
        {
            foreach (var character in value)
            {
                if (text.Length > MaxLength) break;
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = text.Length > 0;
                }
                else
                {
                    if (pendingSpace) text.Append(' ');
                    pendingSpace = false;
                    text.Append(character);
                }
            }
        }

        void AddBlocks(ContainerBlock blocks)
        {
            foreach (var block in blocks)
            {
                if (text.Length > MaxLength) break;
                switch (block)
                {
                    case ContainerBlock container: AddBlocks(container); break;
                    case CodeBlock code: Append(code.Lines.ToString()); break;
                    case LeafBlock leaf when leaf.Inline is { } inline: AddInlines(inline); break;
                    case HtmlBlock html: Append(html.Lines.ToString()); break;
                }
                Append(" ");
            }
        }

        void AddInlines(ContainerInline inlines)
        {
            foreach (var inline in inlines)
            {
                if (text.Length > MaxLength) break;
                switch (inline)
                {
                    case LiteralInline literal: Append(literal.Content.ToString()); break;
                    case CodeInline code: Append(code.Content); break;
                    case LineBreakInline: Append(" "); break;
                    case AutolinkInline link: Append(link.Url); break;
                    case HtmlInline html: Append(html.Tag); break;
                    case ContainerInline container: AddInlines(container); break;
                }
            }
        }
    }
}
