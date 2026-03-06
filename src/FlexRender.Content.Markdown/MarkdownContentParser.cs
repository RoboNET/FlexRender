using FlexRender.Abstractions;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace FlexRender.Content.Markdown;

/// <summary>
/// Parses Markdown-formatted text into FlexRender template elements using the Markdig library.
/// </summary>
public sealed class MarkdownContentParser : IContentParser
{
    /// <summary>
    /// Maximum allowed recursion depth for nested block/inline conversion to prevent
    /// <see cref="StackOverflowException"/> on deeply nested input.
    /// </summary>
    private const int MaxDepth = 64;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <inheritdoc />
    public string FormatName => "markdown";

    /// <inheritdoc />
    public IReadOnlyList<TemplateElement> Parse(string text, ContentParserContext context, IReadOnlyDictionary<string, object>? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text)) return [];

        var document = Markdig.Markdown.Parse(text, Pipeline);
        var elements = new List<TemplateElement>();

        foreach (var block in document)
        {
            var converted = ConvertBlock(block, depth: 0);
            if (converted is not null)
            {
                elements.Add(converted);
            }
        }

        return elements;
    }

    private static TemplateElement? ConvertBlock(Block block, int depth = 0)
    {
        if (depth > MaxDepth) return null;

        return block switch
        {
            HeadingBlock heading => ConvertHeading(heading, depth),
            ParagraphBlock paragraph => ConvertParagraph(paragraph, depth),
            ThematicBreakBlock => new SeparatorElement(),
            ListBlock list => ConvertList(list, depth),
            QuoteBlock quote => ConvertBlockquote(quote, depth),
            FencedCodeBlock fencedCode => ConvertCodeBlock(ExtractCodeBlockText(fencedCode)),
            CodeBlock code => ConvertCodeBlock(ExtractCodeBlockText(code)),
            _ => null
        };
    }

    private static TemplateElement ConvertHeading(HeadingBlock heading, int depth)
    {
        var size = heading.Level switch
        {
            1 => "2em",
            2 => "1.5em",
            3 => "1.2em",
            4 => "1em",
            _ => "0.9em"
        };

        // A heading may contain mixed inline formatting (bold, italic, etc.)
        // but the entire heading is bold by default.
        var inlineElements = CollectInlines(heading.Inline, isBold: true, isItalic: false, depth: depth + 1);

        if (inlineElements.Count == 1 && inlineElements[0] is TextElement singleText)
        {
            singleText.Size = size;
            return singleText;
        }

        // Multiple inline segments: wrap in a flex row so they appear on the same line
        var container = new FlexElement { Direction = FlexDirection.Row };
        foreach (var element in inlineElements)
        {
            if (element is TextElement te)
            {
                te.Size = size;
            }

            container.AddChild(element);
        }

        return container;
    }

    private static TemplateElement ConvertParagraph(ParagraphBlock paragraph, int depth)
    {
        var inlineElements = CollectInlines(paragraph.Inline, isBold: false, isItalic: false, depth: depth + 1);

        if (inlineElements.Count == 1)
        {
            return inlineElements[0];
        }

        // Multiple inline segments with different formatting: wrap in a flex row
        var container = new FlexElement { Direction = FlexDirection.Row };
        foreach (var element in inlineElements)
        {
            container.AddChild(element);
        }

        return container;
    }

    private static FlexElement ConvertList(ListBlock list, int depth)
    {
        var container = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "4"
        };

        var index = 1;
        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem) continue;

            var prefix = list.IsOrdered ? $"{index}. " : "\u2022 ";
            var itemElement = ConvertListItem(listItem, prefix, depth);
            container.AddChild(itemElement);
            index++;
        }

        return container;
    }

    private static TemplateElement ConvertListItem(ListItemBlock listItem, string prefix, int depth)
    {
        // Collect all inline content from the list item's paragraphs
        var allInlines = new List<TemplateElement>();
        foreach (var subBlock in listItem)
        {
            if (subBlock is ParagraphBlock paragraph)
            {
                var inlines = CollectInlines(paragraph.Inline, isBold: false, isItalic: false, depth: depth + 1);
                allInlines.AddRange(inlines);
            }
            else
            {
                var converted = ConvertBlock(subBlock, depth + 1);
                if (converted is not null)
                {
                    allInlines.Add(converted);
                }
            }
        }

        // Prepend the bullet/number prefix to the first text element
        if (allInlines.Count > 0 && allInlines[0] is TextElement firstText)
        {
            firstText.Content = prefix + firstText.Content.Value;
        }
        else
        {
            allInlines.Insert(0, new TextElement { Content = prefix });
        }

        if (allInlines.Count == 1)
        {
            return allInlines[0];
        }

        var row = new FlexElement { Direction = FlexDirection.Row };
        foreach (var element in allInlines)
        {
            row.AddChild(element);
        }

        return row;
    }

    private static FlexElement ConvertBlockquote(QuoteBlock quote, int depth)
    {
        var container = new FlexElement
        {
            Padding = "0 0 0 12",
            Background = "#f5f5f5",
            Direction = FlexDirection.Column
        };

        foreach (var subBlock in quote)
        {
            var converted = ConvertBlock(subBlock, depth + 1);
            if (converted is not null)
            {
                container.AddChild(converted);
            }
        }

        return container;
    }

    private static FlexElement ConvertCodeBlock(string codeText)
    {
        var container = new FlexElement
        {
            Background = "#f0f0f0",
            Padding = "8"
        };

        container.AddChild(new TextElement { Content = codeText });

        return container;
    }

    private static string ExtractCodeBlockText(LeafBlock codeBlock)
    {
        var lines = codeBlock.Lines;
        var builder = new System.Text.StringBuilder();

        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            var line = lines.Lines[i];
            builder.Append(line.Slice.AsSpan());
        }

        return builder.ToString();
    }

    /// <summary>
    /// Walks the inline tree and produces a list of template elements, accumulating
    /// contiguous text runs with the same formatting into single TextElements.
    /// </summary>
    /// <param name="container">The container inline to walk.</param>
    /// <param name="isBold">Whether text is currently bold.</param>
    /// <param name="isItalic">Whether text is currently italic.</param>
    /// <param name="depth">Current recursion depth for stack overflow protection.</param>
    /// <returns>A list of template elements representing the inline content.</returns>
    private static List<TemplateElement> CollectInlines(
        ContainerInline? container,
        bool isBold,
        bool isItalic,
        int depth = 0)
    {
        var results = new List<TemplateElement>();
        if (container is null || depth > MaxDepth) return results;

        // Track current formatting state for text accumulation
        var currentText = new System.Text.StringBuilder();
        var currentBold = isBold;
        var currentItalic = isItalic;
        var currentIsCode = false;

        void FlushText()
        {
            if (currentText.Length == 0) return;

            var element = new TextElement { Content = currentText.ToString() };

            if (currentBold)
                element.FontWeight = Parsing.Ast.FontWeight.Bold;
            if (currentItalic)
                element.FontStyle = Parsing.Ast.FontStyle.Italic;
            if (currentIsCode)
                element.Background = "#f0f0f0";

            results.Add(element);
            currentText.Clear();
        }

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                {
                    // Check if formatting state matches current accumulation
                    if (isBold != currentBold || isItalic != currentItalic || currentIsCode)
                    {
                        FlushText();
                        currentBold = isBold;
                        currentItalic = isItalic;
                        currentIsCode = false;
                    }

                    currentText.Append(literal.Content.AsSpan());
                    break;
                }

                case EmphasisInline emphasis:
                {
                    FlushText();

                    var childBold = emphasis.DelimiterCount >= 2 ? true : isBold;
                    var childItalic = emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3 ? true : isItalic;

                    var childElements = CollectInlines(emphasis, childBold, childItalic, depth + 1);
                    results.AddRange(childElements);

                    // Reset state after emphasis children
                    currentBold = isBold;
                    currentItalic = isItalic;
                    currentIsCode = false;
                    break;
                }

                case CodeInline code:
                {
                    FlushText();
                    currentBold = isBold;
                    currentItalic = isItalic;
                    currentIsCode = true;

                    currentText.Append(code.Content);
                    FlushText();
                    currentIsCode = false;
                    break;
                }

                case LinkInline link:
                {
                    FlushText();

                    if (link.IsImage)
                    {
                        results.Add(new ImageElement { Src = link.Url ?? "" });
                    }
                    else
                    {
                        // For regular links, render the link text as plain text
                        var linkInlines = CollectInlines(link, isBold, isItalic, depth + 1);
                        results.AddRange(linkInlines);
                    }

                    currentBold = isBold;
                    currentItalic = isItalic;
                    currentIsCode = false;
                    break;
                }

                case LineBreakInline:
                {
                    currentText.Append('\n');
                    break;
                }

                case ContainerInline nestedContainer:
                {
                    FlushText();
                    var nested = CollectInlines(nestedContainer, isBold, isItalic, depth + 1);
                    results.AddRange(nested);
                    currentBold = isBold;
                    currentItalic = isItalic;
                    currentIsCode = false;
                    break;
                }
            }
        }

        FlushText();
        return results;
    }
}
