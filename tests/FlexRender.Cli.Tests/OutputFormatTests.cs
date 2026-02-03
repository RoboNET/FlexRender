using Xunit;

namespace FlexRender.Cli.Tests;

/// <summary>
/// Tests for the OutputFormat enum and extensions.
/// </summary>
public class OutputFormatTests
{
    /// <summary>
    /// Verifies that valid file extensions return the correct output format.
    /// </summary>
    [Theory]
    [InlineData("output.png", OutputFormat.Png)]
    [InlineData("output.PNG", OutputFormat.Png)]
    [InlineData("output.jpg", OutputFormat.Jpeg)]
    [InlineData("output.jpeg", OutputFormat.Jpeg)]
    [InlineData("output.JPEG", OutputFormat.Jpeg)]
    [InlineData("output.bmp", OutputFormat.Bmp)]
    [InlineData("output.BMP", OutputFormat.Bmp)]
    public void FromExtension_WithValidExtension_ReturnsCorrectFormat(string path, OutputFormat expected)
    {
        // Act
        var result = OutputFormatExtensions.FromPath(path);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that invalid file extensions throw ArgumentException.
    /// </summary>
    [Theory]
    [InlineData("output.gif")]
    [InlineData("output.webp")]
    [InlineData("output")]
    public void FromExtension_WithInvalidExtension_ThrowsArgumentException(string path)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => OutputFormatExtensions.FromPath(path));
    }

    /// <summary>
    /// Verifies that output formats return the correct file extension.
    /// </summary>
    [Theory]
    [InlineData(OutputFormat.Png, ".png")]
    [InlineData(OutputFormat.Jpeg, ".jpg")]
    [InlineData(OutputFormat.Bmp, ".bmp")]
    public void ToExtension_ReturnsCorrectExtension(OutputFormat format, string expected)
    {
        // Act
        var result = format.ToExtension();

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that output formats return the correct MIME type.
    /// </summary>
    [Theory]
    [InlineData(OutputFormat.Png, "image/png")]
    [InlineData(OutputFormat.Jpeg, "image/jpeg")]
    [InlineData(OutputFormat.Bmp, "image/bmp")]
    public void ToMimeType_ReturnsCorrectMimeType(OutputFormat format, string expected)
    {
        // Act
        var result = format.ToMimeType();

        // Assert
        Assert.Equal(expected, result);
    }
}
