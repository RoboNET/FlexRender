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

    /// <summary>Gap between items (px, %, em).</summary>
    public string Gap { get; set; } = "0";

    /// <summary>Main axis alignment.</summary>
    public JustifyContent Justify { get; set; } = JustifyContent.Start;

    /// <summary>Cross axis alignment.</summary>
    public AlignItems Align { get; set; } = AlignItems.Stretch;

    /// <summary>Alignment of wrapped lines.</summary>
    public AlignContent AlignContent { get; set; } = AlignContent.Stretch;

    // Item properties (when this flex is inside another flex)

    /// <summary>Flex grow factor.</summary>
    public float Grow { get; set; }

    /// <summary>Flex shrink factor.</summary>
    public float Shrink { get; set; } = 1f;

    /// <summary>Flex basis (px, %, em, auto).</summary>
    public string Basis { get; set; } = "auto";

    /// <summary>Self alignment override.</summary>
    public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;

    /// <summary>Display order.</summary>
    public int Order { get; set; }

    /// <summary>Width (px, %, em, auto).</summary>
    public string? Width { get; set; }

    /// <summary>Height (px, %, em, auto).</summary>
    public string? Height { get; set; }

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
