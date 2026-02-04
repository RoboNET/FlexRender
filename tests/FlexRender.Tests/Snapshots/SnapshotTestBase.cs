using System.Reflection;
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Abstract base class for visual snapshot testing.
/// Provides infrastructure for rendering templates and comparing against golden images.
/// </summary>
/// <remarks>
/// <para>
/// This class handles the complete snapshot testing workflow:
/// <list type="bullet">
/// <item>Rendering templates to bitmaps using <see cref="SkiaRenderer"/></item>
/// <item>Comparing rendered output against golden images</item>
/// <item>Generating diff images when mismatches occur</item>
/// <item>Updating golden images when <c>UPDATE_SNAPSHOTS</c> environment variable is set</item>
/// </list>
/// </para>
/// <para>
/// Golden images are stored in a single directory. Cross-platform font rendering
/// differences can be tolerated via the <c>maxDifferencePercent</c> parameter.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MySnapshotTests : SnapshotTestBase
/// {
///     [Fact]
///     public void SimpleText_RendersCorrectly()
///     {
///         var template = Parser.Parse("canvas:\n  width: 200\nlayout:\n  - type: text\n    content: Hello");
///         var data = new ObjectValue();
///
///         AssertSnapshot("simple_text", template, data);
///     }
/// }
/// </code>
/// </example>
public abstract class SnapshotTestBase : IDisposable
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
    /// Accounts for anti-aliasing differences between macOS (Core Text) and Linux (FreeType).
    /// </summary>
    private const double DefaultMaxDifferencePercent = 5.0;

    private readonly SkiaRenderer _renderer;
    private readonly TemplateParser _parser;
    private readonly string _snapshotsBasePath;
    private readonly bool _updateSnapshots;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotTestBase"/> class.
    /// </summary>
    protected SnapshotTestBase()
    {
        _renderer = new SkiaRenderer(new ResourceLimits(), new QrProvider(), new BarcodeProvider());
        _parser = new TemplateParser();
        _snapshotsBasePath = GetSnapshotsBasePath();
        _updateSnapshots = IsUpdateSnapshotsEnabled();

        // Register Inter font for deterministic cross-platform rendering
        var fontPath = Path.Combine(_snapshotsBasePath, "Fonts", "Inter-Regular.ttf");
        _renderer.FontManager.RegisterFont("main", fontPath);
        _renderer.FontManager.RegisterFont("default", fontPath);
    }

    /// <summary>
    /// Gets the renderer instance for rendering templates.
    /// </summary>
    protected SkiaRenderer Renderer => _renderer;

    /// <summary>
    /// Gets the template parser instance for parsing YAML templates.
    /// </summary>
    protected TemplateParser Parser => _parser;

    /// <summary>
    /// Gets a value indicating whether golden image update mode is enabled.
    /// </summary>
    protected bool IsUpdateMode => _updateSnapshots;

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
    /// Default is <see cref="SnapshotComparer.DefaultColorThreshold"/>.
    /// </param>
    /// <param name="maxDifferencePercent">
    /// Maximum allowed percentage of differing pixels (0.0–100.0).
    /// Default is 5.0% to tolerate cross-platform anti-aliasing differences.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="testName"/>, <paramref name="template"/>, or <paramref name="data"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="testName"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="Xunit.Sdk.XunitException">
    /// Thrown when the rendered image does not match the golden image and update mode is disabled.
    /// </exception>
    protected void AssertSnapshot(
        string testName,
        Template template,
        ObjectValue data,
        int colorThreshold = SnapshotComparer.DefaultColorThreshold,
        double maxDifferencePercent = DefaultMaxDifferencePercent)
    {
        ArgumentNullException.ThrowIfNull(testName);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Render the template to a bitmap
        using var actualBitmap = RenderTemplate(template, data);

        if (_updateSnapshots)
        {
            UpdateGoldenImage(testName, actualBitmap);
            return;
        }

        CompareWithGolden(testName, actualBitmap, colorThreshold, maxDifferencePercent);
    }

    /// <summary>
    /// Renders a template to a bitmap.
    /// </summary>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data for variable substitution.</param>
    /// <returns>A bitmap containing the rendered template.</returns>
    private SKBitmap RenderTemplate(Template template, ObjectValue data)
    {
        var size = _renderer.Measure(template, data);
        var width = (int)Math.Ceiling(size.Width);
        var height = (int)Math.Ceiling(size.Height);

        // Ensure minimum size
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _renderer.Render(bitmap, template, data);

        return bitmap;
    }

    /// <summary>
    /// Updates the golden image with the current rendering.
    /// </summary>
    /// <param name="testName">The test name for the golden image.</param>
    /// <param name="bitmap">The bitmap to save as the new golden image.</param>
    private void UpdateGoldenImage(string testName, SKBitmap bitmap)
    {
        var goldenPath = GetGoldenImagePath(testName);
        var goldenDirectory = Path.GetDirectoryName(goldenPath)!;

        Directory.CreateDirectory(goldenDirectory);

        using var image = SKImage.FromBitmap(bitmap);
        using var encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(goldenPath);
        encodedData.SaveTo(stream);
    }

    /// <summary>
    /// Compares the actual bitmap with the golden image and asserts they match.
    /// </summary>
    /// <param name="testName">The test name for locating the golden image.</param>
    /// <param name="actualBitmap">The actual rendered bitmap.</param>
    /// <param name="colorThreshold">The color threshold for comparison.</param>
    /// <param name="maxDifferencePercent">Maximum allowed percentage of differing pixels.</param>
    private void CompareWithGolden(string testName, SKBitmap actualBitmap, int colorThreshold, double maxDifferencePercent)
    {
        var goldenPath = GetGoldenImagePath(testName);

        if (!File.Exists(goldenPath))
        {
            SaveActualImage(testName, actualBitmap);
            Assert.Fail(
                $"Golden image not found: {goldenPath}. " +
                $"Run tests with UPDATE_SNAPSHOTS=true to create it. " +
                $"Actual image saved to: {GetActualImagePath(testName)}");
        }

        using var goldenBitmap = LoadBitmap(goldenPath);
        using var result = SnapshotComparer.Compare(goldenBitmap, actualBitmap, colorThreshold);

        if (result.DifferencePercent <= maxDifferencePercent)
        {
            return;
        }

        // Save actual and diff images for debugging
        SaveActualImage(testName, actualBitmap);

        if (result.DiffImage != null)
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
    /// Loads a bitmap from the specified file path.
    /// </summary>
    /// <param name="path">The path to the image file.</param>
    /// <returns>The loaded bitmap.</returns>
    private static SKBitmap LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        return SKBitmap.Decode(stream);
    }

    /// <summary>
    /// Saves the actual rendered image to the output directory.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <param name="bitmap">The bitmap to save.</param>
    private void SaveActualImage(string testName, SKBitmap bitmap)
    {
        var path = GetActualImagePath(testName);
        SaveBitmap(bitmap, path);
    }

    /// <summary>
    /// Saves the diff image to the output directory.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <param name="bitmap">The diff bitmap to save.</param>
    private void SaveDiffImage(string testName, SKBitmap bitmap)
    {
        var path = GetDiffImagePath(testName);
        SaveBitmap(bitmap, path);
    }

    /// <summary>
    /// Saves a bitmap to the specified path, creating directories as needed.
    /// </summary>
    /// <param name="bitmap">The bitmap to save.</param>
    /// <param name="path">The file path to save to.</param>
    private static void SaveBitmap(SKBitmap bitmap, string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        using var image = SKImage.FromBitmap(bitmap);
        using var encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        encodedData.SaveTo(stream);
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

        // Navigate from bin/Debug/net10.0 back to the project root, then to Snapshots
        // This handles both debug and release configurations
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
            if (parent == null)
            {
                break;
            }

            current = parent.FullName;
        }

        // Fallback to the assembly directory if project root not found
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

        // Accept "true", "1", "yes" as truthy values (case-insensitive)
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
