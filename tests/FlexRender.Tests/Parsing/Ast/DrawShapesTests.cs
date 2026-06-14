using System.Collections.Generic;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the immutable draw-shape DTOs.
/// </summary>
public sealed class DrawShapesTests
{
    [Fact]
    public void DrawLine_StoresCoordinatesAndStroke()
    {
        var line = new DrawLine(0f, 100f, 400f, 50f, "#333333", 2f);
        Assert.Equal(0f, line.X1);
        Assert.Equal(100f, line.Y1);
        Assert.Equal(400f, line.X2);
        Assert.Equal(50f, line.Y2);
        Assert.Equal("#333333", line.Stroke);
        Assert.Equal(2f, line.StrokeWidth);
    }

    [Fact]
    public void DrawPolyline_StoresPointsAndStroke()
    {
        var points = new List<PathPoint> { new(0f, 10f), new(50f, 40f) };
        var polyline = new DrawPolyline(points, "#4A90D9", 1f, fill: null);
        Assert.Equal(2, polyline.Points.Count);
        Assert.Equal("#4A90D9", polyline.Stroke);
    }

    [Fact]
    public void DrawRect_StoresGeometryFillStrokeRadius()
    {
        var rect = new DrawRect(10f, 10f, 80f, 40f, "#eeeeee", stroke: null, strokeWidth: 0f, radius: 4f);
        Assert.Equal(10f, rect.X);
        Assert.Equal(80f, rect.Width);
        Assert.Equal("#eeeeee", rect.Fill);
        Assert.Equal(4f, rect.Radius);
    }

    [Fact]
    public void DrawCircle_StoresCenterRadiusFill()
    {
        var circle = new DrawCircle(200f, 75f, 30f, "#e74c3c", stroke: null, strokeWidth: 0f);
        Assert.Equal(200f, circle.Cx);
        Assert.Equal(75f, circle.Cy);
        Assert.Equal(30f, circle.R);
        Assert.Equal("#e74c3c", circle.Fill);
    }

    [Fact]
    public void DrawPath_StoresCommandsFillStroke()
    {
        var commands = PathDataParser.Parse("M 0 0 L 100 50 Z");
        var path = new DrawPath(commands, "#2ecc71", stroke: null, strokeWidth: 0f);
        Assert.Equal(3, path.Commands.Count);
        Assert.Equal("#2ecc71", path.Fill);
    }
}
