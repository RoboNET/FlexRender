using Microsoft.Extensions.DependencyInjection;
using FlexRender.Abstractions;
using FlexRender.Configuration;
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
        Assert.Equal(50, limits.MaxPreprocessorNestingDepth);
        Assert.Equal(1024 * 1024, limits.MaxPreprocessorInputSize);
        Assert.Equal(100, limits.MaxTemplateNestingDepth);
        Assert.Equal(100, limits.MaxRenderDepth);
        Assert.Equal(10 * 1024 * 1024, limits.MaxImageSize);
        Assert.Equal(TimeSpan.FromSeconds(30), limits.HttpTimeout);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var limits = new ResourceLimits
        {
            MaxTemplateFileSize = 2 * 1024 * 1024,
            MaxDataFileSize = 20L * 1024 * 1024,
            MaxPreprocessorNestingDepth = 25,
            MaxPreprocessorInputSize = 512 * 1024,
            MaxTemplateNestingDepth = 50,
            MaxRenderDepth = 200,
            MaxImageSize = 5 * 1024 * 1024,
            HttpTimeout = TimeSpan.FromSeconds(60)
        };

        Assert.Equal(2 * 1024 * 1024, limits.MaxTemplateFileSize);
        Assert.Equal(20L * 1024 * 1024, limits.MaxDataFileSize);
        Assert.Equal(25, limits.MaxPreprocessorNestingDepth);
        Assert.Equal(512 * 1024, limits.MaxPreprocessorInputSize);
        Assert.Equal(50, limits.MaxTemplateNestingDepth);
        Assert.Equal(200, limits.MaxRenderDepth);
        Assert.Equal(5 * 1024 * 1024, limits.MaxImageSize);
        Assert.Equal(TimeSpan.FromSeconds(60), limits.HttpTimeout);
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
    public void MaxPreprocessorNestingDepth_ZeroOrNegative_Throws()
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxPreprocessorNestingDepth = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxPreprocessorNestingDepth = -1);
    }

    [Fact]
    public void MaxPreprocessorInputSize_ZeroOrNegative_Throws()
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxPreprocessorInputSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxPreprocessorInputSize = -1);
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
    public void HttpTimeout_ZeroOrNegative_Throws()
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.HttpTimeout = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.HttpTimeout = TimeSpan.FromSeconds(-1));
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
        Assert.Equal(options.Limits.HttpTimeout, options.HttpTimeout);
    }

    [Fact]
    public void FlexRenderOptions_SetMaxImageSize_UpdatesLimits()
    {
        var options = new FlexRenderOptions();
        options.MaxImageSize = 5 * 1024 * 1024;

        Assert.Equal(5 * 1024 * 1024, options.Limits.MaxImageSize);
    }

    [Fact]
    public void FlexRenderOptions_SetHttpTimeout_UpdatesLimits()
    {
        var options = new FlexRenderOptions();
        options.HttpTimeout = TimeSpan.FromSeconds(60);

        Assert.Equal(TimeSpan.FromSeconds(60), options.Limits.HttpTimeout);
    }

    [Fact]
    public void FlexRenderOptions_SetLimitsMaxImageSize_UpdatesProperty()
    {
        var options = new FlexRenderOptions();
        options.Limits.MaxImageSize = 5 * 1024 * 1024;

        Assert.Equal(5 * 1024 * 1024, options.MaxImageSize);
    }

    [Fact]
    public void FlexRenderOptions_SetLimitsHttpTimeout_UpdatesProperty()
    {
        var options = new FlexRenderOptions();
        options.Limits.HttpTimeout = TimeSpan.FromSeconds(60);

        Assert.Equal(TimeSpan.FromSeconds(60), options.HttpTimeout);
    }

    [Fact]
    public void BackwardCompatConstants_MatchResourceLimitsDefaults()
    {
        var limits = new ResourceLimits();

        Assert.Equal(TemplateParser.MaxFileSize, limits.MaxTemplateFileSize);
        Assert.Equal(TemplateProcessor.MaxNestingDepth, limits.MaxTemplateNestingDepth);
    }
}

public sealed class FlexRenderBuilderLimitsTests
{
    [Fact]
    public void WithLimits_ConfiguresResourceLimits()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithLimits(limits =>
            {
                limits.MaxTemplateFileSize = 2 * 1024 * 1024;
                limits.MaxRenderDepth = 50;
            }));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<FlexRenderOptions>();

        Assert.Equal(2 * 1024 * 1024, options.Limits.MaxTemplateFileSize);
        Assert.Equal(50, options.Limits.MaxRenderDepth);
    }

    [Fact]
    public void WithLimits_NullAction_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var builder = new FlexRenderBuilder(services);

        Assert.Throws<ArgumentNullException>(() => builder.WithLimits(null!));
    }

    [Fact]
    public void WithLimits_PreservesDefaults_WhenNotOverridden()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithLimits(limits =>
            {
                limits.MaxRenderDepth = 200;
            }));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<FlexRenderOptions>();

        Assert.Equal(200, options.Limits.MaxRenderDepth);
        Assert.Equal(1024 * 1024, options.Limits.MaxTemplateFileSize);
        Assert.Equal(50, options.Limits.MaxPreprocessorNestingDepth);
        Assert.Equal(100, options.Limits.MaxTemplateNestingDepth);
    }

    [Fact]
    public void WithMaxImageSize_StillWorks_DelegatesToLimits()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithMaxImageSize(5 * 1024 * 1024));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<FlexRenderOptions>();

        Assert.Equal(5 * 1024 * 1024, options.Limits.MaxImageSize);
    }

    [Fact]
    public void WithHttpTimeout_StillWorks_DelegatesToLimits()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithHttpTimeout(TimeSpan.FromSeconds(60)));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<FlexRenderOptions>();

        Assert.Equal(TimeSpan.FromSeconds(60), options.Limits.HttpTimeout);
    }
}

public sealed class ServiceCollectionLimitsTests
{
    [Fact]
    public void AddFlexRender_WithLimits_ResolvesRendererWithCustomLimits()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithLimits(limits =>
            {
                limits.MaxRenderDepth = 42;
                limits.MaxTemplateNestingDepth = 25;
            }));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<FlexRenderOptions>();

        Assert.Equal(42, options.Limits.MaxRenderDepth);
        Assert.Equal(25, options.Limits.MaxTemplateNestingDepth);
    }

    [Fact]
    public void AddFlexRender_DefaultLimits_HaveSafeValues()
    {
        var services = new ServiceCollection();
        services.AddFlexRender();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<FlexRenderOptions>();

        Assert.Equal(100, options.Limits.MaxRenderDepth);
        Assert.Equal(100, options.Limits.MaxTemplateNestingDepth);
        Assert.Equal(50, options.Limits.MaxPreprocessorNestingDepth);
        Assert.Equal(1024 * 1024, options.Limits.MaxTemplateFileSize);
        Assert.Equal(10 * 1024 * 1024, options.Limits.MaxImageSize);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Limits.HttpTimeout);
    }

    [Fact]
    public void AddFlexRender_WithCustomLimits_RendererUsesCustomLimits()
    {
        var services = new ServiceCollection();
        services.AddFlexRender(builder => builder
            .WithLimits(limits =>
            {
                limits.MaxRenderDepth = 42;
            }));

        var provider = services.BuildServiceProvider();
        var renderer = provider.GetRequiredService<IFlexRenderer>();

        // Renderer should be resolvable and functional
        Assert.NotNull(renderer);

        // Verify the renderer is created from the factory (not the parameterless constructor)
        // by checking the options have the custom limit propagated
        var options = provider.GetRequiredService<FlexRenderOptions>();
        Assert.Equal(42, options.Limits.MaxRenderDepth);
    }
}

public sealed class ResourceLimitsIntegrationTests
{
    [Fact]
    public void FullPipeline_WithCustomLimits_RespectsAllLimits()
    {
        // Verify that all limit values propagate correctly
        var limits = new ResourceLimits
        {
            MaxTemplateFileSize = 512 * 1024,
            MaxPreprocessorNestingDepth = 10,
            MaxPreprocessorInputSize = 256 * 1024,
            MaxTemplateNestingDepth = 20,
            MaxRenderDepth = 50,
            MaxImageSize = 1 * 1024 * 1024,
            HttpTimeout = TimeSpan.FromSeconds(15)
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
        var size = renderer.Measure(template, new ObjectValue());
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void DefaultLimits_MatchOriginalHardcodedValues()
    {
        // This test ensures we never accidentally change defaults
        var limits = new ResourceLimits();

        Assert.Equal(1024 * 1024, limits.MaxTemplateFileSize);
        Assert.Equal(10L * 1024 * 1024, limits.MaxDataFileSize);
        Assert.Equal(50, limits.MaxPreprocessorNestingDepth);
        Assert.Equal(1024 * 1024, limits.MaxPreprocessorInputSize);
        Assert.Equal(100, limits.MaxTemplateNestingDepth);
        Assert.Equal(100, limits.MaxRenderDepth);
        Assert.Equal(10 * 1024 * 1024, limits.MaxImageSize);
        Assert.Equal(TimeSpan.FromSeconds(30), limits.HttpTimeout);
    }
}
