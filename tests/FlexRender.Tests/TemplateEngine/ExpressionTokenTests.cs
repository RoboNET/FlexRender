using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public class ExpressionTokenTests
{
    [Fact]
    public void TextToken_StoresValue()
    {
        var token = new TextToken("Hello World");

        Assert.Equal("Hello World", token.Value);
        Assert.Equal(TokenType.Text, token.Type);
    }

    [Fact]
    public void VariableToken_StoresPath()
    {
        var token = new VariableToken("user.name");

        Assert.Equal("user.name", token.Path);
        Assert.Equal(TokenType.Variable, token.Type);
    }

    [Fact]
    public void IfStartToken_StoresCondition()
    {
        var token = new IfStartToken("hasDiscount");

        Assert.Equal("hasDiscount", token.Condition);
        Assert.Equal(TokenType.IfStart, token.Type);
    }

    [Fact]
    public void ElseToken_HasCorrectType()
    {
        var token = new ElseToken();

        Assert.Equal(TokenType.Else, token.Type);
    }

    [Fact]
    public void IfEndToken_HasCorrectType()
    {
        var token = new IfEndToken();

        Assert.Equal(TokenType.IfEnd, token.Type);
    }

    [Fact]
    public void EachStartToken_StoresArrayPath()
    {
        var token = new EachStartToken("items");

        Assert.Equal("items", token.ArrayPath);
        Assert.Equal(TokenType.EachStart, token.Type);
    }

    [Fact]
    public void EachEndToken_HasCorrectType()
    {
        var token = new EachEndToken();

        Assert.Equal(TokenType.EachEnd, token.Type);
    }
}
