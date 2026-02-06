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

    // ========================================================================
    // FORMAT-SPECIFIC YAML METHODS
    // ========================================================================

    /// <summary>
    /// Renders a YAML template string to a PNG byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="yaml">The YAML template content.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">PNG encoding options. Pass <c>null</c> for defaults.</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in PNG format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="yaml"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the YAML content cannot be parsed.</exception>
    public static async Task<byte[]> RenderYamlToPng(
        this IFlexRender render,
        string yaml,
        ObjectValue? data = null,
        PngOptions? options = null,
        RenderOptions? renderOptions = null,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(yaml);

        parser ??= new TemplateParser();
        var template = parser.Parse(yaml);
        return await render.RenderToPng(template, data, options, renderOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template string to a JPEG byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="yaml">The YAML template content.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">JPEG encoding options. Pass <c>null</c> for defaults (quality 90).</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in JPEG format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="yaml"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the YAML content cannot be parsed.</exception>
    public static async Task<byte[]> RenderYamlToJpeg(
        this IFlexRender render,
        string yaml,
        ObjectValue? data = null,
        JpegOptions? options = null,
        RenderOptions? renderOptions = null,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(yaml);

        parser ??= new TemplateParser();
        var template = parser.Parse(yaml);
        return await render.RenderToJpeg(template, data, options, renderOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template string to a BMP byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="yaml">The YAML template content.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">BMP encoding options. Pass <c>null</c> for defaults (Bgra32).</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in BMP format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="yaml"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the YAML content cannot be parsed.</exception>
    public static async Task<byte[]> RenderYamlToBmp(
        this IFlexRender render,
        string yaml,
        ObjectValue? data = null,
        BmpOptions? options = null,
        RenderOptions? renderOptions = null,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(yaml);

        parser ??= new TemplateParser();
        var template = parser.Parse(yaml);
        return await render.RenderToBmp(template, data, options, renderOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template string to raw BGRA8888 pixel data.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="yaml">The YAML template content.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>Raw pixel bytes in BGRA8888 format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="yaml"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the YAML content cannot be parsed.</exception>
    public static async Task<byte[]> RenderYamlToRaw(
        this IFlexRender render,
        string yaml,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(yaml);

        parser ??= new TemplateParser();
        var template = parser.Parse(yaml);
        return await render.RenderToRaw(template, data, renderOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    // ========================================================================
    // FORMAT-SPECIFIC FILE METHODS
    // ========================================================================

    /// <summary>
    /// Renders a YAML template file to a PNG byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="path">The path to the YAML template file.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">PNG encoding options. Pass <c>null</c> for defaults.</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in PNG format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the template file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when the file content cannot be parsed or exceeds maximum size.</exception>
    public static async Task<byte[]> RenderFileToPng(
        this IFlexRender render,
        string path,
        ObjectValue? data = null,
        PngOptions? options = null,
        RenderOptions? renderOptions = null,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(path);

        parser ??= new TemplateParser();
        var template = await parser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);
        return await render.RenderToPng(template, data, options, renderOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template file to a JPEG byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="path">The path to the YAML template file.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">JPEG encoding options. Pass <c>null</c> for defaults (quality 90).</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in JPEG format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the template file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when the file content cannot be parsed or exceeds maximum size.</exception>
    public static async Task<byte[]> RenderFileToJpeg(
        this IFlexRender render,
        string path,
        ObjectValue? data = null,
        JpegOptions? options = null,
        RenderOptions? renderOptions = null,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(path);

        parser ??= new TemplateParser();
        var template = await parser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);
        return await render.RenderToJpeg(template, data, options, renderOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template file to a BMP byte array.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="path">The path to the YAML template file.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="options">BMP encoding options. Pass <c>null</c> for defaults (Bgra32).</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>The image bytes in BMP format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the template file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when the file content cannot be parsed or exceeds maximum size.</exception>
    public static async Task<byte[]> RenderFileToBmp(
        this IFlexRender render,
        string path,
        ObjectValue? data = null,
        BmpOptions? options = null,
        RenderOptions? renderOptions = null,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(path);

        parser ??= new TemplateParser();
        var template = await parser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);
        return await render.RenderToBmp(template, data, options, renderOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Renders a YAML template file to raw BGRA8888 pixel data.
    /// </summary>
    /// <param name="render">The render instance to use.</param>
    /// <param name="path">The path to the YAML template file.</param>
    /// <param name="data">Optional data for template variable substitution.</param>
    /// <param name="renderOptions">Per-call rendering options. Pass <c>null</c> for defaults.</param>
    /// <param name="parser">Optional template parser for reuse across multiple calls.</param>
    /// <param name="cancellationToken">Token to cancel the rendering operation.</param>
    /// <returns>Raw pixel bytes in BGRA8888 format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="render"/> or <paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the template file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when the file content cannot be parsed or exceeds maximum size.</exception>
    public static async Task<byte[]> RenderFileToRaw(
        this IFlexRender render,
        string path,
        ObjectValue? data = null,
        RenderOptions? renderOptions = null,
        TemplateParser? parser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(path);

        parser ??= new TemplateParser();
        var template = await parser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);
        return await render.RenderToRaw(template, data, renderOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
