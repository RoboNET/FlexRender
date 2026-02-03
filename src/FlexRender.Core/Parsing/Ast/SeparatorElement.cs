using FlexRender.Layout;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Orientation of a separator element.
/// </summary>
public enum SeparatorOrientation
{
    /// <summary>
    /// Horizontal separator (stretches width, uses thickness as height).
    /// </summary>
    Horizontal,

    /// <summary>
    /// Vertical separator (stretches height, uses thickness as width).
    /// </summary>
    Vertical
}

/// <summary>
/// Visual style of a separator element.
/// </summary>
public enum SeparatorStyle
{
    /// <summary>
    /// Dotted line pattern.
    /// </summary>
    Dotted,

    /// <summary>
    /// Dashed line pattern.
    /// </summary>
    Dashed,

    /// <summary>
    /// Solid line.
    /// </summary>
    Solid
}

/// <summary>
/// A separator element for drawing horizontal or vertical lines.
/// </summary>
public sealed class SeparatorElement : TemplateElement
{
    /// <inheritdoc/>
    public override ElementType Type => ElementType.Separator;

    /// <summary>
    /// The orientation of the separator.
    /// </summary>
    public SeparatorOrientation Orientation { get; set; } = SeparatorOrientation.Horizontal;

    /// <summary>
    /// The visual style of the separator line.
    /// Defaults to <see cref="SeparatorStyle.Dotted"/> which is intentional for the
    /// primary receipt printing use case where dotted separators are the convention.
    /// </summary>
    public SeparatorStyle Style { get; set; } = SeparatorStyle.Dotted;

    /// <summary>
    /// The thickness of the separator in pixels.
    /// </summary>
    public float Thickness { get; set; } = 1f;

    /// <summary>
    /// The color of the separator in hex format.
    /// </summary>
    public string Color { get; set; } = "#000000";

    /// <summary>
    /// Explicit width override.
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// Explicit height override.
    /// </summary>
    public string? Height { get; set; }

    /// <summary>
    /// Flex grow factor.
    /// </summary>
    public float Grow { get; set; }

    /// <summary>
    /// Flex shrink factor.
    /// </summary>
    public float Shrink { get; set; } = 1f;

    /// <summary>
    /// Flex basis value.
    /// </summary>
    public string Basis { get; set; } = "auto";

    /// <summary>
    /// Flex order for sorting.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Individual alignment override.
    /// </summary>
    public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;
}
