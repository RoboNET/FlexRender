namespace FlexRender.Parsing.Ast;

/// <summary>
/// An AST element that iterates over an array, creating child elements for each item.
/// </summary>
public sealed class EachElement : TemplateElement
{
    /// <summary>
    /// Gets the element type.
    /// </summary>
    public override ElementType Type => ElementType.Each;

    /// <summary>
    /// Gets or sets the path to the array in the data context.
    /// Example: "items" or "order.lines".
    /// </summary>
    public string ArrayPath { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional variable name for each item.
    /// When set to "item", use {{item.name}} to access properties.
    /// When null, item properties are accessed directly.
    /// </summary>
    public string? ItemVariable { get; set; }

    /// <summary>
    /// Gets the template elements to render for each iteration.
    /// </summary>
    public IReadOnlyList<TemplateElement> ItemTemplate { get; }

    /// <summary>
    /// Creates a new EachElement with the specified item template.
    /// </summary>
    /// <param name="itemTemplate">The elements to render for each item.</param>
    /// <exception cref="ArgumentNullException">Thrown when itemTemplate is null.</exception>
    public EachElement(IReadOnlyList<TemplateElement> itemTemplate)
    {
        ItemTemplate = itemTemplate ?? throw new ArgumentNullException(nameof(itemTemplate));
    }
}
