using System.CommandLine;
using FlexRender.Barcode;
using FlexRender.Barcode.ImageSharp;
using FlexRender.Cli.Commands;
using FlexRender.Configuration;
using FlexRender.Http;
using FlexRender.QrCode;
using FlexRender.QrCode.ImageSharp;
using FlexRender.SvgElement;

namespace FlexRender.Cli;

/// <summary>
/// Entry point for the FlexRender CLI application.
/// </summary>
public sealed class Program
{
    /// <summary>
    /// Main entry point for the CLI application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code indicating success (0) or failure (non-zero).</returns>
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = CreateRootCommand();
        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Validates that the output format is compatible with the selected backend.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    /// <param name="format">The output format to validate.</param>
    /// <param name="backend">The rendering backend name.</param>
    /// <returns>Null if the combination is valid; otherwise, an error message describing the incompatibility.</returns>
    internal static string? ValidateBackendFormat(OutputFormat format, string backend)
    {
        if (format == OutputFormat.Svg &&
            !string.Equals(backend, "svg", StringComparison.OrdinalIgnoreCase))
        {
            return "Error: SVG output requires --backend svg. " +
                   "The skia and imagesharp backends produce raster output only.";
        }

        if (format != OutputFormat.Svg &&
            string.Equals(backend, "svg", StringComparison.OrdinalIgnoreCase))
        {
            return $"Error: The svg backend produces SVG output only. " +
                   $"Use --backend skia or --backend imagesharp for {format} output.";
        }

        return null;
    }

    /// <summary>
    /// Creates a configured FlexRender builder with the specified rendering backend.
    /// </summary>
    /// <param name="basePath">Optional base path for resolving relative file references.</param>
    /// <param name="backend">
    /// Primary rendering backend: "skia" (default, direct raster), "imagesharp" (pure .NET raster),
    /// or "svg" (vector output with embedded raster support).
    /// </param>
    /// <param name="rasterBackend">
    /// Raster backend used for embedded rasterization when <paramref name="backend"/> is "svg".
    /// Accepts "skia" (default) or "imagesharp". Ignored when the primary backend is not "svg".
    /// </param>
    /// <returns>A configured <see cref="FlexRenderBuilder"/> instance.</returns>
    public static FlexRenderBuilder CreateRenderBuilder(
        string? basePath = null,
        string backend = "skia",
        string rasterBackend = "skia")
    {
        var builder = new FlexRenderBuilder()
            .WithFilters()
            .WithHttpLoader();

        switch (backend)
        {
            case "imagesharp":
                builder.WithImageSharp(imageSharp => imageSharp
                    .WithQr()
                    .WithBarcode());
                break;

            case "svg":
                if (string.Equals(rasterBackend, "imagesharp", StringComparison.OrdinalIgnoreCase))
                {
                    builder.WithSvg(svg => svg.WithRasterBackend(
                        ImageSharpFlexRenderBuilderExtensions.CreateRendererFactory(
                            imageSharp => imageSharp.WithQr().WithBarcode())));
                }
                else
                {
                    builder.WithSvg(svg => svg.WithSkia(skia => skia
                        .WithQr()
                        .WithBarcode()
                        .WithSvgElement()));
                }
                break;

            default: // "skia"
                builder.WithSkia(skia => skia
                    .WithQr()
                    .WithBarcode()
                    .WithSvgElement());
                break;
        }

        if (basePath is not null)
        {
            builder.WithBasePath(basePath);
        }

        return builder;
    }

    /// <summary>
    /// Creates and configures the root command with all subcommands.
    /// </summary>
    /// <returns>The configured root command.</returns>
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("FlexRender - Render images from YAML templates");

        rootCommand.Add(GlobalOptions.Verbose);
        rootCommand.Add(GlobalOptions.Fonts);
        rootCommand.Add(GlobalOptions.Scale);
        rootCommand.Add(GlobalOptions.BasePath);
        rootCommand.Add(GlobalOptions.Backend);
        rootCommand.Add(GlobalOptions.RasterBackend);

        rootCommand.Add(RenderCommand.Create());
        rootCommand.Add(ValidateCommand.Create());
        rootCommand.Add(InfoCommand.Create());
        rootCommand.Add(WatchCommand.Create());
        rootCommand.Add(DebugLayoutCommand.Create());

        return rootCommand;
    }
}
