using FlexRender.TemplateEngine;

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
    public ExprValue<SeparatorOrientation> Orientation { get; set; } = SeparatorOrientation.Horizontal;

    /// <summary>
    /// The visual style of the separator line.
    /// Defaults to <see cref="SeparatorStyle.Dotted"/> which is intentional for the
    /// primary receipt printing use case where dotted separators are the convention.
    /// </summary>
    public ExprValue<SeparatorStyle> Style { get; set; } = SeparatorStyle.Dotted;

    /// <summary>
    /// The thickness of the separator in pixels.
    /// </summary>
    public ExprValue<float> Thickness { get; set; } = 1f;

    /// <summary>
    /// The color of the separator in hex format.
    /// </summary>
    public ExprValue<string> Color { get; set; } = "#000000";

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Orientation = Orientation.Resolve(resolver, data);
        Style = Style.Resolve(resolver, data);
        Thickness = Thickness.Resolve(resolver, data);
        Color = Color.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Orientation = Orientation.Materialize(nameof(Orientation));
        Style = Style.Materialize(nameof(Style));
        Thickness = Thickness.Materialize(nameof(Thickness));
        Color = Color.Materialize(nameof(Color), ValueKind.Color);
    }
}
