using System.CommandLine;
using Xunit;

namespace FlexRender.Cli.Tests.Commands;

/// <summary>
/// Tests for the validate CLI command.
/// </summary>
public class ValidateCommandTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateCommandTests"/> class.
    /// </summary>
    public ValidateCommandTests()
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

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Verifies that the root command has a validate subcommand.
    /// </summary>
    [Fact]
    public void CreateRootCommand_HasValidateSubcommand()
    {
        // Act
        var rootCommand = Program.CreateRootCommand(_serviceProvider);

        // Assert
        var validateCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "validate");
        Assert.NotNull(validateCommand);
        Assert.Equal("Validate a template file without rendering", validateCommand.Description);
    }

    /// <summary>
    /// Verifies that validating a valid template returns exit code 0.
    /// </summary>
    [Fact]
    public async Task Validate_WithValidTemplate_ReturnsZero()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");

        // Act
        var result = await Program.Main(["validate", templatePath]);

        // Assert
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies that validating an invalid template returns non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Validate_WithInvalidTemplate_ReturnsNonZero()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "invalid-template.yaml");

        // Act
        var result = await Program.Main(["validate", templatePath]);

        // Assert
        Assert.NotEqual(0, result);
    }

    /// <summary>
    /// Verifies that validating a non-existent file returns non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Validate_WithNonExistentFile_ReturnsNonZero()
    {
        // Act
        var result = await Program.Main(["validate", "non-existent-file.yaml"]);

        // Assert
        Assert.NotEqual(0, result);
    }
}
