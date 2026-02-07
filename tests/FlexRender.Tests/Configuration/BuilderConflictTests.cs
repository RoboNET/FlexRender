using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Svg;
using Xunit;

namespace FlexRender.Tests.Configuration;

/// <summary>
/// Tests for architectural validation that prevents conflicting backend configurations
/// in <see cref="FlexRenderBuilder"/> and <see cref="SvgBuilder"/>.
/// </summary>
public sealed class BuilderConflictTests
{
    #region FlexRenderBuilder backend conflict tests

    /// <summary>
    /// Verifies that calling WithSkia() then WithImageSharp() throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void WithSkia_ThenWithImageSharp_ThrowsInvalidOperation()
    {
        var builder = new FlexRenderBuilder()
            .WithSkia();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.WithImageSharp());

        Assert.Contains("already configured", exception.Message);
    }

    /// <summary>
    /// Verifies that calling WithImageSharp() then WithSkia() throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void WithImageSharp_ThenWithSkia_ThrowsInvalidOperation()
    {
        var builder = new FlexRenderBuilder()
            .WithImageSharp();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.WithSkia());

        Assert.Contains("already configured", exception.Message);
    }

    /// <summary>
    /// Verifies that calling WithSkia() then WithSvg() throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void WithSkia_ThenWithSvg_ThrowsInvalidOperation()
    {
        var builder = new FlexRenderBuilder()
            .WithSkia();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.WithSvg());

        Assert.Contains("already configured", exception.Message);
    }

    /// <summary>
    /// Verifies that calling WithSvg() then WithSkia() throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void WithSvg_ThenWithSkia_ThrowsInvalidOperation()
    {
        var builder = new FlexRenderBuilder()
            .WithSvg();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.WithSkia());

        Assert.Contains("already configured", exception.Message);
    }

    /// <summary>
    /// Verifies that calling WithSvg() then WithImageSharp() throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void WithSvg_ThenWithImageSharp_ThrowsInvalidOperation()
    {
        var builder = new FlexRenderBuilder()
            .WithSvg();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.WithImageSharp());

        Assert.Contains("already configured", exception.Message);
    }

    /// <summary>
    /// Verifies that calling WithImageSharp() then WithSvg() throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void WithImageSharp_ThenWithSvg_ThrowsInvalidOperation()
    {
        var builder = new FlexRenderBuilder()
            .WithImageSharp();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.WithSvg());

        Assert.Contains("already configured", exception.Message);
    }

    #endregion

    #region FlexRenderBuilder single backend positive tests

    /// <summary>
    /// Verifies that configuring WithSkia() once builds successfully.
    /// </summary>
    [Fact]
    public void WithSkia_Once_Works()
    {
        using var render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        Assert.NotNull(render);
        Assert.IsAssignableFrom<IFlexRender>(render);
    }

    /// <summary>
    /// Verifies that configuring WithSvg() once builds successfully.
    /// </summary>
    [Fact]
    public void WithSvg_Once_Works()
    {
        using var render = new FlexRenderBuilder()
            .WithSvg()
            .Build();

        Assert.NotNull(render);
        Assert.IsAssignableFrom<IFlexRender>(render);
    }

    /// <summary>
    /// Verifies that configuring WithImageSharp() once builds successfully.
    /// </summary>
    [Fact]
    public void WithImageSharp_Once_Works()
    {
        using var render = new FlexRenderBuilder()
            .WithImageSharp()
            .Build();

        Assert.NotNull(render);
        Assert.IsAssignableFrom<IFlexRender>(render);
    }

    #endregion

    #region SvgBuilder raster backend conflict tests

    /// <summary>
    /// Verifies that calling WithSkia() then WithRasterBackend() on SvgBuilder is allowed,
    /// with the last call's factory taking effect.
    /// </summary>
    [Fact]
    public void SvgBuilder_WithSkia_ThenWithRasterBackend_LastCallWins()
    {
        var svgBuilder = new SvgBuilder();
        svgBuilder.WithSkia();
        svgBuilder.WithRasterBackend(_ => null!);

        // Both are accepted -- the last call sets the raster factory
        Assert.NotNull(svgBuilder.RasterFactory);
    }

    /// <summary>
    /// Verifies that calling WithRasterBackend() then WithSkia() on SvgBuilder is allowed,
    /// with the last call's factory taking effect.
    /// </summary>
    [Fact]
    public void SvgBuilder_WithRasterBackend_ThenWithSkia_LastCallWins()
    {
        var svgBuilder = new SvgBuilder();
        svgBuilder.WithRasterBackend(_ => null!);
        svgBuilder.WithSkia();

        Assert.NotNull(svgBuilder.RasterFactory);
    }

    /// <summary>
    /// Verifies that calling WithSkia() alone on SvgBuilder works correctly.
    /// </summary>
    [Fact]
    public void SvgBuilder_WithSkia_Once_Works()
    {
        using var render = new FlexRenderBuilder()
            .WithSvg(svg => svg.WithSkia())
            .Build();

        Assert.NotNull(render);
        Assert.IsAssignableFrom<IFlexRender>(render);
    }

    /// <summary>
    /// Verifies that calling WithRasterBackend() alone on SvgBuilder works correctly.
    /// </summary>
    [Fact]
    public void SvgBuilder_WithRasterBackend_Once_Works()
    {
        using var render = new FlexRenderBuilder()
            .WithSvg(svg => svg.WithRasterBackend(
                ImageSharpFlexRenderBuilderExtensions.CreateRendererFactory()))
            .Build();

        Assert.NotNull(render);
        Assert.IsAssignableFrom<IFlexRender>(render);
    }

    #endregion

    #region Error message quality tests

    /// <summary>
    /// Verifies that the error message mentions which backend methods are allowed.
    /// </summary>
    [Fact]
    public void SetRendererFactory_Twice_ErrorMessageMentionsAllBackends()
    {
        var builder = new FlexRenderBuilder()
            .WithSkia();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.WithSkia());

        Assert.Contains("WithSkia()", exception.Message);
        Assert.Contains("WithImageSharp()", exception.Message);
        Assert.Contains("WithSvg()", exception.Message);
        Assert.Contains("only once", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
