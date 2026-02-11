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
    private static Task<int> Execute(
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
            return Task.FromResult(1);
        }

        // Validate data file if specified
        if (dataFile is not null && !dataFile.Exists)
        {
            Console.Error.WriteLine($"Error: Data file not found: {dataFile.FullName}");
            return Task.FromResult(1);
        }

        // Validate fonts directory if specified
        if (fontsDir is not null && !fontsDir.Exists)
        {
            Console.Error.WriteLine($"Error: Fonts directory not found: {fontsDir.FullName}");
            return Task.FromResult(1);
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

            // Create renderer (has TextMeasurer configured)
            using var renderer = new SkiaRenderer();

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
                    renderer.FontManager.RegisterFont(fontName, fontPath);
                }
            }

            // Compute layout using the renderer (same as actual rendering)
            var root = renderer.ComputeLayout(template, templateData);

            // Print layout tree
            Console.WriteLine("Layout Tree:");
            PrintLayoutNode(root, 0, verbose);

            // Optionally render debug image
            if (outputFile is not null)
            {
                RenderDebugImage(template, root, templateData, outputFile.FullName, renderer);
                Console.WriteLine();
                Console.WriteLine($"Debug image: {outputFile.FullName}");
            }

            return Task.FromResult(0);
        }
        catch (TemplateParseException ex)
        {
            Console.Error.WriteLine($"Template error: {ex.Message}");
            return Task.FromResult(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return Task.FromResult(1);
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

        Console.WriteLine($"{prefix}{elementType}: X={node.X:F1}, Y={node.Y:F1}, W={node.Width:F1}, H={node.Height:F1}{extra}");

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
        return element switch
        {
            FlexElement f => $" [{f.Direction.ToString().ToLowerInvariant()}]",
            TextElement t => $" \"{Truncate(t.Content.Value, 30)}\"",
            QrElement => " [qr]",
            BarcodeElement => " [barcode]",
            ImageElement => " [image]",
            _ => ""
        };
    }

    /// <summary>
    /// Renders a debug image with the template and overlay.
    /// </summary>
    /// <param name="template">The parsed template.</param>
    /// <param name="root">The root layout node.</param>
    /// <param name="data">The template data.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="renderer">The renderer with fonts already registered.</param>
    private static void RenderDebugImage(
        Template template,
        LayoutNode root,
        ObjectValue data,
        string outputPath,
        SkiaRenderer renderer)
    {
        var size = renderer.Measure(template, data);

        using var bitmap = new SKBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
        using var canvas = new SKCanvas(bitmap);

        // Render template normally
        renderer.Render(canvas, template, data);

        // Draw debug overlay
        DrawDebugOverlay(canvas, root, 0, 0);

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
    private static void DrawDebugOverlay(SKCanvas canvas, LayoutNode node, float offsetX, float offsetY)
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

        // Recursively draw children
        foreach (var child in node.Children)
        {
            DrawDebugOverlay(canvas, child, x, y);
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
