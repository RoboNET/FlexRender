using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for each/if control-flow elements via the XML parser.
/// </summary>
public class XmlControlFlowTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_Each_WithChildren()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <each array="items" as="item">
                <text content="{{ item.name }}"/>
              </each>
            </flexrender>
            """;

        var each = Assert.IsType<EachElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal("items", each.ArrayPath);
        Assert.Equal("item", each.ItemVariable);
        Assert.Single(each.ItemTemplate);
        Assert.IsType<TextElement>(each.ItemTemplate[0]);
    }

    [Fact]
    public void Parse_If_ThenElse()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <if condition="paid" equals="true">
                <then>
                  <text content="PAID"/>
                </then>
                <else>
                  <text content="DUE"/>
                </else>
              </if>
            </flexrender>
            """;

        var ifEl = Assert.IsType<IfElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal("paid", ifEl.ConditionPath);
        Assert.Equal(ConditionOperator.Equals, ifEl.Operator);
        Assert.Equal("true", ifEl.CompareValue);
        Assert.Single(ifEl.ThenBranch);
        Assert.Single(ifEl.ElseBranch);
        Assert.Equal("PAID", Assert.IsType<TextElement>(ifEl.ThenBranch[0]).Content);
        Assert.Equal("DUE", Assert.IsType<TextElement>(ifEl.ElseBranch[0]).Content);
    }

    [Fact]
    public void Parse_If_ElseIf()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <if condition="status" equals="hot">
                <then><text content="HOT"/></then>
                <else-if>
                  <if condition="status" equals="warm">
                    <then><text content="WARM"/></then>
                  </if>
                </else-if>
              </if>
            </flexrender>
            """;

        var ifEl = Assert.IsType<IfElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.NotNull(ifEl.ElseIf);
        Assert.Equal("warm", ifEl.ElseIf!.CompareValue);
    }

    [Fact]
    public void Parse_ElseIf_WithNoChild_Throws()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <if condition="status" equals="hot">
                <then><text content="HOT"/></then>
                <else-if></else-if>
              </if>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("else-if", ex.Message);
    }

    [Fact]
    public void Parse_ElseIf_WithNonIfChild_Throws()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <if condition="status" equals="hot">
                <then><text content="HOT"/></then>
                <else-if>
                  <text content="oops"/>
                </else-if>
              </if>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("else-if", ex.Message);
    }

    [Fact]
    public void Parse_ElseIf_WithMultipleChildren_Throws()
    {
        const string xml = """
            <flexrender>
              <canvas width="300"/>
              <if condition="status" equals="hot">
                <then><text content="HOT"/></then>
                <else-if>
                  <if condition="status" equals="warm">
                    <then><text content="WARM"/></then>
                  </if>
                  <if condition="status" equals="cool">
                    <then><text content="COOL"/></then>
                  </if>
                </else-if>
              </if>
            </flexrender>
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(xml));
        Assert.Contains("else-if", ex.Message);
    }
}
