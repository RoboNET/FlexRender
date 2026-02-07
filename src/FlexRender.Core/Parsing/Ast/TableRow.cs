namespace FlexRender.Parsing.Ast;

/// <summary>
/// Represents a single static row in a table element.
/// Contains a dictionary of column key to cell value pairs,
/// plus optional per-row styling overrides.
/// </summary>
public sealed class TableRow
{
    /// <summary>
    /// Gets the cell values keyed by column key.
    /// </summary>
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the font override for this row.
    /// </summary>
    public string? Font { get; set; }

    /// <summary>
    /// Gets or sets the color override for this row.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the font size override for this row.
    /// </summary>
    public string? Size { get; set; }
}
