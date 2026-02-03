using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public class TemplateEngineExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var ex = new TemplateEngineException("Test error");

        Assert.Equal("Test error", ex.Message);
    }

    [Fact]
    public void Constructor_WithPosition_IncludesPositionInMessage()
    {
        var ex = new TemplateEngineException("Unexpected token", position: 15);

        Assert.Contains("15", ex.Message);
        Assert.Contains("Unexpected token", ex.Message);
        Assert.Equal(15, ex.Position);
    }

    [Fact]
    public void Constructor_WithExpression_IncludesExpressionInMessage()
    {
        var ex = new TemplateEngineException("Invalid syntax", expression: "{{broken");

        Assert.Contains("{{broken", ex.Message);
        Assert.Equal("{{broken", ex.Expression);
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesInner()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new TemplateEngineException("Outer error", inner);

        Assert.Same(inner, ex.InnerException);
    }
}
