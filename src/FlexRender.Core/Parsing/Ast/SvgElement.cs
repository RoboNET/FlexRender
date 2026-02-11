using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// An SVG element for rendering vector graphics content in templates.
/// Supports both external SVG files (via <see cref="Src"/>) and inline SVG markup
/// (via <see cref="Content"/>). These properties are mutually exclusive.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="ImageElement"/>, SVG elements are rendered as vector graphics,
/// providing crisp rendering at any scale without pixelation.
/// </para>
/// <para>
/// Requires an SVG provider to be registered via the builder:
/// <c>.WithSkia(skia => skia.WithSvg())</c>.
/// </para>
/// </remarks>
public sealed class SvgElement : TemplateElement
{
    /// <inheritdoc />
    public override ElementType Type => ElementType.Svg;

    /// <summary>
    /// The source path to an SVG file. Loaded via resource loaders (same as image).
    /// Mutually exclusive with <see cref="Content"/>.
    /// </summary>
    public ExprValue<string> Src { get; set; }

    /// <summary>
    /// Inline SVG markup string. Mutually exclusive with <see cref="Src"/>.
    /// </summary>
    public ExprValue<string> Content { get; set; }

    /// <summary>
    /// The target width for the SVG rendering in pixels.
    /// If null, uses the SVG's intrinsic width or container dimensions.
    /// </summary>
    public ExprValue<int?> SvgWidth { get; set; }

    /// <summary>
    /// The target height for the SVG rendering in pixels.
    /// If null, uses the SVG's intrinsic height or container dimensions.
    /// </summary>
    public ExprValue<int?> SvgHeight { get; set; }

    /// <summary>
    /// How the SVG should be fitted within its container bounds.
    /// </summary>
    public ExprValue<ImageFit> Fit { get; set; } = ImageFit.Contain;

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Src = Src.Resolve(resolver, data);
        Content = Content.Resolve(resolver, data);
        SvgWidth = SvgWidth.Resolve(resolver, data);
        SvgHeight = SvgHeight.Resolve(resolver, data);
        Fit = Fit.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Src = Src.Materialize(nameof(Src), ValueKind.Path);
        Content = Content.Materialize(nameof(Content));
        SvgWidth = SvgWidth.Materialize(nameof(SvgWidth));
        SvgHeight = SvgHeight.Materialize(nameof(SvgHeight));
        Fit = Fit.Materialize(nameof(Fit));
    }
}
