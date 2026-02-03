using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using FlexRender.Abstractions;
using FlexRender.Parsing;
using FlexRender.Rendering;
using SkiaSharp;

namespace FlexRender.Cli.Commands;

/// <summary>
/// Command to render a template to an image file.
/// </summary>
public static class RenderCommand
{
    /// <summary>
    /// Creates the render command.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <returns>The configured render command.</returns>
    public static Command Create(IServiceProvider serviceProvider)
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
            Description = "Path to the output image file"
        };

        var qualityOption = new Option<int>("--quality")
        {
            Description = "JPEG quality (0-100, only applies to JPEG output)",
            DefaultValueFactory = _ => 90
        };

        var openOption = new Option<bool>("--open")
        {
            Description = "Open the rendered file after saving"
        };

        var command = new Command("render", "Render a template to an image file")
        {
            templateArg,
            dataOption,
            outputOption,
            qualityOption,
            openOption
        };

        command.SetAction(async (parseResult) =>
        {
            var templateFile = parseResult.GetValue(templateArg);
            var dataFile = parseResult.GetValue(dataOption);
            var outputFile = parseResult.GetValue(outputOption);
            var quality = parseResult.GetValue(qualityOption);
            var open = parseResult.GetValue(openOption);
            var scale = parseResult.GetValue(GlobalOptions.Scale);
            var fontsDir = parseResult.GetValue(GlobalOptions.Fonts);
            var verbose = parseResult.GetValue(GlobalOptions.Verbose);

            return await Execute(serviceProvider, templateFile!, dataFile, outputFile, quality, open, scale, fontsDir, verbose);
        });

        return command;
    }

    private static async Task<int> Execute(
        IServiceProvider serviceProvider,
        FileInfo templateFile,
        FileInfo? dataFile,
        FileInfo? outputFile,
        int quality,
        bool open,
        float scale,
        DirectoryInfo? fontsDir,
        bool verbose)
    {
        // Validate output file is specified
        if (outputFile is null)
        {
            Console.Error.WriteLine("Error: Output file is required. Use -o or --output to specify.");
            return 1;
        }

        // Validate template exists
        if (!templateFile.Exists)
        {
            Console.Error.WriteLine($"Error: Template file not found: {templateFile.FullName}");
            return 1;
        }

        // Validate output format
        OutputFormat format;
        try
        {
            format = OutputFormatExtensions.FromPath(outputFile.FullName);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        // Validate quality range
        if (quality < 0 || quality > 100)
        {
            Console.Error.WriteLine($"Error: Quality must be between 0 and 100, got {quality}");
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

            // Parse template with data preprocessing (handles {{#each}} blocks)
            var parser = new TemplateParser();
            var yaml = await File.ReadAllTextAsync(templateFile.FullName);
            var template = parser.Parse(yaml, data);

            if (verbose)
            {
                Console.WriteLine($"Template: {template.Name ?? templateFile.Name}");
                Console.WriteLine($"Canvas: {template.Canvas.Width}x{template.Canvas.Height}px ({template.Canvas.Fixed})");
                Console.WriteLine($"Output format: {format}");
                Console.WriteLine($"Scale: {scale}x");
                if (format == OutputFormat.Jpeg)
                {
                    Console.WriteLine($"Quality: {quality}");
                }
            }

            // Get renderer from DI and load fonts
            var renderer = serviceProvider.GetRequiredService<IFlexRenderer>();
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
                    if (verbose)
                    {
                        Console.WriteLine($"Registered font: {fontName}");
                    }
                }

                if (verbose && fontFiles.Length > 0)
                {
                    Console.WriteLine($"Loaded {fontFiles.Length} font(s) from: {fontsDir.FullName}");
                }
            }

            // Measure template to determine bitmap size
            var renderData = data ?? new ObjectValue();
            var size = await renderer.Measure(template, renderData);
            var scaledWidth = (int)Math.Ceiling(size.Width * scale);
            var scaledHeight = (int)Math.Ceiling(size.Height * scale);

            if (verbose)
            {
                Console.WriteLine($"Measured size: {size.Width}x{size.Height}px");
                Console.WriteLine($"Scaled size: {scaledWidth}x{scaledHeight}px");
            }

            // Create bitmap and render
            using var bitmap = new SKBitmap(scaledWidth, scaledHeight);
            using var canvas = new SKCanvas(bitmap);

            // Apply scale transform
            canvas.Scale(scale);

            await renderer.Render(bitmap, template, renderData);

            // Encode to specified format
            // Note: SkiaSharp does not support BMP encoding via SKImage.Encode() - it returns null.
            // For BMP requests, we fall back to PNG and warn the user.
            var effectiveFormat = format;
            if (format == OutputFormat.Bmp)
            {
                Console.WriteLine("Warning: BMP encoding is not supported by SkiaSharp. Saving as PNG instead.");
                effectiveFormat = OutputFormat.Png;
            }

            var skFormat = effectiveFormat switch
            {
                OutputFormat.Png => SKEncodedImageFormat.Png,
                OutputFormat.Jpeg => SKEncodedImageFormat.Jpeg,
                _ => SKEncodedImageFormat.Png
            };

            using var image = SKImage.FromBitmap(bitmap);
            using var encodedData = image.Encode(skFormat, quality);

            if (encodedData is null)
            {
                throw new InvalidOperationException(
                    $"Failed to encode image to {effectiveFormat}. The encoding operation returned null.");
            }

            // Ensure output directory exists and save
            FileOpener.EnsureDirectoryExists(outputFile.FullName);
            await using var stream = File.Create(outputFile.FullName);
            encodedData.SaveTo(stream);

            Console.WriteLine($"Rendered: {outputFile.FullName}");
            if (open)
            {
                FileOpener.Open(outputFile.FullName);
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
}
