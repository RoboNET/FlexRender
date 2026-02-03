namespace FlexRender;

/// <summary>
/// Represents a boolean value in template data.
/// </summary>
public sealed class BoolValue : TemplateValue
{
    /// <summary>
    /// Gets the boolean value.
    /// </summary>
    public bool Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BoolValue"/> class.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    public BoolValue(bool value)
    {
        Value = value;
    }

    /// <inheritdoc />
    public override bool Equals(TemplateValue? other)
    {
        return other is BoolValue boolValue && Value == boolValue.Value;
    }

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Value ? "true" : "false";
}
