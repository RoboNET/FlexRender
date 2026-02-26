using System.Globalization;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Holds parsed filter arguments — an optional positional argument plus named key:value pairs and flags.
/// </summary>
public sealed class FilterArguments
{
    /// <summary>
    /// Empty arguments instance. Used when a filter has no arguments.
    /// </summary>
    public static readonly FilterArguments Empty = new(null, new Dictionary<string, TemplateValue?>());

    private readonly IReadOnlyDictionary<string, TemplateValue?> _named;

    /// <summary>
    /// Initializes a new instance with positional and named arguments.
    /// </summary>
    /// <param name="positional">The positional argument, or null if absent.</param>
    /// <param name="named">Named arguments. A null value indicates a boolean flag.</param>
    public FilterArguments(TemplateValue? positional, IReadOnlyDictionary<string, TemplateValue?> named)
    {
        ArgumentNullException.ThrowIfNull(named);
        Positional = positional;
        _named = named;
    }

    /// <summary>
    /// Gets the positional (first, unnamed) argument, or null if not provided.
    /// </summary>
    public TemplateValue? Positional { get; }

    /// <summary>
    /// Gets a named argument by key, returning <paramref name="defaultValue"/> if absent.
    /// </summary>
    /// <param name="name">The name of the argument to retrieve.</param>
    /// <param name="defaultValue">The value to return if the named argument is not found.</param>
    /// <returns>The named argument value if present and non-null; otherwise, <paramref name="defaultValue"/>.</returns>
    public TemplateValue GetNamed(string name, TemplateValue defaultValue)
    {
        if (_named.TryGetValue(name, out var value) && value is not null)
        {
            return value;
        }

        return defaultValue;
    }

    /// <summary>
    /// Returns true if the given flag is present (a named key with no value).
    /// </summary>
    /// <param name="name">The flag name to check.</param>
    /// <returns>True if the flag is present (key exists with null value); otherwise, false.</returns>
    public bool HasFlag(string name)
    {
        return _named.TryGetValue(name, out var value) && value is null;
    }
}

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
    /// <param name="arguments">The filter arguments (positional, named, and flags).</param>
    /// <param name="culture">The culture to use for formatting operations.</param>
    /// <returns>The transformed value.</returns>
    TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture);
}
