using System;
using System.Collections.Generic;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A free-form drawing element. Participates in flex layout as a box with explicit
/// width/height; inside, an ordered list of absolute-coordinate shapes is painted
/// in list order (painter's algorithm).
/// </summary>
/// <remarks>
/// Shapes use absolute coordinates relative to the element's top-left corner.
/// The shape list is fixed at parse time and is not expression-resolvable.
/// </remarks>
public sealed class DrawElement : TemplateElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawElement"/> class.
    /// </summary>
    /// <param name="shapes">The ordered shapes to paint.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shapes"/> is null.</exception>
    public DrawElement(IReadOnlyList<DrawShape> shapes)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        Shapes = shapes;
    }

    /// <inheritdoc/>
    public override ElementType Type => ElementType.Draw;

    /// <summary>
    /// The ordered list of shapes painted inside this element.
    /// </summary>
    public IReadOnlyList<DrawShape> Shapes { get; }

    /// <inheritdoc />
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new DrawElement(Shapes);
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }
}
