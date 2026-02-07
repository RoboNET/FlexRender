using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Converts a string value to lowercase using the provided <see cref="CultureInfo"/>.
/// Handles culture-specific casing rules (e.g., Turkish dotted/dotless I).
/// </summary>
/// <example>
/// <c>{{name | lower}}</c> with name="JOHN" produces <c>john</c>.
/// </example>
public sealed class LowerFilter : ITemplateFilter
{
    /// <inheritdoc />
    public string Name => "lower";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, TemplateValue? argument, CultureInfo culture)
    {
        if (input is StringValue str)
        {
            return new StringValue(str.Value.ToLower(culture));
        }

        return input;
    }
}
