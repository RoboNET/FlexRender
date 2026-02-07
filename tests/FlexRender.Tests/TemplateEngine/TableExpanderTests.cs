using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

/// <summary>
/// Tests for TableElement expansion in <see cref="TemplateExpander"/>.
/// </summary>
public sealed class TableExpanderTests
{
    private readonly TemplateExpander _expander = new();

    private static Template CreateTemplate(params TemplateElement[] elements)
    {
        var template = new Template();
        foreach (var element in elements)
        {
            template.AddElement(element);
        }
        return template;
    }

    // === Dynamic Table Tests ===

    [Fact]
    public void Expand_DynamicTable_CreatesOuterColumnFlex()
    {
        var table = CreateDynamicTable("items", "item",
            new TableColumn { Key = "name", Label = "Name", Grow = 1 });

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Item1") }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(FlexDirection.Column, outerFlex.Direction);
    }

    [Fact]
    public void Expand_DynamicTable_WithHeaders_CreatesHeaderRow()
    {
        var table = CreateDynamicTable("items", "item",
            new TableColumn { Key = "name", Label = "Name", Grow = 1 },
            new TableColumn { Key = "price", Label = "Price", Width = "60", Align = TextAlign.Right });

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue
                {
                    ["name"] = new StringValue("Apples"),
                    ["price"] = new StringValue("3.50")
                }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        // First child should be the header row
        var headerRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        Assert.Equal(FlexDirection.Row, headerRow.Direction);
        Assert.Equal(2, headerRow.Children.Count);

        var nameHeader = Assert.IsType<TextElement>(headerRow.Children[0]);
        Assert.Equal("Name", nameHeader.Content);
        Assert.Equal(1f, nameHeader.Grow);

        var priceHeader = Assert.IsType<TextElement>(headerRow.Children[1]);
        Assert.Equal("Price", priceHeader.Content);
        Assert.Equal("60", priceHeader.Width);
        Assert.Equal(TextAlign.Right, priceHeader.Align);
    }

    [Fact]
    public void Expand_DynamicTable_WithData_CreatesDataRows()
    {
        var table = CreateDynamicTable("items", "item",
            new TableColumn { Key = "name", Grow = 1 });

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("A") },
                new ObjectValue { ["name"] = new StringValue("B") },
                new ObjectValue { ["name"] = new StringValue("C") }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        // No headers (no labels), so all 3 children should be data row FlexElements
        Assert.Equal(3, outerFlex.Children.Count);

        foreach (var child in outerFlex.Children)
        {
            var rowFlex = Assert.IsType<FlexElement>(child);
            Assert.Equal(FlexDirection.Row, rowFlex.Direction);
            var cell = Assert.IsType<TextElement>(rowFlex.Children[0]);
            Assert.NotEmpty(cell.Content);
        }
    }

    [Fact]
    public void Expand_DynamicTable_WithEmptyArray_OnlyHeaders()
    {
        var table = CreateDynamicTable("items", "item",
            new TableColumn { Key = "name", Label = "Name", Grow = 1 });

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        // Just the header row, no data rows
        Assert.Single(outerFlex.Children);
        Assert.IsType<FlexElement>(outerFlex.Children[0]);
    }

    [Fact]
    public void Expand_DynamicTable_WithoutItemVariable_UsesDirectAccess()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 }
        };

        var table = new TableElement(columns)
        {
            ArrayPath = "items"
            // No ItemVariable set
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Direct") }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var rowFlex = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var cell = Assert.IsType<TextElement>(rowFlex.Children[0]);
        Assert.Equal("Direct", cell.Content);
    }

    [Fact]
    public void Expand_DynamicTable_WithFormat_UsesFormatString()
    {
        var table = CreateDynamicTable("items", "item",
            new TableColumn { Key = "price", Grow = 1, Format = "{{item.price}} $" });

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["price"] = new StringValue("9.99") }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var rowFlex = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var cell = Assert.IsType<TextElement>(rowFlex.Children[0]);
        Assert.Equal("9.99 $", cell.Content);
    }

    // === Static Table Tests ===

    [Fact]
    public void Expand_StaticTable_CreatesRows()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "label", Grow = 1 },
            new() { Key = "value", Width = "80", Align = TextAlign.Right }
        };

        var rows = new List<TableRow>
        {
            new() { Values = { ["label"] = "Subtotal", ["value"] = "42.50" } },
            new() { Values = { ["label"] = "Tax", ["value"] = "4.25" } }
        };

        var table = new TableElement(columns, rows);

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        // No headers (no labels), so 2 data rows
        Assert.Equal(2, outerFlex.Children.Count);

        var firstRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var labelCell = Assert.IsType<TextElement>(firstRow.Children[0]);
        Assert.Equal("Subtotal", labelCell.Content);
        Assert.Equal(1f, labelCell.Grow);

        var valueCell = Assert.IsType<TextElement>(firstRow.Children[1]);
        Assert.Equal("42.50", valueCell.Content);
        Assert.Equal("80", valueCell.Width);
        Assert.Equal(TextAlign.Right, valueCell.Align);
    }

    [Fact]
    public void Expand_StaticTable_WithHeaders_IncludesHeaderRow()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "label", Label = "Description", Grow = 1 },
            new() { Key = "value", Label = "Amount", Width = "80" }
        };

        var rows = new List<TableRow>
        {
            new() { Values = { ["label"] = "Total", ["value"] = "100" } }
        };

        var table = new TableElement(columns, rows);

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        // Header row + 1 data row
        Assert.Equal(2, outerFlex.Children.Count);

        var headerRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var headerCell = Assert.IsType<TextElement>(headerRow.Children[0]);
        Assert.Equal("Description", headerCell.Content);
    }

    [Fact]
    public void Expand_StaticTable_PerRowStyling_Applied()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "label", Grow = 1 }
        };

        var rows = new List<TableRow>
        {
            new() { Values = { ["label"] = "Total" }, Font = "bold", Color = "#FF0000", Size = "16" }
        };

        var table = new TableElement(columns, rows)
        {
            Font = "main",
            Color = "#000000",
            Size = "12"
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var rowFlex = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var cell = Assert.IsType<TextElement>(rowFlex.Children[0]);

        // Row-level styling should override table defaults
        Assert.Equal("bold", cell.Font);
        Assert.Equal("#FF0000", cell.Color);
        Assert.Equal("16", cell.Size);
    }

    // === Header Border Bottom Tests ===

    [Fact]
    public void Expand_WithHeaderBorderBottom_CreatesSeparator()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Label = "Name", Grow = 1 }
        };

        var table = new TableElement(columns)
        {
            ArrayPath = "items",
            HeaderBorderBottom = "solid"
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Test") }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        // header row + separator + data row
        Assert.Equal(3, outerFlex.Children.Count);
        Assert.IsType<FlexElement>(outerFlex.Children[0]); // header
        var sep = Assert.IsType<SeparatorElement>(outerFlex.Children[1]); // separator
        Assert.Equal(SeparatorStyle.Solid, sep.Style);
        Assert.IsType<FlexElement>(outerFlex.Children[2]); // data row
    }

    [Fact]
    public void Expand_HeaderBorderBottomTrue_UsesDottedStyle()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Label = "Name", Grow = 1 }
        };

        var table = new TableElement(columns)
        {
            ArrayPath = "items",
            HeaderBorderBottom = "true"
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var sep = Assert.IsType<SeparatorElement>(outerFlex.Children[1]);
        Assert.Equal(SeparatorStyle.Dotted, sep.Style);
    }

    // === Spacing Tests ===

    [Fact]
    public void Expand_RowGap_AppliedToOuterFlex()
    {
        var table = CreateDynamicTable("items", "item",
            new TableColumn { Key = "name", Grow = 1 });
        table.RowGap = "4";

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("A") }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal("4", outerFlex.Gap);
    }

    [Fact]
    public void Expand_ColumnGap_AppliedToRowFlex()
    {
        var table = CreateDynamicTable("items", "item",
            new TableColumn { Key = "a", Grow = 1 },
            new TableColumn { Key = "b", Grow = 1 });
        table.ColumnGap = "8";

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue
                {
                    ["a"] = new StringValue("1"),
                    ["b"] = new StringValue("2")
                }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var rowFlex = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        Assert.Equal("8", rowFlex.Gap);
    }

    // === Style Inheritance Tests ===

    [Fact]
    public void Expand_HeaderStyling_OverridesTableDefaults()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Label = "Name", Grow = 1 }
        };

        var table = new TableElement(columns)
        {
            Font = "main",
            Color = "#000000",
            Size = "12",
            HeaderFont = "bold",
            HeaderColor = "#FF0000",
            HeaderSize = "14",
            ArrayPath = "items"
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var headerRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var headerCell = Assert.IsType<TextElement>(headerRow.Children[0]);

        Assert.Equal("bold", headerCell.Font);
        Assert.Equal("#FF0000", headerCell.Color);
        Assert.Equal("14", headerCell.Size);
    }

    [Fact]
    public void Expand_ColumnStyling_OverridesTableDefaults()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1, Font = "italic", Color = "#00FF00", Size = "16" }
        };

        var table = new TableElement(columns)
        {
            Font = "main",
            Color = "#000000",
            Size = "12",
            ArrayPath = "items"
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Test") }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var rowFlex = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var cell = Assert.IsType<TextElement>(rowFlex.Children[0]);

        Assert.Equal("italic", cell.Font);
        Assert.Equal("#00FF00", cell.Color);
        Assert.Equal("16", cell.Size);
    }

    // === Base Properties Tests ===

    [Fact]
    public void Expand_BaseProperties_CopiedToOuterFlex()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "a", Grow = 1 }
        };

        var table = new TableElement(columns)
        {
            Padding = "10",
            Margin = "5",
            Background = "#FFFFFF",
            ArrayPath = "items"
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal("10", outerFlex.Padding);
        Assert.Equal("5", outerFlex.Margin);
        Assert.Equal("#FFFFFF", outerFlex.Background);
    }

    [Fact]
    public void Expand_MissingKeyInData_ResolvesToEmptyString()
    {
        var table = CreateDynamicTable("items", "item",
            new TableColumn { Key = "missing_key", Grow = 1 });

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["other"] = new StringValue("value") }
            })
        };

        var result = _expander.Expand(template, data);

        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var rowFlex = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var cell = Assert.IsType<TextElement>(rowFlex.Children[0]);
        // Missing key resolves to empty string (via template expression)
        Assert.Equal("", cell.Content);
    }

    // === Helper Methods ===

    private static TableElement CreateDynamicTable(string arrayPath, string itemVariable, params TableColumn[] columns)
    {
        return new TableElement(columns.ToList())
        {
            ArrayPath = arrayPath,
            ItemVariable = itemVariable
        };
    }
}
