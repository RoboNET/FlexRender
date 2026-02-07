using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing table elements from YAML.
/// </summary>
public sealed class TableParsingTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_DynamicTable_ParsesCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                as: item
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

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Equal("items", table.ArrayPath);
        Assert.Equal("item", table.ItemVariable);
        Assert.Equal(2, table.Columns.Count);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void Parse_StaticTable_ParsesCorrectly()
    {
        const string yaml = """
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
                  - label: "Tax"
                    value: "4.25 $"
            """;

        var template = _parser.Parse(yaml);

        var table = Assert.IsType<TableElement>(template.Elements[0]);
        Assert.Null(table.ArrayPath);
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Subtotal", table.Rows[0].Values["label"]);
        Assert.Equal("42.50 $", table.Rows[0].Values["value"]);
    }

    [Fact]
    public void Parse_ColumnProperties_ParsedCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                columns:
                  - key: name
                    label: "Name"
                    width: "100"
                    grow: 1.5
                    align: center
                    font: bold
                    color: "#FF0000"
                    size: "14"
                    format: "{{item.name}} ({{item.id}})"
                rows:
                  - name: "test"
            """;

        var template = _parser.Parse(yaml);
        var table = Assert.IsType<TableElement>(template.Elements[0]);
        var col = table.Columns[0];

        Assert.Equal("name", col.Key);
        Assert.Equal("Name", col.Label);
        Assert.Equal("100", col.Width);
        Assert.Equal(1.5f, col.Grow);
        Assert.Equal(TextAlign.Center, col.Align);
        Assert.Equal("bold", col.Font);
        Assert.Equal("#FF0000", col.Color);
        Assert.Equal("14", col.Size);
        Assert.Equal("{{item.name}} ({{item.id}})", col.Format);
    }

    [Fact]
    public void Parse_TableDefaults_ParsedCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                font: monospace
                size: "12"
                color: "#333333"
                rowGap: "4"
                columnGap: "8"
                headerFont: bold
                headerColor: "#000000"
                headerSize: "14"
                headerBorderBottom: "solid"
                columns:
                  - key: a
                rows:
                  - a: "test"
            """;

        var template = _parser.Parse(yaml);
        var table = Assert.IsType<TableElement>(template.Elements[0]);

        Assert.Equal("monospace", table.Font);
        Assert.Equal("12", table.Size);
        Assert.Equal("#333333", table.Color);
        Assert.Equal("4", table.RowGap);
        Assert.Equal("8", table.ColumnGap);
        Assert.Equal("bold", table.HeaderFont);
        Assert.Equal("#000000", table.HeaderColor);
        Assert.Equal("14", table.HeaderSize);
        Assert.Equal("solid", table.HeaderBorderBottom);
    }

    [Fact]
    public void Parse_KebabCaseProperties_ParsedCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                row-gap: "4"
                column-gap: "8"
                header-font: bold
                header-color: "#000"
                header-size: "14"
                header-border-bottom: "dashed"
                columns:
                  - key: a
                rows:
                  - a: "test"
            """;

        var template = _parser.Parse(yaml);
        var table = Assert.IsType<TableElement>(template.Elements[0]);

        Assert.Equal("4", table.RowGap);
        Assert.Equal("8", table.ColumnGap);
        Assert.Equal("bold", table.HeaderFont);
        Assert.Equal("#000", table.HeaderColor);
        Assert.Equal("14", table.HeaderSize);
        Assert.Equal("dashed", table.HeaderBorderBottom);
    }

    [Fact]
    public void Parse_MissingColumns_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                rows:
                  - a: "test"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("columns", ex.Message);
    }

    [Fact]
    public void Parse_BothArrayAndRows_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                array: items
                columns:
                  - key: a
                rows:
                  - a: "test"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("both", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_EmptyColumns_Throws()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                columns: []
                rows:
                  - a: "test"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("at least one column", ex.Message);
    }

    [Fact]
    public void Parse_RowWithPerRowStyling_ParsedCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                columns:
                  - key: label
                  - key: value
                rows:
                  - label: "Total"
                    value: "100"
                    font: bold
                    color: "#FF0000"
                    size: "16"
            """;

        var template = _parser.Parse(yaml);
        var table = Assert.IsType<TableElement>(template.Elements[0]);
        var row = table.Rows[0];

        Assert.Equal("Total", row.Values["label"]);
        Assert.Equal("100", row.Values["value"]);
        Assert.Equal("bold", row.Font);
        Assert.Equal("#FF0000", row.Color);
        Assert.Equal("16", row.Size);
    }

    [Fact]
    public void Parse_FlexItemProperties_AppliedToTable()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                grow: 1
                padding: "10"
                margin: "5"
                background: "#FFFFFF"
                columns:
                  - key: a
                rows:
                  - a: "test"
            """;

        var template = _parser.Parse(yaml);
        var table = Assert.IsType<TableElement>(template.Elements[0]);

        Assert.Equal(1f, table.Grow);
        Assert.Equal("10", table.Padding);
        Assert.Equal("5", table.Margin);
        Assert.Equal("#FFFFFF", table.Background);
    }

    [Fact]
    public void Parse_ColumnAlignValues_ParsedCorrectly()
    {
        const string yaml = """
            canvas:
              width: 300
            layout:
              - type: table
                columns:
                  - key: a
                    align: left
                  - key: b
                    align: center
                  - key: c
                    align: right
                  - key: d
                    align: start
                  - key: e
                    align: end
                rows:
                  - a: "1"
                    b: "2"
                    c: "3"
                    d: "4"
                    e: "5"
            """;

        var template = _parser.Parse(yaml);
        var table = Assert.IsType<TableElement>(template.Elements[0]);

        Assert.Equal(TextAlign.Left, table.Columns[0].Align);
        Assert.Equal(TextAlign.Center, table.Columns[1].Align);
        Assert.Equal(TextAlign.Right, table.Columns[2].Align);
        Assert.Equal(TextAlign.Start, table.Columns[3].Align);
        Assert.Equal(TextAlign.End, table.Columns[4].Align);
    }

    [Fact]
    public void Parse_TableIsRegisteredInSupportedTypes()
    {
        Assert.Contains("table", _parser.SupportedElementTypes);
    }
}
