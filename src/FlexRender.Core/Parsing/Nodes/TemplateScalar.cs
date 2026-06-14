namespace FlexRender.Parsing.Nodes;

/// <summary>A leaf node holding a single (possibly null) string value.</summary>
public sealed class TemplateScalar : TemplateNode
{
    /// <summary>Initializes a new scalar with the given value.</summary>
    /// <param name="value">The scalar string value (may be null).</param>
    public TemplateScalar(string? value) => Value = value;

    /// <summary>Gets the scalar string value (may be null).</summary>
    public string? Value { get; }
}
