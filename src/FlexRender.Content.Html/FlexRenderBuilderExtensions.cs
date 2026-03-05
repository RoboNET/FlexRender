using FlexRender.Configuration;

namespace FlexRender.Content.Html;

/// <summary>
/// Extension methods for configuring HTML content parsing in FlexRender.
/// </summary>
public static class FlexRenderBuilderExtensions
{
    /// <summary>
    /// Adds HTML content parsing support. Enables <c>type: content</c> elements with <c>format: html</c>.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static FlexRenderBuilder WithHtml(this FlexRenderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithContentParser(new HtmlContentParser());
    }
}
