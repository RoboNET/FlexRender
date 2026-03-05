using System.Globalization;
using FlexRender.Abstractions;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using HtmlAgilityPack;

namespace FlexRender.Content.Html;

/// <summary>
/// Parses HTML-formatted text into FlexRender template elements using HtmlAgilityPack.
/// </summary>
public sealed class HtmlContentParser : IContentParser
{
    /// <summary>
    /// Maximum allowed recursion depth for nested node processing to prevent
    /// <see cref="StackOverflowException"/> on deeply nested input.
    /// </summary>
    private const int MaxDepth = 64;

    private static readonly HashSet<string> IgnoredTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "head", "meta", "link", "title"
    };

    private static readonly HashSet<string> PassthroughTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "body"
    };

    private static readonly Dictionary<string, string> HeadingSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["h1"] = "2em",
        ["h2"] = "1.5em",
        ["h3"] = "1.2em",
        ["h4"] = "1em",
        ["h5"] = "0.9em",
        ["h6"] = "0.8em"
    };

    /// <inheritdoc />
    public string FormatName => "html";

    /// <inheritdoc />
    public IReadOnlyList<TemplateElement> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text)) return [];

        var doc = new HtmlDocument();
        doc.LoadHtml(text);

        var context = new InlineContext();
        var elements = new List<TemplateElement>();
        ProcessNodes(doc.DocumentNode.ChildNodes, elements, context, depth: 0);
        return elements;
    }

    private static void ProcessNodes(
        HtmlNodeCollection nodes,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        if (depth > MaxDepth) return;

        foreach (var node in nodes)
        {
            ProcessNode(node, results, context, depth);
        }
    }

    private static void ProcessNode(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                ProcessTextNode(node, results, context);
                break;

            case HtmlNodeType.Element:
                ProcessElementNode(node, results, context, depth);
                break;
        }
    }

    private static void ProcessTextNode(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context)
    {
        var text = HtmlEntity.DeEntitize(node.InnerText);
        if (string.IsNullOrWhiteSpace(text)) return;

        results.Add(CreateTextElement(text, context));
    }

    private static void ProcessElementNode(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        var tag = node.Name.ToLowerInvariant();

        if (IgnoredTags.Contains(tag)) return;

        if (PassthroughTags.Contains(tag))
        {
            ProcessNodes(node.ChildNodes, results, context, depth);
            return;
        }

        switch (tag)
        {
            case "br":
                results.Add(new TextElement { Content = "\n" });
                break;

            case "hr":
                results.Add(new SeparatorElement());
                break;

            case "img":
                ProcessImage(node, results);
                break;

            case "b" or "strong":
                ProcessInlineFormatting(node, results, context with { Weight = FontWeight.Bold }, depth);
                break;

            case "i" or "em":
                ProcessInlineFormatting(node, results, context with { Style = Parsing.Ast.FontStyle.Italic }, depth);
                break;

            case "code" when !IsInsidePre(node):
                ProcessInlineFormatting(node, results, context with { Background = "#f0f0f0" }, depth);
                break;

            case "span":
                var spanContext = ApplyInlineStyles(node, context);
                ProcessInlineFormatting(node, results, spanContext, depth);
                break;

            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                ProcessHeading(node, results, context, tag, depth);
                break;

            case "p":
                ProcessParagraph(node, results, context, depth);
                break;

            case "ul":
                ProcessUnorderedList(node, results, context, depth);
                break;

            case "ol":
                ProcessOrderedList(node, results, context, depth);
                break;

            case "li":
                // li outside a list context: treat as paragraph
                ProcessParagraph(node, results, context, depth);
                break;

            case "blockquote":
                ProcessBlockquote(node, results, context, depth);
                break;

            case "pre":
                ProcessPreformatted(node, results, context);
                break;

            case "code" when IsInsidePre(node):
                // code inside pre: just process children as-is
                ProcessNodes(node.ChildNodes, results, context, depth + 1);
                break;

            case "div" or "section" or "article" or "nav" or "header" or "footer" or "main" or "aside":
                ProcessContainer(node, results, context, depth);
                break;

            case "a":
                // Treat links as styled inline text
                ProcessInlineFormatting(node, results, context with { Color = "#0066cc" }, depth);
                break;

            default:
                // Unknown tags: process children
                ProcessNodes(node.ChildNodes, results, context, depth + 1);
                break;
        }
    }

    private static void ProcessImage(HtmlNode node, List<TemplateElement> results)
    {
        var src = node.GetAttributeValue("src", "");
        if (string.IsNullOrWhiteSpace(src)) return;

        var img = new ImageElement { Src = src };

        var widthAttr = node.GetAttributeValue("width", "");
        if (int.TryParse(widthAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w))
            img.ImageWidth = w;

        var heightAttr = node.GetAttributeValue("height", "");
        if (int.TryParse(heightAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
            img.ImageHeight = h;

        results.Add(img);
    }

    private static void ProcessInlineFormatting(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        context = ApplyInlineStyles(node, context);
        ProcessNodes(node.ChildNodes, results, context, depth + 1);
    }

    private static void ProcessHeading(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        string tag,
        int depth)
    {
        var size = HeadingSizes.GetValueOrDefault(tag, "1em");
        var headingContext = ApplyInlineStyles(node, context with { Weight = FontWeight.Bold, Size = size });

        var children = CollectInlineChildren(node, headingContext, depth);
        if (children.Count == 1)
        {
            results.Add(children[0]);
        }
        else if (children.Count > 1)
        {
            var container = new FlexElement { Direction = FlexDirection.Row };
            foreach (var child in children)
                container.AddChild(child);
            results.Add(container);
        }
    }

    private static void ProcessParagraph(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        context = ApplyInlineStyles(node, context);
        var children = CollectInlineChildren(node, context, depth);

        if (children.Count == 1)
        {
            results.Add(children[0]);
        }
        else if (children.Count > 1)
        {
            var container = new FlexElement { Direction = FlexDirection.Row };
            foreach (var child in children)
                container.AddChild(child);
            results.Add(container);
        }
    }

    private static void ProcessUnorderedList(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        context = ApplyInlineStyles(node, context);
        var list = new FlexElement { Direction = FlexDirection.Column, Gap = "4" };

        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element) continue;
            if (!child.Name.Equals("li", StringComparison.OrdinalIgnoreCase)) continue;

            var itemElements = CollectInlineChildren(child, context, depth);
            if (itemElements.Count == 0) continue;

            // Prepend bullet to the first text element
            if (itemElements[0] is TextElement firstText)
            {
                firstText.Content = "\u2022 " + firstText.Content.Value;
                if (itemElements.Count == 1)
                {
                    list.AddChild(firstText);
                }
                else
                {
                    var row = new FlexElement { Direction = FlexDirection.Row };
                    foreach (var item in itemElements)
                        row.AddChild(item);
                    list.AddChild(row);
                }
            }
            else
            {
                var row = new FlexElement { Direction = FlexDirection.Row };
                row.AddChild(new TextElement { Content = "\u2022 " });
                foreach (var item in itemElements)
                    row.AddChild(item);
                list.AddChild(row);
            }
        }

        if (list.Children.Count > 0)
            results.Add(list);
    }

    private static void ProcessOrderedList(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        context = ApplyInlineStyles(node, context);
        var list = new FlexElement { Direction = FlexDirection.Column, Gap = "4" };
        var index = 1;

        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element) continue;
            if (!child.Name.Equals("li", StringComparison.OrdinalIgnoreCase)) continue;

            var prefix = $"{index}. ";
            var itemElements = CollectInlineChildren(child, context, depth);
            if (itemElements.Count == 0) { index++; continue; }

            if (itemElements[0] is TextElement firstText)
            {
                firstText.Content = prefix + firstText.Content.Value;
                if (itemElements.Count == 1)
                {
                    list.AddChild(firstText);
                }
                else
                {
                    var row = new FlexElement { Direction = FlexDirection.Row };
                    foreach (var item in itemElements)
                        row.AddChild(item);
                    list.AddChild(row);
                }
            }
            else
            {
                var row = new FlexElement { Direction = FlexDirection.Row };
                row.AddChild(new TextElement { Content = prefix });
                foreach (var item in itemElements)
                    row.AddChild(item);
                list.AddChild(row);
            }

            index++;
        }

        if (list.Children.Count > 0)
            results.Add(list);
    }

    private static void ProcessBlockquote(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        context = ApplyInlineStyles(node, context);
        var container = new FlexElement
        {
            Padding = "0 0 0 12",
            Background = "#f5f5f5",
            Direction = FlexDirection.Column
        };

        var children = new List<TemplateElement>();
        ProcessNodes(node.ChildNodes, children, context, depth + 1);
        foreach (var child in children)
            container.AddChild(child);

        if (container.Children.Count > 0)
            results.Add(container);
    }

    private static void ProcessPreformatted(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context)
    {
        var container = new FlexElement
        {
            Background = "#f0f0f0",
            Padding = "8",
            Direction = FlexDirection.Column
        };

        // For <pre>, get the raw inner text preserving whitespace
        var text = HtmlEntity.DeEntitize(node.InnerText);
        if (!string.IsNullOrEmpty(text))
        {
            container.AddChild(CreateTextElement(text, context));
        }

        results.Add(container);
    }

    private static void ProcessContainer(
        HtmlNode node,
        List<TemplateElement> results,
        InlineContext context,
        int depth)
    {
        context = ApplyInlineStyles(node, context);
        var styleProps = ParseStyleAttribute(node.GetAttributeValue("style", ""));

        var container = new FlexElement { Direction = FlexDirection.Column };

        if (styleProps.TryGetValue("padding", out var padding))
            container.Padding = padding;
        if (styleProps.TryGetValue("background-color", out var bg))
            container.Background = bg;
        else if (styleProps.TryGetValue("background", out bg))
            container.Background = bg;

        var children = new List<TemplateElement>();
        ProcessNodes(node.ChildNodes, children, context, depth + 1);
        foreach (var child in children)
            container.AddChild(child);

        if (container.Children.Count > 0)
            results.Add(container);
    }

    private static List<TemplateElement> CollectInlineChildren(HtmlNode node, InlineContext context, int depth)
    {
        var elements = new List<TemplateElement>();
        ProcessNodes(node.ChildNodes, elements, context, depth + 1);
        return elements;
    }

    private static TextElement CreateTextElement(string text, InlineContext context)
    {
        var element = new TextElement { Content = text };

        if (context.Weight != FontWeight.Normal)
            element.FontWeight = context.Weight;
        if (context.Style != Parsing.Ast.FontStyle.Normal)
            element.FontStyle = context.Style;
        if (context.Size is not null)
            element.Size = context.Size;
        if (context.Color is not null)
            element.Color = context.Color;
        if (context.Background is not null)
            element.Background = context.Background;
        if (context.Align is not null)
            element.Align = context.Align.Value;

        return element;
    }

    private static InlineContext ApplyInlineStyles(HtmlNode node, InlineContext context)
    {
        var style = node.GetAttributeValue("style", "");
        if (string.IsNullOrWhiteSpace(style)) return context;

        var props = ParseStyleAttribute(style);

        if (props.TryGetValue("color", out var color))
            context = context with { Color = color };

        if (props.TryGetValue("font-size", out var fontSize))
            context = context with { Size = fontSize };

        if (props.TryGetValue("background-color", out var bgColor))
            context = context with { Background = bgColor };
        else if (props.TryGetValue("background", out var bg))
            context = context with { Background = bg };

        if (props.TryGetValue("font-weight", out var fontWeight))
            context = context with { Weight = ParseFontWeight(fontWeight) };

        if (props.TryGetValue("font-style", out var fontStyle))
            context = context with { Style = ParseFontStyle(fontStyle) };

        if (props.TryGetValue("text-align", out var textAlign))
            context = context with { Align = ParseTextAlign(textAlign) };

        return context;
    }

    private static Dictionary<string, string> ParseStyleAttribute(string style)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(style)) return result;

        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIndex = declaration.IndexOf(':');
            if (colonIndex <= 0 || colonIndex >= declaration.Length - 1) continue;

            var property = declaration[..colonIndex].Trim();
            var value = declaration[(colonIndex + 1)..].Trim();
            if (property.Length > 0 && value.Length > 0)
                result[property] = value;
        }

        return result;
    }

    private static FontWeight ParseFontWeight(string value)
    {
        if (value.Equals("bold", StringComparison.OrdinalIgnoreCase))
            return FontWeight.Bold;
        if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            return FontWeight.Normal;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            // Map to closest enum value
            return numeric switch
            {
                <= 100 => FontWeight.Thin,
                <= 200 => FontWeight.ExtraLight,
                <= 300 => FontWeight.Light,
                <= 400 => FontWeight.Normal,
                <= 500 => FontWeight.Medium,
                <= 600 => FontWeight.SemiBold,
                <= 700 => FontWeight.Bold,
                <= 800 => FontWeight.ExtraBold,
                _ => FontWeight.Black
            };
        }

        return FontWeight.Normal;
    }

    private static Parsing.Ast.FontStyle ParseFontStyle(string value)
    {
        if (value.Equals("italic", StringComparison.OrdinalIgnoreCase))
            return Parsing.Ast.FontStyle.Italic;
        if (value.Equals("oblique", StringComparison.OrdinalIgnoreCase))
            return Parsing.Ast.FontStyle.Oblique;
        return Parsing.Ast.FontStyle.Normal;
    }

    private static TextAlign ParseTextAlign(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "center" => TextAlign.Center,
            "right" => TextAlign.Right,
            "left" => TextAlign.Left,
            _ => TextAlign.Left
        };
    }

    private static bool IsInsidePre(HtmlNode node)
    {
        var parent = node.ParentNode;
        while (parent is not null)
        {
            if (parent.Name.Equals("pre", StringComparison.OrdinalIgnoreCase))
                return true;
            parent = parent.ParentNode;
        }

        return false;
    }

    /// <summary>
    /// Tracks inherited inline formatting state during DOM traversal.
    /// </summary>
    private sealed record InlineContext
    {
        /// <summary>Gets the current font weight.</summary>
        public FontWeight Weight { get; init; } = FontWeight.Normal;

        /// <summary>Gets the current font style.</summary>
        public Parsing.Ast.FontStyle Style { get; init; } = Parsing.Ast.FontStyle.Normal;

        /// <summary>Gets the current font size override, or null for default.</summary>
        public string? Size { get; init; }

        /// <summary>Gets the current text color override, or null for default.</summary>
        public string? Color { get; init; }

        /// <summary>Gets the current background color override, or null for default.</summary>
        public string? Background { get; init; }

        /// <summary>Gets the current text alignment override, or null for default.</summary>
        public TextAlign? Align { get; init; }
    }
}
