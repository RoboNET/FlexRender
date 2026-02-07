using FlexRender.Barcode.ImageSharp;
using FlexRender.Configuration;
using FlexRender.ImageSharp;
using FlexRender.QrCode.ImageSharp;
using Xunit;

namespace FlexRender.ImageSharp.Tests;

public sealed class ImageSharpBuilderExtensionTests
{
    [Fact]
    public void WithQr_EnablesQrOnBuilder()
    {
        var builder = new ImageSharpBuilder();
        var result = builder.WithQr();

        Assert.Same(builder, result);
        Assert.NotNull(builder.QrProvider);
    }

    [Fact]
    public void WithBarcode_EnablesBarcodeOnBuilder()
    {
        var builder = new ImageSharpBuilder();
        var result = builder.WithBarcode();

        Assert.Same(builder, result);
        Assert.NotNull(builder.BarcodeProvider);
    }

    [Fact]
    public void WithQr_NullBuilder_Throws()
    {
        ImageSharpBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.WithQr());
    }

    [Fact]
    public void WithBarcode_NullBuilder_Throws()
    {
        ImageSharpBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.WithBarcode());
    }

    [Fact]
    public void FluentChaining_AllProviders()
    {
        var builder = new ImageSharpBuilder();
        builder.WithQr().WithBarcode();

        Assert.NotNull(builder.QrProvider);
        Assert.NotNull(builder.BarcodeProvider);
    }

    [Fact]
    public void WithImageSharp_WithQr_BuildsSuccessfully()
    {
        using var render = new FlexRenderBuilder()
            .WithImageSharp(imageSharp => imageSharp.WithQr())
            .Build();

        Assert.NotNull(render);
    }

    [Fact]
    public void WithImageSharp_WithAllProviders_BuildsSuccessfully()
    {
        using var render = new FlexRenderBuilder()
            .WithImageSharp(imageSharp => imageSharp
                .WithQr()
                .WithBarcode())
            .Build();

        Assert.NotNull(render);
    }
}
