namespace FlexRender.Parsing;

/// <summary>
/// Exception thrown when template parsing fails.
/// </summary>
public sealed class TemplateParseException : Exception
{
    /// <summary>
    /// Gets the line number where the error occurred, if available.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Gets the column number where the error occurred, if available.
    /// </summary>
    public int? Column { get; }

    /// <summary>
    /// Gets the property path where the error occurred, if available.
    /// </summary>
    public string? Property { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateParseException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message describing the parse failure.</param>
    public TemplateParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateParseException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message describing the parse failure.</param>
    /// <param name="innerException">The exception that caused this parse failure.</param>
    public TemplateParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateParseException"/> class with location information.
    /// </summary>
    /// <param name="message">The error message describing the parse failure.</param>
    /// <param name="line">The line number where the error occurred.</param>
    /// <param name="column">The column number where the error occurred.</param>
    /// <param name="property">The property path where the error occurred.</param>
    public TemplateParseException(string message, int? line, int? column = null, string? property = null)
        : base(FormatMessage(message, line, column, property))
    {
        Line = line;
        Column = column;
        Property = property;
    }

    /// <summary>
    /// Formats the exception message with location information.
    /// </summary>
    /// <param name="message">The base error message.</param>
    /// <param name="line">The line number where the error occurred.</param>
    /// <param name="column">The column number where the error occurred.</param>
    /// <param name="property">The property path where the error occurred.</param>
    /// <returns>A formatted message including location details.</returns>
    private static string FormatMessage(string message, int? line, int? column, string? property)
    {
        var parts = new List<string>();

        if (line.HasValue)
        {
            parts.Add($"Line {line}");
            if (column.HasValue)
            {
                parts.Add($"Column {column}");
            }
        }

        if (!string.IsNullOrEmpty(property))
        {
            parts.Add($"Property '{property}'");
        }

        return parts.Count > 0
            ? $"{string.Join(", ", parts)}: {message}"
            : message;
    }
}
