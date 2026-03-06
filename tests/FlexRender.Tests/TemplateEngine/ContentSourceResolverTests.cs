using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public sealed class ContentSourceResolverTests
{


    [Fact]
    public async Task Resolve_BytesVariable_ReturnsBinaryContent()
    {
        var rawBytes = new byte[] { 0x1B, 0x40, 0x48, 0x65, 0x6C, 0x6F };
        var context = new TemplateContext(new ObjectValue
        {
            ["payload"] = new BytesValue(rawBytes, "application/octet-stream")
        });
        var source = ExprValue<string>.Expression("{{payload}}");

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: null);

        var binary = Assert.IsType<BinaryContent>(result);
        Assert.Equal(rawBytes, binary.Data.ToArray());
        Assert.Equal("application/octet-stream", binary.MimeType);
    }

    [Fact]
    public async Task Resolve_Base64Prefix_ReturnsBinaryContent()
    {
        var rawBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var base64 = Convert.ToBase64String(rawBytes);
        var context = new TemplateContext(new ObjectValue());
        var source = ExprValue<string>.RawLiteral($"base64:{base64}", $"base64:{base64}");

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: null);

        var binary = Assert.IsType<BinaryContent>(result);
        Assert.Equal(rawBytes, binary.Data.ToArray());
    }

    [Fact]
    public async Task Resolve_Base64Variable_ReturnsBinaryContent()
    {
        var rawBytes = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        var base64 = Convert.ToBase64String(rawBytes);
        var context = new TemplateContext(new ObjectValue
        {
            ["data"] = new StringValue($"base64:{base64}")
        });
        var source = ExprValue<string>.Expression("{{data}}");

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: null,
            substituteVariables: (raw, ctx) =>
            {
                // Simulate variable substitution: {{data}} -> "base64:..."
                var resolved = ExpressionEvaluator.Resolve("data", ctx);
                return resolved is StringValue sv ? sv.Value : raw;
            });

        var binary = Assert.IsType<BinaryContent>(result);
        Assert.Equal(rawBytes, binary.Data.ToArray());
    }

    [Fact]
    public async Task Resolve_PlainText_ReturnsTextContent()
    {
        var context = new TemplateContext(new ObjectValue());
        ExprValue<string> source = "Hello World";

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: null);

        var text = Assert.IsType<TextContent>(result);
        Assert.Equal("Hello World", text.Text);
    }

    [Fact]
    public async Task Resolve_TextVariable_ReturnsTextContent()
    {
        var context = new TemplateContext(new ObjectValue
        {
            ["msg"] = new StringValue("Greetings")
        });
        var source = ExprValue<string>.Expression("{{msg}}");

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: null,
            substituteVariables: (raw, ctx) =>
            {
                var resolved = ExpressionEvaluator.Resolve("msg", ctx);
                return resolved is StringValue sv ? sv.Value : raw;
            });

        var text = Assert.IsType<TextContent>(result);
        Assert.Equal("Greetings", text.Text);
    }

    [Fact]
    public async Task Resolve_InvalidBase64_ThrowsTemplateEngineException()
    {
        var context = new TemplateContext(new ObjectValue());
        var source = ExprValue<string>.RawLiteral("base64:!!!not-valid!!!", "base64:!!!not-valid!!!");

        var ex = await Assert.ThrowsAsync<TemplateEngineException>(async () =>
            await ContentSourceResolver.ResolveAsync(source, context, loaders: null));

        Assert.Contains("Invalid base64", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_FileUri_ReturnsBinaryContent()
    {
        var expectedBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var loader = new FakeResourceLoader("test", expectedBytes);
        var context = new TemplateContext(new ObjectValue());
        var source = ExprValue<string>.RawLiteral("file:test.png", "file:test.png");

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: [loader]);

        var binary = Assert.IsType<BinaryContent>(result);
        Assert.Equal(expectedBytes, binary.Data.ToArray());
    }

    [Fact]
    public async Task Resolve_NullSource_ReturnsEmptyTextContent()
    {
        var context = new TemplateContext(new ObjectValue());
        var source = new ExprValue<string>(null!);

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: null);

        var text = Assert.IsType<TextContent>(result);
        Assert.Equal(string.Empty, text.Text);
    }

    [Fact]
    public async Task Resolve_PlainTextWithFileLoader_ReturnsTextContent()
    {
        var context = new TemplateContext(new ObjectValue());
        var loaders = new IResourceLoader[] { new FileResourceLoader(new FlexRenderOptions()) };
        ExprValue<string> source = "ТЕСТОВЫЙ БАНК\nТЕЛ: 8-800-000-00-00";

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders);

        var text = Assert.IsType<TextContent>(result);
        Assert.Equal("ТЕСТОВЫЙ БАНК\nТЕЛ: 8-800-000-00-00", text.Text);
    }

    [Fact]
    public async Task Resolve_TextPrefix_ReturnsTextContent()
    {
        var context = new TemplateContext(new ObjectValue());
        var loaders = new IResourceLoader[] { new FileResourceLoader(new FlexRenderOptions()) };
        var source = ExprValue<string>.RawLiteral("text:path/that/looks-like-file.txt", "text:path/that/looks-like-file.txt");

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders);

        var text = Assert.IsType<TextContent>(result);
        Assert.Equal("path/that/looks-like-file.txt", text.Text);
    }

    [Fact]
    public async Task Resolve_LoaderCannotHandle_FallsToText()
    {
        var loader = new FakeResourceLoader("http://", []);
        var context = new TemplateContext(new ObjectValue());
        var source = ExprValue<string>.RawLiteral("ftp://server/file", "ftp://server/file");

        var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: [loader]);

        var text = Assert.IsType<TextContent>(result);
        Assert.Equal("ftp://server/file", text.Text);
    }

    [Theory]
    [InlineData("file:../../../etc/passwd")]
    [InlineData("file:///tmp/../etc/passwd")]
    [InlineData("file:data/../../secret.bin")]
    public async Task Resolve_FileSchemeWithPathTraversal_Throws(string source)
    {
        var context = new TemplateContext(new ObjectValue());
        var contentSource = new ExprValue<string>(source);

        var ex = await Assert.ThrowsAsync<TemplateEngineException>(async () =>
            await ContentSourceResolver.ResolveAsync(contentSource, context, loaders: null));
        Assert.Contains("Path traversal", ex.Message);
    }

    /// <summary>
    /// Fake resource loader for testing URI-based content loading.
    /// </summary>
    private sealed class FakeResourceLoader(string scheme, byte[] data) : IResourceLoader
    {
        /// <inheritdoc />
        public int Priority => 100;

        /// <inheritdoc />
        public bool CanHandle(string uri) => uri.StartsWith(scheme, StringComparison.Ordinal);

        /// <inheritdoc />
        public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
        {
            var ms = new MemoryStream(data);
            return Task.FromResult<Stream?>(ms);
        }
    }
}
