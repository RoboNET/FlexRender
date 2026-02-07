using FlexRender.Barcode.Providers;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.QrCode.Providers;
using FlexRender.SvgElement.Providers;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Compile-time verification that Skia providers implement <see cref="ISkiaNativeProvider{TElement}"/>.
/// These tests use explicit interface variable assignments to verify at compile time
/// rather than relying on reflection.
/// </summary>
public sealed class SkiaNativeProviderInterfaceTests
{
    [Fact]
    public void QrProvider_ImplementsISkiaNativeProvider()
    {
        var provider = new QrProvider();
        ISkiaNativeProvider<QrElement> nativeProvider = provider;

        var element = new QrElement { Data = "test", Size = 50 };
        using var bitmap = nativeProvider.GenerateBitmap(element, 50, 50);

        Assert.NotNull(bitmap);
        Assert.IsType<SKBitmap>(bitmap);
    }

    [Fact]
    public void BarcodeProvider_ImplementsISkiaNativeProvider()
    {
        var provider = new BarcodeProvider();
        ISkiaNativeProvider<BarcodeElement> nativeProvider = provider;

        var element = new BarcodeElement { Data = "ABC123" };
        using var bitmap = nativeProvider.GenerateBitmap(element, 200, 80);

        Assert.NotNull(bitmap);
        Assert.IsType<SKBitmap>(bitmap);
    }

    [Fact]
    public void SvgElementProvider_ImplementsISkiaNativeProvider()
    {
        var provider = new SvgElementProvider();
        ISkiaNativeProvider<FlexRender.Parsing.Ast.SvgElement> nativeProvider = provider;

        var element = new FlexRender.Parsing.Ast.SvgElement
        {
            Content = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"50\" height=\"50\"><rect width=\"50\" height=\"50\" fill=\"red\"/></svg>"
        };
        using var bitmap = nativeProvider.GenerateBitmap(element, 50, 50);

        Assert.NotNull(bitmap);
        Assert.IsType<SKBitmap>(bitmap);
    }
}
