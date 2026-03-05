using FlexRender.Abstractions;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Registry for content parsers, mapping format names to <see cref="IContentParser"/> implementations.
/// </summary>
public sealed class ContentParserRegistry
{
    private readonly Dictionary<string, IContentParser> _parsers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a content parser for its format name.
    /// </summary>
    /// <param name="parser">The content parser to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parser"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a parser for the same format is already registered.</exception>
    public void Register(IContentParser parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentException.ThrowIfNullOrWhiteSpace(parser.FormatName, nameof(parser));
        if (!_parsers.TryAdd(parser.FormatName, parser))
        {
            throw new InvalidOperationException(
                $"A content parser for format '{parser.FormatName}' is already registered.");
        }
    }

    /// <summary>
    /// Gets the content parser for the specified format name.
    /// </summary>
    /// <param name="formatName">The format name to look up.</param>
    /// <returns>The content parser if found; otherwise, <c>null</c>.</returns>
    public IContentParser? GetParser(string formatName)
    {
        return _parsers.GetValueOrDefault(formatName);
    }

    /// <summary>
    /// Gets whether any content parsers are registered.
    /// </summary>
    internal bool HasParsers => _parsers.Count > 0;
}
