using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for ContentElement AST model.
/// </summary>
public sealed class ContentElementTests
{
    /// <summary>
    /// Verifies that the element type is Content.
    /// </summary>
    [Fact]
    public void Type_ReturnsContent()
    {
        var element = new ContentElement();
        Assert.Equal(ElementType.Content, element.Type);
    }

    /// <summary>
    /// Verifies that Source defaults to an empty string.
    /// </summary>
    [Fact]
    public void Source_DefaultsToEmptyString()
    {
        var element = new ContentElement();
        Assert.Equal("", element.Source.Value);
    }

    /// <summary>
    /// Verifies that Format defaults to an empty string.
    /// </summary>
    [Fact]
    public void Format_DefaultsToEmptyString()
    {
        var element = new ContentElement();
        Assert.Equal("", element.Format.Value);
    }

    /// <summary>
    /// Verifies that Options defaults to null.
    /// </summary>
    [Fact]
    public void Options_DefaultsToNull()
    {
        var element = new ContentElement();
        Assert.Null(element.Options);
    }

    /// <summary>
    /// Verifies that Options can be set to a dictionary with nested values.
    /// </summary>
    [Fact]
    public void Options_CanBeSetToDictionary()
    {
        var options = new Dictionary<string, object>
        {
            ["input_encoding"] = "cp866",
            ["charsets"] = new Dictionary<string, object>
            {
                ["I"] = new Dictionary<string, object> { ["bold"] = true }
            }
        };

        var element = new ContentElement { Options = options };

        Assert.NotNull(element.Options);
        Assert.Equal("cp866", element.Options["input_encoding"]);
    }

    /// <summary>
    /// Verifies that ResolveExpressions resolves Source and Format expressions,
    /// and Materialize populates the typed values.
    /// </summary>
    [Fact]
    public void ResolveExpressions_ResolvesSourceAndFormat()
    {
        var element = new ContentElement
        {
            Source = ExprValue<string>.Expression("{{body}}"),
            Format = ExprValue<string>.Expression("{{fmt}}")
        };

        var data = new ObjectValue
        {
            ["body"] = new StringValue("hello world"),
            ["fmt"] = new StringValue("markdown")
        };

        element.ResolveExpressions(
            (raw, _) => raw.Replace("{{body}}", "hello world").Replace("{{fmt}}", "markdown"),
            data);

        // After resolve, raw values are populated
        Assert.Equal("hello world", element.Source.RawValue);
        Assert.Equal("markdown", element.Format.RawValue);

        // After materialize, typed values are populated
        element.Materialize();
        Assert.Equal("hello world", element.Source.Value);
        Assert.Equal("markdown", element.Format.Value);
    }
}
