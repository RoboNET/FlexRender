using System.CommandLine;
using FlexRender.Barcode;
using FlexRender.Cli.Commands;
using FlexRender.Configuration;
using FlexRender.Http;
using FlexRender.QrCode;

namespace FlexRender.Cli;

/// <summary>
/// Entry point for the FlexRender CLI application.
/// </summary>
public class Program
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
    /// Creates a configured FlexRender builder with all features enabled.
    /// </summary>
    /// <param name="basePath">Optional base path for resolving relative file references.</param>
    /// <returns>A configured <see cref="FlexRenderBuilder"/> instance.</returns>
    public static FlexRenderBuilder CreateRenderBuilder(string? basePath = null)
    {
        var builder = new FlexRenderBuilder()
            .WithFilters()
            .WithHttpLoader()
            .WithSkia(skia => skia
                .WithQr()
                .WithBarcode());

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

        rootCommand.Add(RenderCommand.Create());
        rootCommand.Add(ValidateCommand.Create());
        rootCommand.Add(InfoCommand.Create());
        rootCommand.Add(WatchCommand.Create());
        rootCommand.Add(DebugLayoutCommand.Create());

        return rootCommand;
    }
}
