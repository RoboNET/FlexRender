using System.Collections.Generic;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Base type for a single shape inside a <see cref="DrawElement"/>.
/// All coordinates are absolute, relative to the draw element's top-left corner.
/// </summary>
public abstract record DrawShape;

/// <summary>
/// A straight line segment.
/// </summary>
public sealed record DrawLine : DrawShape
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawLine"/> record.
    /// </summary>
    /// <param name="x1">Start X.</param>
    /// <param name="y1">Start Y.</param>
    /// <param name="x2">End X.</param>
    /// <param name="y2">End Y.</param>
    /// <param name="stroke">Stroke color (hex). Null means default black.</param>
    /// <param name="strokeWidth">Stroke width in pixels.</param>
    public DrawLine(float x1, float y1, float x2, float y2, string? stroke, float strokeWidth)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
    }

    /// <summary>Start X.</summary>
    public float X1 { get; init; }

    /// <summary>Start Y.</summary>
    public float Y1 { get; init; }

    /// <summary>End X.</summary>
    public float X2 { get; init; }

    /// <summary>End Y.</summary>
    public float Y2 { get; init; }

    /// <summary>Stroke color (hex). Null means default black.</summary>
    public string? Stroke { get; init; }

    /// <summary>Stroke width in pixels.</summary>
    public float StrokeWidth { get; init; }
}

/// <summary>
/// A connected sequence of line segments through the given points.
/// </summary>
public sealed record DrawPolyline : DrawShape
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawPolyline"/> record.
    /// </summary>
    /// <param name="points">The vertices in order.</param>
    /// <param name="stroke">Stroke color (hex). Null means default black.</param>
    /// <param name="strokeWidth">Stroke width in pixels.</param>
    /// <param name="fill">Optional fill color (hex) for the enclosed area. Null means no fill.</param>
    public DrawPolyline(IReadOnlyList<PathPoint> points, string? stroke, float strokeWidth, string? fill)
    {
        Points = points;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        Fill = fill;
    }

    /// <summary>The vertices in order.</summary>
    public IReadOnlyList<PathPoint> Points { get; init; }

    /// <summary>Stroke color (hex). Null means default black.</summary>
    public string? Stroke { get; init; }

    /// <summary>Stroke width in pixels.</summary>
    public float StrokeWidth { get; init; }

    /// <summary>Optional fill color (hex) for the enclosed area. Null means no fill.</summary>
    public string? Fill { get; init; }
}

/// <summary>
/// A rectangle, optionally rounded.
/// </summary>
public sealed record DrawRect : DrawShape
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawRect"/> record.
    /// </summary>
    /// <param name="x">Top-left X.</param>
    /// <param name="y">Top-left Y.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="fill">Optional fill color (hex). Null means no fill.</param>
    /// <param name="stroke">Optional stroke color (hex). Null means no stroke.</param>
    /// <param name="strokeWidth">Stroke width in pixels.</param>
    /// <param name="radius">Corner radius in pixels.</param>
    public DrawRect(
        float x, float y, float width, float height,
        string? fill, string? stroke, float strokeWidth, float radius)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Fill = fill;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        Radius = radius;
    }

    /// <summary>Top-left X.</summary>
    public float X { get; init; }

    /// <summary>Top-left Y.</summary>
    public float Y { get; init; }

    /// <summary>Width in pixels.</summary>
    public float Width { get; init; }

    /// <summary>Height in pixels.</summary>
    public float Height { get; init; }

    /// <summary>Optional fill color (hex). Null means no fill.</summary>
    public string? Fill { get; init; }

    /// <summary>Optional stroke color (hex). Null means no stroke.</summary>
    public string? Stroke { get; init; }

    /// <summary>Stroke width in pixels.</summary>
    public float StrokeWidth { get; init; }

    /// <summary>Corner radius in pixels.</summary>
    public float Radius { get; init; }
}

/// <summary>
/// A circle centred at (Cx, Cy) with radius R.
/// </summary>
public sealed record DrawCircle : DrawShape
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawCircle"/> record.
    /// </summary>
    /// <param name="cx">Centre X.</param>
    /// <param name="cy">Centre Y.</param>
    /// <param name="r">Radius in pixels.</param>
    /// <param name="fill">Optional fill color (hex). Null means no fill.</param>
    /// <param name="stroke">Optional stroke color (hex). Null means no stroke.</param>
    /// <param name="strokeWidth">Stroke width in pixels.</param>
    public DrawCircle(float cx, float cy, float r, string? fill, string? stroke, float strokeWidth)
    {
        Cx = cx;
        Cy = cy;
        R = r;
        Fill = fill;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
    }

    /// <summary>Centre X.</summary>
    public float Cx { get; init; }

    /// <summary>Centre Y.</summary>
    public float Cy { get; init; }

    /// <summary>Radius in pixels.</summary>
    public float R { get; init; }

    /// <summary>Optional fill color (hex). Null means no fill.</summary>
    public string? Fill { get; init; }

    /// <summary>Optional stroke color (hex). Null means no stroke.</summary>
    public string? Stroke { get; init; }

    /// <summary>Stroke width in pixels.</summary>
    public float StrokeWidth { get; init; }
}

/// <summary>
/// A free-form path made of pre-parsed absolute commands.
/// </summary>
public sealed record DrawPath : DrawShape
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawPath"/> record.
    /// </summary>
    /// <param name="commands">The parsed path commands (M/L/Q/C/Z).</param>
    /// <param name="fill">Optional fill color (hex). Null means no fill.</param>
    /// <param name="stroke">Optional stroke color (hex). Null means no stroke.</param>
    /// <param name="strokeWidth">Stroke width in pixels.</param>
    public DrawPath(IReadOnlyList<PathCommand> commands, string? fill, string? stroke, float strokeWidth)
    {
        Commands = commands;
        Fill = fill;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
    }

    /// <summary>The parsed path commands (M/L/Q/C/Z).</summary>
    public IReadOnlyList<PathCommand> Commands { get; init; }

    /// <summary>Optional fill color (hex). Null means no fill.</summary>
    public string? Fill { get; init; }

    /// <summary>Optional stroke color (hex). Null means no stroke.</summary>
    public string? Stroke { get; init; }

    /// <summary>Stroke width in pixels.</summary>
    public float StrokeWidth { get; init; }
}
