using FlexRender.Configuration;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for the RenderXml extension method.
/// </summary>
public sealed class XmlRenderExtensionTests
{
    /// <summary>
    /// Verifies that RenderXml parses an XML template and renders it end-to-end to non-empty image bytes.
    /// </summary>
    [Fact]
    public async Task RenderXml_ProducesNonEmptyImage()
    {
        using var render = new FlexRenderBuilder()
            .WithSkia()
            .Build();

        const string xml = """
            <flexrender>
              <canvas width="200" height="60"/>
              <text content="Hi" size="1em"/>
            </flexrender>
            """;

        var bytes = await render.RenderXml(xml);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }
}
