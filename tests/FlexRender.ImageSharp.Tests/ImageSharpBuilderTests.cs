using FlexRender.Barcode.ImageSharp;
using FlexRender.QrCode.ImageSharp;
using Xunit;

namespace FlexRender.ImageSharp.Tests;

public sealed class ImageSharpBuilderTests
{
    [Fact]
    public void WithQr_SetsQrProvider()
    {
        var builder = new ImageSharpBuilder();
        builder.WithQr();
        Assert.NotNull(builder.QrProvider);
    }

    [Fact]
    public void WithBarcode_SetsBarcodeProvider()
    {
        var builder = new ImageSharpBuilder();
        builder.WithBarcode();
        Assert.NotNull(builder.BarcodeProvider);
    }

    [Fact]
    public void DefaultBuilder_HasNoProviders()
    {
        var builder = new ImageSharpBuilder();
        Assert.Null(builder.QrProvider);
        Assert.Null(builder.BarcodeProvider);
    }

    [Fact]
    public void WithQr_CalledTwice_Throws()
    {
        var builder = new ImageSharpBuilder();
        builder.WithQr();
        Assert.Throws<InvalidOperationException>(() => builder.WithQr());
    }

    [Fact]
    public void WithBarcode_CalledTwice_Throws()
    {
        var builder = new ImageSharpBuilder();
        builder.WithBarcode();
        Assert.Throws<InvalidOperationException>(() => builder.WithBarcode());
    }
}
