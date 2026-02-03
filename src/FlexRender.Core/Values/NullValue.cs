namespace FlexRender;

/// <summary>
/// Represents a null/missing value in template data. Uses singleton pattern.
/// </summary>
public sealed class NullValue : TemplateValue
{
    /// <summary>
    /// Gets the singleton instance of <see cref="NullValue"/>.
    /// </summary>
    public static NullValue Instance { get; } = new();

    private NullValue() { }

    /// <inheritdoc />
    public override bool Equals(TemplateValue? other)
    {
        return other is NullValue;
    }

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public override string ToString() => "null";
}
