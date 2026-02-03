using Xunit;

namespace FlexRender.Cli.Tests;

/// <summary>
/// Tests for the DataLoader class.
/// </summary>
public class DataLoaderTests
{
    /// <summary>
    /// Verifies that loading valid JSON returns an ObjectValue.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithValidJson_ReturnsObjectValue()
    {
        // Arrange
        var dataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-data.json");

        // Act
        var result = DataLoader.LoadFromFile(dataPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("title"));
    }

    /// <summary>
    /// Verifies that string values are parsed correctly.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithStringValue_ParsesCorrectly()
    {
        // Arrange
        var dataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-data.json");

        // Act
        var result = DataLoader.LoadFromFile(dataPath);

        // Assert
        var titleValue = result["title"];
        Assert.IsType<StringValue>(titleValue);
        Assert.Equal("Hello World", ((StringValue)titleValue).Value);
    }

    /// <summary>
    /// Verifies that number values are parsed correctly.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithNumberValue_ParsesCorrectly()
    {
        // Arrange
        var dataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-data.json");

        // Act
        var result = DataLoader.LoadFromFile(dataPath);

        // Assert
        var countValue = result["count"];
        Assert.IsType<NumberValue>(countValue);
        Assert.Equal(42m, ((NumberValue)countValue).Value);
    }

    /// <summary>
    /// Verifies that boolean values are parsed correctly.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithBoolValue_ParsesCorrectly()
    {
        // Arrange
        var dataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-data.json");

        // Act
        var result = DataLoader.LoadFromFile(dataPath);

        // Assert
        var enabledValue = result["enabled"];
        Assert.IsType<BoolValue>(enabledValue);
        Assert.True(((BoolValue)enabledValue).Value);
    }

    /// <summary>
    /// Verifies that array values are parsed correctly.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithArrayValue_ParsesCorrectly()
    {
        // Arrange
        var dataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-data.json");

        // Act
        var result = DataLoader.LoadFromFile(dataPath);

        // Assert
        var itemsValue = result["items"];
        Assert.IsType<ArrayValue>(itemsValue);
        Assert.Equal(2, ((ArrayValue)itemsValue).Items.Count);
    }

    /// <summary>
    /// Verifies that nested objects are parsed correctly.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithNestedObject_ParsesCorrectly()
    {
        // Arrange
        var dataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-data.json");

        // Act
        var result = DataLoader.LoadFromFile(dataPath);

        // Assert
        var nestedValue = result["nested"];
        Assert.IsType<ObjectValue>(nestedValue);
        var nested = (ObjectValue)nestedValue;
        Assert.Equal("value", ((StringValue)nested["key"]).Value);
    }

    /// <summary>
    /// Verifies that loading a non-existent file throws FileNotFoundException.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => DataLoader.LoadFromFile("non-existent.json"));
    }

    /// <summary>
    /// Verifies that loading invalid JSON throws an exception.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, "{ invalid json }");

        try
        {
            // Act & Assert
            Assert.ThrowsAny<Exception>(() => DataLoader.LoadFromFile(tempPath));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Verifies that LoadFromFile rejects files exceeding a custom max file size.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithCustomMaxFileSize_RejectsOversizedFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write content > 100 bytes
            File.WriteAllText(tempFile, new string('x', 200));

            var ex = Assert.Throws<InvalidOperationException>(() =>
                DataLoader.LoadFromFile(tempFile, maxFileSize: 100));
            Assert.Contains("exceeds maximum", ex.Message.ToLowerInvariant());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that LoadFromFile accepts files within a larger custom limit.
    /// </summary>
    [Fact]
    public void LoadFromFile_WithLargerCustomLimit_AcceptsFiles()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """{"key": "value"}""");

            var result = DataLoader.LoadFromFile(tempFile, maxFileSize: 10 * 1024 * 1024);

            Assert.NotNull(result);
            Assert.True(result.ContainsKey("key"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that LoadFromFile uses the 10 MB default limit when no custom limit is specified.
    /// </summary>
    [Fact]
    public void LoadFromFile_DefaultLimit_UsesTenMb()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """{"key": "value"}""");

            // Without maxFileSize param, should use 10 MB default
            var result = DataLoader.LoadFromFile(tempFile);

            Assert.NotNull(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
