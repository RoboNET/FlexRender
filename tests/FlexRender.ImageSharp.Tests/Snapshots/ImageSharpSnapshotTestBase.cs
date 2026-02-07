using System.Reflection;
using FlexRender.Abstractions;
using FlexRender.Barcode.ImageSharp;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.QrCode.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Snapshots;

/// <summary>
/// Abstract base class for ImageSharp visual snapshot testing.
/// Provides infrastructure for rendering templates and comparing against golden images.
/// </summary>
/// <remarks>
/// <para>
/// This class handles the complete snapshot testing workflow:
/// <list type="bullet">
/// <item>Rendering templates to images using <see cref="ImageSharpRender"/></item>
/// <item>Comparing rendered output against golden images</item>
/// <item>Generating diff images when mismatches occur</item>
/// <item>Updating golden images when <c>UPDATE_SNAPSHOTS</c> environment variable is set</item>
/// </list>
/// </para>
/// <para>
/// Golden images are stored in a single directory. Cross-platform font rendering
/// differences can be tolerated via the <c>maxDifferencePercent</c> parameter.
/// </para>
/// <para>
/// Fonts are registered through the template's <c>Fonts</c> dictionary using
/// <see cref="FontDefinition"/> objects pointing to the Inter-Regular.ttf font.
/// This ensures deterministic rendering across platforms.
/// </para>
/// </remarks>
public abstract class ImageSharpSnapshotTestBase : IDisposable
{
    /// <summary>
    /// Environment variable name that enables golden image update mode.
    /// </summary>
    private const string UpdateSnapshotsEnvVar = "UPDATE_SNAPSHOTS";

    /// <summary>
    /// Directory name for golden (expected) images.
    /// </summary>
    private const string GoldenDirectoryName = "golden";

    /// <summary>
    /// Directory name for output (actual and diff) images.
    /// </summary>
    private const string OutputDirectoryName = "output";

    /// <summary>
    /// Default maximum allowed percentage of differing pixels for cross-platform tolerance.
    /// </summary>
    private const double DefaultMaxDifferencePercent = 5.0;

    private readonly ImageSharpRender _renderer;
    private readonly string _snapshotsBasePath;
    private readonly string _fontPath;
    private readonly bool _updateSnapshots;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageSharpSnapshotTestBase"/> class.
    /// </summary>
    protected ImageSharpSnapshotTestBase()
    {
        _snapshotsBasePath = GetSnapshotsBasePath();
        _updateSnapshots = IsUpdateSnapshotsEnabled();

        // Resolve the deterministic font path (Fonts directory is sibling to Snapshots)
        _fontPath = Path.GetFullPath(Path.Combine(_snapshotsBasePath, "..", "Fonts", "Inter-Regular.ttf"));

        var limits = new ResourceLimits();
        var options = new FlexRenderOptions();

        var imageSharpBuilder = new ImageSharpBuilder();
        imageSharpBuilder.WithQr();
        imageSharpBuilder.WithBarcode();

        _renderer = new ImageSharpRender(
            limits,
            options,
            Array.Empty<IResourceLoader>(),
            imageSharpBuilder);
    }

    /// <summary>
    /// Gets a value indicating whether golden image update mode is enabled.
    /// </summary>
    protected bool IsUpdateMode => _updateSnapshots;

    /// <summary>
    /// Gets the absolute path to the deterministic test font.
    /// </summary>
    protected string FontPath => _fontPath;

    /// <summary>
    /// Creates a template with the specified canvas size, white background,
    /// and the deterministic Inter font registered for consistent rendering.
    /// </summary>
    /// <param name="width">The canvas width in pixels.</param>
    /// <param name="height">The canvas height in pixels.</param>
    /// <returns>A new template configured with the specified dimensions and fonts.</returns>
    protected Template CreateTemplate(int width, int height)
    {
        return new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Both,
                Width = width,
                Height = height,
                Background = "#ffffff"
            },
            Fonts = new Dictionary<string, FontDefinition>
            {
                ["default"] = new FontDefinition(_fontPath),
                ["main"] = new FontDefinition(_fontPath)
            }
        };
    }

    /// <summary>
    /// Asserts that a rendered template matches the golden snapshot image.
    /// </summary>
    /// <param name="testName">
    /// A unique name for this test, used to locate the golden image file.
    /// Should be a valid filename without extension.
    /// </param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data to use for template variable substitution.</param>
    /// <param name="colorThreshold">
    /// Tolerance per color channel (0-255) for pixel comparison.
    /// Default is <see cref="ImageSharpSnapshotComparer.DefaultColorThreshold"/>.
    /// </param>
    /// <param name="maxDifferencePercent">
    /// Maximum allowed percentage of differing pixels (0.0 to 100.0).
    /// Default is 5.0% to tolerate cross-platform anti-aliasing differences.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="testName"/>, <paramref name="template"/>, or <paramref name="data"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="testName"/> is empty or whitespace.
    /// </exception>
    protected void AssertSnapshot(
        string testName,
        Template template,
        ObjectValue data,
        int colorThreshold = ImageSharpSnapshotComparer.DefaultColorThreshold,
        double maxDifferencePercent = DefaultMaxDifferencePercent)
    {
        ArgumentNullException.ThrowIfNull(testName);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Render to PNG bytes via the ImageSharpRender
        var pngBytes = _renderer.RenderToPng(template, data).GetAwaiter().GetResult();

        using var actualImage = Image.Load<Rgba32>(pngBytes);

        if (_updateSnapshots)
        {
            UpdateGoldenImage(testName, actualImage);
            return;
        }

        CompareWithGolden(testName, actualImage, colorThreshold, maxDifferencePercent);
    }

    /// <summary>
    /// Updates the golden image with the current rendering.
    /// </summary>
    /// <param name="testName">The test name for the golden image.</param>
    /// <param name="image">The image to save as the new golden image.</param>
    private void UpdateGoldenImage(string testName, Image<Rgba32> image)
    {
        var goldenPath = GetGoldenImagePath(testName);
        var goldenDirectory = Path.GetDirectoryName(goldenPath)!;

        Directory.CreateDirectory(goldenDirectory);
        image.Save(goldenPath, new PngEncoder());
    }

    /// <summary>
    /// Compares the actual image with the golden image and asserts they match.
    /// </summary>
    /// <param name="testName">The test name for locating the golden image.</param>
    /// <param name="actualImage">The actual rendered image.</param>
    /// <param name="colorThreshold">The color threshold for comparison.</param>
    /// <param name="maxDifferencePercent">Maximum allowed percentage of differing pixels.</param>
    private void CompareWithGolden(
        string testName,
        Image<Rgba32> actualImage,
        int colorThreshold,
        double maxDifferencePercent)
    {
        var goldenPath = GetGoldenImagePath(testName);

        if (!File.Exists(goldenPath))
        {
            SaveActualImage(testName, actualImage);
            Assert.Fail(
                $"Golden image not found: {goldenPath}. " +
                $"Run tests with UPDATE_SNAPSHOTS=true to create it. " +
                $"Actual image saved to: {GetActualImagePath(testName)}");
        }

        using var goldenImage = Image.Load<Rgba32>(goldenPath);
        using var result = ImageSharpSnapshotComparer.Compare(goldenImage, actualImage, colorThreshold);

        if (result.DifferencePercent <= maxDifferencePercent)
        {
            return;
        }

        // Save actual and diff images for debugging
        SaveActualImage(testName, actualImage);

        if (result.DiffImage is not null)
        {
            SaveDiffImage(testName, result.DiffImage);
        }

        var actualPath = GetActualImagePath(testName);
        var diffPath = GetDiffImagePath(testName);

        Assert.Fail(
            $"Snapshot mismatch for '{testName}'.\n" +
            $"Difference: {result.DifferencePercent:F2}% ({maxDifferencePercent:F2}% allowed).\n" +
            $"Golden: {goldenPath}\n" +
            $"Actual: {actualPath}\n" +
            $"Diff:   {diffPath}\n" +
            $"Run tests with UPDATE_SNAPSHOTS=true to update the golden image.");
    }

    /// <summary>
    /// Saves the actual rendered image to the output directory.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <param name="image">The image to save.</param>
    private void SaveActualImage(string testName, Image<Rgba32> image)
    {
        var path = GetActualImagePath(testName);
        SaveImage(image, path);
    }

    /// <summary>
    /// Saves the diff image to the output directory.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <param name="image">The diff image to save.</param>
    private void SaveDiffImage(string testName, Image<Rgba32> image)
    {
        var path = GetDiffImagePath(testName);
        SaveImage(image, path);
    }

    /// <summary>
    /// Saves an image to the specified path, creating directories as needed.
    /// </summary>
    /// <param name="image">The image to save.</param>
    /// <param name="path">The file path to save to.</param>
    private static void SaveImage(Image<Rgba32> image, string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        image.Save(path, new PngEncoder());
    }

    /// <summary>
    /// Gets the path to the golden image for the specified test.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <returns>The full path to the golden image.</returns>
    private string GetGoldenImagePath(string testName)
    {
        return Path.Combine(_snapshotsBasePath, GoldenDirectoryName, $"{testName}.png");
    }

    /// <summary>
    /// Gets the path to the actual output image for the specified test.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <returns>The full path to the actual image.</returns>
    private string GetActualImagePath(string testName)
    {
        return Path.Combine(_snapshotsBasePath, OutputDirectoryName, $"{testName}.actual.png");
    }

    /// <summary>
    /// Gets the path to the diff image for the specified test.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <returns>The full path to the diff image.</returns>
    private string GetDiffImagePath(string testName)
    {
        return Path.Combine(_snapshotsBasePath, OutputDirectoryName, $"{testName}.diff.png");
    }

    /// <summary>
    /// Gets the base path for snapshot files, relative to the test assembly location.
    /// </summary>
    /// <returns>The absolute path to the Snapshots directory.</returns>
    private static string GetSnapshotsBasePath()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)!;

        var projectRoot = FindProjectRoot(assemblyDirectory);
        return Path.Combine(projectRoot, "Snapshots");
    }

    /// <summary>
    /// Finds the project root directory by looking for the .csproj file.
    /// </summary>
    /// <param name="startDirectory">The directory to start searching from.</param>
    /// <returns>The project root directory path.</returns>
    private static string FindProjectRoot(string startDirectory)
    {
        var current = startDirectory;

        while (!string.IsNullOrEmpty(current))
        {
            var csprojFiles = Directory.GetFiles(current, "*.csproj");
            if (csprojFiles.Length > 0)
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return startDirectory;
    }

    /// <summary>
    /// Checks if the UPDATE_SNAPSHOTS environment variable is set to enable golden update mode.
    /// </summary>
    /// <returns>True if update mode is enabled; otherwise, false.</returns>
    private static bool IsUpdateSnapshotsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(UpdateSnapshotsEnvVar);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Disposes resources used by this test base.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(); false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _renderer.Dispose();
        }

        _disposed = true;
    }
}
