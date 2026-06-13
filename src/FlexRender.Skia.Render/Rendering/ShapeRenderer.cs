using System;
using System.Collections.Generic;
using FlexRender.Layout.Units;
using FlexRender.Parsing;
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
    internal static void DrawRect(
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
    internal static void DrawCircle(
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
    internal static void DrawEllipse(
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

    /// <summary>
    /// Paints the ordered shapes of a <see cref="DrawElement"/> inside its layout box.
    /// Shapes use absolute coordinates relative to the box's top-left corner; the canvas is
    /// clipped to the box and translated so each shape's coordinates are box-local.
    /// Shape fills are solid colors only (no gradients).
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="draw">The draw element whose shapes are painted in list order.</param>
    /// <param name="x">The left edge of the draw box in pixels.</param>
    /// <param name="y">The top edge of the draw box in pixels.</param>
    /// <param name="width">The width of the draw box in pixels.</param>
    /// <param name="height">The height of the draw box in pixels.</param>
    /// <param name="antialias">Whether to enable anti-aliasing for fills and strokes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> or <paramref name="draw"/> is null.</exception>
    internal static void DrawShapes(
        SKCanvas canvas,
        DrawElement draw,
        float x,
        float y,
        float width,
        float height,
        bool antialias)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(draw);

        canvas.Save();
        try
        {
            canvas.ClipRect(new SKRect(x, y, x + width, y + height));
            canvas.Translate(x, y);

            foreach (var shape in draw.Shapes)
            {
                switch (shape)
                {
                    case DrawLine line:
                        DrawLineShape(canvas, line, antialias);
                        break;
                    case DrawPolyline polyline:
                        DrawPolylineShape(canvas, polyline, antialias);
                        break;
                    case DrawRect rect:
                        DrawRectShape(canvas, rect, antialias);
                        break;
                    case DrawCircle circle:
                        DrawCircleShape(canvas, circle, antialias);
                        break;
                    case DrawPath path:
                        DrawPathShape(canvas, path, antialias);
                        break;
                }
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>Draws a <see cref="DrawLine"/> as a stroked segment, defaulting to black.</summary>
    private static void DrawLineShape(SKCanvas canvas, DrawLine line, bool antialias)
    {
        if (TryCreateStrokePaint(line.Stroke ?? "#000000", line.StrokeWidth, antialias, out var paint))
        {
            using (paint)
                canvas.DrawLine(line.X1, line.Y1, line.X2, line.Y2, paint);
        }
    }

    /// <summary>Draws a <see cref="DrawPolyline"/>, optionally filling the enclosed area, then stroking (default black).</summary>
    private static void DrawPolylineShape(SKCanvas canvas, DrawPolyline polyline, bool antialias)
    {
        if (polyline.Points.Count < 2)
            return;

        using var path = new SKPath();
        var first = polyline.Points[0];
        path.MoveTo(first.X, first.Y);
        for (var i = 1; i < polyline.Points.Count; i++)
            path.LineTo(polyline.Points[i].X, polyline.Points[i].Y);

        FillSolid(polyline.Fill, antialias, paint => canvas.DrawPath(path, paint));

        if (TryCreateStrokePaint(polyline.Stroke ?? "#000000", polyline.StrokeWidth, antialias, out var strokePaint))
        {
            using (strokePaint)
                canvas.DrawPath(path, strokePaint);
        }
    }

    /// <summary>Draws a <see cref="DrawRect"/> with an optional solid fill and optional stroke, honouring corner radius.</summary>
    private static void DrawRectShape(SKCanvas canvas, DrawRect rect, bool antialias)
    {
        var radius = rect.Radius;

        FillSolid(rect.Fill, antialias,
            paint => DrawRectGeometry(canvas, rect.X, rect.Y, rect.Width, rect.Height, radius, paint));

        if (TryCreateStrokePaint(rect.Stroke, rect.StrokeWidth, antialias, out var strokePaint))
        {
            using (strokePaint)
                DrawRectGeometry(canvas, rect.X, rect.Y, rect.Width, rect.Height, radius, strokePaint);
        }
    }

    /// <summary>Draws a <see cref="DrawCircle"/> with an optional solid fill and optional stroke.</summary>
    private static void DrawCircleShape(SKCanvas canvas, DrawCircle circle, bool antialias)
    {
        FillSolid(circle.Fill, antialias,
            paint => canvas.DrawCircle(circle.Cx, circle.Cy, circle.R, paint));

        if (TryCreateStrokePaint(circle.Stroke, circle.StrokeWidth, antialias, out var strokePaint))
        {
            using (strokePaint)
                canvas.DrawCircle(circle.Cx, circle.Cy, circle.R, strokePaint);
        }
    }

    /// <summary>Draws a <see cref="DrawPath"/> built from pre-parsed absolute commands, with optional fill and stroke.</summary>
    private static void DrawPathShape(SKCanvas canvas, DrawPath drawPath, bool antialias)
    {
        using var path = BuildPath(drawPath.Commands);

        FillSolid(drawPath.Fill, antialias, paint => canvas.DrawPath(path, paint));

        if (TryCreateStrokePaint(drawPath.Stroke, drawPath.StrokeWidth, antialias, out var strokePaint))
        {
            using (strokePaint)
                canvas.DrawPath(path, strokePaint);
        }
    }

    /// <summary>Builds an <see cref="SKPath"/> from parsed path commands (MoveTo/LineTo/QuadTo/CubicTo/Close).</summary>
    private static SKPath BuildPath(IReadOnlyList<PathCommand> commands)
    {
        var path = new SKPath();
        foreach (var command in commands)
        {
            switch (command.Kind)
            {
                case PathCommandKind.MoveTo:
                    path.MoveTo(command.Points[0].X, command.Points[0].Y);
                    break;
                case PathCommandKind.LineTo:
                    path.LineTo(command.Points[0].X, command.Points[0].Y);
                    break;
                case PathCommandKind.QuadTo:
                    path.QuadTo(
                        command.Points[0].X, command.Points[0].Y,
                        command.Points[1].X, command.Points[1].Y);
                    break;
                case PathCommandKind.CubicTo:
                    path.CubicTo(
                        command.Points[0].X, command.Points[0].Y,
                        command.Points[1].X, command.Points[1].Y,
                        command.Points[2].X, command.Points[2].Y);
                    break;
                case PathCommandKind.Close:
                    path.Close();
                    break;
            }
        }

        return path;
    }

    /// <summary>Draws a sharp or rounded rectangle depending on whether <paramref name="radius"/> is positive.</summary>
    private static void DrawRectGeometry(SKCanvas canvas, float x, float y, float width, float height, float radius, SKPaint paint)
    {
        if (radius > 0f)
            canvas.DrawRoundRect(x, y, width, height, radius, radius, paint);
        else
            canvas.DrawRect(x, y, width, height, paint);
    }

    /// <summary>
    /// Invokes <paramref name="draw"/> with a solid fill paint when <paramref name="fill"/> is a
    /// non-empty color; does nothing otherwise. Used for draw-element shapes, which never use gradients.
    /// </summary>
    private static void FillSolid(string? fill, bool antialias, Action<SKPaint> draw)
    {
        if (string.IsNullOrEmpty(fill))
            return;

        using var paint = new SKPaint { Color = ColorParser.Parse(fill), Style = SKPaintStyle.Fill, IsAntialias = antialias };
        draw(paint);
    }
}
