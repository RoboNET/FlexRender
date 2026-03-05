using FlexRender.Parsing.Ast;

namespace FlexRender.Abstractions;

/// <summary>
/// Parses formatted text content into a subtree of template elements.
/// </summary>
public interface IContentParser
{
    /// <summary>
    /// Gets the format name this parser handles (e.g., "markdown", "escpos", "xml").
    /// </summary>
    string FormatName { get; }

    /// <summary>
    /// Parses the formatted text into template elements.
    /// </summary>
    /// <param name="text">The formatted text to parse.</param>
    /// <returns>
    /// A list of renderable template elements (e.g., <see cref="TextElement"/>, <see cref="FlexElement"/>,
    /// <see cref="SeparatorElement"/>). Must not contain control-flow elements
    /// (<c>EachElement</c>, <c>IfElement</c>, <c>ContentElement</c>) as they will not be expanded.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    IReadOnlyList<TemplateElement> Parse(string text);
}
