// Tests for YAML parsing of the table element.
// Validates that TemplateParser correctly parses table YAML into TableElement AST.

using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Table;

/// <summary>
/// Tests for YAML parsing of the table element.
/// </summary>
public sealed class TableParsingTests
{
    private readonly TemplateParser _parser = new();

    // === Dynamic table parsing ===

    [Fact]
    public void Parse_DynamicTable_ParsesCorrectly()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                columns:
                  - key: name
                    label: "Item"
                    grow: 1
                  - key: price
                    label: "Price"
                    width: "60"
                    align: right
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Equal("items", table.ArrayPath);
        Assert.Empty(table.Rows);
        Assert.Equal(2, table.Columns.Count);

        Assert.Equal("name", table.Columns[0].Key);
        Assert.Equal("Item", table.Columns[0].Label);
        Assert.Equal(1, table.Columns[0].Grow);

        Assert.Equal("price", table.Columns[1].Key);
        Assert.Equal("Price", table.Columns[1].Label);
        Assert.Equal("60", table.Columns[1].Width);
        Assert.Equal(TextAlign.Right, table.Columns[1].Align);
    }

    [Fact]
    public void Parse_DynamicTableWithAs_ParsesAsVariable()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                as: item
                columns:
                  - key: name
                    grow: 1
            """;

        var template = _parser.Parse(yaml);

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Equal("items", table.ArrayPath);
        Assert.Equal("item", table.ItemVariable);
    }

    [Fact]
    public void Parse_DynamicTableWithFormat_ParsesFormatString()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                as: item
                columns:
                  - key: price
                    format: "{{item.price}} $"
            """;

        var template = _parser.Parse(yaml);

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Equal("{{item.price}} $", table.Columns[0].Format);
    }

    // === Static table parsing ===

    [Fact]
    public void Parse_StaticTable_ParsesCorrectly()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                columns:
                  - key: label
                    grow: 1
                  - key: value
                    width: "80"
                    align: right
                rows:
                  - label: "Subtotal"
                    value: "42.50 $"
                  - label: "Tax (10%)"
                    value: "4.25 $"
            """;

        var template = _parser.Parse(yaml);

        Assert.Single(template.Elements);
        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Null(table.ArrayPath);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Subtotal", table.Rows[0].Values["label"]);
        Assert.Equal("42.50 $", table.Rows[0].Values["value"]);
    }

    [Fact]
    public void Parse_StaticTableWithRowFont_ParsesPerRowOverrides()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                columns:
                  - key: label
                    grow: 1
                  - key: value
                    width: "80"
                rows:
                  - label: "Total"
                    value: "46.75 $"
                    font: bold
            """;

        var template = _parser.Parse(yaml);

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        // Row-level font override should be accessible
        Assert.Equal("bold", table.Rows[0].Font);
    }

    // === Table-level styling ===

    [Fact]
    public void Parse_TableWithStyling_ParsesAllStyleProperties()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                font: main
                size: "12"
                color: "#333333"
                row-gap: "4"
                column-gap: "8"
                columns:
                  - key: name
                    grow: 1
            """;

        var template = _parser.Parse(yaml);

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Equal("main", table.Font);
        Assert.Equal("12", table.Size);
        Assert.Equal("#333333", table.Color);
        Assert.Equal("4", table.RowGap);
        Assert.Equal("8", table.ColumnGap);
    }

    [Fact]
    public void Parse_TableWithHeaderStyling_ParsesHeaderProperties()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                header-font: bold
                header-color: "#000000"
                header-size: "14"
                header-border-bottom: "1 solid #cccccc"
                columns:
                  - key: name
                    grow: 1
            """;

        var template = _parser.Parse(yaml);

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Equal("bold", table.HeaderFont);
        Assert.Equal("#000000", table.HeaderColor);
        Assert.Equal("14", table.HeaderSize);
        Assert.Equal("1 solid #cccccc", table.HeaderBorderBottom);
    }

    // === Edge cases ===

    [Fact]
    public void Parse_BothArrayAndRows_ThrowsParseError()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                rows:
                  - label: "test"
                columns:
                  - key: label
                    grow: 1
            """;

        // Both array and rows should cause a parser error
        Assert.ThrowsAny<Exception>(() => _parser.Parse(yaml));
    }

    [Fact]
    public void Parse_ColumnWithAllProperties_ParsesCorrectly()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                columns:
                  - key: price
                    label: "Price"
                    width: "60"
                    grow: 0
                    align: right
                    font: bold
                    color: "#ff0000"
                    size: "14"
                    format: "{{item.price}} $"
            """;

        var template = _parser.Parse(yaml);

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        var col = table.Columns[0];
        Assert.Equal("price", col.Key);
        Assert.Equal("Price", col.Label);
        Assert.Equal("60", col.Width);
        Assert.Equal(0, col.Grow);
        Assert.Equal(TextAlign.Right, col.Align);
        Assert.Equal("bold", col.Font);
        Assert.Equal("#ff0000", col.Color);
        Assert.Equal("14", col.Size);
        Assert.Equal("{{item.price}} $", col.Format);
    }

    // === Base property parsing ===

    [Fact]
    public void Parse_TableWithBaseProperties_ParsesFlexItemProperties()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                padding: "8"
                margin: "4"
                background: "#ffffff"
                grow: 1
                columns:
                  - key: name
                    grow: 1
            """;

        var template = _parser.Parse(yaml);

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Equal("8", table.Padding);
        Assert.Equal("4", table.Margin);
        Assert.Equal("#ffffff", table.Background);
        Assert.Equal(1, table.Grow);
    }
}
