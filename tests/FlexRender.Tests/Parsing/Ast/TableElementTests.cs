using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for <see cref="TableElement"/>, <see cref="TableColumn"/>, and <see cref="TableRow"/> AST classes.
/// </summary>
public sealed class TableElementTests
{
    [Fact]
    public void Constructor_WithValidColumns_SetsProperties()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "name", Label = "Name" },
            new() { Key = "value", Label = "Value" }
        };

        var table = new TableElement(columns);

        Assert.Equal(ElementType.Table, table.Type);
        Assert.Equal(2, table.Columns.Count);
        Assert.Empty(table.Rows);
        Assert.Null(table.ArrayPath);
        Assert.Null(table.ItemVariable);
    }

    [Fact]
    public void Constructor_WithColumnsAndRows_SetsProperties()
    {
        var columns = new List<TableColumn>
        {
            new() { Key = "name" }
        };

        var rows = new List<TableRow>
        {
            new() { Values = { ["name"] = "Alice" } }
        };

        var table = new TableElement(columns, rows);

        Assert.Single(table.Columns);
        Assert.Single(table.Rows);
    }

    [Fact]
    public void Constructor_WithNullColumns_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TableElement(null!));
    }

    [Fact]
    public void Constructor_WithEmptyColumns_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TableElement(Array.Empty<TableColumn>()));
    }

    [Fact]
    public void Constructor_WithNullRows_DefaultsToEmpty()
    {
        var columns = new List<TableColumn> { new() { Key = "a" } };

        var table = new TableElement(columns, null);

        Assert.Empty(table.Rows);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var columns = new List<TableColumn> { new() { Key = "a" } };
        var table = new TableElement(columns);

        Assert.Equal("main", table.Font);
        Assert.Equal("1em", table.Size);
        Assert.Equal("#000000", table.Color);
        Assert.Null(table.RowGap);
        Assert.Null(table.ColumnGap);
        Assert.Null(table.HeaderFont);
        Assert.Null(table.HeaderColor);
        Assert.Null(table.HeaderSize);
        Assert.Null(table.HeaderBorderBottom);
    }

    [Fact]
    public void TableColumn_DefaultValues_AreCorrect()
    {
        var col = new TableColumn();

        Assert.Equal("", col.Key);
        Assert.Null(col.Label);
        Assert.Null(col.Width);
        Assert.Equal(0f, col.Grow);
        Assert.Equal(TextAlign.Left, col.Align);
        Assert.Null(col.Font);
        Assert.Null(col.Color);
        Assert.Null(col.Size);
        Assert.Null(col.Format);
    }

    [Fact]
    public void TableRow_Values_AreCaseInsensitive()
    {
        var row = new TableRow();
        row.Values["Name"] = "Alice";

        Assert.True(row.Values.ContainsKey("name"));
        Assert.Equal("Alice", row.Values["name"]);
    }

    [Fact]
    public void TableRow_DefaultValues_AreCorrect()
    {
        var row = new TableRow();

        Assert.Empty(row.Values);
        Assert.Null(row.Font);
        Assert.Null(row.Color);
        Assert.Null(row.Size);
    }

    [Fact]
    public void Type_ReturnsTable()
    {
        var columns = new List<TableColumn> { new() { Key = "a" } };
        var table = new TableElement(columns);

        Assert.Equal(ElementType.Table, table.Type);
    }
}
