using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FlexRender.Rendering;

/// <summary>
/// Shared SVG formatting utilities for XML escaping and float formatting.
/// Used by SVG rendering engines and SVG-native content providers.
/// </summary>
public static partial class SvgFormatting
{
    /// <summary>
    /// Formats a float value using invariant culture with no trailing zeros.
    /// </summary>
    /// <param name="value">The float value to format.</param>
    /// <returns>A string representation of the value using invariant culture.</returns>
    public static string FormatFloat(float value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Escapes XML special characters in attribute values and text content.
    /// Returns the original string if no escaping is needed (zero-allocation fast path).
    /// </summary>
    /// <param name="value">The string value to escape.</param>
    /// <returns>The escaped string, or the original string if no escaping was needed.</returns>
    public static string EscapeXml(string value)
    {
        if (value.AsSpan().IndexOfAny("&<>\"'") < 0)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&apos;"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Sanitizes SVG content by stripping dangerous elements and attributes that could
    /// enable script injection or external resource loading when SVG content is embedded
    /// in output documents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method removes the following dangerous constructs:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>&lt;script&gt;</c> and <c>&lt;/script&gt;</c> tags (including content between them)</description></item>
    ///   <item><description><c>&lt;foreignObject&gt;</c> and <c>&lt;/foreignObject&gt;</c> tags (including content between them)</description></item>
    ///   <item><description>Event handler attributes such as <c>onload</c>, <c>onclick</c>, <c>onerror</c>, etc.</description></item>
    ///   <item><description><c>javascript:</c> protocol references in <c>href</c> and <c>xlink:href</c> attributes</description></item>
    /// </list>
    /// <para>
    /// All matching is case-insensitive. Returns the original string unchanged when no
    /// dangerous content is detected (zero-allocation fast path).
    /// </para>
    /// </remarks>
    /// <param name="content">The raw SVG content to sanitize.</param>
    /// <returns>The sanitized SVG content with dangerous constructs removed.</returns>
    public static string SanitizeSvgContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var result = ScriptTagRegex().Replace(content, string.Empty);
        result = ForeignObjectTagRegex().Replace(result, string.Empty);
        result = EventHandlerRegex().Replace(result, string.Empty);
        result = JavaScriptHrefRegex().Replace(result, "$1\"\"");

        return result;
    }

    /// <summary>
    /// Matches <c>&lt;script&gt;...&lt;/script&gt;</c> tags and their content, as well as
    /// self-closing <c>&lt;script .../&gt;</c> tags. Case-insensitive.
    /// </summary>
    [GeneratedRegex("""<script[\s>][\s\S]*?</script\s*>|<script\s[^>]*/\s*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagRegex();

    /// <summary>
    /// Matches <c>&lt;foreignObject&gt;...&lt;/foreignObject&gt;</c> tags and their content, as well as
    /// self-closing <c>&lt;foreignObject .../&gt;</c> tags. Case-insensitive.
    /// </summary>
    [GeneratedRegex("""<foreignObject[\s>][\s\S]*?</foreignObject\s*>|<foreignObject\s[^>]*/\s*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ForeignObjectTagRegex();

    /// <summary>
    /// Matches inline event handler attributes such as <c>onload="..."</c>, <c>onclick="..."</c>,
    /// <c>onerror="..."</c>, etc. Supports both double-quoted and single-quoted attribute values.
    /// Case-insensitive.
    /// </summary>
    [GeneratedRegex("""\s+on\w+\s*=\s*(?:"[^"]*"|'[^']*')""", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerRegex();

    /// <summary>
    /// Matches <c>href="javascript:..."</c> and <c>xlink:href="javascript:..."</c> attribute values.
    /// Replaces the value with an empty string while preserving the attribute name.
    /// Supports both double-quoted and single-quoted values. Case-insensitive.
    /// </summary>
    [GeneratedRegex("""((?:xlink:)?href\s*=\s*)(?:"javascript:[^"]*"|'javascript:[^']*')""", RegexOptions.IgnoreCase)]
    private static partial Regex JavaScriptHrefRegex();
}
