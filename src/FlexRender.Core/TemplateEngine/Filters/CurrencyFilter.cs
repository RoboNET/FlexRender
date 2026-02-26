using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Formats a number as currency with thousands separator and 2 decimal places.
/// Uses the provided <see cref="CultureInfo"/> for culture-aware formatting.
/// </summary>
/// <example>
/// <c>{{price | currency}}</c> with price=1234.5 produces <c>1,234.50</c> (InvariantCulture)
/// or <c>1 234,50</c> (ru-RU).
/// </example>
public sealed class CurrencyFilter : ITemplateFilter
{
    /// <inheritdoc />
    public string Name => "currency";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture)
    {
        if (input is NumberValue num)
        {
            return new StringValue(num.Value.ToString("N2", culture));
        }

        return NullValue.Instance;
    }
}
