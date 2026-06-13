using System;
using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Draws box shape elements (rectangle, circle, ellipse) to a SkiaSharp canvas.
/// Each shape supports a solid color or CSS gradient fill plus an optional solid stroke.
/// </summary>
internal static class ShapeRenderer
{
    /// <summary>
    /// Draws a <see cref="RectElement"/> as a filled and/or stroked rectangle, using rounded
    /// corners when a positive corner radius is specified.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="rect">The rectangle element describing fill, stroke, and corner radius.</param>
    /// <param name="x">The left edge of the shape box in pixels.</param>
    /// <param name="y">The top edge of the shape box in pixels.</param>
    /// <param name="width">The width of the shape box in pixels.</param>
    /// <param name="height">The height of the shape box in pixels.</param>
    /// <param name="fontSize">The effective font size in pixels, used to resolve em-based radii.</param>
    /// <param name="antialias">Whether to enable anti-aliasing for the fill and stroke.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> or <paramref name="rect"/> is null.</exception>
    public static void DrawRect(
        SKCanvas canvas,
        RectElement rect,
        float x,
        float y,
        float width,
        float height,
        float fontSize,
        bool antialias)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(rect);

        var radius = ResolveRadius(rect.Radius.Value, Math.Min(width, height), fontSize);

        FillRegion(canvas, rect.Fill.Value, x, y, width, height, antialias, paint =>
        {
            if (radius > 0f)
                canvas.DrawRoundRect(x, y, width, height, radius, radius, paint);
            else
                canvas.DrawRect(x, y, width, height, paint);
        });

        if (TryCreateStrokePaint(rect.Stroke.Value, rect.StrokeWidth.Value, antialias, out var strokePaint))
        {
            using (strokePaint)
            {
                if (radius > 0f)
                    canvas.DrawRoundRect(x, y, width, height, radius, radius, strokePaint);
                else
                    canvas.DrawRect(x, y, width, height, strokePaint);
            }
        }
    }

    /// <summary>
    /// Draws a <see cref="CircleElement"/> as a filled and/or stroked circle inscribed in the box.
    /// The diameter is the smaller of <paramref name="width"/> and <paramref name="height"/>.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="circle">The circle element describing fill and stroke.</param>
    /// <param name="x">The left edge of the shape box in pixels.</param>
    /// <param name="y">The top edge of the shape box in pixels.</param>
    /// <param name="width">The width of the shape box in pixels.</param>
    /// <param name="height">The height of the shape box in pixels.</param>
    /// <param name="antialias">Whether to enable anti-aliasing for the fill and stroke.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> or <paramref name="circle"/> is null.</exception>
    public static void DrawCircle(
        SKCanvas canvas,
        CircleElement circle,
        float x,
        float y,
        float width,
        float height,
        bool antialias)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(circle);

        var diameter = Math.Min(width, height);
        var r = diameter / 2f;
        var cx = x + (width / 2f);
        var cy = y + (height / 2f);

        FillRegion(canvas, circle.Fill.Value, x, y, width, height, antialias,
            paint => canvas.DrawCircle(cx, cy, r, paint));

        if (TryCreateStrokePaint(circle.Stroke.Value, circle.StrokeWidth.Value, antialias, out var strokePaint))
        {
            using (strokePaint)
                canvas.DrawCircle(cx, cy, r, strokePaint);
        }
    }

    /// <summary>
    /// Draws an <see cref="EllipseElement"/> as a filled and/or stroked ellipse inscribed in the box.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="ellipse">The ellipse element describing fill and stroke.</param>
    /// <param name="x">The left edge of the shape box in pixels.</param>
    /// <param name="y">The top edge of the shape box in pixels.</param>
    /// <param name="width">The width of the shape box in pixels.</param>
    /// <param name="height">The height of the shape box in pixels.</param>
    /// <param name="antialias">Whether to enable anti-aliasing for the fill and stroke.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> or <paramref name="ellipse"/> is null.</exception>
    public static void DrawEllipse(
        SKCanvas canvas,
        EllipseElement ellipse,
        float x,
        float y,
        float width,
        float height,
        bool antialias)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(ellipse);

        var cx = x + (width / 2f);
        var cy = y + (height / 2f);
        var rx = width / 2f;
        var ry = height / 2f;

        FillRegion(canvas, ellipse.Fill.Value, x, y, width, height, antialias,
            paint => canvas.DrawOval(cx, cy, rx, ry, paint));

        if (TryCreateStrokePaint(ellipse.Stroke.Value, ellipse.StrokeWidth.Value, antialias, out var strokePaint))
        {
            using (strokePaint)
                canvas.DrawOval(cx, cy, rx, ry, strokePaint);
        }
    }

    /// <summary>
    /// Fills a shape region using either a CSS gradient or a solid color, invoking
    /// <paramref name="draw"/> with the configured fill paint. Does nothing when
    /// <paramref name="fill"/> is null or empty.
    /// </summary>
    /// <param name="canvas">The canvas the shape is drawn on (used to size gradients).</param>
    /// <param name="fill">The fill value: a solid color or a CSS gradient function string.</param>
    /// <param name="x">The left edge of the shape box in pixels.</param>
    /// <param name="y">The top edge of the shape box in pixels.</param>
    /// <param name="width">The width of the shape box in pixels.</param>
    /// <param name="height">The height of the shape box in pixels.</param>
    /// <param name="antialias">Whether to enable anti-aliasing for the fill.</param>
    /// <param name="draw">The action that draws the geometry with the supplied paint.</param>
    private static void FillRegion(
        SKCanvas canvas,
        string? fill,
        float x,
        float y,
        float width,
        float height,
        bool antialias,
        Action<SKPaint> draw)
    {
        if (string.IsNullOrEmpty(fill))
            return;

        if (GradientParser.IsGradient(fill) &&
            GradientParser.TryParse(fill, out var gradient) &&
            gradient is not null)
        {
            using var shader = GradientParser.CreateShader(gradient, x, y, width, height);
            if (shader is not null)
            {
                using var gradientPaint = new SKPaint { Shader = shader, Style = SKPaintStyle.Fill, IsAntialias = antialias };
                draw(gradientPaint);
                return;
            }
        }

        using var paint = new SKPaint { Color = ColorParser.Parse(fill), Style = SKPaintStyle.Fill, IsAntialias = antialias };
        draw(paint);
    }

    /// <summary>
    /// Attempts to create a solid stroke paint when a stroke color is present and the stroke
    /// width is positive.
    /// </summary>
    /// <param name="stroke">The stroke color string, or null/empty for no stroke.</param>
    /// <param name="strokeWidth">The stroke width in pixels; must be greater than zero to stroke.</param>
    /// <param name="antialias">Whether to enable anti-aliasing for the stroke.</param>
    /// <param name="paint">The created stroke paint when this method returns true; otherwise null.</param>
    /// <returns><see langword="true"/> when a stroke paint was created; otherwise <see langword="false"/>.</returns>
    private static bool TryCreateStrokePaint(string? stroke, float strokeWidth, bool antialias, out SKPaint paint)
    {
        if (string.IsNullOrEmpty(stroke) || strokeWidth <= 0f)
        {
            paint = null!;
            return false;
        }

        paint = new SKPaint
        {
            Color = ColorParser.Parse(stroke),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            IsAntialias = antialias
        };
        return true;
    }

    /// <summary>
    /// Resolves a corner radius string (e.g. "8", "0.5em") to pixels, returning zero when the
    /// value is empty or cannot be resolved.
    /// </summary>
    /// <param name="radius">The radius value string, or null/empty for square corners.</param>
    /// <param name="containerSize">The reference size for percentage radii in pixels.</param>
    /// <param name="fontSize">The effective font size in pixels for em-based radii.</param>
    /// <returns>The resolved radius in pixels, or zero.</returns>
    private static float ResolveRadius(string? radius, float containerSize, float fontSize)
    {
        if (string.IsNullOrEmpty(radius))
            return 0f;

        return UnitParser.Parse(radius).Resolve(containerSize, fontSize) ?? 0f;
    }
}
