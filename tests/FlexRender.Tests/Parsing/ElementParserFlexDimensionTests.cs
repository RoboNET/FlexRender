using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests that content-specific dimensions (BarcodeWidth, QR Size, ImageWidth/Height)
/// are propagated to flex Width/Height for proper layout centering.
/// </summary>
public sealed class ElementParserFlexDimensionTests
{
    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Verifies that a barcode element with explicit width/height propagates those values
    /// to the flex Width/Height properties for proper layout centering.
    /// </summary>
    [Fact]
    public void Parse_BarcodeWithWidth_SetsFlexWidth()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: barcode
                data: "TEST"
                width: 180
                height: 50
            """;

        var template = _parser.Parse(yaml);
        var barcode = Assert.IsType<BarcodeElement>(template.Elements[0]);

        Assert.Equal(180, barcode.BarcodeWidth);
        Assert.Equal("180", barcode.Width);
        Assert.Equal("50", barcode.Height);
    }

    /// <summary>
    /// Verifies that a QR element with explicit size propagates the value
    /// to both flex Width and Height (QR codes are square).
    /// </summary>
    [Fact]
    public void Parse_QrWithSize_SetsFlexWidthAndHeight()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: qr
                data: "test"
                size: 140
            """;

        var template = _parser.Parse(yaml);
        var qr = Assert.IsType<QrElement>(template.Elements[0]);

        Assert.Equal(140, qr.Size);
        Assert.Equal("140", qr.Width);
        Assert.Equal("140", qr.Height);
    }

    /// <summary>
    /// Verifies that an image element with explicit width/height propagates those values
    /// to the flex Width/Height properties for proper layout centering.
    /// </summary>
    [Fact]
    public void Parse_ImageWithDimensions_SetsFlexWidthAndHeight()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: image
                src: "test.png"
                width: 200
                height: 100
            """;

        var template = _parser.Parse(yaml);
        var image = Assert.IsType<ImageElement>(template.Elements[0]);

        Assert.Equal(200, image.ImageWidth);
        Assert.Equal("200", image.Width);
        Assert.Equal("100", image.Height);
    }

    /// <summary>
    /// Verifies that a barcode element without explicit width still gets the default
    /// BarcodeWidth (200) propagated to flex Width.
    /// </summary>
    [Fact]
    public void Parse_BarcodeWithoutWidth_FlexWidthIsDefault()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: barcode
                data: "TEST"
            """;

        var template = _parser.Parse(yaml);
        var barcode = Assert.IsType<BarcodeElement>(template.Elements[0]);

        // Default BarcodeWidth is 200
        Assert.Equal(200, barcode.BarcodeWidth);
        Assert.Equal("200", barcode.Width);
    }

    /// <summary>
    /// Verifies that a QR element without explicit size still gets the default
    /// Size (100) propagated to both flex Width and Height.
    /// </summary>
    [Fact]
    public void Parse_QrWithoutSize_FlexWidthIsDefault()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: qr
                data: "test"
            """;

        var template = _parser.Parse(yaml);
        var qr = Assert.IsType<QrElement>(template.Elements[0]);

        // Default QR Size is 100
        Assert.Equal(100, qr.Size);
        Assert.Equal("100", qr.Width);
        Assert.Equal("100", qr.Height);
    }

    /// <summary>
    /// Verifies that an image element without explicit dimensions leaves
    /// flex Width and Height as null (images can be auto-sized).
    /// </summary>
    [Fact]
    public void Parse_ImageWithoutDimensions_FlexWidthIsNull()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: image
                src: "test.png"
            """;

        var template = _parser.Parse(yaml);
        var image = Assert.IsType<ImageElement>(template.Elements[0]);

        Assert.Null(image.ImageWidth);
        Assert.Null(image.Width);
        Assert.Null(image.Height);
    }
}
