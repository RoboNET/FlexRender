using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Truncates a string to a maximum length with a configurable suffix.
/// Supports truncation from start or end of string.
/// </summary>
/// <remarks>
/// <para>Parameters:</para>
/// <list type="bullet">
///   <item><c>length</c> (positional, default 50): Maximum length of the result including suffix.</item>
///   <item><c>suffix</c> (named, default "..."): The suffix/prefix to add when truncating.</item>
///   <item><c>fromEnd</c> (flag): When present, keeps the end of the string and adds suffix as prefix.</item>
/// </list>
/// <para>
/// Non-string input is converted: <see cref="NumberValue"/> and <see cref="BoolValue"/>
/// become strings; <see cref="NullValue"/> becomes empty string;
/// <see cref="ArrayValue"/> and <see cref="ObjectValue"/> are returned unchanged.
/// </para>
/// </remarks>
/// <example>
/// <c>{{desc | truncate:10}}</c> produces <c>Hello W...</c>.
/// <c>{{path | truncate:20 fromEnd suffix:'...'}}</c> produces <c>...ts/SkiaLayout/src</c>.
/// </example>
public sealed class TruncateFilter : ITemplateFilter
{
    /// <summary>
    /// Maximum allowed truncation length to prevent misuse.
    /// </summary>
    private const int MaxLength = 10000;

    /// <summary>
    /// Maximum allowed suffix length to prevent misuse.
    /// </summary>
    private const int MaxSuffixLength = 100;

    /// <summary>
    /// Default suffix appended or prepended when a string is truncated.
    /// </summary>
    private const string DefaultSuffix = "...";

    /// <inheritdoc />
    public string Name => "truncate";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture)
    {
        // Convert non-string input to string
        var text = input switch
        {
            StringValue sv => sv.Value,
            NumberValue nv => nv.Value.ToString("G", culture),
            BoolValue bv => bv.Value ? "true" : "false",
            NullValue => "",
            _ => (string?)null
        };

        if (text is null)
        {
            return input; // ArrayValue, ObjectValue — return as-is
        }

        // Resolve length: named "length" overrides positional
        var maxLen = 50;
        var lengthSource = arguments.GetNamed("length", NullValue.Instance);
        if (lengthSource is StringValue lenStr &&
            int.TryParse(lenStr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lenParsed))
        {
            maxLen = Math.Clamp(lenParsed, 0, MaxLength);
        }
        else if (arguments.Positional is StringValue argStr &&
                 int.TryParse(argStr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            maxLen = Math.Clamp(parsed, 0, MaxLength);
        }

        if (text.Length <= maxLen)
        {
            return new StringValue(text);
        }

        // Resolve suffix
        var suffix = DefaultSuffix;
        var suffixValue = arguments.GetNamed("suffix", NullValue.Instance);
        if (suffixValue is StringValue suffixStr)
        {
            suffix = suffixStr.Value;
            if (suffix.Length > MaxSuffixLength)
            {
                suffix = suffix[..MaxSuffixLength];
            }
        }

        // Resolve direction
        var fromEnd = arguments.HasFlag("fromEnd");

        // Edge case: length <= suffix length
        if (maxLen <= suffix.Length)
        {
            return new StringValue(suffix[..maxLen]);
        }

        var contentLen = maxLen - suffix.Length;

        if (fromEnd)
        {
            return new StringValue(string.Concat(suffix, text.AsSpan(text.Length - contentLen, contentLen)));
        }

        return new StringValue(string.Concat(text.AsSpan(0, contentLen), suffix));
    }
}
