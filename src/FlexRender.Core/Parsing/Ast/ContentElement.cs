namespace FlexRender.Parsing.Ast;

/// <summary>
/// A content element that receives formatted text and expands it into a subtree
/// of template elements via a registered <see cref="Abstractions.IContentParser"/>.
/// </summary>
public sealed class ContentElement : TemplateElement
{
    /// <inheritdoc />
    public override ElementType Type => ElementType.Content;

    /// <summary>
    /// Gets or sets the source text to parse. Supports <c>{{expressions}}</c>.
    /// </summary>
    public ExprValue<string> Source { get; set; } = "";

    /// <summary>
    /// Gets or sets the format name (e.g., "markdown", "escpos", "xml").
    /// Supports <c>{{expressions}}</c>. Must match a registered <see cref="Abstractions.IContentParser.FormatName"/>.
    /// </summary>
    public ExprValue<string> Format { get; set; } = "";

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Source = Source.Resolve(resolver, data);
        Format = Format.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Source = Source.Materialize(nameof(Source));
        Format = Format.Materialize(nameof(Format));
    }
}
