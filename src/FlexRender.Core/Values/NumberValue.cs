using System.Globalization;

namespace FlexRender;

/// <summary>
/// Represents a numeric value in template data.
/// </summary>
public sealed class NumberValue : TemplateValue
{
    /// <summary>
    /// Gets the numeric value.
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberValue"/> class.
    /// </summary>
    /// <param name="value">The decimal value.</param>
    public NumberValue(decimal value)
    {
        Value = value;
    }

    /// <inheritdoc />
    public override bool Equals(TemplateValue? other)
    {
        return other is NumberValue numberValue && Value == numberValue.Value;
    }

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
