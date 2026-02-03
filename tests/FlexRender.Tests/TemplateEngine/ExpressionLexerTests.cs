using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public class ExpressionLexerTests
{
    [Fact]
    public void Tokenize_PlainText_ReturnsSingleTextToken()
    {
        var tokens = ExpressionLexer.Tokenize("Hello World");

        Assert.Single(tokens);
        var text = Assert.IsType<TextToken>(tokens[0]);
        Assert.Equal("Hello World", text.Value);
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmpty()
    {
        var tokens = ExpressionLexer.Tokenize("");

        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_SimpleVariable_ReturnsVariableToken()
    {
        var tokens = ExpressionLexer.Tokenize("{{name}}");

        Assert.Single(tokens);
        var variable = Assert.IsType<VariableToken>(tokens[0]);
        Assert.Equal("name", variable.Path);
    }

    [Fact]
    public void Tokenize_VariableWithSpaces_TrimsPath()
    {
        var tokens = ExpressionLexer.Tokenize("{{ name }}");

        Assert.Single(tokens);
        var variable = Assert.IsType<VariableToken>(tokens[0]);
        Assert.Equal("name", variable.Path);
    }

    [Fact]
    public void Tokenize_TextWithVariable_ReturnsBothTokens()
    {
        var tokens = ExpressionLexer.Tokenize("Hello {{name}}!");

        Assert.Equal(3, tokens.Count);
        Assert.IsType<TextToken>(tokens[0]);
        Assert.Equal("Hello ", ((TextToken)tokens[0]).Value);
        Assert.IsType<VariableToken>(tokens[1]);
        Assert.Equal("name", ((VariableToken)tokens[1]).Path);
        Assert.IsType<TextToken>(tokens[2]);
        Assert.Equal("!", ((TextToken)tokens[2]).Value);
    }

    [Fact]
    public void Tokenize_MultipleVariables_ReturnsAllTokens()
    {
        var tokens = ExpressionLexer.Tokenize("{{first}} and {{second}}");

        Assert.Equal(3, tokens.Count);
        Assert.Equal("first", ((VariableToken)tokens[0]).Path);
        Assert.Equal(" and ", ((TextToken)tokens[1]).Value);
        Assert.Equal("second", ((VariableToken)tokens[2]).Path);
    }

    [Fact]
    public void Tokenize_DotNotation_PreservesPath()
    {
        var tokens = ExpressionLexer.Tokenize("{{user.address.city}}");

        Assert.Single(tokens);
        var variable = Assert.IsType<VariableToken>(tokens[0]);
        Assert.Equal("user.address.city", variable.Path);
    }

    [Fact]
    public void Tokenize_ArrayIndex_PreservesPath()
    {
        var tokens = ExpressionLexer.Tokenize("{{items[0].name}}");

        Assert.Single(tokens);
        var variable = Assert.IsType<VariableToken>(tokens[0]);
        Assert.Equal("items[0].name", variable.Path);
    }

    [Fact]
    public void Tokenize_UnclosedExpression_ThrowsException()
    {
        var ex = Assert.Throws<TemplateEngineException>(() =>
            ExpressionLexer.Tokenize("Hello {{name"));

        Assert.Contains("Unclosed", ex.Message);
    }

    // Task 4: Control Flow Tokens
    [Fact]
    public void Tokenize_IfBlock_ReturnsControlTokens()
    {
        var tokens = ExpressionLexer.Tokenize("{{#if show}}content{{/if}}");

        Assert.Equal(3, tokens.Count);
        var ifStart = Assert.IsType<IfStartToken>(tokens[0]);
        Assert.Equal("show", ifStart.Condition);
        Assert.IsType<TextToken>(tokens[1]);
        Assert.IsType<IfEndToken>(tokens[2]);
    }

    [Fact]
    public void Tokenize_IfElseBlock_ReturnsAllControlTokens()
    {
        var tokens = ExpressionLexer.Tokenize("{{#if show}}yes{{else}}no{{/if}}");

        Assert.Equal(5, tokens.Count);
        Assert.IsType<IfStartToken>(tokens[0]);
        Assert.Equal("yes", ((TextToken)tokens[1]).Value);
        Assert.IsType<ElseToken>(tokens[2]);
        Assert.Equal("no", ((TextToken)tokens[3]).Value);
        Assert.IsType<IfEndToken>(tokens[4]);
    }

    [Fact]
    public void Tokenize_EachBlock_ReturnsControlTokens()
    {
        var tokens = ExpressionLexer.Tokenize("{{#each items}}item{{/each}}");

        Assert.Equal(3, tokens.Count);
        var eachStart = Assert.IsType<EachStartToken>(tokens[0]);
        Assert.Equal("items", eachStart.ArrayPath);
        Assert.IsType<TextToken>(tokens[1]);
        Assert.IsType<EachEndToken>(tokens[2]);
    }

    [Fact]
    public void Tokenize_NestedBlocks_ReturnsAllTokens()
    {
        var tokens = ExpressionLexer.Tokenize("{{#each items}}{{#if visible}}{{name}}{{/if}}{{/each}}");

        Assert.Equal(5, tokens.Count);
        Assert.IsType<EachStartToken>(tokens[0]);
        Assert.IsType<IfStartToken>(tokens[1]);
        Assert.IsType<VariableToken>(tokens[2]);
        Assert.IsType<IfEndToken>(tokens[3]);
        Assert.IsType<EachEndToken>(tokens[4]);
    }

    [Fact]
    public void Tokenize_LoopVariables_ReturnsVariableTokens()
    {
        var tokens = ExpressionLexer.Tokenize("{{@index}} {{@first}} {{@last}}");

        Assert.Equal(5, tokens.Count);
        Assert.Equal("@index", ((VariableToken)tokens[0]).Path);
        Assert.Equal("@first", ((VariableToken)tokens[2]).Path);
        Assert.Equal("@last", ((VariableToken)tokens[4]).Path);
    }

    [Fact]
    public void Tokenize_ComplexCondition_PreservesCondition()
    {
        var tokens = ExpressionLexer.Tokenize("{{#if user.isActive}}active{{/if}}");

        var ifStart = Assert.IsType<IfStartToken>(tokens[0]);
        Assert.Equal("user.isActive", ifStart.Condition);
    }
}
