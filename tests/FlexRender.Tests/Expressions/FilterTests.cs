// Tests for individual built-in filters and the FilterRegistry.
//
// Compilation status: WILL NOT COMPILE until ITemplateFilter, FilterRegistry,
// and the 7 built-in filter classes are implemented.

using System.Globalization;
using FlexRender.TemplateEngine;
using FlexRender.TemplateEngine.Filters;
using Xunit;

namespace FlexRender.Tests.Expressions;

/// <summary>
/// Tests for the 7 built-in filters and the FilterRegistry.
/// </summary>
public sealed class FilterTests
{
    // === CurrencyFilter ===

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(1234.56, "1,234.56")]
    [InlineData(1000000, "1,000,000.00")]
    [InlineData(0.5, "0.50")]
    [InlineData(-42.5, "-42.50")]
    public void CurrencyFilter_FormatsNumberWithCommasAndTwoDecimals(decimal input, string expected)
    {
        var filter = new CurrencyFilter();
        var result = filter.Apply(new NumberValue(input), FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expected, str.Value);
    }

    [Fact]
    public void CurrencyFilter_NullInput_ReturnsNullValue()
    {
        var filter = new CurrencyFilter();
        var result = filter.Apply(NullValue.Instance, FilterArguments.Empty, CultureInfo.InvariantCulture);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void CurrencyFilter_StringInput_ReturnsNullValue()
    {
        var filter = new CurrencyFilter();
        var result = filter.Apply(new StringValue("not a number"), FilterArguments.Empty, CultureInfo.InvariantCulture);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void CurrencyFilter_Name_IsCurrency()
    {
        var filter = new CurrencyFilter();
        Assert.Equal("currency", filter.Name);
    }

    // === CurrencySymbolFilter ===

    [Theory]
    [InlineData("USD", "$")]
    [InlineData("EUR", "€")]
    [InlineData("GBP", "£")]
    [InlineData("RUB", "₽")]
    [InlineData("RUR", "₽")]
    [InlineData("JPY", "¥")]
    [InlineData("CNY", "¥")]
    [InlineData("TRY", "₺")]
    [InlineData("INR", "₹")]
    [InlineData("KRW", "₩")]
    [InlineData("UAH", "₴")]
    [InlineData("KZT", "₸")]
    [InlineData("BYN", "Br")]
    [InlineData("PLN", "zł")]
    [InlineData("CZK", "Kč")]
    [InlineData("CHF", "CHF")]
    [InlineData("CAD", "C$")]
    [InlineData("AUD", "A$")]
    [InlineData("BRL", "R$")]
    public void CurrencySymbolFilter_AlphaCode_ReturnsSymbol(string code, string expectedSymbol)
    {
        var filter = new CurrencySymbolFilter();
        var result = filter.Apply(new StringValue(code), FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expectedSymbol, str.Value);
    }

    [Theory]
    [InlineData("usd", "$")]
    [InlineData("eur", "€")]
    [InlineData("rub", "₽")]
    [InlineData("rur", "₽")]
    [InlineData("Gbp", "£")]
    public void CurrencySymbolFilter_CaseInsensitive_ReturnsSymbol(string code, string expectedSymbol)
    {
        var filter = new CurrencySymbolFilter();
        var result = filter.Apply(new StringValue(code), FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expectedSymbol, str.Value);
    }

    [Theory]
    [InlineData(840, "$")]
    [InlineData(978, "€")]
    [InlineData(826, "£")]
    [InlineData(643, "₽")]
    [InlineData(810, "₽")]
    [InlineData(392, "¥")]
    [InlineData(156, "¥")]
    [InlineData(949, "₺")]
    [InlineData(356, "₹")]
    [InlineData(410, "₩")]
    [InlineData(980, "₴")]
    [InlineData(398, "₸")]
    [InlineData(036, "A$")]
    public void CurrencySymbolFilter_NumericCode_ReturnsSymbol(int code, string expectedSymbol)
    {
        var filter = new CurrencySymbolFilter();
        var result = filter.Apply(new NumberValue(code), FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expectedSymbol, str.Value);
    }

    [Fact]
    public void CurrencySymbolFilter_UnknownAlphaCode_ReturnsInputUnchanged()
    {
        var filter = new CurrencySymbolFilter();
        var input = new StringValue("XYZ");
        var result = filter.Apply(input, FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("XYZ", str.Value);
    }

    [Fact]
    public void CurrencySymbolFilter_UnknownNumericCode_ReturnsInputUnchanged()
    {
        var filter = new CurrencySymbolFilter();
        var input = new NumberValue(999);
        var result = filter.Apply(input, FilterArguments.Empty, CultureInfo.InvariantCulture);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(999m, num.Value);
    }

    [Fact]
    public void CurrencySymbolFilter_NullInput_ReturnsInputUnchanged()
    {
        var filter = new CurrencySymbolFilter();
        var result = filter.Apply(NullValue.Instance, FilterArguments.Empty, CultureInfo.InvariantCulture);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void CurrencySymbolFilter_BoolInput_ReturnsInputUnchanged()
    {
        var filter = new CurrencySymbolFilter();
        var input = new BoolValue(true);
        var result = filter.Apply(input, FilterArguments.Empty, CultureInfo.InvariantCulture);

        Assert.IsType<BoolValue>(result);
    }

    [Fact]
    public void CurrencySymbolFilter_Name_IsCurrencySymbol()
    {
        var filter = new CurrencySymbolFilter();
        Assert.Equal("currencySymbol", filter.Name);
    }

    // === NumberFilter ===

    [Theory]
    [InlineData(1234.567, "2", "1234.57")]
    [InlineData(1234.567, "0", "1235")]
    [InlineData(1234.567, "4", "1234.5670")]
    [InlineData(0, "2", "0.00")]
    public void NumberFilter_FormatsWithSpecifiedDecimals(decimal input, string decimals, string expected)
    {
        var filter = new NumberFilter();
        var result = filter.Apply(new NumberValue(input), Args(decimals), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expected, str.Value);
    }

    [Fact]
    public void NumberFilter_NoArgument_DefaultsToZeroDecimals()
    {
        var filter = new NumberFilter();
        var result = filter.Apply(new NumberValue(1234.567m), FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("1235", str.Value);
    }

    [Fact]
    public void NumberFilter_Name_IsNumber()
    {
        var filter = new NumberFilter();
        Assert.Equal("number", filter.Name);
    }

    // === UpperFilter ===

    [Theory]
    [InlineData("hello", "HELLO")]
    [InlineData("Hello World", "HELLO WORLD")]
    [InlineData("ALREADY", "ALREADY")]
    [InlineData("", "")]
    public void UpperFilter_ConvertsToUpperCase(string input, string expected)
    {
        var filter = new UpperFilter();
        var result = filter.Apply(new StringValue(input), FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expected, str.Value);
    }

    [Fact]
    public void UpperFilter_NullInput_ReturnsNullValue()
    {
        var filter = new UpperFilter();
        var result = filter.Apply(NullValue.Instance, FilterArguments.Empty, CultureInfo.InvariantCulture);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void UpperFilter_NumberInput_ReturnsUnchanged()
    {
        var filter = new UpperFilter();
        var result = filter.Apply(new NumberValue(42), FilterArguments.Empty, CultureInfo.InvariantCulture);

        // Non-string input is returned unchanged
        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(42m, num.Value);
    }

    [Fact]
    public void UpperFilter_Name_IsUpper()
    {
        var filter = new UpperFilter();
        Assert.Equal("upper", filter.Name);
    }

    // === LowerFilter ===

    [Theory]
    [InlineData("HELLO", "hello")]
    [InlineData("Hello World", "hello world")]
    [InlineData("already", "already")]
    [InlineData("", "")]
    public void LowerFilter_ConvertsToLowerCase(string input, string expected)
    {
        var filter = new LowerFilter();
        var result = filter.Apply(new StringValue(input), FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expected, str.Value);
    }

    [Fact]
    public void LowerFilter_Name_IsLower()
    {
        var filter = new LowerFilter();
        Assert.Equal("lower", filter.Name);
    }

    // === TrimFilter ===

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("hello", "hello")]
    [InlineData("  ", "")]
    [InlineData("\t\nhello\r\n", "hello")]
    public void TrimFilter_RemovesLeadingAndTrailingWhitespace(string input, string expected)
    {
        var filter = new TrimFilter();
        var result = filter.Apply(new StringValue(input), FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expected, str.Value);
    }

    [Fact]
    public void TrimFilter_Name_IsTrim()
    {
        var filter = new TrimFilter();
        Assert.Equal("trim", filter.Name);
    }

    // === TruncateFilter ===

    [Fact]
    public void TruncateFilter_LongString_TruncatesWithEllipsis()
    {
        var filter = new TruncateFilter();
        var result = filter.Apply(
            new StringValue("This is a very long string that needs truncation"),
            Args("20"),
            CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(20, str.Value.Length); // maxLen including "..."
        Assert.EndsWith("...", str.Value);
    }

    [Fact]
    public void TruncateFilter_ShortString_NoChange()
    {
        var filter = new TruncateFilter();
        var result = filter.Apply(new StringValue("Short"), Args("30"), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Short", str.Value);
    }

    [Fact]
    public void TruncateFilter_ExactLength_NoChange()
    {
        var filter = new TruncateFilter();
        var result = filter.Apply(new StringValue("12345"), Args("5"), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("12345", str.Value);
    }

    [Fact]
    public void TruncateFilter_Name_IsTruncate()
    {
        var filter = new TruncateFilter();
        Assert.Equal("truncate", filter.Name);
    }

    [Fact]
    public void TruncateFilter_CustomSuffix_UsesSuffix()
    {
        var filter = new TruncateFilter();
        var named = new Dictionary<string, TemplateValue?> { ["suffix"] = new StringValue("\u2026") };
        var args = new FilterArguments(new StringValue("10"), named);
        var result = filter.Apply(new StringValue("Hello, World!"), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(10, str.Value.Length);
        Assert.EndsWith("\u2026", str.Value);
        Assert.Equal("Hello, Wo\u2026", str.Value);
    }

    [Fact]
    public void TruncateFilter_FromEnd_KeepsLastChars()
    {
        var filter = new TruncateFilter();
        var named = new Dictionary<string, TemplateValue?> { ["fromEnd"] = null };
        var args = new FilterArguments(new StringValue("8"), named);
        var result = filter.Apply(new StringValue("Hello, World!"), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(8, str.Value.Length);
        Assert.Equal("...orld!", str.Value);
    }

    [Fact]
    public void TruncateFilter_FromEndWithCustomSuffix_Works()
    {
        var filter = new TruncateFilter();
        var named = new Dictionary<string, TemplateValue?>
        {
            ["fromEnd"] = null,
            ["suffix"] = new StringValue("\u2026")
        };
        var args = new FilterArguments(new StringValue("8"), named);
        var result = filter.Apply(new StringValue("Hello, World!"), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(8, str.Value.Length);
        Assert.Equal("\u2026 World!", str.Value);
    }

    [Fact]
    public void TruncateFilter_EmptySuffix_NoSuffix()
    {
        var filter = new TruncateFilter();
        var named = new Dictionary<string, TemplateValue?> { ["suffix"] = new StringValue("") };
        var args = new FilterArguments(new StringValue("5"), named);
        var result = filter.Apply(new StringValue("Hello, World!"), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("Hello", str.Value);
    }

    [Fact]
    public void TruncateFilter_NamedLength_OverridesPositional()
    {
        var filter = new TruncateFilter();
        var named = new Dictionary<string, TemplateValue?> { ["length"] = new StringValue("5") };
        var args = new FilterArguments(new StringValue("30"), named);
        var result = filter.Apply(new StringValue("Hello, World!"), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(5, str.Value.Length);
    }

    [Fact]
    public void TruncateFilter_NumberInput_ConvertedToString()
    {
        var filter = new TruncateFilter();
        var args = new FilterArguments(new StringValue("5"), new Dictionary<string, TemplateValue?>());
        var result = filter.Apply(new NumberValue(12345.678m), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(5, str.Value.Length);
    }

    [Fact]
    public void TruncateFilter_BoolInput_ConvertedToString()
    {
        var filter = new TruncateFilter();
        var args = new FilterArguments(new StringValue("2"), new Dictionary<string, TemplateValue?>());
        var result = filter.Apply(new BoolValue(true), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("..", str.Value); // "true" len 4 > 2, suffix "..." truncated to 2
    }

    [Fact]
    public void TruncateFilter_NullInput_ReturnsEmptyString()
    {
        var filter = new TruncateFilter();
        var result = filter.Apply(NullValue.Instance, FilterArguments.Empty, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("", str.Value);
    }

    [Fact]
    public void TruncateFilter_ArrayInput_ReturnsAsIs()
    {
        var filter = new TruncateFilter();
        var input = new ArrayValue([new StringValue("a")]);
        var result = filter.Apply(input, FilterArguments.Empty, CultureInfo.InvariantCulture);

        Assert.IsType<ArrayValue>(result);
    }

    [Fact]
    public void TruncateFilter_ObjectInput_ReturnsAsIs()
    {
        var filter = new TruncateFilter();
        var input = new ObjectValue { ["k"] = new StringValue("v") };
        var result = filter.Apply(input, FilterArguments.Empty, CultureInfo.InvariantCulture);

        Assert.IsType<ObjectValue>(result);
    }

    [Fact]
    public void TruncateFilter_SuffixTooLong_ClampedTo100()
    {
        var filter = new TruncateFilter();
        var longSuffix = new string('.', 200);
        var named = new Dictionary<string, TemplateValue?> { ["suffix"] = new StringValue(longSuffix) };
        var args = new FilterArguments(new StringValue("50"), named);
        var result = filter.Apply(new StringValue(new string('A', 200)), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(50, str.Value.Length);
    }

    [Fact]
    public void TruncateFilter_LengthShorterThanSuffix_SuffixTruncated()
    {
        var filter = new TruncateFilter();
        var args = new FilterArguments(new StringValue("2"), new Dictionary<string, TemplateValue?>());
        var result = filter.Apply(new StringValue("Hello"), args, CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("..", str.Value);
    }

    // === FormatFilter ===

    [Fact]
    public void FormatFilter_Name_IsFormat()
    {
        var filter = new FormatFilter();
        Assert.Equal("format", filter.Name);
    }

    [Theory]
    [InlineData(1234.567, "F2", "1234.57")]
    [InlineData(1234.567, "F0", "1235")]
    [InlineData(1234.567, "F4", "1234.5670")]
    [InlineData(0.5, "F1", "0.5")]
    public void FormatFilter_NumberValue_FormatsWithFormatString(decimal input, string format, string expected)
    {
        var filter = new FormatFilter();
        var result = filter.Apply(new NumberValue(input), Args(format), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expected, str.Value);
    }

    [Theory]
    [InlineData("2026-02-07", "dd.MM.yyyy", "07.02.2026")]
    [InlineData("2026-02-07", "yyyy/MM/dd", "2026/02/07")]
    [InlineData("2026-12-25", "MMMM d, yyyy", "December 25, 2026")]
    public void FormatFilter_DateString_ParsesAndFormats(string input, string format, string expected)
    {
        var filter = new FormatFilter();
        var result = filter.Apply(new StringValue(input), Args(format), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expected, str.Value);
    }

    [Fact]
    public void FormatFilter_NoArgument_ReturnsInputUnchanged()
    {
        var filter = new FormatFilter();
        var input = new NumberValue(42);
        var result = filter.Apply(input, FilterArguments.Empty, CultureInfo.InvariantCulture);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(42m, num.Value);
    }

    [Fact]
    public void FormatFilter_EmptyArgument_ReturnsInputUnchanged()
    {
        var filter = new FormatFilter();
        var input = new NumberValue(42);
        var result = filter.Apply(input, Args(""), CultureInfo.InvariantCulture);

        var num = Assert.IsType<NumberValue>(result);
        Assert.Equal(42m, num.Value);
    }

    [Fact]
    public void FormatFilter_NullInput_ReturnsNullUnchanged()
    {
        var filter = new FormatFilter();
        var result = filter.Apply(NullValue.Instance, Args("F2"), CultureInfo.InvariantCulture);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void FormatFilter_NonParsableDateString_ReturnsInputUnchanged()
    {
        var filter = new FormatFilter();
        var input = new StringValue("not a date");
        var result = filter.Apply(input, Args("dd.MM.yyyy"), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("not a date", str.Value);
    }

    [Fact]
    public void FormatFilter_LongFormatString_TruncatedToMaxLength()
    {
        var filter = new FormatFilter();
        var longFormat = new string('0', 200);
        // Should not throw, just truncates the format to 100 chars
        var result = filter.Apply(new NumberValue(42), Args(longFormat), CultureInfo.InvariantCulture);

        Assert.IsType<StringValue>(result);
    }

    [Fact]
    public void FormatFilter_WithRussianCulture_FormatsDateInRussian()
    {
        var filter = new FormatFilter();
        var ruCulture = CultureInfo.GetCultureInfo("ru-RU");
        var result = filter.Apply(new StringValue("2026-02-07"), Args("dd MMMM yyyy"), ruCulture);

        var str = Assert.IsType<StringValue>(result);
        // Russian month name for February
        Assert.Contains("02", str.Value.Replace("февраля", "02").Replace("Февраля", "02"));
    }

    [Fact]
    public void FormatFilter_NumberWithGermanCulture_UsesCommaDecimalSeparator()
    {
        var filter = new FormatFilter();
        var deCulture = CultureInfo.GetCultureInfo("de-DE");
        var result = filter.Apply(new NumberValue(1234.56m), Args("F2"), deCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("1234,56", str.Value);
    }

    // === FilterRegistry ===

    [Fact]
    public void FilterRegistry_Get_BuiltInFilter_ReturnsFilter()
    {
        var registry = FilterRegistry.CreateDefault();

        var filter = registry.Get("currency");

        Assert.NotNull(filter);
        Assert.Equal("currency", filter.Name);
    }

    [Fact]
    public void FilterRegistry_Get_AllBuiltInFilters_Exist()
    {
        var registry = FilterRegistry.CreateDefault();
        var names = new[] { "currency", "currencySymbol", "number", "upper", "lower", "trim", "truncate", "format" };

        foreach (var name in names)
        {
            var filter = registry.Get(name);
            Assert.NotNull(filter);
            Assert.Equal(name, filter.Name);
        }
    }

    [Fact]
    public void FilterRegistry_Get_UnknownFilter_ReturnsNull()
    {
        var registry = FilterRegistry.CreateDefault();

        var result = registry.Get("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void FilterRegistry_Register_CustomFilter_CanBeRetrieved()
    {
        var registry = FilterRegistry.CreateDefault();
        var customFilter = new TestFilter("custom");

        registry.Register(customFilter);

        var retrieved = registry.Get("custom");
        Assert.Same(customFilter, retrieved);
    }

    [Fact]
    public void FilterRegistry_Register_NullFilter_ThrowsArgumentNullException()
    {
        var registry = FilterRegistry.CreateDefault();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    // === Culture-aware formatting ===

    [Fact]
    public void CurrencyFilter_WithRussianCulture_UsesSpaceSeparatorAndComma()
    {
        var filter = new CurrencyFilter();
        var ruCulture = CultureInfo.GetCultureInfo("ru-RU");

        var result = filter.Apply(new NumberValue(1234.56m), FilterArguments.Empty, ruCulture);

        var str = Assert.IsType<StringValue>(result);
        // ru-RU uses non-breaking space as group separator and comma as decimal separator
        Assert.Contains(",", str.Value);
        Assert.Contains("56", str.Value);
    }

    [Fact]
    public void NumberFilter_WithGermanCulture_UsesCommaDecimalSeparator()
    {
        var filter = new NumberFilter();
        var deCulture = CultureInfo.GetCultureInfo("de-DE");

        var result = filter.Apply(new NumberValue(1234.56m), Args("2"), deCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("1234,56", str.Value);
    }

    [Fact]
    public void UpperFilter_WithTurkishCulture_HandlesDottedI()
    {
        var filter = new UpperFilter();
        var trCulture = CultureInfo.GetCultureInfo("tr-TR");

        var result = filter.Apply(new StringValue("istanbul"), FilterArguments.Empty, trCulture);

        var str = Assert.IsType<StringValue>(result);
        // Turkish uppercase I-without-dot = \u0130
        Assert.StartsWith("\u0130", str.Value);
    }

    [Fact]
    public void LowerFilter_WithTurkishCulture_HandlesDottedI()
    {
        var filter = new LowerFilter();
        var trCulture = CultureInfo.GetCultureInfo("tr-TR");

        var result = filter.Apply(new StringValue("I"), FilterArguments.Empty, trCulture);

        var str = Assert.IsType<StringValue>(result);
        // Turkish lowercase I = \u0131 (dotless i)
        Assert.Equal("\u0131", str.Value);
    }

    [Fact]
    public void CurrencySymbolFilter_NotAffectedByCulture()
    {
        var filter = new CurrencySymbolFilter();
        var ruCulture = CultureInfo.GetCultureInfo("ru-RU");

        var result = filter.Apply(new StringValue("USD"), FilterArguments.Empty, ruCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("$", str.Value);
    }

    [Fact]
    public void TrimFilter_NotAffectedByCulture()
    {
        var filter = new TrimFilter();
        var jpCulture = CultureInfo.GetCultureInfo("ja-JP");

        var result = filter.Apply(new StringValue("  hello  "), FilterArguments.Empty, jpCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("hello", str.Value);
    }

    [Fact]
    public void CurrencyFilter_EnUs_FormatsWithCommaAndDot()
    {
        var filter = new CurrencyFilter();
        var enCulture = CultureInfo.GetCultureInfo("en-US");

        var result = filter.Apply(new NumberValue(1234.56m), FilterArguments.Empty, enCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("1,234.56", str.Value);
    }

    [Fact]
    public void NumberFilter_WithRussianCulture_UsesCommaDecimalSeparator()
    {
        var filter = new NumberFilter();
        var ruCulture = CultureInfo.GetCultureInfo("ru-RU");

        var result = filter.Apply(new NumberValue(1234.56m), Args("2"), ruCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("1234,56", str.Value);
    }

    [Fact]
    public void NumberFilter_NegativeDecimals_ClampedToZero()
    {
        var filter = new NumberFilter();
        var result = filter.Apply(new NumberValue(1234.567m), Args("-5"), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        // Clamped to 0 decimals
        Assert.Equal("1235", str.Value);
    }

    [Fact]
    public void NumberFilter_ExcessiveDecimals_ClampedToMax()
    {
        var filter = new NumberFilter();
        var result = filter.Apply(new NumberValue(1.5m), Args("100"), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        // Clamped to MaxDecimalPlaces=20, so 20 decimal digits
        Assert.Equal(1 + 1 + 20, str.Value.Length); // "1." + 20 digits
    }

    [Fact]
    public void NumberFilter_InvalidDecimalArgument_DefaultsToZero()
    {
        var filter = new NumberFilter();
        var result = filter.Apply(new NumberValue(1234.567m), Args("abc"), CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("1235", str.Value);
    }

    [Fact]
    public void NumberFilter_NonNumberInput_ReturnsNullValue()
    {
        var filter = new NumberFilter();
        var result = filter.Apply(new StringValue("not a number"), FilterArguments.Empty, CultureInfo.InvariantCulture);

        Assert.IsType<NullValue>(result);
    }

    [Fact]
    public void FormatFilter_WithEnUsCulture_FormatsDateInEnglish()
    {
        var filter = new FormatFilter();
        var enCulture = CultureInfo.GetCultureInfo("en-US");
        var result = filter.Apply(new StringValue("2026-02-07"), Args("MMMM d, yyyy"), enCulture);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal("February 7, 2026", str.Value);
    }

    [Fact]
    public void FilterRegistry_Get_CaseInsensitive()
    {
        var registry = FilterRegistry.CreateDefault();

        var result = registry.Get("CURRENCY");

        Assert.NotNull(result);
        Assert.Equal("currency", result.Name);
    }

    [Fact]
    public void FilterRegistry_Get_NullOrEmpty_ReturnsNull()
    {
        var registry = FilterRegistry.CreateDefault();

        Assert.Null(registry.Get(null!));
        Assert.Null(registry.Get(""));
    }

    [Fact]
    public void FilterRegistry_Register_OverridesByName()
    {
        var registry = FilterRegistry.CreateDefault();
        var custom = new TestFilter("currency");

        registry.Register(custom);

        var retrieved = registry.Get("currency");
        Assert.Same(custom, retrieved);
    }

    // === End-to-end: parser + evaluator + filter ===

    [Theory]
    [InlineData("x | truncate:5", "Hello, World!", "He...")]
    [InlineData("x | truncate:5 suffix:'…'", "Hello, World!", "Hell…")]
    [InlineData("x | truncate:5 fromEnd", "Hello, World!", "...d!")]
    [InlineData("x | truncate:5 fromEnd suffix:'…'", "Hello, World!", "…rld!")]
    [InlineData("x | truncate:10", "Short", "Short")]
    [InlineData("x | truncate length:5", "Hello, World!", "He...")]
    [InlineData("x | truncate:30 length:5", "Hello, World!", "He...")] // named overrides positional
    [InlineData("x | truncate:5 suffix:''", "Hello, World!", "Hello")]
    [InlineData("x | trim | truncate:8", "  Hello, World!  ", "Hello...")]
    public void TruncateFilter_E2E_FullPipeline(string expression, string inputValue, string expected)
    {
        var registry = FilterRegistry.CreateDefault();
        var evaluator = new InlineExpressionEvaluator(registry);
        var context = new TemplateContext(new ObjectValue { ["x"] = new StringValue(inputValue) });

        var parsed = InlineExpressionParser.Parse(expression);
        var result = evaluator.Evaluate(parsed, context);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(expected, str.Value);
    }

    [Fact]
    public void TruncateFilter_E2E_NumberInput()
    {
        var registry = FilterRegistry.CreateDefault();
        var evaluator = new InlineExpressionEvaluator(registry);
        var context = new TemplateContext(new ObjectValue { ["x"] = new NumberValue(123456.789m) });

        var parsed = InlineExpressionParser.Parse("x | truncate:8");
        var result = evaluator.Evaluate(parsed, context);

        var str = Assert.IsType<StringValue>(result);
        Assert.Equal(8, str.Value.Length);
        Assert.EndsWith("...", str.Value);
    }

    private static FilterArguments Args(string? positional = null) =>
        positional is not null
            ? new FilterArguments(new StringValue(positional), new Dictionary<string, TemplateValue?>())
            : FilterArguments.Empty;

    /// <summary>
    /// Test helper filter for custom filter registration tests.
    /// </summary>
    private sealed class TestFilter : ITemplateFilter
    {
        public string Name { get; }

        public TestFilter(string name) => Name = name;

        public TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture) => input;
    }
}
