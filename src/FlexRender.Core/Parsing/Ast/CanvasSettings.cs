using FlexRender.Layout;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Specifies which dimension of the canvas is fixed.
/// </summary>
public enum FixedDimension
{
    /// <summary>
    /// The width is fixed; height adjusts to content.
    /// </summary>
    Width,

    /// <summary>
    /// The height is fixed; width adjusts to content.
    /// </summary>
    Height,

    /// <summary>
    /// Both dimensions are fixed.
    /// </summary>
    Both,

    /// <summary>
    /// Neither dimension is fixed; both adjust to content.
    /// </summary>
    None
}

/// <summary>
/// Canvas configuration for the template.
/// </summary>
public sealed class CanvasSettings
{
    /// <summary>
    /// Which dimension is fixed (the other adjusts to content).
    /// </summary>
    public FixedDimension Fixed { get; set; } = FixedDimension.Width;

    /// <summary>
    /// Width of the canvas in pixels.
    /// </summary>
    public int Width { get; set; } = 300;

    /// <summary>
    /// Height of the canvas in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Background color in hex format (e.g., "#ffffff").
    /// </summary>
    public ExprValue<string> Background { get; set; } = "#ffffff";

    /// <summary>
    /// Canvas rotation applied to the final rendered image.
    /// Values: "none", "left" (270° / 90° CCW), "right" (90° CW), "flip" (180°), or a number for arbitrary degrees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rotation is applied AFTER rendering is complete. For 90° or 270° rotations,
    /// width and height of the final image are swapped.
    /// </para>
    /// <para>
    /// For thermal printers: use "right" to rotate a wide receipt (e.g., 630x300) into
    /// a tall image (300x630) suitable for vertical printing.
    /// </para>
    /// <para>
    /// Important: All element sizes and positions should be specified for the PRE-rotation
    /// layout. The rotation transforms the entire rendered result.
    /// </para>
    /// </remarks>
    public ExprValue<string> Rotate { get; set; } = "none";

    /// <summary>
    /// Default text direction for the entire template.
    /// Individual elements can override this.
    /// </summary>
    public TextDirection TextDirection { get; set; } = TextDirection.Ltr;

    /// <summary>
    /// Resolves template expressions in all <see cref="ExprValue{T}"/> properties.
    /// </summary>
    /// <param name="resolver">Function that resolves a raw template string to a concrete string value.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    public void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        Background = Background.Resolve(resolver, data);
        Rotate = Rotate.Resolve(resolver, data);
    }

    /// <summary>
    /// Materializes all resolved <see cref="ExprValue{T}"/> properties into typed values.
    /// </summary>
    public void Materialize()
    {
        Background = Background.Materialize("Canvas.Background", ValueKind.Color);
        Rotate = Rotate.Materialize("Canvas.Rotate");
    }
}
