using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Trims leading and trailing whitespace from a string value.
/// Not affected by culture settings.
/// </summary>
/// <example>
/// <c>{{name | trim}}</c> with name="  John  " produces <c>John</c>.
/// </example>
public sealed class TrimFilter : ITemplateFilter
{
    /// <inheritdoc />
    public string Name => "trim";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture)
    {
        if (input is StringValue str)
        {
            return new StringValue(str.Value.Trim());
        }

        return input;
    }
}
