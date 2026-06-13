using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for nested flex containers and leaf elements via the XML parser.
/// </summary>
public class XmlFlexNestingTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_NestedFlexWithChildren()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <flex direction="row" gap="8" justify="center">
                <text content="Left"/>
                <flex direction="column">
                  <text content="A"/>
                  <text content="B"/>
                </flex>
              </flex>
            </flexrender>
            """;

        var flex = Assert.IsType<FlexElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(FlexDirection.Row, flex.Direction);
        Assert.Equal(JustifyContent.Center, flex.Justify);
        Assert.Equal(2, flex.Children.Count);

        var inner = Assert.IsType<FlexElement>(flex.Children[1]);
        Assert.Equal(FlexDirection.Column, inner.Direction);
        Assert.Equal(2, inner.Children.Count);
        Assert.Equal("A", Assert.IsType<TextElement>(inner.Children[0]).Content);
    }

    [Fact]
    public void Parse_QrBarcodeImage()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <qr data="hello" size="120" errorCorrection="H"/>
              <barcode data="12345" width="200" height="80" format="ean13"/>
              <image src="logo.png" width="100" height="50" fit="cover"/>
            </flexrender>
            """;

        var elements = _parser.Parse(xml).Elements;
        var qr = Assert.IsType<QrElement>(elements[0]);
        Assert.Equal("hello", qr.Data.Value);
        Assert.Equal(ErrorCorrectionLevel.H, qr.ErrorCorrection.Value);

        var barcode = Assert.IsType<BarcodeElement>(elements[1]);
        Assert.Equal(BarcodeFormat.Ean13, barcode.Format.Value);

        var image = Assert.IsType<ImageElement>(elements[2]);
        Assert.Equal("logo.png", image.Src.Value);
        Assert.Equal(ImageFit.Cover, image.Fit.Value);
    }
}
