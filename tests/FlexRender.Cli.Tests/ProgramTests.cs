using System.CommandLine;
using Xunit;

namespace FlexRender.Cli.Tests;

/// <summary>
/// Tests for the CLI Program class.
/// </summary>
public class ProgramTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgramTests"/> class.
    /// </summary>
    public ProgramTests()
    {
        _serviceProvider = Program.CreateServiceProvider();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Verifies that the root command is created with the expected description.
    /// </summary>
    [Fact]
    public void CreateRootCommand_ReturnsRootCommand_WithDescription()
    {
        // Act
        var rootCommand = Program.CreateRootCommand(_serviceProvider);

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
        // Act
        var result = await Program.Main(["--help"]);

        // Assert
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies that the --version flag returns exit code 0.
    /// </summary>
    [Fact]
    public async Task Main_WithVersionFlag_ReturnsZero()
    {
        // Act
        var result = await Program.Main(["--version"]);

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
