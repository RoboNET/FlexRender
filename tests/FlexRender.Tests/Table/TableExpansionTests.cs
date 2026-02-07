using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Table;

/// <summary>
/// Tests for TableElement expansion into FlexElement tree via TemplateExpander.
/// </summary>
public sealed class TableExpansionTests
{
    private readonly TemplateExpander _expander = new(new ResourceLimits(), FilterRegistry.CreateDefault());

    private static Template CreateTemplate(params TemplateElement[] elements)
    {
        var template = new Template();
        foreach (var element in elements)
        {
            template.AddElement(element);
        }
        return template;
    }

    private static TableElement CreateDynamicTable(
        List<TableColumn> columns,
        string arrayPath = "items",
        string? itemVariable = null)
    {
        var table = new TableElement(columns)
        {
            ArrayPath = arrayPath
        };
        if (itemVariable is not null)
        {
            table.ItemVariable = itemVariable;
        }
        return table;
    }

    // === Dynamic table expansion ===

    [Fact]
    public void Expand_DynamicTable_CreatesFlexContainerWithRows()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 },
            new() { Key = "price", Width = "60", Align = TextAlign.Right }
        };
        var table = CreateDynamicTable(columns);

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Coffee"), ["price"] = new StringValue("$4.50") },
                new ObjectValue { ["name"] = new StringValue("Tea"), ["price"] = new StringValue("$3.00") }
            })
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: should produce a column FlexElement wrapping rows
        Assert.Single(result.Elements);
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);

        // Should have 2 data rows (no header since no labels)
        Assert.Equal(2, outerFlex.Children.Count);
    }

    [Fact]
    public void Expand_DynamicTableWithHeaders_CreatesHeaderRowAndDataRows()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Label = "Item", Grow = 1 },
            new() { Key = "price", Label = "Price", Width = "60", Align = TextAlign.Right }
        };
        var table = CreateDynamicTable(columns);

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Coffee"), ["price"] = new StringValue("$4.50") }
            })
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: should have header row + data rows
        Assert.Single(result.Elements);
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);

        // With labels: header row + 1 data row = at least 2 children
        Assert.True(outerFlex.Children.Count >= 2,
            $"Expected at least 2 children (header + data row), got {outerFlex.Children.Count}");

        // First child should be a header row (FlexElement with direction: row)
        var headerRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);

        // Header row should contain TextElements with label content
        Assert.Equal(2, headerRow.Children.Count);
        var headerText1 = Assert.IsType<TextElement>(headerRow.Children[0]);
        var headerText2 = Assert.IsType<TextElement>(headerRow.Children[1]);
        Assert.Equal("Item", headerText1.Content);
        Assert.Equal("Price", headerText2.Content);
    }

    [Fact]
    public void Expand_DynamicTable_DataRowsHaveCorrectContent()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 },
            new() { Key = "price", Width = "60" }
        };
        var table = CreateDynamicTable(columns);

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Coffee"), ["price"] = new StringValue("$4.50") }
            })
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: the data row should contain TextElements with substituted values
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var dataRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        Assert.Equal(2, dataRow.Children.Count);

        var cell1 = Assert.IsType<TextElement>(dataRow.Children[0]);
        var cell2 = Assert.IsType<TextElement>(dataRow.Children[1]);
        Assert.Equal("Coffee", cell1.Content);
        Assert.Equal("$4.50", cell2.Content);
    }

    [Fact]
    public void Expand_DynamicTableWithFormat_UsesFormatString()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 },
            new() { Key = "price", Width = "60", Format = "{{item.price}} $" }
        };
        var table = CreateDynamicTable(columns, itemVariable: "item");

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Coffee"), ["price"] = new StringValue("4.50") }
            })
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: price column should use format string
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var dataRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var priceCell = Assert.IsType<TextElement>(dataRow.Children[1]);
        Assert.Equal("4.50 $", priceCell.Content);
    }

    [Fact]
    public void Expand_DynamicTableWithAsVariable_SetsItemVariable()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 }
        };
        var table = CreateDynamicTable(columns, itemVariable: "item");

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Coffee") }
            })
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: should expand correctly with 'as' variable
        Assert.Single(result.Elements);
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.True(outerFlex.Children.Count >= 1);
    }

    // === Static table expansion ===

    [Fact]
    public void Expand_StaticTableWithRows_CreatesFlexContainerWithRows()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "label", Grow = 1 },
            new() { Key = "value", Width = "80", Align = TextAlign.Right }
        };
        var rows = new List<TableRow>
        {
            new() { Values = { ["label"] = "Subtotal", ["value"] = "42.50 $" } },
            new() { Values = { ["label"] = "Tax", ["value"] = "4.25 $" } }
        };
        var table = new TableElement(columns, rows);

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        // Act
        var result = _expander.Expand(template, data);

        // Assert
        Assert.Single(result.Elements);
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal(2, outerFlex.Children.Count);

        // Each row should be a FlexElement with direction Row
        var row1 = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        Assert.Equal(2, row1.Children.Count);

        var label1 = Assert.IsType<TextElement>(row1.Children[0]);
        var value1 = Assert.IsType<TextElement>(row1.Children[1]);
        Assert.Equal("Subtotal", label1.Content);
        Assert.Equal("42.50 $", value1.Content);
    }

    // === Column property propagation ===

    [Fact]
    public void Expand_ColumnWidth_AppliedToEachCell()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 },
            new() { Key = "price", Width = "60" }
        };
        var rows = new List<TableRow>
        {
            new() { Values = { ["name"] = "Coffee", ["price"] = "$4.50" } }
        };
        var table = new TableElement(columns, rows);

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        // Act
        var result = _expander.Expand(template, data);

        // Assert: the second column cell should have width "60"
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var dataRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var priceCell = Assert.IsType<TextElement>(dataRow.Children[1]);
        Assert.Equal("60", priceCell.Width);
    }

    [Fact]
    public void Expand_ColumnGrow_AppliedToCell()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 }
        };
        var rows = new List<TableRow>
        {
            new() { Values = { ["name"] = "Coffee" } }
        };
        var table = new TableElement(columns, rows);

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        // Act
        var result = _expander.Expand(template, data);

        // Assert: the cell should have grow=1
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var dataRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var nameCell = Assert.IsType<TextElement>(dataRow.Children[0]);
        Assert.Equal(1, nameCell.Grow);
    }

    [Fact]
    public void Expand_ColumnAlign_AppliedToTextElement()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "price", Align = TextAlign.Right }
        };
        var rows = new List<TableRow>
        {
            new() { Values = { ["price"] = "$4.50" } }
        };
        var table = new TableElement(columns, rows);

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        // Act
        var result = _expander.Expand(template, data);

        // Assert
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var dataRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var priceCell = Assert.IsType<TextElement>(dataRow.Children[0]);
        Assert.Equal(TextAlign.Right, priceCell.Align);
    }

    [Fact]
    public void Expand_ColumnFontAndColor_AppliedToCell()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "total", Font = "bold", Color = "#ff0000", Size = "16" }
        };
        var rows = new List<TableRow>
        {
            new() { Values = { ["total"] = "$46.75" } }
        };
        var table = new TableElement(columns, rows);

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        // Act
        var result = _expander.Expand(template, data);

        // Assert
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var dataRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var totalCell = Assert.IsType<TextElement>(dataRow.Children[0]);
        Assert.Equal("bold", totalCell.Font);
        Assert.Equal("#ff0000", totalCell.Color);
        Assert.Equal("16", totalCell.Size);
    }

    // === Edge cases ===

    [Fact]
    public void Expand_EmptyArray_HeaderRendersNoDataRows()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Label = "Item", Grow = 1 }
        };
        var table = CreateDynamicTable(columns);

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>())
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: header should still render, but no data rows
        Assert.Single(result.Elements);
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);

        // Should have at least the header row
        Assert.True(outerFlex.Children.Count >= 1,
            "Expected at least header row for empty array");

        // First child should be header
        var headerRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var headerText = Assert.IsType<TextElement>(headerRow.Children[0]);
        Assert.Equal("Item", headerText.Content);
    }

    [Fact]
    public void Expand_MissingKeyInData_ResolvesToEmptyString()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 },
            new() { Key = "nonexistent", Width = "60" }
        };
        var table = CreateDynamicTable(columns);

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Coffee") }
            })
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: missing key should resolve to empty string
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        var dataRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        var missingCell = Assert.IsType<TextElement>(dataRow.Children[1]);
        Assert.Equal("", missingCell.Content);
    }

    // === Spacing ===

    [Fact]
    public void Expand_TableWithGap_SetsGapOnFlexContainers()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Grow = 1 },
            new() { Key = "price", Width = "60" }
        };
        var rows = new List<TableRow>
        {
            new() { Values = { ["name"] = "Coffee", ["price"] = "$4.50" } }
        };
        var table = new TableElement(columns, rows)
        {
            RowGap = "4",
            ColumnGap = "8"
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue();

        // Act
        var result = _expander.Expand(template, data);

        // Assert: the outer flex should have rowGap, inner row flex should have columnGap
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.Equal("4", outerFlex.Gap);

        var dataRow = Assert.IsType<FlexElement>(outerFlex.Children[0]);
        Assert.Equal("8", dataRow.Gap);
    }

    // === Header with border bottom (separator) ===

    [Fact]
    public void Expand_HeaderBorderBottom_CreatesSeparatorAfterHeader()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Label = "Item", Grow = 1 }
        };
        var table = new TableElement(columns)
        {
            ArrayPath = "items",
            HeaderBorderBottom = "1 solid #cccccc"
        };

        var template = CreateTemplate(table);
        var data = new ObjectValue
        {
            ["items"] = new ArrayValue(new List<TemplateValue>
            {
                new ObjectValue { ["name"] = new StringValue("Coffee") }
            })
        };

        // Act
        var result = _expander.Expand(template, data);

        // Assert: should have header, separator, data row
        var outerFlex = Assert.IsType<FlexElement>(result.Elements[0]);
        Assert.True(outerFlex.Children.Count >= 3,
            "Expected header + separator + data row");

        // Second child should be a SeparatorElement
        Assert.IsType<SeparatorElement>(outerFlex.Children[1]);
    }
}
