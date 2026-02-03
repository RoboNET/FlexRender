using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public class ExpressionLexerTests
{
    private readonly ExpressionLexer _lexer = new();

    [Fact]
    public void Tokenize_PlainText_ReturnsSingleTextToken()
    {
        var tokens = _lexer.Tokenize("Hello World").ToList();

        Assert.Single(tokens);
        var text = Assert.IsType<TextToken>(tokens[0]);
        Assert.Equal("Hello World", text.Value);
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmpty()
    {
        var tokens = _lexer.Tokenize("").ToList();

        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_SimpleVariable_ReturnsVariableToken()
    {
        var tokens = _lexer.Tokenize("{{name}}").ToList();

        Assert.Single(tokens);
        var variable = Assert.IsType<VariableToken>(tokens[0]);
        Assert.Equal("name", variable.Path);
    }

    [Fact]
    public void Tokenize_VariableWithSpaces_TrimsPath()
    {
        var tokens = _lexer.Tokenize("{{ name }}").ToList();

        Assert.Single(tokens);
        var variable = Assert.IsType<VariableToken>(tokens[0]);
        Assert.Equal("name", variable.Path);
    }

    [Fact]
    public void Tokenize_TextWithVariable_ReturnsBothTokens()
    {
        var tokens = _lexer.Tokenize("Hello {{name}}!").ToList();

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
        var tokens = _lexer.Tokenize("{{first}} and {{second}}").ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal("first", ((VariableToken)tokens[0]).Path);
        Assert.Equal(" and ", ((TextToken)tokens[1]).Value);
        Assert.Equal("second", ((VariableToken)tokens[2]).Path);
    }

    [Fact]
    public void Tokenize_DotNotation_PreservesPath()
    {
        var tokens = _lexer.Tokenize("{{user.address.city}}").ToList();

        Assert.Single(tokens);
        var variable = Assert.IsType<VariableToken>(tokens[0]);
        Assert.Equal("user.address.city", variable.Path);
    }

    [Fact]
    public void Tokenize_ArrayIndex_PreservesPath()
    {
        var tokens = _lexer.Tokenize("{{items[0].name}}").ToList();

        Assert.Single(tokens);
        var variable = Assert.IsType<VariableToken>(tokens[0]);
        Assert.Equal("items[0].name", variable.Path);
    }

    [Fact]
    public void Tokenize_UnclosedExpression_ThrowsException()
    {
        var ex = Assert.Throws<TemplateEngineException>(() =>
            _lexer.Tokenize("Hello {{name").ToList());

        Assert.Contains("Unclosed", ex.Message);
    }

    // Task 4: Control Flow Tokens
    [Fact]
    public void Tokenize_IfBlock_ReturnsControlTokens()
    {
        var tokens = _lexer.Tokenize("{{#if show}}content{{/if}}").ToList();

        Assert.Equal(3, tokens.Count);
        var ifStart = Assert.IsType<IfStartToken>(tokens[0]);
        Assert.Equal("show", ifStart.Condition);
        Assert.IsType<TextToken>(tokens[1]);
        Assert.IsType<IfEndToken>(tokens[2]);
    }

    [Fact]
    public void Tokenize_IfElseBlock_ReturnsAllControlTokens()
    {
        var tokens = _lexer.Tokenize("{{#if show}}yes{{else}}no{{/if}}").ToList();

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
        var tokens = _lexer.Tokenize("{{#each items}}item{{/each}}").ToList();

        Assert.Equal(3, tokens.Count);
        var eachStart = Assert.IsType<EachStartToken>(tokens[0]);
        Assert.Equal("items", eachStart.ArrayPath);
        Assert.IsType<TextToken>(tokens[1]);
        Assert.IsType<EachEndToken>(tokens[2]);
    }

    [Fact]
    public void Tokenize_NestedBlocks_ReturnsAllTokens()
    {
        var tokens = _lexer.Tokenize("{{#each items}}{{#if visible}}{{name}}{{/if}}{{/each}}").ToList();

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
        var tokens = _lexer.Tokenize("{{@index}} {{@first}} {{@last}}").ToList();

        Assert.Equal(5, tokens.Count);
        Assert.Equal("@index", ((VariableToken)tokens[0]).Path);
        Assert.Equal("@first", ((VariableToken)tokens[2]).Path);
        Assert.Equal("@last", ((VariableToken)tokens[4]).Path);
    }

    [Fact]
    public void Tokenize_ComplexCondition_PreservesCondition()
    {
        var tokens = _lexer.Tokenize("{{#if user.isActive}}active{{/if}}").ToList();

        var ifStart = Assert.IsType<IfStartToken>(tokens[0]);
        Assert.Equal("user.isActive", ifStart.Condition);
    }
}
