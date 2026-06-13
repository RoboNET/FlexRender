using FlexRender.Parsing.Ast;
using FlexRender.Xml;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests for the table element via the XML parser.
/// </summary>
public class XmlTableTests
{
    private readonly XmlTemplateParser _parser = new();

    [Fact]
    public void Parse_DynamicTable_Columns()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <table array="lines" as="line">
                <columns>
                  <column key="name" label="Item" grow="1"/>
                  <column key="price" label="Price" align="right"/>
                </columns>
              </table>
            </flexrender>
            """;

        var table = Assert.IsType<TableElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal("lines", table.ArrayPath);
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal("name", table.Columns[0].Key);
        Assert.Equal("Item", table.Columns[0].Label);
        Assert.Equal(TextAlign.Right, table.Columns[1].Align);
    }

    [Fact]
    public void Parse_StaticTable_Rows()
    {
        const string xml = """
            <flexrender>
              <canvas width="400"/>
              <table>
                <columns>
                  <column key="name" label="Item"/>
                  <column key="qty" label="Qty"/>
                </columns>
                <rows>
                  <row name="Coffee" qty="2"/>
                  <row name="Tea" qty="1"/>
                </rows>
              </table>
            </flexrender>
            """;

        var table = Assert.IsType<TableElement>(Assert.Single(_parser.Parse(xml).Elements));
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Coffee", table.Rows[0].Values["name"]);
        Assert.Equal("1", table.Rows[1].Values["qty"]);
    }
}
