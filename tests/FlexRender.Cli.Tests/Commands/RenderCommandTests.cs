using System.CommandLine;
using Xunit;

namespace FlexRender.Cli.Tests.Commands;

/// <summary>
/// Tests for the render CLI command.
/// </summary>
public class RenderCommandTests
{
    /// <summary>
    /// Verifies that the root command has a render subcommand.
    /// </summary>
    [Fact]
    public void CreateRootCommand_HasRenderSubcommand()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();

        // Assert
        var renderCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "render");
        Assert.NotNull(renderCommand);
        Assert.Equal("Render a template to an image file", renderCommand.Description);
    }

    /// <summary>
    /// Verifies that the render command has a template argument.
    /// </summary>
    [Fact]
    public void RenderCommand_HasTemplateArgument()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var renderCommand = rootCommand.Subcommands.First(c => c.Name == "render");

        // Assert
        var templateArg = renderCommand.Arguments.FirstOrDefault(a => a.Name == "template");
        Assert.NotNull(templateArg);
    }

    /// <summary>
    /// Verifies that the render command has a data option.
    /// </summary>
    [Fact]
    public void RenderCommand_HasDataOption()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var renderCommand = rootCommand.Subcommands.First(c => c.Name == "render");

        // Assert
        var dataOption = renderCommand.Options.FirstOrDefault(o => o.Name == "--data");
        Assert.NotNull(dataOption);
        Assert.Contains("-d", dataOption.Aliases);
    }

    /// <summary>
    /// Verifies that the render command has an output option.
    /// </summary>
    [Fact]
    public void RenderCommand_HasOutputOption()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var renderCommand = rootCommand.Subcommands.First(c => c.Name == "render");

        // Assert
        var outputOption = renderCommand.Options.FirstOrDefault(o => o.Name == "--output");
        Assert.NotNull(outputOption);
        Assert.Contains("-o", outputOption.Aliases);
    }

    /// <summary>
    /// Verifies that the render command has a quality option.
    /// </summary>
    [Fact]
    public void RenderCommand_HasQualityOption()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var renderCommand = rootCommand.Subcommands.First(c => c.Name == "render");

        // Assert
        var qualityOption = renderCommand.Options.FirstOrDefault(o => o.Name == "--quality");
        Assert.NotNull(qualityOption);
    }

    /// <summary>
    /// Verifies that the render command has an open option.
    /// </summary>
    [Fact]
    public void RenderCommand_HasOpenOption()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();
        var renderCommand = rootCommand.Subcommands.First(c => c.Name == "render");

        // Assert
        var openOption = renderCommand.Options.FirstOrDefault(o => o.Name == "--open");
        Assert.NotNull(openOption);
    }

    /// <summary>
    /// Verifies that rendering without output option returns non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Render_WithoutOutput_ReturnsNonZero()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");

        // Act
        var result = await Program.Main(["render", templatePath]);

        // Assert
        Assert.NotEqual(0, result);
    }

    /// <summary>
    /// Verifies that rendering with non-existent template returns non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Render_WithNonExistentTemplate_ReturnsNonZero()
    {
        // Act
        var result = await Program.Main(["render", "non-existent.yaml", "-o", "output.png"]);

        // Assert
        Assert.NotEqual(0, result);
    }

    /// <summary>
    /// Verifies that rendering with valid inputs returns exit code 0.
    /// </summary>
    [Fact]
    public async Task Render_WithValidInputs_ReturnsZero()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");
        var dataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-data.json");
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}.png");

        try
        {
            // Act
            var result = await Program.Main(["render", templatePath, "-d", dataPath, "-o", outputPath]);

            // Assert
            Assert.Equal(0, result);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    /// <summary>
    /// Verifies that rendering with invalid output format returns non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Render_WithInvalidOutputFormat_ReturnsNonZero()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}.gif");

        // Act
        var result = await Program.Main(["render", templatePath, "-o", outputPath]);

        // Assert
        Assert.NotEqual(0, result);
    }
}
