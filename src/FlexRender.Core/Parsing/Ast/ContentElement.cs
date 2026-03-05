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

    /// <summary>
    /// Gets or sets parser-specific options (e.g., charset mappings, encoding settings).
    /// Parsed from the YAML <c>options</c> block. May be <c>null</c> if no options specified.
    /// </summary>
    /// <remarks>
    /// This dictionary is passed through directly to
    /// <see cref="Abstractions.IContentParser.Parse"/> as the <c>options</c> parameter.
    /// Each content parser defines its own supported keys and value types. Refer to
    /// the specific parser documentation for the available options.
    /// </remarks>
    public IReadOnlyDictionary<string, object>? Options { get; set; }

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
