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

    /// <summary>Effective text direction for this node.</summary>
    public TextDirection Direction { get; set; } = TextDirection.Ltr;

    /// <summary>
    /// Pre-computed text lines after wrapping, max-lines, and ellipsis processing.
    /// Only populated for <see cref="Parsing.Ast.TextElement"/> nodes when an
    /// <see cref="ITextShaper"/> is available during layout. Null for non-text elements.
    /// </summary>
    public IReadOnlyList<string>? TextLines { get; set; }

    /// <summary>
    /// Computed line height in pixels for text rendering.
    /// Only meaningful when <see cref="TextLines"/> is not null.
    /// </summary>
    public float ComputedLineHeight { get; set; }

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

    /// <summary>
    /// Sorts flow (non-absolute) children by their <see cref="TemplateElement.Order"/> property
    /// using a stable sort. Items with equal order values preserve their original source order.
    /// Absolute-positioned children are excluded from sorting and appended at the end,
    /// preserving their original relative order.
    /// </summary>
    internal void SortChildrenByOrder()
    {
        var count = _children.Count;
        var flow = new List<(LayoutNode node, int order, int index)>();
        var absolute = new List<LayoutNode>();

        for (var i = 0; i < count; i++)
        {
            if (_children[i].Element.Position.Value == Position.Absolute)
                absolute.Add(_children[i]);
            else
                flow.Add((_children[i], _children[i].Element.Order.Value, i));
        }

        flow.Sort(static (a, b) =>
        {
            var cmp = a.order.CompareTo(b.order);
            return cmp != 0 ? cmp : a.index.CompareTo(b.index);
        });

        _children.Clear();
        foreach (var item in flow)
            _children.Add(item.node);
        foreach (var item in absolute)
            _children.Add(item);
    }
}
