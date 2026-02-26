using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Truncates a string to a maximum length, appending "..." if truncated.
/// Not affected by culture settings.
/// </summary>
/// <example>
/// <c>{{desc | truncate:10}}</c> with desc="Hello World!" produces <c>Hello W...</c>.
/// </example>
public sealed class TruncateFilter : ITemplateFilter
{
    /// <summary>
    /// Maximum allowed truncation length to prevent misuse.
    /// </summary>
    private const int MaxLength = 10000;

    /// <summary>
    /// Suffix appended when a string is truncated.
    /// </summary>
    private const string Ellipsis = "...";

    /// <inheritdoc />
    public string Name => "truncate";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture)
    {
        if (input is not StringValue str)
        {
            return input;
        }

        var maxLen = 50; // default
        if (arguments.Positional is StringValue argStr &&
            int.TryParse(argStr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            maxLen = Math.Clamp(parsed, 0, MaxLength);
        }

        if (str.Value.Length <= maxLen)
        {
            return str;
        }

        if (maxLen <= Ellipsis.Length)
        {
            return new StringValue(Ellipsis[..maxLen]);
        }

        return new StringValue(string.Concat(str.Value.AsSpan(0, maxLen - Ellipsis.Length), Ellipsis));
    }
}
