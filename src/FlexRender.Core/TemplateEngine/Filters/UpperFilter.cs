using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Converts a string value to uppercase using the provided <see cref="CultureInfo"/>.
/// Handles culture-specific casing rules (e.g., Turkish dotted/dotless I).
/// </summary>
/// <example>
/// <c>{{name | upper}}</c> with name="john" produces <c>JOHN</c>.
/// </example>
public sealed class UpperFilter : ITemplateFilter
{
    /// <inheritdoc />
    public string Name => "upper";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, TemplateValue? argument, CultureInfo culture)
    {
        if (input is StringValue str)
        {
            return new StringValue(str.Value.ToUpper(culture));
        }

        return input;
    }
}
