using System.Reflection;
using FlexRender.Barcode.Svg.Providers;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.QrCode.Svg.Providers;
using FlexRender.Svg.Rendering;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Abstract base class for SVG golden snapshot testing.
/// Provides infrastructure for rendering templates to SVG strings and comparing against golden files.
/// </summary>
/// <remarks>
/// <para>
/// This class handles the complete SVG snapshot testing workflow:
/// <list type="bullet">
/// <item>Rendering templates to SVG strings using <see cref="SvgRenderingEngine"/></item>
/// <item>Comparing rendered SVG output against golden <c>.svg</c> files via string comparison</item>
/// <item>Saving actual output for debugging when mismatches occur</item>
/// <item>Updating golden files when the <c>UPDATE_SNAPSHOTS</c> environment variable is set</item>
/// </list>
/// </para>
/// <para>
/// Because SVG output is deterministic text (no font rasterization or anti-aliasing),
/// comparison is an exact string match rather than pixel-based tolerance.
/// </para>
/// <para>
/// Golden SVG files are stored in <c>tests/FlexRender.Tests/Snapshots/golden/{testName}.svg</c>.
/// Actual output on mismatch is saved to <c>tests/FlexRender.Tests/Snapshots/output/{testName}.actual.svg</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MySvgTests : SvgSnapshotTestBase
/// {
///     [Fact]
///     public void SimpleText_RendersCorrectly()
///     {
///         var template = CreateTemplate(200, 100);
///         template.AddElement(new TextElement { Content = "Hello", Size = "16" });
///
///         AssertSvgSnapshot("simple_text", template, new ObjectValue());
///     }
/// }
/// </code>
/// </example>
public abstract class SvgSnapshotTestBase
{
    /// <summary>
    /// Environment variable name that enables golden file update mode.
    /// </summary>
    private const string UpdateSnapshotsEnvVar = "UPDATE_SNAPSHOTS";

    /// <summary>
    /// Directory name for golden (expected) SVG files.
    /// </summary>
    private const string GoldenDirectoryName = "golden";

    /// <summary>
    /// Directory name for output (actual) SVG files.
    /// </summary>
    private const string OutputDirectoryName = "output";

    private readonly SvgRenderingEngine _engine;
    private readonly string _snapshotsBasePath;
    private readonly bool _updateSnapshots;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgSnapshotTestBase"/> class.
    /// Creates an <see cref="SvgRenderingEngine"/> configured with QR and barcode SVG providers.
    /// </summary>
    protected SvgSnapshotTestBase()
    {
        _snapshotsBasePath = GetSnapshotsBasePath();
        _updateSnapshots = IsUpdateSnapshotsEnabled();

        var limits = new ResourceLimits();
        var expander = new TemplateExpander(limits);
        var preprocessor = new SvgPreprocessor(new TemplateProcessor(limits));
        var layoutEngine = new LayoutEngine(limits);

        _engine = new SvgRenderingEngine(
            limits,
            expander,
            preprocessor,
            layoutEngine,
            baseFontSize: 16f,
            qrSvgProvider: new QrSvgProvider(),
            barcodeSvgProvider: new BarcodeSvgProvider());
    }

    /// <summary>
    /// Gets a value indicating whether golden file update mode is enabled.
    /// </summary>
    protected bool IsUpdateMode => _updateSnapshots;

    /// <summary>
    /// Creates a template with the specified canvas size and white background.
    /// Uses <see cref="FixedDimension.Both"/> to produce a fixed-size SVG canvas.
    /// </summary>
    /// <param name="width">The canvas width in SVG user units.</param>
    /// <param name="height">The canvas height in SVG user units.</param>
    /// <returns>A new template configured with the specified dimensions.</returns>
    protected static Template CreateTemplate(int width, int height)
    {
        return new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Both,
                Width = width,
                Height = height,
                Background = "#ffffff"
            }
        };
    }

    /// <summary>
    /// Asserts that the SVG output for a rendered template matches the golden snapshot file.
    /// </summary>
    /// <param name="testName">
    /// A unique name for this test, used to locate the golden SVG file.
    /// Should be a valid filename without extension (e.g., "svg_text_basic").
    /// </param>
    /// <param name="template">The template to render.</param>
    /// <param name="data">The data to use for template variable substitution.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="testName"/>, <paramref name="template"/>, or <paramref name="data"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="testName"/> is empty or whitespace.
    /// </exception>
    protected void AssertSvgSnapshot(string testName, Template template, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(testName);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        var actualSvg = _engine.RenderToSvg(template, data);

        if (_updateSnapshots)
        {
            UpdateGoldenFile(testName, actualSvg);
            return;
        }

        CompareWithGolden(testName, actualSvg);
    }

    /// <summary>
    /// Updates the golden SVG file with the current rendering output.
    /// </summary>
    /// <param name="testName">The test name for the golden file.</param>
    /// <param name="svgContent">The SVG content to save as the new golden file.</param>
    private void UpdateGoldenFile(string testName, string svgContent)
    {
        var goldenPath = GetGoldenFilePath(testName);
        var goldenDirectory = Path.GetDirectoryName(goldenPath)!;

        Directory.CreateDirectory(goldenDirectory);
        File.WriteAllText(goldenPath, svgContent);
    }

    /// <summary>
    /// Compares the actual SVG output with the golden file and asserts they match exactly.
    /// </summary>
    /// <param name="testName">The test name for locating the golden file.</param>
    /// <param name="actualSvg">The actual SVG output string.</param>
    private void CompareWithGolden(string testName, string actualSvg)
    {
        var goldenPath = GetGoldenFilePath(testName);

        if (!File.Exists(goldenPath))
        {
            SaveActualOutput(testName, actualSvg);
            Assert.Fail(
                $"Golden SVG file not found: {goldenPath}. " +
                $"Run tests with UPDATE_SNAPSHOTS=true to create it. " +
                $"Actual output saved to: {GetActualFilePath(testName)}");
        }

        var expectedSvg = File.ReadAllText(goldenPath);

        if (string.Equals(expectedSvg, actualSvg, StringComparison.Ordinal))
        {
            return;
        }

        // Save actual output for debugging
        SaveActualOutput(testName, actualSvg);

        var actualPath = GetActualFilePath(testName);

        // Find the first difference position for helpful diagnostics
        var diffPosition = FindFirstDifference(expectedSvg, actualSvg);
        var contextStart = Math.Max(0, diffPosition - 40);
        var expectedSnippet = GetSnippet(expectedSvg, contextStart, 80);
        var actualSnippet = GetSnippet(actualSvg, contextStart, 80);

        Assert.Fail(
            $"SVG snapshot mismatch for '{testName}'.\n" +
            $"First difference at character {diffPosition}.\n" +
            $"Expected length: {expectedSvg.Length}, Actual length: {actualSvg.Length}\n" +
            $"Expected: ...{expectedSnippet}...\n" +
            $"Actual:   ...{actualSnippet}...\n" +
            $"Golden: {goldenPath}\n" +
            $"Actual: {actualPath}\n" +
            $"Run tests with UPDATE_SNAPSHOTS=true to update the golden file.");
    }

    /// <summary>
    /// Saves the actual SVG output to the output directory for debugging.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <param name="svgContent">The SVG content to save.</param>
    private void SaveActualOutput(string testName, string svgContent)
    {
        var path = GetActualFilePath(testName);
        var directory = Path.GetDirectoryName(path)!;

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, svgContent);
    }

    /// <summary>
    /// Gets the path to the golden SVG file for the specified test.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <returns>The full path to the golden SVG file.</returns>
    private string GetGoldenFilePath(string testName)
    {
        return Path.Combine(_snapshotsBasePath, GoldenDirectoryName, $"{testName}.svg");
    }

    /// <summary>
    /// Gets the path to the actual output SVG file for the specified test.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <returns>The full path to the actual SVG file.</returns>
    private string GetActualFilePath(string testName)
    {
        return Path.Combine(_snapshotsBasePath, OutputDirectoryName, $"{testName}.actual.svg");
    }

    /// <summary>
    /// Gets the base path for snapshot files by locating the project root.
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
    /// Finds the project root directory by walking up from the given directory
    /// and looking for a <c>.csproj</c> file.
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
    /// Checks if the <c>UPDATE_SNAPSHOTS</c> environment variable is set to enable golden update mode.
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
    /// Finds the index of the first character that differs between two strings.
    /// </summary>
    /// <param name="expected">The expected string.</param>
    /// <param name="actual">The actual string.</param>
    /// <returns>The zero-based index of the first difference, or the length of the shorter string if one is a prefix of the other.</returns>
    private static int FindFirstDifference(string expected, string actual)
    {
        var minLength = Math.Min(expected.Length, actual.Length);

        for (var i = 0; i < minLength; i++)
        {
            if (expected[i] != actual[i])
            {
                return i;
            }
        }

        return minLength;
    }

    /// <summary>
    /// Extracts a substring snippet from the given string for diagnostic output.
    /// </summary>
    /// <param name="text">The source string.</param>
    /// <param name="start">The starting index.</param>
    /// <param name="length">The maximum length of the snippet.</param>
    /// <returns>A substring of the specified length, or shorter if the string is not long enough.</returns>
    private static string GetSnippet(string text, int start, int length)
    {
        if (start >= text.Length)
        {
            return "<end of string>";
        }

        var end = Math.Min(start + length, text.Length);
        return text[start..end];
    }
}
