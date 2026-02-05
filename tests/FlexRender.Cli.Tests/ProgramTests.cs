using System.CommandLine;
using System.IO;
using Xunit;

namespace FlexRender.Cli.Tests;

/// <summary>
/// Tests for the CLI Program class.
/// </summary>
public class ProgramTests
{
    /// <summary>
    /// Verifies that the root command is created with the expected description.
    /// </summary>
    [Fact]
    public void CreateRootCommand_ReturnsRootCommand_WithDescription()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();

        // Assert
        Assert.NotNull(rootCommand);
        Assert.Equal("FlexRender - Render images from YAML templates", rootCommand.Description);
    }

    /// <summary>
    /// Verifies that the --help flag returns exit code 0.
    /// </summary>
    [Fact]
    public async Task Main_WithHelpFlag_ReturnsZero()
    {
        // Arrange - use InvocationConfiguration with TextWriter.Null to avoid
        // Console.Out disposal race conditions in xUnit on net8.0
        var rootCommand = Program.CreateRootCommand();
        var invocationConfig = new InvocationConfiguration
        {
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        };

        // Act
        var result = await rootCommand.Parse(["--help"]).InvokeAsync(invocationConfig);

        // Assert
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies that the --version flag returns exit code 0.
    /// </summary>
    [Fact]
    public async Task Main_WithVersionFlag_ReturnsZero()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();
        var invocationConfig = new InvocationConfiguration
        {
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        };

        // Act
        var result = await rootCommand.Parse(["--version"]).InvokeAsync(invocationConfig);

        // Assert
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies that the verbose global option is configured correctly.
    /// </summary>
    [Fact]
    public void CreateRootCommand_HasVerboseOption()
    {
        // Assert - verify global option exists and has correct configuration
        Assert.Equal("--verbose", GlobalOptions.Verbose.Name);
        Assert.Contains("-v", GlobalOptions.Verbose.Aliases);
        Assert.True(GlobalOptions.Verbose.Recursive);
    }

    /// <summary>
    /// Verifies that the fonts global option is configured correctly.
    /// </summary>
    [Fact]
    public void CreateRootCommand_HasFontsOption()
    {
        // Assert
        Assert.Equal("--fonts", GlobalOptions.Fonts.Name);
        Assert.True(GlobalOptions.Fonts.Recursive);
    }

    /// <summary>
    /// Verifies that the scale global option is configured correctly.
    /// </summary>
    [Fact]
    public void CreateRootCommand_HasScaleOption()
    {
        // Assert
        Assert.Equal("--scale", GlobalOptions.Scale.Name);
        Assert.True(GlobalOptions.Scale.Recursive);
    }
}
