using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Formats a number with a specified number of decimal places.
/// Uses the provided <see cref="CultureInfo"/> for culture-aware formatting.
/// </summary>
/// <example>
/// <c>{{val | number:2}}</c> with val=1234.567 produces <c>1234.57</c> (InvariantCulture)
/// or <c>1234,57</c> (ru-RU).
/// </example>
public sealed class NumberFilter : ITemplateFilter
{
    /// <summary>
    /// Maximum allowed decimal places to prevent excessive string allocation.
    /// </summary>
    private const int MaxDecimalPlaces = 20;

    /// <inheritdoc />
    public string Name => "number";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture)
    {
        if (input is not NumberValue num)
        {
            return NullValue.Instance;
        }

        var decimals = 0;
        if (arguments.Positional is StringValue argStr &&
            int.TryParse(argStr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            decimals = Math.Clamp(parsed, 0, MaxDecimalPlaces);
        }

        return new StringValue(num.Value.ToString($"F{decimals}", culture));
    }
}
