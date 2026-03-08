using System.CommandLine;
using FlexRender.Layout;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;

namespace FlexRender.Cli.Commands;

/// <summary>
/// Command to debug layout by showing the element tree with positions and sizes.
/// </summary>
public static class DebugLayoutCommand
{
    /// <summary>
    /// Creates the debug-layout command.
    /// </summary>
    /// <returns>The configured debug-layout command.</returns>
    public static Command Create()
    {
        var templateArg = new Argument<FileInfo>("template")
        {
            Description = "Path to the YAML template file"
        };

        var dataOption = new Option<FileInfo?>("--data", "-d")
        {
            Description = "Path to the JSON data file"
        };

        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Output debug image (PNG) with overlay"
        };

        var command = new Command("debug-layout", "Show layout tree with positions and sizes")
        {
            templateArg,
            dataOption,
            outputOption
        };

        command.SetAction(async (parseResult) =>
        {
            var templateFile = parseResult.GetValue(templateArg);
            var dataFile = parseResult.GetValue(dataOption);
            var outputFile = parseResult.GetValue(outputOption);
            var verbose = parseResult.GetValue(GlobalOptions.Verbose);
            var fontsDir = parseResult.GetValue(GlobalOptions.Fonts);

            return await Execute(templateFile!, dataFile, outputFile, verbose, fontsDir);
        });

        return command;
    }

    /// <summary>
    /// Executes the debug-layout command.
    /// </summary>
    /// <param name="templateFile">The template file to process.</param>
    /// <param name="dataFile">Optional data file for template variables.</param>
    /// <param name="outputFile">Optional output file for debug image.</param>
    /// <param name="verbose">Whether to enable verbose output.</param>
    /// <param name="fontsDir">Optional fonts directory.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    private static async Task<int> Execute(
        FileInfo templateFile,
        FileInfo? dataFile,
        FileInfo? outputFile,
        bool verbose,
        DirectoryInfo? fontsDir)
    {
        // Validate template exists
        if (!templateFile.Exists)
        {
            Console.Error.WriteLine($"Error: Template file not found: {templateFile.FullName}");
            return 1;
        }

        // Validate data file if specified
        if (dataFile is not null && !dataFile.Exists)
        {
            Console.Error.WriteLine($"Error: Data file not found: {dataFile.FullName}");
            return 1;
        }

        // Validate fonts directory if specified
        if (fontsDir is not null && !fontsDir.Exists)
        {
            Console.Error.WriteLine($"Error: Fonts directory not found: {fontsDir.FullName}");
            return 1;
        }

        try
        {
            // Load data if provided
            ObjectValue? data = null;
            if (dataFile is not null)
            {
                data = DataLoader.LoadFromFile(dataFile.FullName);
                if (verbose)
                {
                    Console.WriteLine($"Data loaded: {data.Keys.Count()} properties");
                }
            }

            // Parse template
            var parser = new TemplateParser();
            var yaml = File.ReadAllText(templateFile.FullName);
            var template = parser.Parse(yaml);

            if (verbose)
            {
                Console.WriteLine($"Template: {template.Name ?? templateFile.Name}");
                Console.WriteLine($"Canvas: {template.Canvas.Width}x{template.Canvas.Height}px ({template.Canvas.Fixed})");
            }

            var templateData = data ?? new ObjectValue();

            // Use the same builder as the render command for consistent configuration
            using var flexRender = Program.CreateRenderBuilder(templateFile.DirectoryName).Build();
            var skiaRender = flexRender as FlexRender.Skia.SkiaRender
                ?? throw new InvalidOperationException("Debug layout requires Skia backend");

            // Register extra fonts from --fonts dir
            if (fontsDir is not null)
            {
                var fontExtensions = new[] { ".ttf", ".otf" };
                var fontFiles = Directory.GetFiles(fontsDir.FullName)
                    .Where(f => fontExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToArray();
                foreach (var fontPath in fontFiles)
                {
                    var fontName = Path.GetFileNameWithoutExtension(fontPath);
                    skiaRender.FontManager.RegisterFont(fontName, fontPath);
                }
            }

            // Compute layout using the same renderer as actual rendering
            var root = await skiaRender.ComputeLayout(template, templateData);

            // Print registered fonts
            Console.WriteLine("Fonts:");
            foreach (var (fontName, fontPath) in skiaRender.FontManager.RegisteredFontPaths)
            {
                var info = skiaRender.FontManager.GetTypefaceInfo(fontName);
                var typefaceDesc = info.HasValue
                    ? $"{info.Value.FamilyName}, fixedPitch={info.Value.IsFixedPitch}"
                    : "not loaded";
                Console.WriteLine($"  {fontName}: {fontPath} -> {typefaceDesc}");
            }
            Console.WriteLine();

            // Print layout tree
            Console.WriteLine("Layout Tree:");
            PrintLayoutNode(root, 0, verbose);

            // Optionally render debug image
            if (outputFile is not null)
            {
                await RenderDebugImage(template, root, templateData, outputFile.FullName, skiaRender);
                Console.WriteLine();
                Console.WriteLine($"Debug image: {outputFile.FullName}");
            }

            return 0;
        }
        catch (TemplateParseException ex)
        {
            Console.Error.WriteLine($"Template error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    /// <summary>
    /// Prints a layout node and its children to the console.
    /// </summary>
    /// <param name="node">The layout node to print.</param>
    /// <param name="indent">The indentation level.</param>
    /// <param name="verbose">Whether to show verbose output.</param>
    private static void PrintLayoutNode(LayoutNode node, int indent, bool verbose)
    {
        var prefix = new string(' ', indent * 2);
        var elementType = node.Element.GetType().Name;
        var extra = GetElementExtra(node.Element);
        var computed = GetComputedExtra(node);

        Console.WriteLine($"{prefix}{elementType}: X={node.X:F1}, Y={node.Y:F1}, W={node.Width:F1}, H={node.Height:F1}{extra}{computed}");

        foreach (var child in node.Children)
        {
            PrintLayoutNode(child, indent + 1, verbose);
        }
    }

    /// <summary>
    /// Gets additional information string for a template element.
    /// </summary>
    /// <param name="element">The template element.</param>
    /// <returns>A string with element-specific information.</returns>
    private static string GetElementExtra(TemplateElement element)
    {
        var parts = new List<string>();

        switch (element)
        {
            case FlexElement f:
                parts.Add(f.Direction.ToString().ToLowerInvariant());
                if (!string.IsNullOrEmpty(f.FontSize.Value))
                    parts.Add($"font-size={f.FontSize.Value}");
                if (f.Align.Value != FlexRender.Layout.AlignItems.Stretch)
                    parts.Add($"align={f.Align.Value}");
                if (f.Justify.Value != FlexRender.Layout.JustifyContent.Start)
                    parts.Add($"justify={f.Justify.Value}");
                break;
            case TextElement t:
                parts.Add($"\"{Truncate(t.Content.Value, 30)}\"");
                if (!string.IsNullOrEmpty(t.Size.Value))
                    parts.Add($"size={t.Size.Value}");
                if (!string.IsNullOrEmpty(t.Font.Value))
                    parts.Add($"font={t.Font.Value}");
                if (!string.IsNullOrEmpty(t.FontFamily.Value))
                    parts.Add($"family={t.FontFamily.Value}");
                if (t.FontWeight.Value != FlexRender.Parsing.Ast.FontWeight.Normal)
                    parts.Add($"weight={t.FontWeight.Value}");
                if (t.FontStyle.Value != FlexRender.Parsing.Ast.FontStyle.Normal)
                    parts.Add($"style={t.FontStyle.Value}");
                if (!string.IsNullOrEmpty(t.Color.Value))
                    parts.Add($"color={t.Color.Value}");
                break;
            case QrElement:
                parts.Add("qr");
                break;
            case BarcodeElement:
                parts.Add("barcode");
                break;
            case ImageElement:
                parts.Add("image");
                break;
        }

        return parts.Count > 0 ? $" [{string.Join(", ", parts)}]" : "";
    }

    private static string GetComputedExtra(LayoutNode node)
    {
        var parts = new List<string>();

        if (node.ComputedFontSize > 0)
            parts.Add($"fontSize={node.ComputedFontSize:F1}px");

        if (node.Baseline > 0)
            parts.Add($"baseline={node.Baseline:F1}");

        if (node.ComputedLineHeight > 0)
            parts.Add($"lineH={node.ComputedLineHeight:F1}");

        if (node.TextLines != null)
            parts.Add($"lines={node.TextLines.Count}");

        if (node.Direction != TextDirection.Ltr)
            parts.Add($"dir={node.Direction}");

        return parts.Count > 0 ? $" {{{string.Join(", ", parts)}}}" : "";
    }

    /// <summary>
    /// Renders a debug image with the template and overlay.
    /// </summary>
    /// <param name="template">The parsed template.</param>
    /// <param name="root">The root layout node.</param>
    /// <param name="data">The template data.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="skiaRender">The SkiaRender instance with fonts already registered.</param>
    private static async Task RenderDebugImage(
        Template template,
        LayoutNode root,
        ObjectValue data,
        string outputPath,
        FlexRender.Skia.SkiaRender skiaRender)
    {
        var size = await skiaRender.Measure(template, data);

        using var bitmap = new SKBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
        using var canvas = new SKCanvas(bitmap);

        // Render template to PNG and decode back to draw onto debug canvas
        var pngBytes = await skiaRender.RenderToPng(template, data);
        using var rendered = SKBitmap.Decode(pngBytes);
        canvas.DrawBitmap(rendered, 0, 0);

        // Draw debug overlay
        DrawDebugOverlay(canvas, root, 0, 0, skiaRender.FontManager);

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null)
        {
            throw new InvalidOperationException("Failed to encode debug image");
        }

        using var stream = File.OpenWrite(outputPath);
        encoded.SaveTo(stream);
    }

    /// <summary>
    /// Draws debug overlay rectangles on the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="node">The current layout node.</param>
    /// <param name="offsetX">The X offset from parent.</param>
    /// <param name="offsetY">The Y offset from parent.</param>
    /// <param name="fontManager">The font manager for creating fonts to measure glyph widths.</param>
    private static void DrawDebugOverlay(SKCanvas canvas, LayoutNode node, float offsetX, float offsetY, FontManager fontManager)
    {
        var x = node.X + offsetX;
        var y = node.Y + offsetY;

        // Choose color based on element type
        var color = node.Element switch
        {
            FlexElement => SKColors.Blue,
            TextElement => SKColors.Green,
            QrElement => SKColors.Purple,
            BarcodeElement => SKColors.Orange,
            ImageElement => SKColors.Cyan,
            _ => SKColors.Gray
        };

        // Draw fill for flex containers
        if (node.Element is FlexElement)
        {
            using var fillPaint = new SKPaint
            {
                Color = color.WithAlpha(30),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(x, y, node.Width, node.Height, fillPaint);
        }

        // Draw stroke
        using var strokePaint = new SKPaint
        {
            Color = color.WithAlpha(180),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        canvas.DrawRect(x, y, node.Width, node.Height, strokePaint);

        // Draw per-glyph boundaries for text elements
        if (node.Element is TextElement text && !string.IsNullOrEmpty(text.Content.Value))
        {
            DrawGlyphBoundaries(canvas, text, x, y, node, fontManager);
        }

        // Recursively draw children
        foreach (var child in node.Children)
        {
            DrawDebugOverlay(canvas, child, x, y, fontManager);
        }
    }

    /// <summary>
    /// Draws per-glyph boundary rectangles for a text element.
    /// Spaces are highlighted with a distinct color to make whitespace visible.
    /// </summary>
    private static void DrawGlyphBoundaries(
        SKCanvas canvas,
        TextElement text,
        float x,
        float y,
        LayoutNode node,
        FontManager fontManager)
    {
        var fontSize = node.ComputedFontSize > 0 ? node.ComputedFontSize : 16f;
        var typeface = fontManager.GetTypeface(text.Font.Value, text.FontFamily.Value, text.FontWeight.Value, text.FontStyle.Value);
        using var font = new SKFont(typeface, FontSizeResolver.Resolve(text.Size.Value, fontSize));

        var content = text.Content.Value;

        // Compute per-character advance widths using cumulative measurement.
        // GetGlyphWidths returns ink bounds (visual shape width), not advance width,
        // so spaces would appear zero-width. Cumulative MeasureText gives correct advances.
        var advances = new float[content.Length];
        float prevWidth = 0;
        for (var i = 0; i < content.Length; i++)
        {
            var cumWidth = font.MeasureText(content.AsSpan(0, i + 1));
            advances[i] = cumWidth - prevWidth;
            prevWidth = cumWidth;
        }

        using var glyphStroke = new SKPaint
        {
            Color = SKColors.Red.WithAlpha(100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f
        };
        using var spaceFill = new SKPaint
        {
            Color = SKColors.Yellow.WithAlpha(60),
            Style = SKPaintStyle.Fill
        };

        var glyphX = x;
        for (var i = 0; i < advances.Length; i++)
        {
            var w = advances[i];

            // Highlight spaces with yellow fill
            if (content[i] == ' ')
            {
                canvas.DrawRect(glyphX, y, w, node.Height, spaceFill);
            }

            canvas.DrawRect(glyphX, y, w, node.Height, glyphStroke);
            glyphX += w;
        }
    }

    /// <summary>
    /// Truncates a string to the specified maximum length.
    /// </summary>
    /// <param name="s">The string to truncate.</param>
    /// <param name="max">The maximum length.</param>
    /// <returns>The truncated string with ellipsis if needed.</returns>
    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 3)] + "...";
}
