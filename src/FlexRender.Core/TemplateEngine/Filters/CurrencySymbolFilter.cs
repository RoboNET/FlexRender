using System.Collections.Frozen;
using System.Globalization;

namespace FlexRender.TemplateEngine.Filters;

/// <summary>
/// Converts ISO 4217 currency codes (alphabetic or numeric) to their symbols.
/// Uses static <see cref="FrozenDictionary{TKey, TValue}"/> lookups for AOT-safe, zero-allocation reads.
/// </summary>
/// <example>
/// <c>{{currency | currencySymbol}}</c> with currency="RUB" produces <c>₽</c>.
/// <c>{{code | currencySymbol}}</c> with code=978 produces <c>€</c>.
/// </example>
public sealed class CurrencySymbolFilter : ITemplateFilter
{
    /// <summary>
    /// Mapping from ISO 4217 alpha-3 codes (uppercase) to currency symbols.
    /// Contains the most commonly used currencies worldwide.
    /// </summary>
    private static readonly FrozenDictionary<string, string> AlphaToSymbol =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Major world currencies
            ["USD"] = "$",
            ["EUR"] = "€",
            ["GBP"] = "£",
            ["JPY"] = "¥",
            ["CNY"] = "¥",
            ["CHF"] = "CHF",

            // CIS & Eastern Europe
            ["RUB"] = "₽",
            ["RUR"] = "₽",     // Pre-1998 denomination, legacy systems
            ["UAH"] = "₴",
            ["KZT"] = "₸",
            ["BYN"] = "Br",
            ["GEL"] = "₾",
            ["AMD"] = "֏",
            ["AZN"] = "₼",
            ["UZS"] = "сўм",
            ["KGS"] = "сом",
            ["TJS"] = "SM",
            ["MDL"] = "L",
            ["TMT"] = "T",

            // Europe
            ["PLN"] = "zł",
            ["CZK"] = "Kč",
            ["HUF"] = "Ft",
            ["RON"] = "lei",
            ["BGN"] = "лв",
            ["HRK"] = "kn",
            ["RSD"] = "din.",
            ["SEK"] = "kr",
            ["NOK"] = "kr",
            ["DKK"] = "kr",
            ["ISK"] = "kr",

            // Americas
            ["CAD"] = "C$",
            ["BRL"] = "R$",
            ["MXN"] = "MX$",
            ["ARS"] = "AR$",
            ["CLP"] = "CL$",
            ["COP"] = "COL$",
            ["PEN"] = "S/",

            // Asia-Pacific
            ["INR"] = "₹",
            ["KRW"] = "₩",
            ["TWD"] = "NT$",
            ["THB"] = "฿",
            ["VND"] = "₫",
            ["PHP"] = "₱",
            ["MYR"] = "RM",
            ["SGD"] = "S$",
            ["IDR"] = "Rp",
            ["HKD"] = "HK$",
            ["AUD"] = "A$",
            ["NZD"] = "NZ$",

            // Middle East & Africa
            ["TRY"] = "₺",
            ["ILS"] = "₪",
            ["SAR"] = "﷼",
            ["AED"] = "د.إ",
            ["QAR"] = "﷼",
            ["EGP"] = "E£",
            ["ZAR"] = "R",
            ["NGN"] = "₦",
            ["KES"] = "KSh",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Mapping from ISO 4217 numeric codes to currency symbols.
    /// </summary>
    private static readonly FrozenDictionary<int, string> NumericToSymbol =
        new Dictionary<int, string>
        {
            // Major world currencies
            [840] = "$",     // USD
            [978] = "€",     // EUR
            [826] = "£",     // GBP
            [392] = "¥",     // JPY
            [156] = "¥",     // CNY
            [756] = "CHF",   // CHF

            // CIS & Eastern Europe
            [643] = "₽",     // RUB
            [810] = "₽",     // RUR (pre-1998 denomination)
            [980] = "₴",     // UAH
            [398] = "₸",     // KZT
            [933] = "Br",    // BYN
            [981] = "₾",     // GEL
            [051] = "֏",     // AMD
            [944] = "₼",     // AZN
            [860] = "сўм",   // UZS
            [417] = "сом",   // KGS
            [972] = "SM",    // TJS
            [498] = "L",     // MDL
            [934] = "T",     // TMT

            // Europe
            [985] = "zł",    // PLN
            [203] = "Kč",    // CZK
            [348] = "Ft",    // HUF
            [946] = "lei",   // RON
            [975] = "лв",    // BGN
            [191] = "kn",    // HRK
            [941] = "din.",  // RSD
            [752] = "kr",    // SEK
            [578] = "kr",    // NOK
            [208] = "kr",    // DKK
            [352] = "kr",    // ISK

            // Americas
            [124] = "C$",    // CAD
            [986] = "R$",    // BRL
            [484] = "MX$",   // MXN
            [032] = "AR$",   // ARS
            [152] = "CL$",   // CLP
            [170] = "COL$",  // COP
            [604] = "S/",    // PEN

            // Asia-Pacific
            [356] = "₹",     // INR
            [410] = "₩",     // KRW
            [901] = "NT$",   // TWD
            [764] = "฿",     // THB
            [704] = "₫",     // VND
            [608] = "₱",     // PHP
            [458] = "RM",    // MYR
            [702] = "S$",    // SGD
            [360] = "Rp",    // IDR
            [344] = "HK$",   // HKD
            [036] = "A$",    // AUD
            [554] = "NZ$",   // NZD

            // Middle East & Africa
            [949] = "₺",     // TRY
            [376] = "₪",     // ILS
            [682] = "﷼",     // SAR
            [784] = "د.إ",   // AED
            [634] = "﷼",     // QAR
            [818] = "E£",    // EGP
            [710] = "R",     // ZAR
            [566] = "₦",     // NGN
            [404] = "KSh",   // KES
        }.ToFrozenDictionary();

    /// <inheritdoc />
    public string Name => "currencySymbol";

    /// <inheritdoc />
    public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture)
    {
        if (input is StringValue str)
        {
            return AlphaToSymbol.TryGetValue(str.Value, out var symbol)
                ? new StringValue(symbol)
                : input;
        }

        if (input is NumberValue num)
        {
            var code = (int)num.Value;
            return NumericToSymbol.TryGetValue(code, out var symbol)
                ? new StringValue(symbol)
                : input;
        }

        return input;
    }
}
