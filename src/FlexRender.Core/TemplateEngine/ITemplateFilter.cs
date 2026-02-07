using System.Globalization;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Interface for template filters that transform values in filter pipe expressions.
/// </summary>
/// <remarks>
/// <para>
/// Filters are applied using the pipe syntax: <c>{{value | filterName}}</c> or
/// <c>{{value | filterName:argument}}</c>.
/// </para>
/// <para>
/// Built-in filters use the <see cref="CultureInfo"/> provided at evaluation time,
/// following the priority chain: <c>RenderOptions.Culture</c> &gt; <c>Template.Culture</c> &gt;
/// <see cref="CultureInfo.InvariantCulture"/> (fallback).
/// Custom filters can be registered via <c>FlexRenderBuilder.WithFilter()</c>.
/// </para>
/// </remarks>
public interface ITemplateFilter
{
    /// <summary>
    /// Gets the name of the filter, used in pipe expressions.
    /// Must be alphanumeric only.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Applies the filter to the input value.
    /// </summary>
    /// <param name="input">The value to transform.</param>
    /// <param name="argument">An optional argument from the filter syntax (e.g., <c>truncate:30</c> passes <c>"30"</c>).</param>
    /// <param name="culture">The culture to use for formatting operations.</param>
    /// <returns>The transformed value.</returns>
    TemplateValue Apply(TemplateValue input, TemplateValue? argument, CultureInfo culture);
}
