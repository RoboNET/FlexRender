namespace FlexRender.Cli;

/// <summary>
/// Supported output image formats.
/// </summary>
public enum OutputFormat
{
    /// <summary>PNG format (lossless).</summary>
    Png,

    /// <summary>JPEG format (lossy, supports quality setting).</summary>
    Jpeg,

    /// <summary>BMP format (uncompressed).</summary>
    Bmp
}

/// <summary>
/// Extension methods for OutputFormat.
/// </summary>
public static class OutputFormatExtensions
{
    /// <summary>
    /// Determines the output format from a file path extension.
    /// </summary>
    /// <param name="path">File path to analyze.</param>
    /// <returns>The determined output format.</returns>
    /// <exception cref="ArgumentException">Thrown when the extension is not supported.</exception>
    public static OutputFormat FromPath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" => OutputFormat.Png,
            ".jpg" or ".jpeg" => OutputFormat.Jpeg,
            ".bmp" => OutputFormat.Bmp,
            _ => throw new ArgumentException(
                $"Unsupported output format: '{extension}'. Supported formats: .png, .jpg, .jpeg, .bmp",
                nameof(path))
        };
    }

    /// <summary>
    /// Gets the file extension for the format.
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <returns>File extension including the dot.</returns>
    public static string ToExtension(this OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Png => ".png",
            OutputFormat.Jpeg => ".jpg",
            OutputFormat.Bmp => ".bmp",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    /// <summary>
    /// Gets the MIME type for the format.
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <returns>MIME type string.</returns>
    public static string ToMimeType(this OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Png => "image/png",
            OutputFormat.Jpeg => "image/jpeg",
            OutputFormat.Bmp => "image/bmp",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }
}
