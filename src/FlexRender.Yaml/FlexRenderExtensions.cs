using FlexRender.Abstractions;
using FlexRender.Parsing;

namespace FlexRender.Yaml;

/// <summary>
/// Extension methods for <see cref="IFlexRender"/> that provide YAML template rendering capabilities.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods bridge the gap between YAML template files and the core rendering interface.
/// They handle template parsing internally, providing a convenient API for common use cases.
/// </para>
/// <para>
/// For scenarios requiring template caching or preprocessing, use <see cref="TemplateParser"/> directly
/// and call <see cref="IFlexRender.Render(Parsing.Ast.Template, ObjectValue?, ImageFormat, CancellationToken)"/>.
/// </para>
/// </remarks>
public static class FlexRenderExtensions
{
    /// <summary>
    /// Renders a YAML template string to a byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="yaml">The YAML template content.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="format">Output image format. Defaults to PNG.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the image bytes in the specified format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="yaml"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the YAML content cannot be parsed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when rendering fails due to invalid template structure or resource loading errors.</exception>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSkia()
    ///     .Build();
    ///
    /// var yaml = @"
    /// canvas:
    ///   width: 300
    ///   height: 200
    /// layout:
    ///   - type: text
    ///     content: Hello, {{name}}!
    /// ";
    ///
    /// var data = new ObjectValue { ["name"] = new StringValue("World") };
    /// var bytes = await render.RenderYaml(yaml, data);
    /// </code>
    /// </example>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    public static async Task<byte[]> RenderYaml(
        this IFlexRender render,
        string yaml,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(yaml);

        parser ??= new TemplateParser();
        var template = parser.Parse(yaml);
        return await render.Render(template, data, format, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template string to a stream.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="output">The output stream to write image data to.</param>
    /// <param name="yaml">The YAML template content.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="format">Output image format. Defaults to PNG.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/>, <paramref name="output"/>, or <paramref name="yaml"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the YAML content cannot be parsed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when rendering fails due to invalid template structure or resource loading errors.</exception>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    public static async Task RenderYaml(
        this IFlexRender render,
        Stream output,
        string yaml,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(yaml);

        parser ??= new TemplateParser();
        var template = parser.Parse(yaml);
        await render.Render(output, template, data, format, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template file to a byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="path">The path to the YAML template file.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="format">Output image format. Defaults to PNG.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the image bytes in the specified format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the template file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when the file content cannot be parsed or exceeds maximum size.</exception>
    /// <exception cref="InvalidOperationException">Thrown when rendering fails due to invalid template structure or resource loading errors.</exception>
    /// <example>
    /// <code>
    /// var render = new FlexRenderBuilder()
    ///     .WithSkia()
    ///     .Build();
    ///
    /// var data = new ObjectValue { ["orderId"] = new StringValue("12345") };
    /// var bytes = await render.RenderFile("templates/receipt.yaml", data);
    /// await File.WriteAllBytesAsync("receipt.png", bytes);
    /// </code>
    /// </example>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    public static async Task<byte[]> RenderFile(
        this IFlexRender render,
        string path,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(path);

        parser ??= new TemplateParser();
        var template = await parser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);
        return await render.Render(template, data, format, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template file to a stream.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="output">The output stream to write image data to.</param>
    /// <param name="path">The path to the YAML template file.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="format">Output image format. Defaults to PNG.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/>, <paramref name="output"/>, or <paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the template file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when the file content cannot be parsed or exceeds maximum size.</exception>
    /// <exception cref="InvalidOperationException">Thrown when rendering fails due to invalid template structure or resource loading errors.</exception>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    public static async Task RenderFile(
        this IFlexRender render,
        Stream output,
        string path,
        ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(path);

        parser ??= new TemplateParser();
        var template = await parser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);
        await render.Render(output, template, data, format, cancellationToken).ConfigureAwait(false);
    }
}
