using SkiaSharp;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Result of comparing two bitmap snapshots.
/// </summary>
/// <param name="IsMatch">True if the images match within the tolerance threshold.</param>
/// <param name="DifferencePercent">Percentage of pixels that differ (0.0 to 100.0).</param>
/// <param name="DiffImage">
/// A bitmap highlighting differences: original pixels where they match, red where they differ.
/// Null if the images match exactly.
/// </param>
public sealed record SnapshotCompareResult(
    bool IsMatch,
    double DifferencePercent,
    SKBitmap? DiffImage) : IDisposable
{
    /// <summary>
    /// Disposes the diff image if present.
    /// </summary>
    public void Dispose()
    {
        DiffImage?.Dispose();
    }
}

/// <summary>
/// Compares two SKBitmap images pixel by pixel for visual snapshot testing.
/// </summary>
public static class SnapshotComparer
{
    /// <summary>
    /// Default threshold for color channel differences to handle antialiasing.
    /// A difference of +/- this value per channel is considered a match.
    /// </summary>
    public const int DefaultColorThreshold = 5;

    /// <summary>
    /// Color used to highlight differences in the diff image.
    /// </summary>
    private static readonly SKColor DiffHighlightColor = new(255, 0, 0, 255); // Red

    /// <summary>
    /// Compares two bitmaps and returns a detailed comparison result.
    /// </summary>
    /// <param name="expected">The expected (baseline) image.</param>
    /// <param name="actual">The actual image to compare.</param>
    /// <param name="colorThreshold">
    /// Tolerance per color channel (0-255). Default is 5 to handle antialiasing differences.
    /// </param>
    /// <returns>A result containing match status, difference percentage, and optional diff image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when expected or actual is null.</exception>
    public static SnapshotCompareResult Compare(
        SKBitmap expected,
        SKBitmap actual,
        int colorThreshold = DefaultColorThreshold)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        // Handle size mismatch - different dimensions = 100% difference
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return CreateSizeMismatchResult(expected, actual);
        }

        return ComparePixels(expected, actual, colorThreshold);
    }

    /// <summary>
    /// Creates a result for images with different dimensions.
    /// </summary>
    private static SnapshotCompareResult CreateSizeMismatchResult(SKBitmap expected, SKBitmap actual)
    {
        // Create a diff image showing the actual image with a red overlay
        var maxWidth = Math.Max(expected.Width, actual.Width);
        var maxHeight = Math.Max(expected.Height, actual.Height);

        var diffImage = new SKBitmap(maxWidth, maxHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(diffImage);
        canvas.Clear(SKColors.Transparent);

        // Draw actual image
        canvas.DrawBitmap(actual, 0, 0);

        // Draw red border/overlay to indicate size mismatch
        using var paint = new SKPaint
        {
            Color = new SKColor(255, 0, 0, 128),
            Style = SKPaintStyle.Fill
        };

        // Highlight areas where dimensions differ
        if (actual.Width < maxWidth)
        {
            canvas.DrawRect(actual.Width, 0, maxWidth - actual.Width, maxHeight, paint);
        }

        if (actual.Height < maxHeight)
        {
            canvas.DrawRect(0, actual.Height, maxWidth, maxHeight - actual.Height, paint);
        }

        return new SnapshotCompareResult(
            IsMatch: false,
            DifferencePercent: 100.0,
            DiffImage: diffImage);
    }

    /// <summary>
    /// Performs pixel-by-pixel comparison of two same-sized bitmaps.
    /// </summary>
    private static SnapshotCompareResult ComparePixels(
        SKBitmap expected,
        SKBitmap actual,
        int colorThreshold)
    {
        var width = expected.Width;
        var height = expected.Height;
        var totalPixels = width * height;

        if (totalPixels == 0)
        {
            return new SnapshotCompareResult(
                IsMatch: true,
                DifferencePercent: 0.0,
                DiffImage: null);
        }

        // First pass: count differences and collect their indices
        var differingIndices = new List<int>();
        var expectedPixels = GetPixels(expected);
        var actualPixels = GetPixels(actual);

        for (var i = 0; i < totalPixels; i++)
        {
            var expectedColor = expectedPixels[i];
            var actualColor = actualPixels[i];

            if (!ColorsMatch(expectedColor, actualColor, colorThreshold))
            {
                differingIndices.Add(i);
            }
        }

        var differencePercent = (double)differingIndices.Count / totalPixels * 100.0;
        var isMatch = differingIndices.Count == 0;

        // Only create diff image if there are differences
        SKBitmap? diffImage = null;
        if (!isMatch)
        {
            diffImage = CreateDiffImage(expected, differingIndices);
        }

        return new SnapshotCompareResult(
            IsMatch: isMatch,
            DifferencePercent: differencePercent,
            DiffImage: diffImage);
    }

    /// <summary>
    /// Creates a diff image highlighting the specified differing pixel indices.
    /// </summary>
    /// <param name="expected">The expected image to use as base.</param>
    /// <param name="differingIndices">List of pixel indices that differ.</param>
    /// <returns>A new bitmap with differences highlighted in red.</returns>
    private static SKBitmap CreateDiffImage(SKBitmap expected, List<int> differingIndices)
    {
        // Copy the expected image
        var diffImage = expected.Copy();

        // Draw red pixels at each differing location using canvas
        using var canvas = new SKCanvas(diffImage);
        using var paint = new SKPaint { Color = DiffHighlightColor };

        var width = expected.Width;
        foreach (var index in differingIndices)
        {
            var x = index % width;
            var y = index / width;
            canvas.DrawPoint(x, y, paint);
        }

        return diffImage;
    }

    /// <summary>
    /// Gets a read-only span of pixels from a bitmap.
    /// </summary>
    private static ReadOnlySpan<SKColor> GetPixels(SKBitmap bitmap)
    {
        var pixels = bitmap.GetPixelSpan();
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, SKColor>(pixels);
    }

    /// <summary>
    /// Determines if two colors match within the specified threshold.
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The actual color.</param>
    /// <param name="threshold">Maximum allowed difference per channel.</param>
    /// <returns>True if all channels are within the threshold; otherwise, false.</returns>
    private static bool ColorsMatch(SKColor expected, SKColor actual, int threshold)
    {
        return Math.Abs(expected.Red - actual.Red) <= threshold
            && Math.Abs(expected.Green - actual.Green) <= threshold
            && Math.Abs(expected.Blue - actual.Blue) <= threshold
            && Math.Abs(expected.Alpha - actual.Alpha) <= threshold;
    }
}
