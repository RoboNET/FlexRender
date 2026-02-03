using FlexRender.Parsing.Ast;

namespace FlexRender.Abstractions;

/// <summary>
/// Parses template content from various formats (YAML, XML, JSON) into the Template AST.
/// </summary>
public interface ITemplateParser
{
    /// <summary>
    /// Parses template content from a string.
    /// </summary>
    /// <param name="content">The template content string.</param>
    /// <returns>The parsed template AST.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    Template Parse(string content);

    /// <summary>
    /// Parses template content from a stream.
    /// </summary>
    /// <param name="stream">The stream containing template content.</param>
    /// <returns>The parsed template AST.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    Template Parse(Stream stream);
}
