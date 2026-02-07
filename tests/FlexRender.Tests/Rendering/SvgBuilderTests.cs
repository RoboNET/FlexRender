using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Svg;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for the SvgBuilder with SVG-native provider slots and raster backend configuration.
/// </summary>
public sealed class SvgBuilderTests
{
    [Fact]
    public void SvgBuilder_SetQrSvgProvider_StoresProvider()
    {
        var builder = new SvgBuilder();
        var provider = new FakeQrSvgProvider();

        builder.SetQrSvgProvider(provider);

        Assert.Same(provider, builder.QrSvgProvider);
    }

    [Fact]
    public void SvgBuilder_SetBarcodeSvgProvider_StoresProvider()
    {
        var builder = new SvgBuilder();
        var provider = new FakeBarcodeSvgProvider();

        builder.SetBarcodeSvgProvider(provider);

        Assert.Same(provider, builder.BarcodeSvgProvider);
    }

    [Fact]
    public void SvgBuilder_WithSkia_SetsRasterFactory()
    {
        var builder = new SvgBuilder();

        builder.WithSkia();

        Assert.NotNull(builder.RasterFactory);
    }

    [Fact]
    public void SvgBuilder_WithSkiaAndConfigure_InvokesAction()
    {
        var builder = new SvgBuilder();
        var configured = false;

        builder.WithSkia(_ => configured = true);

        Assert.True(configured);
        Assert.NotNull(builder.RasterFactory);
    }

    [Fact]
    public void SvgBuilder_WithRasterBackend_StoresFactory()
    {
        var builder = new SvgBuilder();
        Func<FlexRender.Configuration.FlexRenderBuilder, FlexRender.Abstractions.IFlexRender> factory = _ => null!;

        builder.WithRasterBackend(factory);

        Assert.Same(factory, builder.RasterFactory);
    }

    [Fact]
    public void SvgBuilder_SetQrSvgProvider_NullThrows()
    {
        var builder = new SvgBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.SetQrSvgProvider(null!));
    }

    [Fact]
    public void SvgBuilder_SetBarcodeSvgProvider_NullThrows()
    {
        var builder = new SvgBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.SetBarcodeSvgProvider(null!));
    }

    [Fact]
    public void SvgBuilder_SetQrSvgProvider_Twice_ThrowsInvalidOperation()
    {
        var builder = new SvgBuilder();
        builder.SetQrSvgProvider(new FakeQrSvgProvider());

        Assert.Throws<InvalidOperationException>(() => builder.SetQrSvgProvider(new FakeQrSvgProvider()));
    }

    [Fact]
    public void SvgBuilder_SetBarcodeSvgProvider_Twice_ThrowsInvalidOperation()
    {
        var builder = new SvgBuilder();
        builder.SetBarcodeSvgProvider(new FakeBarcodeSvgProvider());

        Assert.Throws<InvalidOperationException>(() => builder.SetBarcodeSvgProvider(new FakeBarcodeSvgProvider()));
    }

    private sealed class FakeQrSvgProvider : ISvgContentProvider<QrElement>
    {
        public string GenerateSvgContent(QrElement element, float width, float height) => "<g/>";
    }

    private sealed class FakeBarcodeSvgProvider : ISvgContentProvider<BarcodeElement>
    {
        public string GenerateSvgContent(BarcodeElement element, float width, float height) => "<g/>";
    }
}
