using FlexRender.Parsing;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Integration;

public class WbReceiptIntegrationTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly TemplateParser _parser = new();

    public void Dispose()
    {
        _renderer.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WbReceipt_RendersWithBlackColumns()
    {
        // Layout: 630px total width with 8px gaps between 3 columns
        // Left column: 130px black, Center: 450px white, Right: 26px black
        // Positions: 0-130, 138-588, 596-622 (with 8px gaps at 130-138 and 588-596)
        const string yaml = """
            canvas:
              width: 630
              background: "#ffffff"
            layout:
              - type: flex
                direction: row
                gap: 8
                children:
                  - type: flex
                    width: 130
                    background: "#000000"
                    height: 200
                    children: []
                  - type: flex
                    width: 450
                    direction: column
                    padding: 12
                    children:
                      - type: text
                        content: "Пополнение счёта"
                        font: bold
                        size: 14px
                      - type: text
                        content: "5 000 ₽"
                        font: bold
                        size: 44px
                  - type: flex
                    width: 26
                    background: "#000000"
                    height: 200
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = new SKBitmap(630, 200);
        var exception = Record.Exception(() => _renderer.Render(bitmap, template, data));

        Assert.Null(exception);

        // Verify left column is black (at x=50, within 130px left column)
        var leftPixel = bitmap.GetPixel(50, 100);
        Assert.Equal(0, leftPixel.Red);
        Assert.Equal(0, leftPixel.Green);
        Assert.Equal(0, leftPixel.Blue);

        // Verify right column is black (at x=610, within the 26px right column starting at 596)
        var rightPixel = bitmap.GetPixel(610, 100);
        Assert.Equal(0, rightPixel.Red);
        Assert.Equal(0, rightPixel.Green);
        Assert.Equal(0, rightPixel.Blue);

        // Verify center area has white background (canvas color)
        var centerPixel = bitmap.GetPixel(315, 100);
        Assert.Equal(255, centerPixel.Red);
        Assert.Equal(255, centerPixel.Green);
        Assert.Equal(255, centerPixel.Blue);
    }

    [Fact]
    public void WbReceipt_TextWithBackgroundButton_Renders()
    {
        const string yaml = """
            canvas:
              width: 200
              background: "#ffffff"
            layout:
              - type: text
                content: "wb-bank.ru"
                font: bold
                size: 12px
                color: "#ffffff"
                background: "#000000"
                padding: 8
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = new SKBitmap(200, 50);
        var exception = Record.Exception(() => _renderer.Render(bitmap, template, data));

        Assert.Null(exception);

        // The text background should be black
        var pixel = bitmap.GetPixel(10, 15);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }
}
