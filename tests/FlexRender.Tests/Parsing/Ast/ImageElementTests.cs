using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for ImageElement AST model.
/// </summary>
public class ImageElementTests
{
    /// <summary>
    /// Verifies default values are set correctly.
    /// </summary>
    [Fact]
    public void ImageElement_DefaultValues()
    {
        var image = new ImageElement();

        Assert.Equal("", image.Src);
        Assert.Null(image.ImageWidth.Value);
        Assert.Null(image.ImageHeight.Value);
        Assert.Equal(ImageFit.Contain, image.Fit);
        Assert.Equal("none", image.Rotate);
    }

    /// <summary>
    /// Verifies custom values can be set.
    /// </summary>
    [Fact]
    public void ImageElement_CustomValues()
    {
        var image = new ImageElement
        {
            Src = "/path/to/image.png",
            ImageWidth = 200,
            ImageHeight = 150,
            Fit = ImageFit.Cover,
            Rotate = "flip"
        };

        Assert.Equal("/path/to/image.png", image.Src);
        Assert.Equal(200, image.ImageWidth);
        Assert.Equal(150, image.ImageHeight);
        Assert.Equal(ImageFit.Cover, image.Fit);
        Assert.Equal("flip", image.Rotate);
    }

    /// <summary>
    /// Verifies ImageElement has correct ElementType.
    /// </summary>
    [Fact]
    public void ImageElement_HasCorrectType()
    {
        var image = new ImageElement();

        Assert.IsAssignableFrom<TemplateElement>(image);
        Assert.Equal(ElementType.Image, image.Type);
    }

    /// <summary>
    /// Verifies all image fit modes are available.
    /// </summary>
    [Theory]
    [InlineData(ImageFit.Fill)]
    [InlineData(ImageFit.Contain)]
    [InlineData(ImageFit.Cover)]
    [InlineData(ImageFit.None)]
    public void ImageFit_AllModesExist(ImageFit fit)
    {
        var image = new ImageElement { Fit = fit };
        Assert.Equal(fit, image.Fit);
    }

    /// <summary>
    /// Verifies flex item properties have correct defaults.
    /// </summary>
    [Fact]
    public void ImageElement_FlexItemProperties_DefaultValues()
    {
        var image = new ImageElement();

        Assert.Equal(0f, image.Grow);
        Assert.Equal(1f, image.Shrink);
        Assert.Equal("auto", image.Basis);
        Assert.Equal(AlignSelf.Auto, image.AlignSelf);
        Assert.Equal(0, image.Order);
        Assert.Null(image.Width.Value);
        Assert.Null(image.Height.Value);
    }

    /// <summary>
    /// Verifies base64 data URL can be used as source.
    /// </summary>
    [Fact]
    public void ImageElement_AcceptsBase64DataUrl()
    {
        var dataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

        var image = new ImageElement { Src = dataUrl };

        Assert.Equal(dataUrl, image.Src);
    }
}
