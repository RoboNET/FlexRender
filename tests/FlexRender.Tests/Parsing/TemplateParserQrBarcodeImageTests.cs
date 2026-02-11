using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for TemplateParser QR, Barcode, and Image element parsing.
/// </summary>
public class TemplateParserQrBarcodeImageTests
{
    private readonly TemplateParser _parser = new();

    #region QR Element Parsing

    /// <summary>
    /// Verifies QR element parsing with minimal configuration.
    /// </summary>
    [Fact]
    public void Parse_QrElement_MinimalConfig()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: qr
                data: "https://example.com"
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var qr = Assert.IsType<QrElement>(template.Elements[0]);
        Assert.Equal("https://example.com", qr.Data);
        Assert.Equal(100, qr.Size);
        Assert.Equal(ErrorCorrectionLevel.M, qr.ErrorCorrection);
    }

    /// <summary>
    /// Verifies QR element parsing with all properties.
    /// </summary>
    [Fact]
    public void Parse_QrElement_AllProperties()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: qr
                data: "Custom QR Data"
                size: 200
                errorCorrection: H
                foreground: "#ff0000"
                background: "#00ff00"
                rotate: right
            """;

        var template = _parser.Parse(yaml);

        var qr = Assert.IsType<QrElement>(template.Elements[0]);
        Assert.Equal("Custom QR Data", qr.Data);
        Assert.Equal(200, qr.Size);
        Assert.Equal(ErrorCorrectionLevel.H, qr.ErrorCorrection);
        Assert.Equal("#ff0000", qr.Foreground);
        Assert.Equal("#00ff00", qr.Background);
        Assert.Equal("right", qr.Rotate);
    }

    /// <summary>
    /// Verifies all error correction levels can be parsed.
    /// </summary>
    [Theory]
    [InlineData("L", ErrorCorrectionLevel.L)]
    [InlineData("M", ErrorCorrectionLevel.M)]
    [InlineData("Q", ErrorCorrectionLevel.Q)]
    [InlineData("H", ErrorCorrectionLevel.H)]
    [InlineData("l", ErrorCorrectionLevel.L)]
    [InlineData("m", ErrorCorrectionLevel.M)]
    public void Parse_QrElement_ErrorCorrectionLevels(string yamlValue, ErrorCorrectionLevel expected)
    {
        var yaml = $"""
            canvas:
              width: 300
            layout:
              - type: qr
                data: "test"
                errorCorrection: {yamlValue}
            """;

        var template = _parser.Parse(yaml);

        var qr = Assert.IsType<QrElement>(template.Elements[0]);
        Assert.Equal(expected, qr.ErrorCorrection);
    }

    #endregion

    #region Barcode Element Parsing

    /// <summary>
    /// Verifies barcode element parsing with minimal configuration.
    /// </summary>
    [Fact]
    public void Parse_BarcodeElement_MinimalConfig()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: barcode
                data: "ABC123"
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var barcode = Assert.IsType<BarcodeElement>(template.Elements[0]);
        Assert.Equal("ABC123", barcode.Data);
        Assert.Equal(BarcodeFormat.Code128, barcode.Format);
        Assert.Equal(200, barcode.BarcodeWidth);
        Assert.Equal(80, barcode.BarcodeHeight);
    }

    /// <summary>
    /// Verifies barcode element parsing with all properties.
    /// </summary>
    [Fact]
    public void Parse_BarcodeElement_AllProperties()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: barcode
                data: "CUSTOM-DATA"
                format: code128
                width: 300
                height: 100
                showText: false
                foreground: "#0000ff"
                background: "#ffff00"
                rotate: left
            """;

        var template = _parser.Parse(yaml);

        var barcode = Assert.IsType<BarcodeElement>(template.Elements[0]);
        Assert.Equal("CUSTOM-DATA", barcode.Data);
        Assert.Equal(BarcodeFormat.Code128, barcode.Format);
        Assert.Equal(300, barcode.BarcodeWidth);
        Assert.Equal(100, barcode.BarcodeHeight);
        Assert.False(barcode.ShowText.Value);
        Assert.Equal("#0000ff", barcode.Foreground);
        Assert.Equal("#ffff00", barcode.Background);
        Assert.Equal("left", barcode.Rotate);
    }

    /// <summary>
    /// Verifies all barcode formats can be parsed.
    /// </summary>
    [Theory]
    [InlineData("code128", BarcodeFormat.Code128)]
    [InlineData("code39", BarcodeFormat.Code39)]
    [InlineData("ean13", BarcodeFormat.Ean13)]
    [InlineData("ean8", BarcodeFormat.Ean8)]
    [InlineData("upc", BarcodeFormat.Upc)]
    [InlineData("CODE128", BarcodeFormat.Code128)]
    public void Parse_BarcodeElement_AllFormats(string yamlValue, BarcodeFormat expected)
    {
        var yaml = $"""
            canvas:
              width: 300
            layout:
              - type: barcode
                data: "test"
                format: {yamlValue}
            """;

        var template = _parser.Parse(yaml);

        var barcode = Assert.IsType<BarcodeElement>(template.Elements[0]);
        Assert.Equal(expected, barcode.Format);
    }

    #endregion

    #region Image Element Parsing

    /// <summary>
    /// Verifies image element parsing with minimal configuration.
    /// </summary>
    [Fact]
    public void Parse_ImageElement_MinimalConfig()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: image
                src: "/path/to/image.png"
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var image = Assert.IsType<ImageElement>(template.Elements[0]);
        Assert.Equal("/path/to/image.png", image.Src);
        Assert.Null(image.ImageWidth.Value);
        Assert.Null(image.ImageHeight.Value);
        Assert.Equal(ImageFit.Contain, image.Fit);
    }

    /// <summary>
    /// Verifies image element parsing with all properties.
    /// </summary>
    [Fact]
    public void Parse_ImageElement_AllProperties()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: image
                src: "/path/to/image.png"
                width: 150
                height: 100
                fit: cover
                rotate: flip
            """;

        var template = _parser.Parse(yaml);

        var image = Assert.IsType<ImageElement>(template.Elements[0]);
        Assert.Equal("/path/to/image.png", image.Src);
        Assert.Equal(150, image.ImageWidth);
        Assert.Equal(100, image.ImageHeight);
        Assert.Equal(ImageFit.Cover, image.Fit);
        Assert.Equal("flip", image.Rotate);
    }

    /// <summary>
    /// Verifies all image fit modes can be parsed.
    /// </summary>
    [Theory]
    [InlineData("fill", ImageFit.Fill)]
    [InlineData("contain", ImageFit.Contain)]
    [InlineData("cover", ImageFit.Cover)]
    [InlineData("none", ImageFit.None)]
    [InlineData("COVER", ImageFit.Cover)]
    public void Parse_ImageElement_AllFitModes(string yamlValue, ImageFit expected)
    {
        var yaml = $"""
            canvas:
              width: 300
            layout:
              - type: image
                src: "test.png"
                fit: {yamlValue}
            """;

        var template = _parser.Parse(yaml);

        var image = Assert.IsType<ImageElement>(template.Elements[0]);
        Assert.Equal(expected, image.Fit);
    }

    /// <summary>
    /// Verifies image element parsing with base64 data URL.
    /// </summary>
    [Fact]
    public void Parse_ImageElement_Base64DataUrl()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: image
                src: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUg=="
            """;

        var template = _parser.Parse(yaml);

        var image = Assert.IsType<ImageElement>(template.Elements[0]);
        Assert.StartsWith("data:image/png;base64,", image.Src.Value);
    }

    /// <summary>
    /// Verifies image element parsing with background property.
    /// </summary>
    [Fact]
    public void Parse_ImageWithBackground_ParsesBackground()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: image
                src: "test.png"
                background: "#cccccc"
            """;

        var template = _parser.Parse(yaml);
        var image = template.Elements[0] as ImageElement;

        Assert.NotNull(image);
        Assert.Equal("#cccccc", image.Background);
    }

    /// <summary>
    /// Verifies image element parsing with padding property.
    /// </summary>
    [Fact]
    public void Parse_ImageWithPadding_ParsesPadding()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: image
                src: "test.png"
                padding: "10"
            """;

        var template = _parser.Parse(yaml);
        var image = template.Elements[0] as ImageElement;

        Assert.NotNull(image);
        Assert.Equal("10", image.Padding);
    }

    /// <summary>
    /// Verifies image element parsing with margin property.
    /// </summary>
    [Fact]
    public void Parse_ImageWithMargin_ParsesMargin()
    {
        const string yaml = """
            canvas:
              width: 100
            layout:
              - type: image
                src: "test.png"
                margin: "5px"
            """;

        var template = _parser.Parse(yaml);
        var image = template.Elements[0] as ImageElement;

        Assert.NotNull(image);
        Assert.Equal("5px", image.Margin);
    }

    #endregion

    #region Mixed Elements

    /// <summary>
    /// Verifies parsing template with mixed element types.
    /// </summary>
    [Fact]
    public void Parse_MixedElements_AllTypesSupported()
    {
        var yaml = """
            canvas:
              width: 500
            layout:
              - type: text
                content: "Product Label"
              - type: qr
                data: "https://example.com/product/123"
              - type: barcode
                data: "123456789"
              - type: image
                src: "/logo.png"
            """;

        var template = _parser.Parse(yaml);

        Assert.Equal(4, template.Elements.Count);
        Assert.IsType<TextElement>(template.Elements[0]);
        Assert.IsType<QrElement>(template.Elements[1]);
        Assert.IsType<BarcodeElement>(template.Elements[2]);
        Assert.IsType<ImageElement>(template.Elements[3]);
    }

    /// <summary>
    /// Verifies supported element types include new types.
    /// </summary>
    [Fact]
    public void SupportedElementTypes_IncludesNewTypes()
    {
        var supported = _parser.SupportedElementTypes;

        Assert.Contains("text", supported, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("qr", supported, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("barcode", supported, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("image", supported, StringComparer.OrdinalIgnoreCase);
    }

    #endregion
}
