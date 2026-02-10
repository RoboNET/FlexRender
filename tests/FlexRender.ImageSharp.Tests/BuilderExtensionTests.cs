using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.ImageSharp.Tests;

public sealed class BuilderExtensionTests
{
    [Fact]
    public void WithImageSharp_BuildsSuccessfully()
    {
        using var render = new FlexRenderBuilder()
            .WithImageSharp()
            .Build();

        Assert.NotNull(render);
        Assert.IsType<ImageSharpRender>(render);
    }

    [Fact]
    public void WithImageSharp_WithConfigure_BuildsSuccessfully()
    {
        using var render = new FlexRenderBuilder()
            .WithImageSharp(builder => { /* future configuration */ })
            .Build();

        Assert.NotNull(render);
    }

    [Fact]
    public async Task WithImageSharp_CanRender()
    {
        using var render = new FlexRenderBuilder()
            .WithImageSharp()
            .Build();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100, Height = 50, Fixed = FixedDimension.Both, Background = "#ff0000" }
        };

        var result = await render.Render(template);

        Assert.True(result.Length > 0);
    }

    [Fact]
    public void WithImageSharp_NullBuilder_Throws()
    {
        FlexRenderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.WithImageSharp());
    }
}
