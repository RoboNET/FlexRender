using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests that canvas modes work correctly with intrinsic sizing.
/// </summary>
public class IntrinsicFallbackTests
{
    private readonly LayoutEngine _engine = new();

    [Fact]
    public void Canvas_FixedBoth_UsesExplicitDimensions()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Both, Width = 400, Height = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Height = "50" }
            }
        };

        var root = _engine.ComputeLayout(template);

        Assert.Equal(400f, root.Width);
        Assert.Equal(300f, root.Height);
    }

    [Fact]
    public void Canvas_FixedNone_SizesFromContent()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.None },
            Elements = new List<TemplateElement>
            {
                new QrElement { Data = "test", Size = 100 }
            }
        };

        var root = _engine.ComputeLayout(template);

        // Canvas should shrink to content
        Assert.True(root.Width > 0, "Width should be positive when fixed=none");
        Assert.True(root.Height > 0, "Height should be positive when fixed=none");
        Assert.True(root.Width <= 200, $"Width {root.Width} should not be huge for a single 100px QR");
        Assert.True(root.Height <= 200, $"Height {root.Height} should not be huge for a single 100px QR");
    }

    [Fact]
    public void Canvas_FixedHeight_WidthFromContent()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Height, Height = 500 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Children = new List<TemplateElement>
                    {
                        new QrElement { Data = "a", Size = 80 },
                        new QrElement { Data = "b", Size = 80 }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);

        Assert.Equal(500f, root.Height);
        // Width should come from content: row of two 80px QRs = 160
        Assert.True(root.Width > 0, "Width should be positive");
        Assert.True(root.Width <= 300, $"Width {root.Width} should be close to 160 for two 80px QRs in a row");
    }

    [Fact]
    public void Canvas_FixedWidth_HeightFromContent()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Width, Width = 300 },
            Elements = new List<TemplateElement>
            {
                new QrElement { Data = "test", Size = 100 },
                new TextElement { Content = "Hello", Height = "30" }
            }
        };

        var root = _engine.ComputeLayout(template);

        Assert.Equal(300f, root.Width);
        // Height from content: QR(100) + Text(30) = 130
        Assert.True(root.Height >= 130f, $"Root height {root.Height} should be >= 130");
    }

    [Fact]
    public void Canvas_FixedBoth_WithoutHeight_Throws()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Both, Width = 400 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello" }
            }
        };

        Assert.Throws<InvalidOperationException>(() => _engine.ComputeLayout(template));
    }
}
