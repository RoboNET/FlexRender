using System.Globalization;
using System.Text;

namespace FlexRender.Content.Ndc;

/// <summary>
/// Character encoding/decoding for NDC printer data character sets.
/// </summary>
internal static class NdcEncodings
{
    // QWERTY->JCUKEN mapping table (lowercase ASCII -> lowercase Cyrillic).
    // Source: Bfs.Integration.Ndc/Utils/NdcDisplayControls.RussianUppercaseLettersDict
    // Verified in production for years across multiple banks.
    // Only lowercase Latin letters are mapped; uppercase Latin letters are preserved as-is
    // (they represent actual Latin characters in NDC data, not Cyrillic).
    private static readonly Dictionary<char, char> QwertyToJcukenMap = new()
    {
        ['q'] = 'й', ['w'] = 'ц', ['e'] = 'у', ['r'] = 'к', ['t'] = 'е',
        ['y'] = 'н', ['u'] = 'г', ['i'] = 'ш', ['o'] = 'щ', ['p'] = 'з',
        ['{'] = 'х', ['}'] = 'ъ',
        ['a'] = 'ф', ['s'] = 'ы', ['d'] = 'в', ['f'] = 'а', ['g'] = 'п',
        ['h'] = 'р', ['j'] = 'о', ['k'] = 'л', ['l'] = 'д',
        ['|'] = 'ж', ['`'] = 'э',
        ['z'] = 'я', ['x'] = 'ч', ['c'] = 'с', ['v'] = 'м', ['b'] = 'и',
        ['n'] = 'т', ['m'] = 'ь',
        ['~'] = 'б', ['\x7f'] = 'ю',
    };

    /// <summary>
    /// Decodes text using the specified encoding name.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <param name="encodingName">
    /// Encoding name: "qwerty-jcuken", "none", "ascii".
    /// Unknown encodings return input unchanged.
    /// </param>
    /// <param name="uppercase">When true, converts mapped Cyrillic characters to uppercase.</param>
    /// <returns>The decoded text.</returns>
    internal static string Decode(string text, string encodingName, bool uppercase = false)
    {
        if (string.Equals(encodingName, "qwerty-jcuken", StringComparison.OrdinalIgnoreCase))
            return DecodeQwertyJcuken(text, uppercase);

        // "none", "ascii", or unknown -> passthrough
        return text;
    }

    private static string DecodeQwertyJcuken(string text, bool uppercase)
    {
        // Fast path: check if any character needs mapping
        var needsMapping = false;
        foreach (var ch in text)
        {
            if (QwertyToJcukenMap.ContainsKey(ch))
            {
                needsMapping = true;
                break;
            }

            if (uppercase && !char.IsUpper(ch))
            {
                needsMapping = true;
                break;
            }
        }

        if (!needsMapping)
            return text;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (QwertyToJcukenMap.TryGetValue(ch, out var mapped))
            {
                sb.Append(uppercase ? char.ToUpper(mapped, CultureInfo.InvariantCulture) : mapped);
            }
            else
            {
                // Uppercase Latin letters and all non-mapped chars preserved as-is
                sb.Append(uppercase ? char.ToUpper(ch, CultureInfo.InvariantCulture) : ch);
            }
        }
        return sb.ToString();
    }
}
