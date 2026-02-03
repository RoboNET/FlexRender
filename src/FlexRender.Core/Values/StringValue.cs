namespace FlexRender;

/// <summary>
/// Represents a string value in template data.
/// </summary>
public sealed class StringValue : TemplateValue
{
    /// <summary>
    /// Gets the string value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringValue"/> class.
    /// </summary>
    /// <param name="value">The string value. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public StringValue(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc />
    public override bool Equals(TemplateValue? other)
    {
        return other is StringValue stringValue && Value == stringValue.Value;
    }

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Value;
}
