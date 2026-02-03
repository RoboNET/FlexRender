namespace FlexRender;

/// <summary>
/// Base class for all template values. AOT-compatible, no reflection required.
/// </summary>
public abstract class TemplateValue : IEquatable<TemplateValue>
{
    /// <summary>
    /// Implicitly converts a string to a <see cref="StringValue"/>.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    public static implicit operator TemplateValue(string value) => new StringValue(value);

    /// <summary>
    /// Implicitly converts an int to a <see cref="NumberValue"/>.
    /// </summary>
    /// <param name="value">The integer value to convert.</param>
    public static implicit operator TemplateValue(int value) => new NumberValue(value);

    /// <summary>
    /// Implicitly converts a long to a <see cref="NumberValue"/>.
    /// </summary>
    /// <param name="value">The long value to convert.</param>
    public static implicit operator TemplateValue(long value) => new NumberValue(value);

    /// <summary>
    /// Implicitly converts a decimal to a <see cref="NumberValue"/>.
    /// </summary>
    /// <param name="value">The decimal value to convert.</param>
    public static implicit operator TemplateValue(decimal value) => new NumberValue(value);

    /// <summary>
    /// Implicitly converts a double to a <see cref="NumberValue"/>.
    /// </summary>
    /// <param name="value">The double value to convert.</param>
    public static implicit operator TemplateValue(double value) => new NumberValue((decimal)value);

    /// <summary>
    /// Implicitly converts a bool to a <see cref="BoolValue"/>.
    /// </summary>
    /// <param name="value">The boolean value to convert.</param>
    public static implicit operator TemplateValue(bool value) => new BoolValue(value);

    /// <summary>
    /// Determines whether this value equals another <see cref="TemplateValue"/>.
    /// </summary>
    /// <param name="other">The other value to compare.</param>
    /// <returns>True if the values are equal; otherwise, false.</returns>
    public abstract bool Equals(TemplateValue? other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TemplateValue other && Equals(other);

    /// <inheritdoc />
    public abstract override int GetHashCode();

    /// <summary>
    /// Determines whether two <see cref="TemplateValue"/> instances are equal.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True if both values are equal; otherwise, false.</returns>
    public static bool operator ==(TemplateValue? left, TemplateValue? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="TemplateValue"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True if the values are not equal; otherwise, false.</returns>
    public static bool operator !=(TemplateValue? left, TemplateValue? right) => !(left == right);
}
