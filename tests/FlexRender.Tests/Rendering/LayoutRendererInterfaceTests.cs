using FlexRender.Abstractions;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for <see cref="ILayoutRenderer{TOutput}"/> interface implementation on <see cref="SkiaRenderer"/>.
/// </summary>
public sealed class LayoutRendererInterfaceTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();

    public void Dispose()
    {
        _renderer.Dispose();
    }

    [Fact]
    public void SkiaRenderer_ImplementsILayoutRenderer()
    {
        Assert.IsAssignableFrom<ILayoutRenderer<SKBitmap>>(_renderer);
    }

    [Fact]
    public async Task Render_ViaInterface_ReturnsBitmap()
    {
        ILayoutRenderer<SKBitmap> renderer = _renderer;

        var template = new Template
        {
            Canvas = new CanvasSettings { Fixed = FixedDimension.Width, Width = 200, Background = "#ffffff" },
            Elements = [new TextElement { Content = "Interface Test", Size = "16" }]
        };
        var data = new ObjectValue();

        using var bitmap = await renderer.Render(template, data);

        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);
        Assert.True(bitmap.Height > 0);
    }

    [Fact]
    public async Task Render_ViaInterface_ThrowsOnNullTemplate()
    {
        ILayoutRenderer<SKBitmap> renderer = _renderer;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => renderer.Render(null!, new ObjectValue()));
    }

    [Fact]
    public async Task Render_ViaInterface_ThrowsOnNullData()
    {
        ILayoutRenderer<SKBitmap> renderer = _renderer;

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = []
        };

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => renderer.Render(template, null!));
    }

    [Fact]
    public async Task Render_ViaInterface_SupportsCancellation()
    {
        ILayoutRenderer<SKBitmap> renderer = _renderer;

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = []
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => renderer.Render(template, new ObjectValue(), cts.Token));
    }
}
