using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FlexRender.ImageSharp.Tests.Snapshots;

/// <summary>
/// Result of comparing two ImageSharp bitmap snapshots.
/// </summary>
/// <param name="IsMatch">True if the images match within the tolerance threshold.</param>
/// <param name="DifferencePercent">Percentage of pixels that differ (0.0 to 100.0).</param>
/// <param name="DiffImage">
/// An image highlighting differences: original pixels where they match, red where they differ.
/// Null if the images match exactly.
/// </param>
internal sealed record ImageSharpSnapshotCompareResult(
    bool IsMatch,
    double DifferencePercent,
    Image<Rgba32>? DiffImage) : IDisposable
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
/// Compares two ImageSharp images pixel by pixel for visual snapshot testing.
/// </summary>
internal static class ImageSharpSnapshotComparer
{
    /// <summary>
    /// Default threshold for color channel differences to handle antialiasing.
    /// A difference of +/- this value per channel is considered a match.
    /// </summary>
    public const int DefaultColorThreshold = 5;

    /// <summary>
    /// Color used to highlight differences in the diff image.
    /// </summary>
    private static readonly Rgba32 DiffHighlightColor = new(255, 0, 0, 255);

    /// <summary>
    /// Compares two images and returns a detailed comparison result.
    /// </summary>
    /// <param name="expected">The expected (baseline) image.</param>
    /// <param name="actual">The actual image to compare.</param>
    /// <param name="colorThreshold">
    /// Tolerance per color channel (0-255). Default is 5 to handle antialiasing differences.
    /// </param>
    /// <returns>A result containing match status, difference percentage, and optional diff image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when expected or actual is null.</exception>
    public static ImageSharpSnapshotCompareResult Compare(
        Image<Rgba32> expected,
        Image<Rgba32> actual,
        int colorThreshold = DefaultColorThreshold)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return new ImageSharpSnapshotCompareResult(
                IsMatch: false,
                DifferencePercent: 100.0,
                DiffImage: null);
        }

        return ComparePixels(expected, actual, colorThreshold);
    }

    /// <summary>
    /// Performs pixel-by-pixel comparison of two same-sized images.
    /// </summary>
    private static ImageSharpSnapshotCompareResult ComparePixels(
        Image<Rgba32> expected,
        Image<Rgba32> actual,
        int colorThreshold)
    {
        var width = expected.Width;
        var height = expected.Height;
        var totalPixels = width * height;

        if (totalPixels == 0)
        {
            return new ImageSharpSnapshotCompareResult(
                IsMatch: true,
                DifferencePercent: 0.0,
                DiffImage: null);
        }

        var differingPositions = new List<(int X, int Y)>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var e = expected[x, y];
                var a = actual[x, y];

                if (!ColorsMatch(e, a, colorThreshold))
                {
                    differingPositions.Add((x, y));
                }
            }
        }

        var differencePercent = (double)differingPositions.Count / totalPixels * 100.0;
        var isMatch = differingPositions.Count == 0;

        Image<Rgba32>? diffImage = null;
        if (!isMatch)
        {
            diffImage = CreateDiffImage(expected, differingPositions);
        }

        return new ImageSharpSnapshotCompareResult(
            IsMatch: isMatch,
            DifferencePercent: differencePercent,
            DiffImage: diffImage);
    }

    /// <summary>
    /// Creates a diff image highlighting the specified differing pixel positions.
    /// </summary>
    /// <param name="expected">The expected image to use as base.</param>
    /// <param name="differingPositions">List of pixel positions that differ.</param>
    /// <returns>A new image with differences highlighted in red.</returns>
    private static Image<Rgba32> CreateDiffImage(
        Image<Rgba32> expected,
        List<(int X, int Y)> differingPositions)
    {
        var diffImage = expected.Clone();

        foreach (var (x, y) in differingPositions)
        {
            diffImage[x, y] = DiffHighlightColor;
        }

        return diffImage;
    }

    /// <summary>
    /// Determines if two colors match within the specified threshold.
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The actual color.</param>
    /// <param name="threshold">Maximum allowed difference per channel.</param>
    /// <returns>True if all channels are within the threshold; otherwise, false.</returns>
    private static bool ColorsMatch(Rgba32 expected, Rgba32 actual, int threshold)
    {
        return Math.Abs(expected.R - actual.R) <= threshold
            && Math.Abs(expected.G - actual.G) <= threshold
            && Math.Abs(expected.B - actual.B) <= threshold
            && Math.Abs(expected.A - actual.A) <= threshold;
    }
}
