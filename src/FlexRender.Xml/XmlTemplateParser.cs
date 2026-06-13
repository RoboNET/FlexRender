using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;

namespace FlexRender.Xml;

/// <summary>
/// Parses FlexRender XML templates into the same <see cref="Template"/> AST as the YAML parser.
/// XML is translated into the equivalent document tree and handed to the shared
/// <see cref="TemplateParser"/> so all element parsing, validation, and resource limits are reused.
/// </summary>
public sealed class XmlTemplateParser : ITemplateParser
{
    private readonly TemplateParser _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTemplateParser"/> class with default resource limits.
    /// </summary>
    public XmlTemplateParser() : this(new ResourceLimits())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTemplateParser"/> class with custom resource limits.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public XmlTemplateParser(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _inner = new TemplateParser(limits);
    }

    /// <summary>
    /// Parses an XML template string into a <see cref="Template"/> AST.
    /// </summary>
    /// <param name="content">The XML template content.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or invalid.</exception>
    public Template Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new TemplateParseException("Template XML is empty or whitespace");
        }

        var root = XmlToYamlNodeConverter.Convert(content);
        return _inner.ParseDocumentRoot(root);
    }

    /// <summary>
    /// Parses an XML template from a stream.
    /// </summary>
    /// <param name="stream">The stream containing XML content.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or invalid.</exception>
    public Template Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
