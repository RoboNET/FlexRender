namespace FlexRender.TemplateEngine;

/// <summary>
/// AOT-compatible registry for template filters. Uses dictionary-based lookup with no reflection.
/// </summary>
public sealed class FilterRegistry
{
    private readonly Dictionary<string, ITemplateFilter> _filters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a filter. If a filter with the same name already exists, it is replaced.
    /// </summary>
    /// <param name="filter">The filter to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the filter name is null or whitespace.</exception>
    public void Register(ITemplateFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentException.ThrowIfNullOrWhiteSpace(filter.Name);
        _filters[filter.Name] = filter;
    }

    /// <summary>
    /// Gets a filter by name.
    /// </summary>
    /// <param name="name">The filter name to look up.</param>
    /// <returns>The filter if found; otherwise, <c>null</c>.</returns>
    public ITemplateFilter? Get(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _filters.GetValueOrDefault(name);
    }

    /// <summary>
    /// Creates a new <see cref="FilterRegistry"/> pre-populated with all built-in filters.
    /// </summary>
    /// <returns>A registry containing the 8 built-in filters.</returns>
    public static FilterRegistry CreateDefault()
    {
        var registry = new FilterRegistry();
        registry.Register(new Filters.CurrencyFilter());
        registry.Register(new Filters.CurrencySymbolFilter());
        registry.Register(new Filters.NumberFilter());
        registry.Register(new Filters.UpperFilter());
        registry.Register(new Filters.LowerFilter());
        registry.Register(new Filters.TrimFilter());
        registry.Register(new Filters.TruncateFilter());
        registry.Register(new Filters.FormatFilter());
        return registry;
    }
}
