using FlexRender.Configuration;

namespace FlexRender.Content.Ndc;

/// <summary>
/// Extension methods for configuring NDC content parsing in FlexRender.
/// </summary>
public static class FlexRenderBuilderExtensions
{
    /// <summary>
    /// Adds NDC content parsing support. Enables <c>type: content</c> elements with <c>format: ndc</c>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static FlexRenderBuilder WithNdc(this FlexRenderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var parser = new NdcContentParser();
        builder.WithContentParser(parser);
        return builder.WithBinaryContentParser(parser);
    }
}
