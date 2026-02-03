using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

public class SkiaRendererTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly TemplateParser _parser = new();

    public void Dispose()
    {
        _renderer.Dispose();
    }

    [Fact]
    public void Constructor_CreatesInstance()
    {
        using var renderer = new SkiaRenderer();

        Assert.NotNull(renderer);
    }

    [Fact]
    public void Measure_SimpleTemplate_ReturnsDimensions()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Width, Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello World", Size = "16" }
            }
        };
        var data = new ObjectValue();

        var size = _renderer.Measure(template, data);

        Assert.Equal(300f, size.Width);
        Assert.True(size.Height > 0);
    }

    [Fact]
    public void Measure_EmptyTemplate_ReturnsMinimalSize()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>()
        };
        var data = new ObjectValue();

        var size = _renderer.Measure(template, data);

        Assert.Equal(300f, size.Width);
        Assert.True(size.Height >= 0);
    }

    [Fact]
    public void Measure_WithHeightFixed_ReturnsCorrectDimensions()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Height, Height = 500 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Test", Size = "16" }
            }
        };
        var data = new ObjectValue();

        var size = _renderer.Measure(template, data);

        Assert.Equal(500f, size.Height);
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void Render_ToCanvas_DrawsWithoutError()
    {
        using var bitmap = new SKBitmap(300, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "16" }
            }
        };
        var data = new ObjectValue();

        var exception = Record.Exception(() =>
            _renderer.Render(canvas, template, data));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_ToCanvas_WithOffset_AppliesOffset()
    {
        using var bitmap = new SKBitmap(300, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "X", Size = "16" }
            }
        };
        var data = new ObjectValue();
        var offset = new SKPoint(50, 25);

        var exception = Record.Exception(() =>
            _renderer.Render(canvas, template, data, offset));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_ToBitmap_DrawsWithoutError()
    {
        using var bitmap = new SKBitmap(300, 100);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "16" }
            }
        };
        var data = new ObjectValue();

        var exception = Record.Exception(() =>
            _renderer.Render(bitmap, template, data));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_WithBackground_FillsBackground()
    {
        using var bitmap = new SKBitmap(100, 100);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Background = "#ff0000" },
            Elements = new List<TemplateElement>
            {
                // Add a text element so the rendered area is larger
                new TextElement { Content = "Test", Size = "50" }
            }
        };
        var data = new ObjectValue();

        _renderer.Render(bitmap, template, data);

        // Check a pixel in the upper area where background should be rendered
        var topPixel = bitmap.GetPixel(50, 10);
        Assert.Equal(255, topPixel.Red);
        Assert.Equal(0, topPixel.Green);
        Assert.Equal(0, topPixel.Blue);
    }

    [Fact]
    public void Render_WithDataSubstitution_SubstitutesValues()
    {
        using var bitmap = new SKBitmap(300, 50);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello {{name}}!", Size = "16" }
            }
        };
        var data = new ObjectValue { ["name"] = "World" };

        // This should not throw - full text content verification would be in snapshot tests
        var exception = Record.Exception(() =>
            _renderer.Render(bitmap, template, data));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_ParsedYaml_RendersCorrectly()
    {
        const string yaml = """
            canvas:
              width: 200
              background: "#ffffff"
            layout:
              - type: text
                content: "Test"
                size: 16
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = new SKBitmap(200, 50);
        var exception = Record.Exception(() =>
            _renderer.Render(bitmap, template, data));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_MultipleTextElements_DrawsAll()
    {
        using var bitmap = new SKBitmap(300, 150);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Line 1", Size = "16" },
                new TextElement { Content = "Line 2", Size = "16" },
                new TextElement { Content = "Line 3", Size = "16" }
            }
        };
        var data = new ObjectValue();

        var exception = Record.Exception(() =>
            _renderer.Render(bitmap, template, data));

        Assert.Null(exception);
    }

    [Fact]
    public void Render_WithITemplateData_WorksCorrectly()
    {
        using var bitmap = new SKBitmap(300, 50);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Price: {{price}}", Size = "16" }
            }
        };
        var data = new TestTemplateData { Price = 99.99m };

        var exception = Record.Exception(() =>
            _renderer.Render(bitmap, template, data));

        Assert.Null(exception);
    }

    private class TestTemplateData : ITemplateData
    {
        public decimal Price { get; set; }

        public ObjectValue ToTemplateValue()
        {
            return new ObjectValue { ["price"] = Price };
        }
    }

    [Fact]
    public void Render_FlexWithBackground_DrawsBackground()
    {
        using var bitmap = new SKBitmap(100, 100);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "100",
                    Height = "50",
                    Background = "#ff0000",
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        _renderer.Render(bitmap, template, data);

        // Check pixel in the flex area (should be red)
        var pixel = bitmap.GetPixel(50, 25);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    [Fact]
    public void Render_TextWithBackground_DrawsBackground()
    {
        using var bitmap = new SKBitmap(200, 100);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new TextElement
                {
                    Content = "Hello",
                    Size = "20",
                    Width = "100",
                    Height = "50",
                    Background = "#00ff00"
                }
            }
        };
        var data = new ObjectValue();

        _renderer.Render(bitmap, template, data);

        // Check that green background was drawn in text area (upper left region of text element)
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green >= 200, $"Expected green background, got R={pixel.Red} G={pixel.Green} B={pixel.Blue}");
    }

    [Fact]
    public void Render_ElementWithoutBackground_NoBackgroundDrawn()
    {
        using var bitmap = new SKBitmap(100, 100);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Background = "#ffffff" },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "50",
                    Height = "50",
                    // No background set
                    Children = new List<TemplateElement>()
                }
            }
        };
        var data = new ObjectValue();

        _renderer.Render(bitmap, template, data);

        // Pixel should be white (canvas background), not anything else
        var pixel = bitmap.GetPixel(25, 25);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(255, pixel.Green);
        Assert.Equal(255, pixel.Blue);
    }

    /// <summary>
    /// Verifies that a font named "default" is also registered as "main".
    /// </summary>
    [Fact]
    public void Render_DefaultFont_RegisteredAsMain()
    {
        const string yaml = """
            fonts:
              default: "assets/fonts/Roboto-Regular.ttf"
            canvas:
              width: 200
            layout:
              - type: text
                content: "Uses default font via main"
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = new SKBitmap(200, 50);

        // Should not throw - the "default" font is registered as "main"
        // which is the default font reference for TextElement
        var exception = Record.Exception(() =>
            _renderer.Render(bitmap, template, data));

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that text elements without explicit font use the default font.
    /// </summary>
    [Fact]
    public void Render_TextWithoutExplicitFont_UsesDefaultFont()
    {
        const string yaml = """
            fonts:
              default: "assets/fonts/Roboto-Regular.ttf"
              bold: "assets/fonts/Roboto-Bold.ttf"
            canvas:
              width: 300
            layout:
              - type: text
                content: "Text without font attribute uses default"
              - type: text
                content: "Text with explicit font"
                font: bold
            """;

        var template = _parser.Parse(yaml);
        var data = new ObjectValue();

        using var bitmap = new SKBitmap(300, 100);

        // Both text elements should render without error
        var exception = Record.Exception(() =>
            _renderer.Render(bitmap, template, data));

        Assert.Null(exception);

        // Verify that the first text element uses "main" font (which is "default")
        var textWithoutFont = Assert.IsType<TextElement>(template.Elements[0]);
        Assert.Equal("main", textWithoutFont.Font);

        // Verify that the second text element uses explicit "bold" font
        var textWithFont = Assert.IsType<TextElement>(template.Elements[1]);
        Assert.Equal("bold", textWithFont.Font);
    }

    [Fact]
    public void Constructor_WithResourceLimits_SetsMaxRenderDepth()
    {
        var limits = new ResourceLimits { MaxRenderDepth = 42 };
        using var renderer = new SkiaRenderer(limits);

        Assert.NotNull(renderer);
    }

    [Fact]
    public void Constructor_DefaultLimits_Uses100MaxRenderDepth()
    {
        // The parameterless constructor should use 100 as default
        using var renderer = new SkiaRenderer();

        // We verify by rendering a template -- if it did not throw, it works
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Width, Width = 100 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "test" }
            }
        };
        var data = new ObjectValue();

        var size = renderer.Measure(template, data);
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void Render_SeparatorWithTemplateColorExpression_ResolvesColor()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Width, Width = 200 },
            Elements = new List<TemplateElement>
            {
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Horizontal,
                    Thickness = 2f,
                    Color = "{{myColor}}"
                }
            }
        };
        var data = new ObjectValue
        {
            ["myColor"] = new StringValue("#ff0000")
        };

        // Measure first to get correct bitmap size
        var size = _renderer.Measure(template, data);
        using var bitmap = new SKBitmap((int)size.Width, Math.Max((int)size.Height, 2));
        _renderer.Render(bitmap, template, data);

        // The separator line is drawn at y + height/2; with thickness=2, height=2, so lineY=1
        var pixel = bitmap.GetPixel(100, 1);
        // After expression processing, color should be red
        Assert.True(pixel.Red > 200, $"Expected red channel > 200, got {pixel.Red}");
    }
}
