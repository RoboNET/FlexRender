namespace FlexRender.TemplateEngine;

/// <summary>
/// Exception thrown when template processing fails.
/// </summary>
public sealed class TemplateEngineException : Exception
{
    /// <summary>
    /// Gets the position in the input where the error occurred, if available.
    /// </summary>
    public int? Position { get; }

    /// <summary>
    /// Gets the expression that caused the error, if available.
    /// </summary>
    public string? Expression { get; }

    /// <summary>
    /// Initializes a new instance with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TemplateEngineException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TemplateEngineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance with position and expression context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="position">The position in the input where the error occurred.</param>
    /// <param name="expression">The expression that caused the error.</param>
    public TemplateEngineException(string message, int? position = null, string? expression = null)
        : base(FormatMessage(message, position, expression))
    {
        Position = position;
        Expression = expression;
    }

    private static string FormatMessage(string message, int? position, string? expression)
    {
        var parts = new List<string> { message };

        if (position.HasValue)
        {
            parts.Add($"at position {position}");
        }

        if (!string.IsNullOrEmpty(expression))
        {
            parts.Add($"in expression '{expression}'");
        }

        return string.Join(" ", parts);
    }
}
