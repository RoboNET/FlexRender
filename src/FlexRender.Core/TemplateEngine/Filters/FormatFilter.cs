using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Formats a value using a .NET format string.
/// </summary>
/// <remarks>
/// <para>
/// For <see cref="NumberValue"/>, applies decimal formatting (e.g., <c>number:2</c>).
/// For <see cref="StringValue"/>, attempts to parse as a date and format it.
/// </para>
/// <para>
/// The format string is passed as the filter argument: <c>{{value | format:"dd.MM.yyyy"}}</c>.
/// Uses the provided <see cref="CultureInfo"/> for culture-aware formatting.
/// </para>
/// </remarks>
/// <example>
/// <c>{{date | format:"dd.MM.yyyy"}}</c> with date="2026-02-07" produces <c>07.02.2026</c>.
/// </example>
public sealed class FormatFilter : ITemplateFilter
{
    /// <summary>
    /// Maximum allowed format string length to prevent excessive allocation.
    /// </summary>
    private const int MaxFormatLength = 100;

    /// <inheritdoc />
    public string Name => "format";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture)
    {
        if (arguments.Positional is not StringValue formatStr || string.IsNullOrEmpty(formatStr.Value))
        {
            return input;
        }

        var format = formatStr.Value;
        if (format.Length > MaxFormatLength)
        {
            format = format[..MaxFormatLength];
        }

        return input switch
        {
            NumberValue num => new StringValue(num.Value.ToString(format, culture)),
            StringValue str when DateTimeOffset.TryParse(str.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                => new StringValue(date.ToString(format, culture)),
            _ => input
        };
    }
}
