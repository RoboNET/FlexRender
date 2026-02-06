using FlexRender.Layout;

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
    public FlexDirection Direction { get; set; } = FlexDirection.Column;

    /// <summary>Whether items wrap.</summary>
    public FlexWrap Wrap { get; set; } = FlexWrap.NoWrap;

    /// <summary>Gap between items (px, %, em). Shorthand sets both row-gap and column-gap.</summary>
    public string Gap { get; set; } = "0";

    /// <summary>Gap between items along the main axis (for row: between columns, for column: between rows).</summary>
    public string? ColumnGap { get; set; }

    /// <summary>Gap between wrapped lines (for row: between rows, for column: between columns).</summary>
    public string? RowGap { get; set; }

    /// <summary>Main axis alignment.</summary>
    public JustifyContent Justify { get; set; } = JustifyContent.Start;

    /// <summary>Cross axis alignment.</summary>
    public AlignItems Align { get; set; } = AlignItems.Stretch;

    /// <summary>Alignment of wrapped lines.</summary>
    public AlignContent AlignContent { get; set; } = AlignContent.Start;

    /// <summary>Overflow behavior for content exceeding container bounds.</summary>
    public Overflow Overflow { get; set; } = Overflow.Visible;

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
}
