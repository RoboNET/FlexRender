using FlexRender.Content.Ndc;
using Xunit;

namespace FlexRender.Tests.Content.Ndc;

public sealed class NdcOptionsTests
{
    [Fact]
    public void FromDictionary_Null_ReturnsDefaults()
    {
        var options = NdcOptions.FromDictionary(null);

        Assert.Equal("latin1", options.InputEncoding);
        Assert.Equal(0.6, options.CharWidthRatio);
        Assert.Null(options.CanvasWidth);
        Assert.Empty(options.Charsets);
    }

    [Fact]
    public void FromDictionary_ParsesInputEncoding()
    {
        var dict = new Dictionary<string, object>
        {
            ["input_encoding"] = "cp866"
        };

        var options = NdcOptions.FromDictionary(dict);

        Assert.Equal("cp866", options.InputEncoding);
    }

    [Fact]
    public void FromDictionary_ParsesCharsets()
    {
        var dict = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["I"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold",
                    ["font_size"] = "14",
                    ["encoding"] = "qwerty-jcuken",
                    ["color"] = "#333"
                },
                ["1"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "default",
                    ["font_size"] = "12"
                }
            }
        };

        var options = NdcOptions.FromDictionary(dict);

        Assert.Equal(2, options.Charsets.Count);

        var charsetI = options.Charsets["I"];
        Assert.Equal("bold", charsetI.Font);
        Assert.Equal("bold", charsetI.FontStyle);
        Assert.Equal(14, charsetI.FontSize);
        Assert.Equal("qwerty-jcuken", charsetI.Encoding);
        Assert.Equal("#333", charsetI.Color);

        var charset1 = options.Charsets["1"];
        Assert.Equal("default", charset1.Font);
        Assert.Null(charset1.FontStyle);
        Assert.Equal(12, charset1.FontSize);
        Assert.Equal("none", charset1.Encoding);
        Assert.Null(charset1.Color);
    }

    [Fact]
    public void FromDictionary_ParsesCharWidthRatio()
    {
        var dict = new Dictionary<string, object>
        {
            ["char_width_ratio"] = "0.55"
        };

        var options = NdcOptions.FromDictionary(dict);

        Assert.Equal(0.55, options.CharWidthRatio);
    }

    [Fact]
    public void FromDictionary_CharWidthRatio_DefaultIs06()
    {
        var options = NdcOptions.FromDictionary(null);

        Assert.Equal(0.6, options.CharWidthRatio);
    }

    [Fact]
    public void FromDictionary_ParsesCanvasWidth()
    {
        var options = NdcOptions.FromDictionary(null, canvasWidth: 576);

        Assert.Equal(576, options.CanvasWidth);
    }

    [Fact]
    public void FromDictionary_CanvasWidth_DefaultIsNull()
    {
        var options = NdcOptions.FromDictionary(null);

        Assert.Null(options.CanvasWidth);
    }

    [Fact]
    public void AutoFontSize_WithCanvasWidth_CalculatesCorrectly()
    {
        var dict = new Dictionary<string, object>
        {
            ["columns"] = "40",
            ["char_width_ratio"] = "0.6"
        };

        var options = NdcOptions.FromDictionary(dict, canvasWidth: 576);

        // 576 / (40 * 0.6) = 24
        Assert.Equal(24, options.AutoFontSize);
    }

    [Fact]
    public void AutoFontSize_WithoutCanvasWidth_ReturnsNull()
    {
        var dict = new Dictionary<string, object>
        {
            ["columns"] = "40"
        };

        var options = NdcOptions.FromDictionary(dict);

        Assert.Null(options.AutoFontSize);
    }

    [Fact]
    public void AutoFontSize_CustomRatio_CalculatesCorrectly()
    {
        var dict = new Dictionary<string, object>
        {
            ["columns"] = "44",
            ["char_width_ratio"] = "0.55"
        };

        var options = NdcOptions.FromDictionary(dict, canvasWidth: 400);

        // 400 / (44 * 0.55) = 16.528... -> 16
        Assert.Equal(16, options.AutoFontSize);
    }

    [Fact]
    public void AutoFontSize_UsesMeasuredColumnsWhenSet()
    {
        var dict = new Dictionary<string, object>
        {
            ["columns"] = "40"
        };

        var options = NdcOptions.FromDictionary(dict, canvasWidth: 576)
            .WithMeasuredColumns(38); // actual data is narrower

        // 576 / (38 * 0.6) = 25.26... -> 25
        Assert.Equal(25, options.AutoFontSize);
    }

    [Fact]
    public void AutoFontSize_FallsBackToColumnsWhenNotMeasured()
    {
        var dict = new Dictionary<string, object>
        {
            ["columns"] = "40"
        };

        var options = NdcOptions.FromDictionary(dict, canvasWidth: 576);
        // MeasuredColumns not set -> uses Columns (40)

        // 576 / (40 * 0.6) = 24
        Assert.Equal(24, options.AutoFontSize);
    }

    [Fact]
    public void GetStyleForCharset_ReturnsConfigured()
    {
        var dict = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["I"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font"] = "bold",
                    ["font_style"] = "bold"
                }
            }
        };

        var options = NdcOptions.FromDictionary(dict);
        var style = options.GetStyleForCharset("I");

        Assert.Equal("bold", style.Font);
        Assert.Equal("bold", style.FontStyle);
    }

    [Fact]
    public void GetStyleForCharset_UnknownDesignator_ReturnsDefault()
    {
        var options = NdcOptions.FromDictionary(null);
        var style = options.GetStyleForCharset("Z");

        Assert.Null(style.Font);
        Assert.Null(style.FontStyle);
        Assert.Null(style.FontSize);
        Assert.Equal("none", style.Encoding);
    }

    [Fact]
    public void FromDictionary_LegacyBold_MapsToFontAndFontStyle()
    {
        var dict = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["I"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["bold"] = "true"
                }
            }
        };

        var options = NdcOptions.FromDictionary(dict);
        var style = options.GetStyleForCharset("I");

        Assert.Equal("bold", style.Font);
        Assert.Equal("bold", style.FontStyle);
    }

    [Fact]
    public void FromDictionary_LegacyBoldFalse_NoFontOrStyle()
    {
        var dict = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["bold"] = "false"
                }
            }
        };

        var options = NdcOptions.FromDictionary(dict);
        var style = options.GetStyleForCharset("1");

        Assert.Null(style.Font);
        Assert.Null(style.FontStyle);
    }

    [Fact]
    public void FromDictionary_ParsesFontFamily()
    {
        var dict = new Dictionary<string, object>
        {
            ["charsets"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["font_family"] = "JetBrains Mono"
                }
            }
        };

        var options = NdcOptions.FromDictionary(dict);
        var style = options.GetStyleForCharset("1");

        Assert.Equal("JetBrains Mono", style.FontFamily);
    }
}
