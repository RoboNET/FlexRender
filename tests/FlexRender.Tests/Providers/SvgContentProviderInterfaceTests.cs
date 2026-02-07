using FlexRender.Providers;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for ISvgContentProvider interface contract.
/// </summary>
public sealed class SvgContentProviderInterfaceTests
{
    /// <summary>
    /// Verifies the interface exists and can be implemented.
    /// </summary>
    [Fact]
    public void ISvgContentProvider_CanBeImplemented()
    {
        var provider = new TestSvgProvider();

        var result = provider.GenerateSvgContent("test", 100f, 100f);

        Assert.Equal("<rect/>", result);
    }

    private sealed class TestSvgProvider : ISvgContentProvider<string>
    {
        public string GenerateSvgContent(string element, float width, float height)
        {
            return "<rect/>";
        }
    }
}
