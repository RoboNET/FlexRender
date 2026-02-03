using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Represents a rectangle with position and size.
/// </summary>
/// <param name="X">X position.</param>
/// <param name="Y">Y position.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct LayoutRect(float X, float Y, float Width, float Height);

/// <summary>
/// Represents a computed layout for an element with position and size.
/// </summary>
public sealed class LayoutNode
{
    /// <summary>The source element this node represents.</summary>
    public TemplateElement Element { get; }

    /// <summary>X position relative to parent.</summary>
    public float X { get; set; }

    /// <summary>Y position relative to parent.</summary>
    public float Y { get; set; }

    /// <summary>Computed width.</summary>
    public float Width { get; set; }

    /// <summary>Computed height.</summary>
    public float Height { get; set; }

    /// <summary>Right edge (X + Width).</summary>
    public float Right => X + Width;

    /// <summary>Bottom edge (Y + Height).</summary>
    public float Bottom => Y + Height;

    /// <summary>Bounds as a rectangle.</summary>
    public LayoutRect Bounds => new(X, Y, Width, Height);

    private readonly List<LayoutNode> _children = new();

    /// <summary>Read-only list of child layout nodes.</summary>
    public IReadOnlyList<LayoutNode> Children => _children;

    /// <summary>
    /// Creates a new layout node.
    /// </summary>
    /// <param name="element">The source element.</param>
    /// <param name="x">X position.</param>
    /// <param name="y">Y position.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    public LayoutNode(TemplateElement element, float x, float y, float width, float height)
    {
        Element = element;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Adds a child node.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when child is null.</exception>
    public void AddChild(LayoutNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Add(child);
    }
}
