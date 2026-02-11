using FlexRender.Layout;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A flex container element for layout.
/// </summary>
public sealed class FlexElement : TemplateElement
{
    private List<TemplateElement> _children = new();

    /// <inheritdoc />
    public override ElementType Type => ElementType.Flex;

    // Container properties

    /// <summary>Direction of the main axis.</summary>
    public ExprValue<FlexDirection> Direction { get; set; } = FlexDirection.Column;

    /// <summary>Whether items wrap.</summary>
    public ExprValue<FlexWrap> Wrap { get; set; } = FlexWrap.NoWrap;

    /// <summary>Gap between items (px, %, em). Shorthand sets both row-gap and column-gap.</summary>
    public ExprValue<string> Gap { get; set; } = "0";

    /// <summary>Gap between items along the main axis (for row: between columns, for column: between rows).</summary>
    public ExprValue<string> ColumnGap { get; set; }

    /// <summary>Gap between wrapped lines (for row: between rows, for column: between columns).</summary>
    public ExprValue<string> RowGap { get; set; }

    /// <summary>Main axis alignment.</summary>
    public ExprValue<JustifyContent> Justify { get; set; } = JustifyContent.Start;

    /// <summary>Cross axis alignment.</summary>
    public ExprValue<AlignItems> Align { get; set; } = AlignItems.Stretch;

    /// <summary>Alignment of wrapped lines.</summary>
    public ExprValue<AlignContent> AlignContent { get; set; } = Layout.AlignContent.Start;

    /// <summary>Overflow behavior for content exceeding container bounds.</summary>
    public ExprValue<Overflow> Overflow { get; set; } = Layout.Overflow.Visible;

    /// <summary>
    /// Gets or sets the child elements.
    /// The getter returns a read-only view; use setter for bulk assignment.
    /// </summary>
    public IReadOnlyList<TemplateElement> Children
    {
        get => _children;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _children = value.ToList();
        }
    }

    /// <summary>
    /// Adds a child element to this flex container.
    /// </summary>
    /// <param name="child">The child element to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when child is null.</exception>
    public void AddChild(TemplateElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Add(child);
    }

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Direction = Direction.Resolve(resolver, data);
        Wrap = Wrap.Resolve(resolver, data);
        Gap = Gap.Resolve(resolver, data);
        ColumnGap = ColumnGap.Resolve(resolver, data);
        RowGap = RowGap.Resolve(resolver, data);
        Justify = Justify.Resolve(resolver, data);
        Align = Align.Resolve(resolver, data);
        AlignContent = AlignContent.Resolve(resolver, data);
        Overflow = Overflow.Resolve(resolver, data);
        foreach (var child in Children)
            child.ResolveExpressions(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Direction = Direction.Materialize(nameof(Direction));
        Wrap = Wrap.Materialize(nameof(Wrap));
        Gap = Gap.Materialize(nameof(Gap), ValueKind.Size);
        ColumnGap = ColumnGap.Materialize(nameof(ColumnGap), ValueKind.Size);
        RowGap = RowGap.Materialize(nameof(RowGap), ValueKind.Size);
        Justify = Justify.Materialize(nameof(Justify));
        Align = Align.Materialize(nameof(Align));
        AlignContent = AlignContent.Materialize(nameof(AlignContent));
        Overflow = Overflow.Materialize(nameof(Overflow));
        foreach (var child in Children)
            child.Materialize();
    }
}
