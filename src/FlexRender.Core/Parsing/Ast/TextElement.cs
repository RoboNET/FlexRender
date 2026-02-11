using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Text alignment options.
/// </summary>
public enum TextAlign
{
    /// <summary>
    /// Align text to the left.
    /// </summary>
    Left,

    /// <summary>
    /// Center text horizontally.
    /// </summary>
    Center,

    /// <summary>
    /// Align text to the right.
    /// </summary>
    Right,

    /// <summary>
    /// Align to the start edge (left in LTR, right in RTL).
    /// </summary>
    Start,

    /// <summary>
    /// Align to the end edge (right in LTR, left in RTL).
    /// </summary>
    End
}

/// <summary>
/// Text overflow handling options.
/// </summary>
public enum TextOverflow
{
    /// <summary>
    /// Truncate text with an ellipsis (...).
    /// </summary>
    Ellipsis,

    /// <summary>
    /// Clip text at the boundary.
    /// </summary>
    Clip,

    /// <summary>
    /// Allow text to overflow visibly.
    /// </summary>
    Visible
}

/// <summary>
/// A text element in the template.
/// </summary>
public sealed class TextElement : TemplateElement
{
    /// <inheritdoc />
    public override ElementType Type => ElementType.Text;

    /// <summary>
    /// Text content, may contain template expressions like {{variable}}.
    /// </summary>
    public ExprValue<string> Content { get; set; } = "";

    /// <summary>
    /// Font name reference.
    /// </summary>
    public ExprValue<string> Font { get; set; } = "main";

    /// <summary>
    /// Font size (pixels, em, or percentage).
    /// </summary>
    public ExprValue<string> Size { get; set; } = "1em";

    /// <summary>
    /// Text color in hex format.
    /// </summary>
    public ExprValue<string> Color { get; set; } = "#000000";

    /// <summary>
    /// Text alignment within the element.
    /// </summary>
    public ExprValue<TextAlign> Align { get; set; } = TextAlign.Left;

    /// <summary>
    /// Whether to wrap text to multiple lines.
    /// </summary>
    public ExprValue<bool> Wrap { get; set; } = true;

    /// <summary>
    /// How to handle text overflow.
    /// </summary>
    public ExprValue<TextOverflow> Overflow { get; set; } = TextOverflow.Ellipsis;

    /// <summary>
    /// Maximum number of lines (null for unlimited).
    /// </summary>
    public ExprValue<int?> MaxLines { get; set; }

    /// <summary>
    /// Line height for multi-line text.
    /// Plain number (e.g. "1.8") is a multiplier of fontSize.
    /// With unit (e.g. "24px", "2em") is an absolute or relative value.
    /// Empty string means default (font-defined spacing).
    /// </summary>
    public ExprValue<string> LineHeight { get; set; } = "";

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Content = Content.Resolve(resolver, data);
        Font = Font.Resolve(resolver, data);
        Size = Size.Resolve(resolver, data);
        Color = Color.Resolve(resolver, data);
        Align = Align.Resolve(resolver, data);
        Wrap = Wrap.Resolve(resolver, data);
        Overflow = Overflow.Resolve(resolver, data);
        MaxLines = MaxLines.Resolve(resolver, data);
        LineHeight = LineHeight.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Content = Content.Materialize(nameof(Content));
        Font = Font.Materialize(nameof(Font));
        Size = Size.Materialize(nameof(Size), ValueKind.Size);
        Color = Color.Materialize(nameof(Color), ValueKind.Color);
        Align = Align.Materialize(nameof(Align));
        Wrap = Wrap.Materialize(nameof(Wrap));
        Overflow = Overflow.Materialize(nameof(Overflow));
        MaxLines = MaxLines.Materialize(nameof(MaxLines));
        LineHeight = LineHeight.Materialize(nameof(LineHeight), ValueKind.Size);
    }
}
