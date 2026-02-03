using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using FlexRender.Cli.Commands;

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
        using var serviceProvider = CreateServiceProvider();
        var rootCommand = CreateRootCommand(serviceProvider);
        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Creates and configures the dependency injection service provider.
    /// </summary>
    /// <returns>The configured service provider.</returns>
    public static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddFlexRender();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates and configures the root command with all subcommands.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <returns>The configured root command.</returns>
    public static RootCommand CreateRootCommand(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("FlexRender - Render images from YAML templates");

        rootCommand.Add(GlobalOptions.Verbose);
        rootCommand.Add(GlobalOptions.Fonts);
        rootCommand.Add(GlobalOptions.Scale);
        rootCommand.Add(GlobalOptions.BasePath);

        rootCommand.Add(RenderCommand.Create(serviceProvider));
        rootCommand.Add(ValidateCommand.Create());
        rootCommand.Add(InfoCommand.Create());
        rootCommand.Add(WatchCommand.Create(serviceProvider));
        rootCommand.Add(DebugLayoutCommand.Create());

        return rootCommand;
    }
}
