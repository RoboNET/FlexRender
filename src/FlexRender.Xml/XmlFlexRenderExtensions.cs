using FlexRender.Abstractions;
using FlexRender.Parsing;

namespace FlexRender.Xml;

/// <summary>
/// Extension methods for <see cref="IFlexRender"/> that provide XML template rendering capabilities.
/// </summary>
/// <remarks>
/// These methods mirror the YAML <c>RenderYaml</c> extensions: the XML is parsed into the shared
/// <see cref="Parsing.Ast.Template"/> AST via <see cref="XmlTemplateParser"/> and handed to the same
/// <see cref="IFlexRender.Render(Parsing.Ast.Template, ObjectValue?, ImageFormat, CancellationToken)"/>
/// downstream path used for YAML rendering.
/// </remarks>
public static class XmlFlexRenderExtensions
{
    /// <summary>
    /// Renders an XML template string to a byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="xml">The XML template content.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="format">Output image format. Defaults to PNG.</param>
    /// <param name="parser">Optional XML template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the image bytes in the specified format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="xml"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the XML content cannot be parsed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when rendering fails due to invalid template structure or resource loading errors.</exception>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSkia()
    ///     .Build();
    ///
    /// var xml = @"
    /// &lt;flexrender&gt;
    ///   &lt;canvas width=""300"" height=""200"" /&gt;
    ///   &lt;text content=""Hello, {{name}}!"" /&gt;
    /// &lt;/flexrender&gt;";
    ///
    /// var data = new ObjectValue { ["name"] = new StringValue("World") };
    /// var bytes = await render.RenderXml(xml, data);
    /// </code>
    /// </example>
    public static async Task<byte[]> RenderXml(
        this IFlexRender render,
        string xml,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        XmlTemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(xml);

        parser ??= new XmlTemplateParser();
        var template = parser.Parse(xml);
        return await render.Render(template, data, format, cancellationToken).ConfigureAwait(false);
    }
}
