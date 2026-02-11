using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

public class AstModelTests
{
    [Fact]
    public void CanvasSettings_DefaultValues()
    {
        var canvas = new CanvasSettings();

        Assert.Equal(FixedDimension.Width, canvas.Fixed);
        Assert.Equal(300, canvas.Width);
        Assert.Equal(0, canvas.Height);
        Assert.Equal("#ffffff", canvas.Background.Value);
        Assert.Equal("none", canvas.Rotate.Value);
    }

    [Fact]
    public void CanvasSettings_CustomValues()
    {
        var canvas = new CanvasSettings
        {
            Fixed = FixedDimension.Height,
            Width = 300,
            Height = 500,
            Background = "#000000",
            Rotate = "right"
        };

        Assert.Equal(FixedDimension.Height, canvas.Fixed);
        Assert.Equal(300, canvas.Width);
        Assert.Equal(500, canvas.Height);
        Assert.Equal("#000000", canvas.Background.Value);
        Assert.Equal("right", canvas.Rotate.Value);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("flip")]
    [InlineData("90")]
    [InlineData("-45")]
    public void CanvasSettings_Rotate_AcceptsAllValidValues(string rotate)
    {
        var canvas = new CanvasSettings { Rotate = rotate };

        Assert.Equal(rotate, canvas.Rotate.Value);
    }

    [Fact]
    public void TextElement_DefaultValues()
    {
        var text = new TextElement();

        Assert.Equal("", text.Content.Value);
        Assert.Equal("main", text.Font.Value);
        Assert.Equal("1em", text.Size.Value);
        Assert.Equal("#000000", text.Color.Value);
        Assert.Equal(TextAlign.Left, text.Align.Value);
        Assert.True(text.Wrap.Value);
        Assert.Equal(TextOverflow.Ellipsis, text.Overflow.Value);
        Assert.Null(text.MaxLines.Value);
    }

    [Fact]
    public void TextElement_CustomValues()
    {
        var text = new TextElement
        {
            Content = "Hello {{name}}",
            Font = "bold",
            Size = "1.5em",
            Color = "#ff0000",
            Align = TextAlign.Center,
            Wrap = false,
            Overflow = TextOverflow.Clip,
            MaxLines = 2
        };

        Assert.Equal("Hello {{name}}", text.Content.Value);
        Assert.Equal("bold", text.Font.Value);
        Assert.Equal("1.5em", text.Size.Value);
        Assert.Equal("#ff0000", text.Color.Value);
        Assert.Equal(TextAlign.Center, text.Align.Value);
        Assert.False(text.Wrap.Value);
        Assert.Equal(TextOverflow.Clip, text.Overflow.Value);
        Assert.Equal(2, text.MaxLines.Value);
    }

    [Fact]
    public void TextElement_IsTemplateElement()
    {
        var text = new TextElement();

        Assert.IsAssignableFrom<TemplateElement>(text);
        Assert.Equal(ElementType.Text, text.Type);
    }

    [Fact]
    public void Template_ContainsCanvasAndElements()
    {
        var template = new Template
        {
            Name = "test-template",
            Version = 1,
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Line 1" },
                new TextElement { Content = "Line 2" }
            }
        };

        Assert.Equal("test-template", template.Name);
        Assert.Equal(1, template.Version);
        Assert.Equal(400, template.Canvas.Width);
        Assert.Equal(2, template.Elements.Count);
    }

    [Fact]
    public void Template_AddElement_AddsToCollection()
    {
        var template = new Template();

        template.AddElement(new TextElement { Content = "Element 1" });
        template.AddElement(new TextElement { Content = "Element 2" });

        Assert.Equal(2, template.Elements.Count);
    }

    [Fact]
    public void Template_AddElement_NullThrowsArgumentNullException()
    {
        var template = new Template();

        Assert.Throws<ArgumentNullException>(() => template.AddElement(null!));
    }

    [Fact]
    public void Template_Elements_IsReadOnly()
    {
        var template = new Template();
        template.AddElement(new TextElement { Content = "Test" });

        Assert.IsAssignableFrom<IReadOnlyList<TemplateElement>>(template.Elements);
    }

    [Fact]
    public void Template_Elements_SetterCopiesList()
    {
        var template = new Template();
        var elements = new List<TemplateElement>
        {
            new TextElement { Content = "Original" }
        };

        template.Elements = elements;

        // Modifying the original list should not affect the template
        elements.Add(new TextElement { Content = "Added" });

        Assert.Single(template.Elements);
    }
}
