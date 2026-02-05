using System.CommandLine;
using Xunit;

namespace FlexRender.Cli.Tests.Commands;

/// <summary>
/// Tests for the watch CLI command.
/// </summary>
public class WatchCommandTests
{
    /// <summary>
    /// Verifies that the root command has a watch subcommand.
    /// </summary>
    [Fact]
    public void CreateRootCommand_HasWatchSubcommand()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();

        // Assert
        var watchCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "watch");
        Assert.NotNull(watchCommand);
        Assert.Equal("Watch files and re-render on changes", watchCommand.Description);
    }

    /// <summary>
    /// Verifies that the watch command has a template argument.
    /// </summary>
    [Fact]
    public void WatchCommand_HasTemplateArgument()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var watchCommand = rootCommand.Subcommands.First(c => c.Name == "watch");

        // Assert
        var templateArg = watchCommand.Arguments.FirstOrDefault(a => a.Name == "template");
        Assert.NotNull(templateArg);
    }

    /// <summary>
    /// Verifies that the watch command has a data option.
    /// </summary>
    [Fact]
    public void WatchCommand_HasDataOption()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var watchCommand = rootCommand.Subcommands.First(c => c.Name == "watch");

        // Assert
        var dataOption = watchCommand.Options.FirstOrDefault(o => o.Name == "--data");
        Assert.NotNull(dataOption);
    }

    /// <summary>
    /// Verifies that the watch command has an output option.
    /// </summary>
    [Fact]
    public void WatchCommand_HasOutputOption()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var watchCommand = rootCommand.Subcommands.First(c => c.Name == "watch");

        // Assert
        var outputOption = watchCommand.Options.FirstOrDefault(o => o.Name == "--output");
        Assert.NotNull(outputOption);
    }

    /// <summary>
    /// Verifies that the watch command has an open option.
    /// </summary>
    [Fact]
    public void WatchCommand_HasOpenOption()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var watchCommand = rootCommand.Subcommands.First(c => c.Name == "watch");

        // Assert
        var openOption = watchCommand.Options.FirstOrDefault(o => o.Name == "--open");
        Assert.NotNull(openOption);
    }

    /// <summary>
    /// Verifies that watching with non-existent template returns non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Watch_WithNonExistentTemplate_ReturnsNonZero()
    {
        // Act
        var result = await Program.Main(["watch", "non-existent.yaml", "-o", "output.png"]);

        // Assert
        Assert.NotEqual(0, result);
    }

    /// <summary>
    /// Verifies that watching without output option returns non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Watch_WithoutOutput_ReturnsNonZero()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");

        // Act
        var result = await Program.Main(["watch", templatePath]);

        // Assert
        Assert.NotEqual(0, result);
    }
}
