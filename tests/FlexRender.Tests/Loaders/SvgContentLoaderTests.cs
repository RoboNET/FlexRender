using FlexRender.Abstractions;
using FlexRender.Loaders;
using Xunit;

namespace FlexRender.Tests.Loaders;

/// <summary>
/// Unit tests for <see cref="SvgContentLoader"/>.
/// </summary>
public sealed class SvgContentLoaderTests
{
    [Fact]
    public void LoadFromLoaders_WithNullLoaders_ReturnsNull()
    {
        var result = SvgContentLoader.LoadFromLoaders(null, "mem://test");
        Assert.Null(result);
    }

    [Fact]
    public void LoadFromLoaders_WithNoMatchingLoader_ReturnsNull()
    {
        var loaders = new IResourceLoader[] { new FakeLoader("mem://", "<svg/>", canHandle: false) };
        var result = SvgContentLoader.LoadFromLoaders(loaders, "mem://test");
        Assert.Null(result);
    }

    [Fact]
    public void LoadFromLoaders_WithMatchingLoader_ReturnsContent()
    {
        var loaders = new IResourceLoader[] { new FakeLoader("mem://", "<svg/>", canHandle: true) };
        var result = SvgContentLoader.LoadFromLoaders(loaders, "mem://test");
        Assert.Equal("<svg/>", result);
    }

    [Fact]
    public void LoadFromLoaders_WhenContentExceedsLimit_Throws()
    {
        var oversized = new string('x', SvgContentLoader.MaxSvgContentSize + 1);
        var loaders = new IResourceLoader[] { new FakeLoader("mem://", oversized, canHandle: true) };

        var ex = Assert.Throws<InvalidOperationException>(
            () => SvgContentLoader.LoadFromLoaders(loaders, "mem://too-big"));

        Assert.Contains("exceeds maximum allowed size", ex.Message);
    }

    [Fact]
    public void ReadFileWithLimit_WithValidFile_ReturnsContent()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "<svg/>");
            var content = SvgContentLoader.ReadFileWithLimit(path);
            Assert.Equal("<svg/>", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFileWithLimit_WithOversizedFile_Throws()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.SetLength(SvgContentLoader.MaxSvgContentSize + 1L);

            var ex = Assert.Throws<InvalidOperationException>(() => SvgContentLoader.ReadFileWithLimit(path));
            Assert.Contains("exceeds maximum allowed size", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class FakeLoader : IResourceLoader
    {
        private readonly string _prefix;
        private readonly string _content;
        private readonly bool _canHandle;

        public FakeLoader(string prefix, string content, bool canHandle)
        {
            _prefix = prefix;
            _content = content;
            _canHandle = canHandle;
        }

        public int Priority => 0;

        public bool CanHandle(string uri)
        {
            return _canHandle && uri.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase);
        }

        public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
        {
            if (!CanHandle(uri))
            {
                return Task.FromResult<Stream?>(null);
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(_content);
            return Task.FromResult<Stream?>(new MemoryStream(bytes));
        }
    }
}
