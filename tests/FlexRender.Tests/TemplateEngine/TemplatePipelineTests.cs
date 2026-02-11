using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Tests for <see cref="TemplatePipeline"/> orchestration of Expand, Resolve, Materialize phases.
/// </summary>
public sealed class TemplatePipelineTests
{
    [Fact]
    public void Process_CanvasBackground_Expression_Resolves()
    {
        // Arrange: create template with expression in canvas background
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Background = ExprValue<string>.Expression("{{bg}}"),
                Width = 300
            }
        };
        var data = new ObjectValue { ["bg"] = new StringValue("#ff0000") };

        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var processor = new TemplateProcessor(limits);
        var pipeline = new TemplatePipeline(expander, processor);

        // Act
        var result = pipeline.Process(template, data);

        // Assert
        Assert.Equal("#ff0000", result.Canvas.Background.Value);
        Assert.True(result.Canvas.Background.IsResolved);
    }

    [Fact]
    public void Process_CanvasLiteral_PassesThrough()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Background = "#ffffff",
                Width = 300
            }
        };
        var data = new ObjectValue();

        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var processor = new TemplateProcessor(limits);
        var pipeline = new TemplatePipeline(expander, processor);

        var result = pipeline.Process(template, data);

        Assert.Equal("#ffffff", result.Canvas.Background.Value);
        Assert.True(result.Canvas.Background.IsResolved);
    }

    [Fact]
    public void Process_CanvasRotate_Expression_Resolves()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Rotate = ExprValue<string>.Expression("{{rotation}}"),
                Width = 300
            }
        };
        var data = new ObjectValue { ["rotation"] = new StringValue("right") };

        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var processor = new TemplateProcessor(limits);
        var pipeline = new TemplatePipeline(expander, processor);

        var result = pipeline.Process(template, data);

        Assert.Equal("right", result.Canvas.Rotate.Value);
        Assert.True(result.Canvas.Rotate.IsResolved);
    }

    [Fact]
    public void Process_NullTemplate_ThrowsArgumentNullException()
    {
        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var processor = new TemplateProcessor(limits);
        var pipeline = new TemplatePipeline(expander, processor);

        Assert.Throws<ArgumentNullException>(() => pipeline.Process(null!, new ObjectValue()));
    }

    [Fact]
    public void Process_NullData_ThrowsArgumentNullException()
    {
        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var processor = new TemplateProcessor(limits);
        var pipeline = new TemplatePipeline(expander, processor);

        var template = new Template { Canvas = new CanvasSettings { Width = 300 } };
        Assert.Throws<ArgumentNullException>(() => pipeline.Process(template, null!));
    }

    [Fact]
    public void Constructor_NullExpander_ThrowsArgumentNullException()
    {
        var processor = new TemplateProcessor();
        Assert.Throws<ArgumentNullException>(() => new TemplatePipeline(null!, processor));
    }

    [Fact]
    public void Constructor_NullProcessor_ThrowsArgumentNullException()
    {
        var expander = new TemplateExpander();
        Assert.Throws<ArgumentNullException>(() => new TemplatePipeline(expander, null!));
    }

    [Fact]
    public void Process_PreservesTemplateMetadata()
    {
        var template = new Template
        {
            Name = "test-pipeline",
            Version = 3,
            Canvas = new CanvasSettings { Width = 500 }
        };
        var data = new ObjectValue();

        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var processor = new TemplateProcessor(limits);
        var pipeline = new TemplatePipeline(expander, processor);

        var result = pipeline.Process(template, data);

        Assert.Equal("test-pipeline", result.Name);
        Assert.Equal(3, result.Version);
        Assert.Equal(500, result.Canvas.Width);
    }

    [Fact]
    public void Process_ExpandsElements()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 }
        };
        template.AddElement(new TextElement { Content = "Hello {{name}}" });

        var data = new ObjectValue { ["name"] = new StringValue("World") };

        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var processor = new TemplateProcessor(limits);
        var pipeline = new TemplatePipeline(expander, processor);

        var result = pipeline.Process(template, data);

        Assert.Single(result.Elements);
    }
}
