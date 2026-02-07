namespace FlexRender.Parsing.Ast;

/// <summary>
/// Defines a single column in a table element.
/// Specifies the data key, header label, layout, and text styling for the column.
/// </summary>
public sealed class TableColumn
{
    /// <summary>
    /// Gets or sets the property name on the row object to extract the cell value.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Gets or sets the header text for this column. When null, no header cell is generated.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the explicit width of the column (px, %, em).
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// Gets or sets the flex grow factor for the column.
    /// </summary>
    public float Grow { get; set; }

    /// <summary>
    /// Gets or sets the text alignment within cells of this column (left, center, right).
    /// </summary>
    public TextAlign Align { get; set; } = TextAlign.Left;

    /// <summary>
    /// Gets or sets the font override for cells in this column.
    /// </summary>
    public string? Font { get; set; }

    /// <summary>
    /// Gets or sets the color override for cells in this column.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the font size override for cells in this column.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// Gets or sets the format string for cell content.
    /// May contain template expressions like <c>{{item.price}} $</c>.
    /// When null, the raw value from the key is used.
    /// </summary>
    public string? Format { get; set; }
}
