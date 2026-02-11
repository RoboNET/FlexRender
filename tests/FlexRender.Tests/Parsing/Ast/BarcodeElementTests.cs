using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for BarcodeElement AST model.
/// </summary>
public class BarcodeElementTests
{
    /// <summary>
    /// Verifies default values are set correctly.
    /// </summary>
    [Fact]
    public void BarcodeElement_DefaultValues()
    {
        var barcode = new BarcodeElement();

        Assert.Equal("", barcode.Data);
        Assert.Equal(BarcodeFormat.Code128, barcode.Format);
        Assert.Null(barcode.BarcodeWidth.Value); // Changed: BarcodeWidth is now nullable, defaults to null (will inherit from container)
        Assert.Null(barcode.BarcodeHeight.Value); // Changed: BarcodeHeight is now nullable, defaults to null (will inherit from container)
        Assert.True(barcode.ShowText.Value);
        Assert.Equal("#000000", barcode.Foreground);
        Assert.Null(barcode.Background.Value);
        Assert.Equal("none", barcode.Rotate);
    }

    /// <summary>
    /// Verifies custom values can be set.
    /// </summary>
    [Fact]
    public void BarcodeElement_CustomValues()
    {
        var barcode = new BarcodeElement
        {
            Data = "ABC123",
            Format = BarcodeFormat.Code39,
            BarcodeWidth = 300,
            BarcodeHeight = 100,
            ShowText = false,
            Foreground = "#0000ff",
            Background = "#ffff00",
            Rotate = "left"
        };

        Assert.Equal("ABC123", barcode.Data);
        Assert.Equal(BarcodeFormat.Code39, barcode.Format);
        Assert.Equal(300, barcode.BarcodeWidth);
        Assert.Equal(100, barcode.BarcodeHeight);
        Assert.False(barcode.ShowText.Value);
        Assert.Equal("#0000ff", barcode.Foreground);
        Assert.Equal("#ffff00", barcode.Background);
        Assert.Equal("left", barcode.Rotate);
    }

    /// <summary>
    /// Verifies BarcodeElement has correct ElementType.
    /// </summary>
    [Fact]
    public void BarcodeElement_HasCorrectType()
    {
        var barcode = new BarcodeElement();

        Assert.IsAssignableFrom<TemplateElement>(barcode);
        Assert.Equal(ElementType.Barcode, barcode.Type);
    }

    /// <summary>
    /// Verifies all barcode formats are available.
    /// </summary>
    [Theory]
    [InlineData(BarcodeFormat.Code128)]
    [InlineData(BarcodeFormat.Code39)]
    [InlineData(BarcodeFormat.Ean13)]
    [InlineData(BarcodeFormat.Ean8)]
    [InlineData(BarcodeFormat.Upc)]
    public void BarcodeFormat_AllFormatsExist(BarcodeFormat format)
    {
        var barcode = new BarcodeElement { Format = format };
        Assert.Equal(format, barcode.Format);
    }

    /// <summary>
    /// Verifies flex item properties have correct defaults.
    /// </summary>
    [Fact]
    public void BarcodeElement_FlexItemProperties_DefaultValues()
    {
        var barcode = new BarcodeElement();

        Assert.Equal(0f, barcode.Grow);
        Assert.Equal(1f, barcode.Shrink);
        Assert.Equal("auto", barcode.Basis);
        Assert.Equal(AlignSelf.Auto, barcode.AlignSelf);
        Assert.Equal(0, barcode.Order);
        Assert.Null(barcode.Width.Value);
        Assert.Null(barcode.Height.Value);
    }
}
