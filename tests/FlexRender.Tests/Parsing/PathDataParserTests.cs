using System.Collections.Generic;
using FlexRender.Parsing;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Edge-case tests for the hand-written absolute-only SVG-style path tokenizer.
/// </summary>
public sealed class PathDataParserTests
{
    [Fact]
    public void Parse_MoveAndLine_ProducesTwoCommands()
    {
        var commands = PathDataParser.Parse("M 0 0 L 100 50");

        Assert.Equal(2, commands.Count);
        Assert.Equal(PathCommandKind.MoveTo, commands[0].Kind);
        Assert.Equal(0f, commands[0].Points[0].X);
        Assert.Equal(0f, commands[0].Points[0].Y);
        Assert.Equal(PathCommandKind.LineTo, commands[1].Kind);
        Assert.Equal(100f, commands[1].Points[0].X);
        Assert.Equal(50f, commands[1].Points[0].Y);
    }

    [Fact]
    public void Parse_QuadraticAndCubicAndClose_ProducesAllCommands()
    {
        var commands = PathDataParser.Parse("M 0 0 Q 150 0 200 50 C 10 20 30 40 50 60 Z");

        Assert.Equal(4, commands.Count);
        Assert.Equal(PathCommandKind.MoveTo, commands[0].Kind);
        Assert.Equal(PathCommandKind.QuadTo, commands[1].Kind);
        Assert.Equal(2, commands[1].Points.Count);
        Assert.Equal(PathCommandKind.CubicTo, commands[2].Kind);
        Assert.Equal(3, commands[2].Points.Count);
        Assert.Equal(PathCommandKind.Close, commands[3].Kind);
        Assert.Empty(commands[3].Points);
    }

    [Fact]
    public void Parse_CommaSeparatedCoordinates_ParsesCorrectly()
    {
        var commands = PathDataParser.Parse("M0,0 L100,50");

        Assert.Equal(2, commands.Count);
        Assert.Equal(100f, commands[1].Points[0].X);
        Assert.Equal(50f, commands[1].Points[0].Y);
    }

    [Fact]
    public void Parse_NegativeAndDecimalCoordinates_ParsesCorrectly()
    {
        var commands = PathDataParser.Parse("M -1.5 -2.25 L 3.0 -4");

        Assert.Equal(-1.5f, commands[0].Points[0].X);
        Assert.Equal(-2.25f, commands[0].Points[0].Y);
        Assert.Equal(3.0f, commands[1].Points[0].X);
        Assert.Equal(-4f, commands[1].Points[0].Y);
    }

    [Fact]
    public void Parse_ImplicitRepeatedLineTo_AfterSingleCommandLetter()
    {
        // SVG semantics: "L 10 10 20 20" means two LineTo commands.
        var commands = PathDataParser.Parse("M 0 0 L 10 10 20 20");

        Assert.Equal(3, commands.Count);
        Assert.Equal(PathCommandKind.LineTo, commands[1].Kind);
        Assert.Equal(10f, commands[1].Points[0].X);
        Assert.Equal(PathCommandKind.LineTo, commands[2].Kind);
        Assert.Equal(20f, commands[2].Points[0].X);
        Assert.Equal(20f, commands[2].Points[0].Y);
    }

    [Fact]
    public void Parse_LowercaseCommands_TreatedAsAbsolute()
    {
        // Lowercase (relative) letters are accepted but treated as absolute,
        // matching the spec's "absolute only" constraint without erroring on case.
        var commands = PathDataParser.Parse("m 0 0 l 100 50");

        Assert.Equal(PathCommandKind.MoveTo, commands[0].Kind);
        Assert.Equal(PathCommandKind.LineTo, commands[1].Kind);
        Assert.Equal(100f, commands[1].Points[0].X);
    }

    [Fact]
    public void Parse_ExtraWhitespace_Ignored()
    {
        var commands = PathDataParser.Parse("  M   0    0\tL\n100  50  ");
        Assert.Equal(2, commands.Count);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        Assert.Empty(PathDataParser.Parse(""));
        Assert.Empty(PathDataParser.Parse("   "));
    }

    [Fact]
    public void Parse_UnknownCommand_ThrowsWithCommandAndPosition()
    {
        var ex = Assert.Throws<PathParseException>(() => PathDataParser.Parse("M 0 0 X 1 1"));
        Assert.Contains("'X'", ex.Message);
        Assert.Contains("position", ex.Message);
    }

    [Fact]
    public void Parse_MissingCoordinate_ThrowsWithCommand()
    {
        var ex = Assert.Throws<PathParseException>(() => PathDataParser.Parse("M 0 0 L 10"));
        Assert.Contains("'L'", ex.Message);
    }

    [Fact]
    public void Parse_DataBeforeFirstCommand_Throws()
    {
        var ex = Assert.Throws<PathParseException>(() => PathDataParser.Parse("10 20 L 30 40"));
        Assert.Contains("position", ex.Message);
    }

    [Fact]
    public void Parse_NullArgument_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => PathDataParser.Parse(null!));
    }
}
