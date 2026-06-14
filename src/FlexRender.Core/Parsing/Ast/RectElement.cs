using System;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A rectangle shape element. Participates in flex layout as a box with explicit
/// width/height and is drawn as a filled and/or stroked rectangle with optional rounded corners.
/// </summary>
public sealed class RectElement : TemplateElement
{
    /// <inheritdoc/>
    public override ElementType Type => ElementType.Rect;

    /// <summary>
    /// Fill: a solid color (e.g. "#4A90D9") or a gradient string produced from the YAML
    /// gradient object form (a CSS-style "linear-gradient(...)" / "radial-gradient(...)").
    /// Null means no fill.
    /// </summary>
    public ExprValue<string> Fill { get; set; }

    /// <summary>Stroke color in hex format. Null means no stroke.</summary>
    public ExprValue<string> Stroke { get; set; }

    /// <summary>Stroke width in pixels. Zero means no stroke.</summary>
    public ExprValue<float> StrokeWidth { get; set; }

    /// <summary>Corner radius (px, em). Null means square corners.</summary>
    public ExprValue<string> Radius { get; set; }

    /// <inheritdoc />
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new RectElement
        {
            Fill = substitutor(Fill.Value)!,
            Stroke = Stroke,
            StrokeWidth = StrokeWidth,
            Radius = Radius
        };
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Fill = Fill.Resolve(resolver, data);
        Stroke = Stroke.Resolve(resolver, data);
        StrokeWidth = StrokeWidth.Resolve(resolver, data);
        Radius = Radius.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Fill = Fill.Materialize(nameof(Fill));
        Stroke = Stroke.Materialize(nameof(Stroke), ValueKind.Color);
        StrokeWidth = StrokeWidth.Materialize(nameof(StrokeWidth));
        Radius = Radius.Materialize(nameof(Radius), ValueKind.Size);
    }
}
