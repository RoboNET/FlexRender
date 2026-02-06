using System.CommandLine;
using FlexRender.Abstractions;
using FlexRender.Parsing;
using FlexRender.Skia;
using FlexRender.Yaml;

namespace FlexRender.Cli.Commands;

/// <summary>
/// Command to render a template to an image file.
/// </summary>
public static class RenderCommand
{
    /// <summary>
    /// Creates the render command.
    /// </summary>
    /// <returns>The configured render command.</returns>
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

        var bmpColorOption = new Option<BmpColorMode>("--bmp-color")
        {
            Description = "BMP color mode: Bgra32, Rgb24, Rgb565, Grayscale8, Grayscale4, Monochrome1 (only applies to BMP output)",
            DefaultValueFactory = _ => BmpColorMode.Bgra32
        };

        var command = new Command("render", "Render a template to an image file")
        {
            templateArg,
            dataOption,
            outputOption,
            qualityOption,
            openOption,
            bmpColorOption
        };

        command.SetAction(async (parseResult) =>
        {
            var templateFile = parseResult.GetValue(templateArg);
            var dataFile = parseResult.GetValue(dataOption);
            var outputFile = parseResult.GetValue(outputOption);
            var quality = parseResult.GetValue(qualityOption);
            var open = parseResult.GetValue(openOption);
            var bmpColor = parseResult.GetValue(bmpColorOption);
            var verbose = parseResult.GetValue(GlobalOptions.Verbose);
            var basePath = parseResult.GetValue(GlobalOptions.BasePath);

            return await Execute(templateFile!, dataFile, outputFile, quality, open, bmpColor, verbose, basePath);
        });

        return command;
    }

    private static async Task<int> Execute(
        FileInfo templateFile,
        FileInfo? dataFile,
        FileInfo? outputFile,
        int quality,
        bool open,
        BmpColorMode bmpColor,
        bool verbose,
        DirectoryInfo? basePath)
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

        try
        {
            // Create renderer with base path
            var effectiveBasePath = basePath?.FullName ?? templateFile.DirectoryName!;
            using var renderer = Program.CreateRenderBuilder(effectiveBasePath).Build();

            // Set BMP color mode if applicable
            if (renderer is SkiaRender skiaRender)
                skiaRender.BmpColorMode = bmpColor;

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

            // Parse template for verbose info only
            if (verbose)
            {
                var parser = new TemplateParser();
                var yaml = await File.ReadAllTextAsync(templateFile.FullName);
                var template = parser.Parse(yaml);
                Console.WriteLine($"Template: {template.Name ?? templateFile.Name}");
                Console.WriteLine($"Canvas: {template.Canvas.Width}x{template.Canvas.Height}px ({template.Canvas.Fixed})");
                Console.WriteLine($"Output format: {format}");
                if (format == OutputFormat.Jpeg)
                {
                    Console.WriteLine($"Quality: {quality}");
                }
                if (format == OutputFormat.Bmp && bmpColor != BmpColorMode.Bgra32)
                {
                    Console.WriteLine($"BMP color mode: {bmpColor}");
                }
            }

            // Ensure output directory exists
            FileOpener.EnsureDirectoryExists(outputFile.FullName);

            // Map output format to ImageFormat
            var imageFormat = format switch
            {
                OutputFormat.Png => ImageFormat.Png,
                OutputFormat.Jpeg => ImageFormat.Jpeg,
                OutputFormat.Bmp => ImageFormat.Bmp,
                _ => ImageFormat.Png
            };

            // Render directly to file
            await using var outputStream = File.Create(outputFile.FullName);
            await renderer.RenderFile(outputStream, templateFile.FullName, data, imageFormat);

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
