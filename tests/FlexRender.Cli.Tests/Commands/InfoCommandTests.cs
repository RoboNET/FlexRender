using System.CommandLine;
using Xunit;

namespace FlexRender.Cli.Tests.Commands;

/// <summary>
/// Tests for the info CLI command.
/// </summary>
public class InfoCommandTests
{
    /// <summary>
    /// Verifies that the root command has an info subcommand.
    /// </summary>
    [Fact]
    public void CreateRootCommand_HasInfoSubcommand()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();

        // Assert
        var infoCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "info");
        Assert.NotNull(infoCommand);
        Assert.Equal("Show template information (size, fonts, variables)", infoCommand.Description);
    }

    /// <summary>
    /// Verifies that info on a valid template returns exit code 0.
    /// </summary>
    [Fact]
    public async Task Info_WithValidTemplate_ReturnsZero()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");

        // Act
        var result = await Program.Main(["info", templatePath]);

        // Assert
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies that info on a non-existent file returns non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Info_WithNonExistentFile_ReturnsNonZero()
    {
        // Act
        var result = await Program.Main(["info", "non-existent-file.yaml"]);

        // Assert
        Assert.NotEqual(0, result);
    }
}
