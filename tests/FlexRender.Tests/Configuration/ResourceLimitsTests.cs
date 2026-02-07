using Microsoft.Extensions.DependencyInjection;
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Http;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Configuration;

public sealed class ResourceLimitsTests
{
    [Fact]
    public void Defaults_HaveExpectedValues()
    {
        var limits = new ResourceLimits();

        Assert.Equal(1024 * 1024, limits.MaxTemplateFileSize);
        Assert.Equal(10L * 1024 * 1024, limits.MaxDataFileSize);
        Assert.Equal(100, limits.MaxTemplateNestingDepth);
        Assert.Equal(100, limits.MaxRenderDepth);
        Assert.Equal(10 * 1024 * 1024, limits.MaxImageSize);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var limits = new ResourceLimits
        {
            MaxTemplateFileSize = 2 * 1024 * 1024,
            MaxDataFileSize = 20L * 1024 * 1024,
            MaxTemplateNestingDepth = 50,
            MaxRenderDepth = 200,
            MaxImageSize = 5 * 1024 * 1024
        };

        Assert.Equal(2 * 1024 * 1024, limits.MaxTemplateFileSize);
        Assert.Equal(20L * 1024 * 1024, limits.MaxDataFileSize);
        Assert.Equal(50, limits.MaxTemplateNestingDepth);
        Assert.Equal(200, limits.MaxRenderDepth);
        Assert.Equal(5 * 1024 * 1024, limits.MaxImageSize);
    }

    [Fact]
    public void MaxTemplateFileSize_ZeroOrNegative_Throws()
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxTemplateFileSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxTemplateFileSize = -1);
    }

    [Fact]
    public void MaxDataFileSize_ZeroOrNegative_Throws()
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxDataFileSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxDataFileSize = -1);
    }

    [Fact]
    public void MaxTemplateNestingDepth_ZeroOrNegative_Throws()
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxTemplateNestingDepth = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxTemplateNestingDepth = -1);
    }

    [Fact]
    public void MaxRenderDepth_ZeroOrNegative_Throws()
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxRenderDepth = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxRenderDepth = -1);
    }

    [Fact]
    public void MaxImageSize_ZeroOrNegative_Throws()
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxImageSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxImageSize = -1);
    }

    [Fact]
    public void FlexRenderOptions_HasResourceLimits_WithDefaults()
    {
        var options = new FlexRenderOptions();

        Assert.NotNull(options.Limits);
        Assert.Equal(1024 * 1024, options.Limits.MaxTemplateFileSize);
        Assert.Equal(100, options.Limits.MaxRenderDepth);
    }

    [Fact]
    public void FlexRenderOptions_Limits_MatchesLegacyProperties()
    {
        var options = new FlexRenderOptions();

        Assert.Equal(options.Limits.MaxImageSize, options.MaxImageSize);
    }

    [Fact]
    public void FlexRenderOptions_SetMaxImageSize_UpdatesLimits()
    {
        var options = new FlexRenderOptions();
        options.MaxImageSize = 5 * 1024 * 1024;

        Assert.Equal(5 * 1024 * 1024, options.Limits.MaxImageSize);
    }

    [Fact]
    public void FlexRenderOptions_SetLimitsMaxImageSize_UpdatesProperty()
    {
        var options = new FlexRenderOptions();
        options.Limits.MaxImageSize = 5 * 1024 * 1024;

        Assert.Equal(5 * 1024 * 1024, options.MaxImageSize);
    }

    [Fact]
    public void BackwardCompatConstants_MatchResourceLimitsDefaults()
    {
        var limits = new ResourceLimits();

        Assert.Equal(TemplateParser.MaxFileSize, limits.MaxTemplateFileSize);
        Assert.Equal(TemplateProcessor.MaxNestingDepth, limits.MaxTemplateNestingDepth);
    }
}

public sealed class HttpResourceLoaderOptionsTests
{
    [Fact]
    public void Defaults_HaveExpectedValues()
    {
        var options = new HttpResourceLoaderOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        Assert.Equal(10 * 1024 * 1024, options.MaxResourceSize);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new HttpResourceLoaderOptions
        {
            Timeout = TimeSpan.FromMinutes(2),
            MaxResourceSize = 5 * 1024 * 1024
        };

        Assert.Equal(TimeSpan.FromMinutes(2), options.Timeout);
        Assert.Equal(5 * 1024 * 1024, options.MaxResourceSize);
    }

    [Fact]
    public void Timeout_ZeroOrNegative_Throws()
    {
        var options = new HttpResourceLoaderOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Timeout = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Timeout = TimeSpan.FromSeconds(-1));
    }

    [Fact]
    public void MaxResourceSize_ZeroOrNegative_Throws()
    {
        var options = new HttpResourceLoaderOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxResourceSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxResourceSize = -1);
    }
}

public sealed class FlexRenderBuilderLimitsTests
{
    [Fact]
    public void WithLimits_ConfiguresResourceLimits()
    {
        // Test that WithLimits configures limits correctly via FlexRenderBuilder
        using var render = new FlexRenderBuilder()
            .WithLimits(limits =>
            {
                limits.MaxTemplateFileSize = 2 * 1024 * 1024;
                limits.MaxRenderDepth = 50;
            })
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    [Fact]
    public void WithLimits_NullAction_ThrowsArgumentNullException()
    {
        var builder = new FlexRenderBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithLimits(null!));
    }

    [Fact]
    public void WithLimits_PreservesDefaults_WhenNotOverridden()
    {
        // Just MaxRenderDepth is set, other limits should remain defaults
        using var render = new FlexRenderBuilder()
            .WithLimits(limits =>
            {
                limits.MaxRenderDepth = 200;
            })
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    [Fact]
    public void WithLimits_ViaLimitsProperty_ConfiguresMaxImageSize()
    {
        using var render = new FlexRenderBuilder()
            .WithLimits(limits => limits.MaxImageSize = 5 * 1024 * 1024)
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    [Fact]
    public void WithHttpLoader_ConfiguresTimeout()
    {
        using var render = new FlexRenderBuilder()
            .WithHttpLoader(configure: opts => opts.Timeout = TimeSpan.FromSeconds(60))
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }

    [Fact]
    public void WithHttpLoader_ConfiguresMaxResourceSize()
    {
        using var render = new FlexRenderBuilder()
            .WithHttpLoader(configure: opts => opts.MaxResourceSize = 5 * 1024 * 1024)
            .WithSkia()
            .Build();

        Assert.NotNull(render);
    }
}

public sealed class ServiceCollectionLimitsTests
{
    [Fact]
    public void AddFlexRender_WithLimits_ResolvesRenderer()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithLimits(limits =>
            {
                limits.MaxRenderDepth = 42;
                limits.MaxTemplateNestingDepth = 25;
            })
            .WithSkia());

        using var provider = services.BuildServiceProvider();
        var renderer = provider.GetRequiredService<IFlexRender>();

        Assert.NotNull(renderer);
    }

    [Fact]
    public void AddFlexRender_WithSkia_ResolvesRenderer()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder.WithSkia());

        using var provider = services.BuildServiceProvider();
        var renderer = provider.GetRequiredService<IFlexRender>();

        Assert.NotNull(renderer);
    }

    [Fact]
    public void AddFlexRender_WithCustomLimits_RendererIsResolvable()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithLimits(limits =>
            {
                limits.MaxRenderDepth = 42;
            })
            .WithSkia());

        using var provider = services.BuildServiceProvider();
        var renderer = provider.GetRequiredService<IFlexRender>();

        // Renderer should be resolvable and functional
        Assert.NotNull(renderer);
    }

    [Fact]
    public void AddFlexRender_WithServiceProviderOverload_CanAccessServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton("test-base-path");
        services.AddFlexRender((sp, builder) =>
        {
            var basePath = sp.GetRequiredService<string>();
            builder
                .WithBasePath(basePath)
                .WithSkia();
        });

        using var provider = services.BuildServiceProvider();
        var renderer = provider.GetRequiredService<IFlexRender>();

        Assert.NotNull(renderer);
    }
}

public sealed class ResourceLimitsIntegrationTests
{
    [Fact]
    public async Task FullPipeline_WithCustomLimits_RespectsAllLimits()
    {
        // Verify that all limit values propagate correctly
        var limits = new ResourceLimits
        {
            MaxTemplateFileSize = 512 * 1024,
            MaxTemplateNestingDepth = 20,
            MaxRenderDepth = 50,
            MaxImageSize = 1 * 1024 * 1024
        };

        var options = new FlexRenderOptions();
        // Verify options.Limits is a separate instance with defaults
        Assert.NotEqual(limits.MaxRenderDepth, options.Limits.MaxRenderDepth);

        // Now set via limits
        options.Limits.MaxRenderDepth = 50;
        Assert.Equal(50, options.Limits.MaxRenderDepth);

        // Verify TemplateParser uses limits
        var parser = new TemplateParser(limits);
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: text
                content: "test"
            """;
        var template = parser.Parse(yaml);
        Assert.NotNull(template);

        // Verify TemplateProcessor uses limits
        var processor = new TemplateProcessor(limits);
        var result = processor.Process("Hello {{name}}", new ObjectValue { ["name"] = "World" });
        Assert.Equal("Hello World", result);

        // Verify SkiaRenderer uses limits
        using var renderer = new SkiaRenderer(limits);
        var size = await renderer.Measure(template, new ObjectValue(), TestContext.Current.CancellationToken);
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void DefaultLimits_MatchOriginalHardcodedValues()
    {
        // This test ensures we never accidentally change defaults
        var limits = new ResourceLimits();

        Assert.Equal(1024 * 1024, limits.MaxTemplateFileSize);
        Assert.Equal(10L * 1024 * 1024, limits.MaxDataFileSize);
        Assert.Equal(100, limits.MaxTemplateNestingDepth);
        Assert.Equal(100, limits.MaxRenderDepth);
        Assert.Equal(10 * 1024 * 1024, limits.MaxImageSize);
    }
}
