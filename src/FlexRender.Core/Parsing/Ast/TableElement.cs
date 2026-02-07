namespace FlexRender.Parsing.Ast;

/// <summary>
/// A table element for structured tabular data.
/// Expands into a FlexElement tree during template expansion, requiring
/// zero changes to LayoutEngine, IntrinsicMeasurer, or SkiaRenderer.
/// Supports both dynamic (data-bound via <see cref="ArrayPath"/>) and
/// static (hardcoded via <see cref="Rows"/>) data sources.
/// </summary>
public sealed class TableElement : TemplateElement
{
    /// <inheritdoc />
    public override ElementType Type => ElementType.Table;

    /// <summary>
    /// Gets or sets the path to the array in the data context for dynamic tables.
    /// Mutually exclusive with <see cref="Rows"/>.
    /// Example: "items" or "order.lines".
    /// </summary>
    public string? ArrayPath { get; set; }

    /// <summary>
    /// Gets or sets the optional variable name for each item in dynamic tables.
    /// When set to "item", use <c>{{item.name}}</c> to access properties.
    /// </summary>
    public string? ItemVariable { get; set; }

    /// <summary>
    /// Gets the column definitions for the table. Required.
    /// </summary>
    public IReadOnlyList<TableColumn> Columns { get; }

    /// <summary>
    /// Gets the static rows for the table.
    /// Mutually exclusive with <see cref="ArrayPath"/>.
    /// </summary>
    public IReadOnlyList<TableRow> Rows { get; }

    /// <summary>
    /// Gets or sets the default font name for table cells.
    /// </summary>
    public string Font { get; set; } = "main";

    /// <summary>
    /// Gets or sets the default font size for table cells.
    /// </summary>
    public string Size { get; set; } = "1em";

    /// <summary>
    /// Gets or sets the default text color for table cells.
    /// </summary>
    public string Color { get; set; } = "#000000";

    /// <summary>
    /// Gets or sets the gap between rows.
    /// </summary>
    public string? RowGap { get; set; }

    /// <summary>
    /// Gets or sets the gap between columns.
    /// </summary>
    public string? ColumnGap { get; set; }

    /// <summary>
    /// Gets or sets the font name for header cells.
    /// </summary>
    public string? HeaderFont { get; set; }

    /// <summary>
    /// Gets or sets the text color for header cells.
    /// </summary>
    public string? HeaderColor { get; set; }

    /// <summary>
    /// Gets or sets the font size for header cells.
    /// </summary>
    public string? HeaderSize { get; set; }

    /// <summary>
    /// Gets or sets whether to render a separator below the header row.
    /// When true, a horizontal dotted separator is added.
    /// Can also be a string for style (e.g., "solid", "dashed", "dotted").
    /// </summary>
    public string? HeaderBorderBottom { get; set; }

    /// <summary>
    /// Creates a new <see cref="TableElement"/> with the specified columns and optional static rows.
    /// </summary>
    /// <param name="columns">The column definitions. Must not be null or empty.</param>
    /// <param name="rows">Optional static rows. Defaults to an empty list.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="columns"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="columns"/> is empty.</exception>
    public TableElement(IReadOnlyList<TableColumn> columns, IReadOnlyList<TableRow>? rows = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
        {
            throw new ArgumentException("Table must have at least one column.", nameof(columns));
        }

        Columns = columns;
        Rows = rows ?? Array.Empty<TableRow>();
    }
}
