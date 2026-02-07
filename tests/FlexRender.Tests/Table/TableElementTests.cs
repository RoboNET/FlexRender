using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Table;

/// <summary>
/// Tests for the TableElement AST model.
/// </summary>
public sealed class TableElementTests
{
    // === ElementType Tests ===

    [Fact]
    public void Type_ReturnsTable()
    {
        // Arrange & Act
        var columns = new List<TableColumn> { new() { Key = "name" } };
        var table = new TableElement(columns);

        // Assert
        Assert.Equal(ElementType.Table, table.Type);
    }

    // === Dynamic Table (array-bound) ===

    [Fact]
    public void DynamicTable_WithArrayPath_SetsArrayPath()
    {
        // Arrange & Act
        var columns = new List<TableColumn> { new() { Key = "name" } };
        var table = new TableElement(columns)
        {
            ArrayPath = "items"
        };

        // Assert
        Assert.Equal("items", table.ArrayPath);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void DynamicTable_WithItemVariable_SetsItemVariable()
    {
        // Arrange & Act
        var columns = new List<TableColumn> { new() { Key = "name" } };
        var table = new TableElement(columns)
        {
            ArrayPath = "items",
            ItemVariable = "item"
        };

        // Assert
        Assert.Equal("item", table.ItemVariable);
    }

    // === Static Table (rows) ===

    [Fact]
    public void StaticTable_WithRows_SetsRows()
    {
        // Arrange
        var columns = new List<TableColumn>
        {
            new() { Key = "label" },
            new() { Key = "value" }
        };
        var rows = new List<TableRow>
        {
            new() { Values = { ["label"] = "Subtotal", ["value"] = "42.50 $" } },
            new() { Values = { ["label"] = "Tax", ["value"] = "4.25 $" } }
        };

        // Act
        var table = new TableElement(columns, rows);

        // Assert
        Assert.Equal(2, table.Rows.Count);
        Assert.Null(table.ArrayPath);
    }

    // === Column Configuration ===

    [Fact]
    public void Columns_WithFullConfiguration_SetsAllProperties()
    {
        // Arrange & Act
        var column = new TableColumn
        {
            Key = "price",
            Label = "Price",
            Width = "60",
            Grow = 0,
            Align = TextAlign.Right,
            Font = "bold",
            Color = "#333333",
            Size = "14",
            Format = "{{item.price}} $"
        };

        // Assert
        Assert.Equal("price", column.Key);
        Assert.Equal("Price", column.Label);
        Assert.Equal("60", column.Width);
        Assert.Equal(0, column.Grow);
        Assert.Equal(TextAlign.Right, column.Align);
        Assert.Equal("bold", column.Font);
        Assert.Equal("#333333", column.Color);
        Assert.Equal("14", column.Size);
        Assert.Equal("{{item.price}} $", column.Format);
    }

    [Fact]
    public void Columns_WithMinimalConfiguration_HasDefaults()
    {
        // Arrange & Act
        var column = new TableColumn
        {
            Key = "name"
        };

        // Assert
        Assert.Equal("name", column.Key);
        Assert.Null(column.Label);
        Assert.Null(column.Width);
        Assert.Equal(TextAlign.Left, column.Align);
        Assert.Null(column.Font);
        Assert.Null(column.Color);
        Assert.Null(column.Size);
        Assert.Null(column.Format);
    }

    [Fact]
    public void Columns_WithGrow_SetsFlexGrowFactor()
    {
        // Arrange & Act
        var column = new TableColumn
        {
            Key = "name",
            Grow = 1
        };

        // Assert
        Assert.Equal(1, column.Grow);
    }

    // === Table-level styling defaults ===

    [Fact]
    public void TableElement_WithStylingDefaults_SetsProperties()
    {
        // Arrange & Act
        var columns = new List<TableColumn> { new() { Key = "name" } };
        var table = new TableElement(columns)
        {
            Font = "main",
            Size = "12",
            Color = "#333333",
            RowGap = "4",
            ColumnGap = "8"
        };

        // Assert
        Assert.Equal("main", table.Font);
        Assert.Equal("12", table.Size);
        Assert.Equal("#333333", table.Color);
        Assert.Equal("4", table.RowGap);
        Assert.Equal("8", table.ColumnGap);
    }

    // === Header styling ===

    [Fact]
    public void TableElement_WithHeaderStyling_SetsHeaderProperties()
    {
        // Arrange & Act
        var columns = new List<TableColumn> { new() { Key = "name" } };
        var table = new TableElement(columns)
        {
            HeaderFont = "bold",
            HeaderColor = "#000000",
            HeaderSize = "14",
            HeaderBorderBottom = "1 solid #cccccc"
        };

        // Assert
        Assert.Equal("bold", table.HeaderFont);
        Assert.Equal("#000000", table.HeaderColor);
        Assert.Equal("14", table.HeaderSize);
        Assert.Equal("1 solid #cccccc", table.HeaderBorderBottom);
    }

    // === Base TemplateElement properties inherited ===

    [Fact]
    public void TableElement_InheritsBaseProperties()
    {
        // Arrange & Act
        var columns = new List<TableColumn> { new() { Key = "name" } };
        var table = new TableElement(columns)
        {
            Padding = "8",
            Margin = "4",
            Background = "#ffffff",
            Grow = 1
        };

        // Assert
        Assert.Equal("8", table.Padding);
        Assert.Equal("4", table.Margin);
        Assert.Equal("#ffffff", table.Background);
        Assert.Equal(1, table.Grow);
    }

    // === Constructor validation ===

    [Fact]
    public void Constructor_NullColumns_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TableElement(null!));
    }

    [Fact]
    public void Constructor_EmptyColumns_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TableElement(new List<TableColumn>()));
    }

    [Fact]
    public void Constructor_NoRows_DefaultsToEmptyList()
    {
        var columns = new List<TableColumn> { new() { Key = "name" } };
        var table = new TableElement(columns);

        Assert.NotNull(table.Rows);
        Assert.Empty(table.Rows);
    }
}
