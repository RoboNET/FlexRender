using Xunit;

namespace FlexRender.Cli.Tests;

/// <summary>
/// Integration tests that verify end-to-end CLI behavior including stdout/stderr capture.
/// </summary>
public sealed class IntegrationTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the IntegrationTests class.
    /// </summary>
    public IntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"skialayout-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Cleans up the temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Executes the CLI with the given arguments and captures all output.
    /// </summary>
    /// <param name="args">The command-line arguments to pass to the CLI.</param>
    /// <returns>A tuple containing the exit code, captured stdout, and captured stderr.</returns>
    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCli(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);

        try
        {
            var exitCode = await Program.Main(args);
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    /// Verifies that validate, info, and render all succeed for a valid template.
    /// </summary>
    [Fact]
    public async Task FullWorkflow_ValidateInfoRender_AllSucceed()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");
        var outputPath = Path.Combine(_tempDir, "output.png");

        // Act & Assert - Validate
        var validateResult = await RunCli("validate", templatePath);
        Assert.True(validateResult.ExitCode == 0,
            $"Validate failed with exit code {validateResult.ExitCode}. stderr: {validateResult.Stderr}");
        Assert.Contains("Valid", validateResult.Stdout);

        // Act & Assert - Info
        var infoResult = await RunCli("info", templatePath);
        Assert.True(infoResult.ExitCode == 0,
            $"Info failed with exit code {infoResult.ExitCode}. stderr: {infoResult.Stderr}");

        // Act & Assert - Render
        var renderResult = await RunCli("render", templatePath, "-o", outputPath);
        Assert.True(renderResult.ExitCode == 0,
            $"Render failed with exit code {renderResult.ExitCode}. stderr: {renderResult.Stderr}");

        // Verify output file was created with non-zero size
        Assert.True(File.Exists(outputPath),
            $"Output file was not created at {outputPath}. stderr: {renderResult.Stderr}");

        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0,
            $"Output file exists but is empty (0 bytes). stderr: {renderResult.Stderr}");
    }

    /// <summary>
    /// Verifies that rendering with data file creates output.
    /// </summary>
    [Fact]
    public async Task RenderWithData_CreatesOutput()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "template-with-variables.yaml");
        var dataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-data.json");
        var outputPath = Path.Combine(_tempDir, "output-with-data.png");

        // Act
        var result = await RunCli("render", templatePath, "-d", dataPath, "-o", outputPath);

        // Assert
        Assert.True(result.ExitCode == 0,
            $"CLI failed with exit code {result.ExitCode}. stderr: {result.Stderr}");
        Assert.True(File.Exists(outputPath),
            $"Output file was not created at {outputPath}. stderr: {result.Stderr}");

        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0,
            $"Output file exists but is empty (0 bytes). stderr: {result.Stderr}");
    }

    /// <summary>
    /// Verifies that rendering with verbose flag shows detailed output.
    /// </summary>
    [Fact]
    public async Task RenderWithVerbose_ShowsDetailedOutput()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");
        var outputPath = Path.Combine(_tempDir, "output-verbose.png");

        // Act
        var result = await RunCli("render", templatePath, "-o", outputPath, "-v");

        // Assert
        Assert.True(result.ExitCode == 0,
            $"CLI failed with exit code {result.ExitCode}. stderr: {result.Stderr}");
        Assert.Contains("Scale:", result.Stdout);
    }

    /// <summary>
    /// Verifies that rendering with scale option succeeds.
    /// </summary>
    [Fact]
    public async Task RenderWithScale_AcceptsScaleOption()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");
        var unscaledOutputPath = Path.Combine(_tempDir, "output-unscaled.png");
        var scaledOutputPath = Path.Combine(_tempDir, "output-scaled.png");

        // Act - Render unscaled (default scale 1.0)
        var unscaledResult = await RunCli("render", templatePath, "-o", unscaledOutputPath);
        Assert.True(unscaledResult.ExitCode == 0,
            $"Unscaled render failed with exit code {unscaledResult.ExitCode}. stderr: {unscaledResult.Stderr}");

        // Act - Render scaled (2.0x)
        var scaledResult = await RunCli("render", templatePath, "-o", scaledOutputPath, "--scale", "2.0");

        // Assert
        Assert.True(scaledResult.ExitCode == 0,
            $"Scaled render failed with exit code {scaledResult.ExitCode}. stderr: {scaledResult.Stderr}");
        Assert.True(File.Exists(scaledOutputPath),
            $"Scaled output file was not created at {scaledOutputPath}. stderr: {scaledResult.Stderr}");

        var unscaledSize = new FileInfo(unscaledOutputPath).Length;
        var scaledSize = new FileInfo(scaledOutputPath).Length;
        Assert.True(scaledSize > unscaledSize,
            $"Scaled file ({scaledSize} bytes) should be larger than unscaled file ({unscaledSize} bytes)");
    }

    /// <summary>
    /// Verifies that rendering to JPEG with quality succeeds.
    /// </summary>
    [Fact]
    public async Task RenderToJpeg_WithQuality_Succeeds()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");
        var outputPath = Path.Combine(_tempDir, "output.jpg");

        // Act
        var result = await RunCli("render", templatePath, "-o", outputPath, "--quality", "75");

        // Assert
        Assert.True(result.ExitCode == 0,
            $"CLI failed with exit code {result.ExitCode}. stderr: {result.Stderr}");
        Assert.True(File.Exists(outputPath),
            $"JPEG output file was not created at {outputPath}. stderr: {result.Stderr}");

        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0,
            $"JPEG output file exists but is empty (0 bytes). stderr: {result.Stderr}");
    }

    /// <summary>
    /// Verifies that rendering to BMP succeeds.
    /// </summary>
    [Fact]
    public async Task RenderToBmp_Succeeds()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");
        var outputPath = Path.Combine(_tempDir, "output.bmp");

        // Act
        var result = await RunCli("render", templatePath, "-o", outputPath);

        // Assert
        Assert.True(result.ExitCode == 0,
            $"CLI failed with exit code {result.ExitCode}. stderr: {result.Stderr}");
        Assert.True(File.Exists(outputPath),
            $"BMP output file was not created at {outputPath}. stderr: {result.Stderr}");

        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0,
            $"BMP output file exists but is empty (0 bytes). stderr: {result.Stderr}");
    }

    /// <summary>
    /// Verifies that rendering to a nested directory creates the directory.
    /// </summary>
    [Fact]
    public async Task RenderToNestedDirectory_CreatesDirectory()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "valid-template.yaml");
        var outputPath = Path.Combine(_tempDir, "nested", "dir", "output.png");

        // Act
        var result = await RunCli("render", templatePath, "-o", outputPath);

        // Assert
        Assert.True(result.ExitCode == 0,
            $"CLI failed with exit code {result.ExitCode}. stderr: {result.Stderr}");
        Assert.True(File.Exists(outputPath),
            $"Output file was not created in nested directory at {outputPath}. stderr: {result.Stderr}");
    }

    /// <summary>
    /// Verifies that info command shows variables for template with variables.
    /// </summary>
    [Fact]
    public async Task Info_WithVariableTemplate_ShowsVariables()
    {
        // Arrange
        var templatePath = Path.Combine(AppContext.BaseDirectory, "TestData", "template-with-variables.yaml");

        // Act
        var result = await RunCli("info", templatePath);

        // Assert
        Assert.True(result.ExitCode == 0,
            $"CLI failed with exit code {result.ExitCode}. stderr: {result.Stderr}");
        Assert.Contains("Variables", result.Stdout);
        Assert.Contains("title", result.Stdout);
        Assert.Contains("count", result.Stdout);
        Assert.Contains("price", result.Stdout);
    }

    /// <summary>
    /// Verifies that help command shows all subcommands.
    /// </summary>
    [Fact]
    public async Task HelpCommand_ShowsAllSubcommands()
    {
        // Act
        var result = await RunCli("--help");

        // Assert
        Assert.True(result.ExitCode == 0,
            $"CLI failed with exit code {result.ExitCode}. stderr: {result.Stderr}");
        Assert.Contains("render", result.Stdout);
        Assert.Contains("validate", result.Stdout);
        Assert.Contains("info", result.Stdout);
        Assert.Contains("watch", result.Stdout);
    }

    /// <summary>
    /// Verifies that subcommand help shows options.
    /// </summary>
    [Theory]
    [InlineData("render", "--help")]
    [InlineData("validate", "--help")]
    [InlineData("info", "--help")]
    [InlineData("watch", "--help")]
    public async Task SubcommandHelp_ShowsOptions(string subcommand, string helpFlag)
    {
        // Act
        var result = await RunCli(subcommand, helpFlag);

        // Assert
        Assert.True(result.ExitCode == 0,
            $"CLI '{subcommand} {helpFlag}' failed with exit code {result.ExitCode}. stderr: {result.Stderr}");
        Assert.Contains("--", result.Stdout);
    }
}
