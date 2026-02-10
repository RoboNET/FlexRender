using System.Reflection;
using System.Text.Json;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Snapshot tests for the WB Bank ATM receipt template.
/// Loads the receipt template from YAML, renders the result with JSON data,
/// and compares against a golden image.
/// </summary>
/// <remarks>
/// <para>
/// This test exercises the full rendering pipeline for a real-world template that includes:
/// <list type="bullet">
/// <item>Custom fonts loaded from relative file paths</item>
/// <item>Images loaded from relative file paths</item>
/// <item>Template variable substitution (<c>{{operationTitle}}</c>, <c>{{amount}}</c>, etc.)</item>
/// <item>AST-level control flow (<c>type: each</c>, <c>type: if</c>) for dynamic content</item>
/// <item>QR code generation from template data</item>
/// <item>Complex nested flex layouts</item>
/// </list>
/// </para>
/// <para>
/// Because the template references fonts and images via relative paths, the test
/// temporarily changes the working directory to the <c>examples/</c> folder so that
/// <see cref="System.IO.Path.GetFullPath(string)"/> resolves them correctly.
/// </para>
/// </remarks>
public sealed class WbReceiptSnapshotTests : SnapshotTestBase
{
    /// <summary>
    /// Renders the WB Bank ATM receipt template with sample data and asserts
    /// that the output matches the golden snapshot image.
    /// </summary>
    [Fact]
    public void WbReceipt_RendersCorrectly()
    {
        var repoRoot = FindRepositoryRoot();
        var yamlPath = Path.Combine(repoRoot, "examples", "private", "wb-receipt.yaml");
        var jsonPath = Path.Combine(repoRoot, "examples", "private", "wb-receipt-data.json");

        if (!File.Exists(yamlPath) || !File.Exists(jsonPath))
        {
            // Private template files are gitignored; skip test in CI
            return;
        }

        var yaml = File.ReadAllText(yamlPath);
        var json = File.ReadAllText(jsonPath);

        var data = ConvertJsonToObjectValue(json);
        var template = Parser.Parse(yaml);

        var previousDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.Combine(repoRoot, "examples"));
            AssertSnapshot("wb_receipt", template, data, colorThreshold: 10);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
        }
    }

    /// <summary>
    /// Finds the repository root directory by navigating upward from the test
    /// assembly location until a directory containing a <c>.sln</c> file or an
    /// <c>examples</c> subdirectory is found.
    /// </summary>
    /// <returns>The absolute path to the repository root directory.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the repository root cannot be located from the assembly directory.
    /// </exception>
    private static string FindRepositoryRoot()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var current = Path.GetDirectoryName(assemblyLocation);

        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "*.sln").Length > 0)
            {
                return current;
            }

            if (Directory.Exists(Path.Combine(current, "examples")))
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

        throw new InvalidOperationException(
            $"Could not find repository root from assembly location: {assemblyLocation}. " +
            "Expected a directory containing a .sln file or an 'examples' subdirectory.");
    }

    /// <summary>
    /// Converts a JSON string into an <see cref="ObjectValue"/> by parsing it with
    /// <see cref="JsonDocument"/> and recursively converting each <see cref="JsonElement"/>
    /// to the corresponding <see cref="TemplateValue"/> subtype.
    /// </summary>
    /// <param name="json">The JSON string to convert. Must represent a JSON object at the root level.</param>
    /// <returns>An <see cref="ObjectValue"/> containing the converted data.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the root JSON element is not an object.
    /// </exception>
    private static ObjectValue ConvertJsonToObjectValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Expected JSON root to be an object, but got {root.ValueKind}.");
        }

        return ConvertJsonObject(root);
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> of kind <see cref="JsonValueKind.Object"/>
    /// to an <see cref="ObjectValue"/>.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <returns>The converted <see cref="ObjectValue"/>.</returns>
    private static ObjectValue ConvertJsonObject(JsonElement element)
    {
        var obj = new ObjectValue();

        foreach (var property in element.EnumerateObject())
        {
            obj[property.Name] = ConvertJsonElement(property.Value);
        }

        return obj;
    }

    /// <summary>
    /// Recursively converts a <see cref="JsonElement"/> to the appropriate
    /// <see cref="TemplateValue"/> subtype based on its <see cref="JsonElement.ValueKind"/>.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <returns>
    /// A <see cref="StringValue"/> for strings, <see cref="NumberValue"/> for numbers,
    /// <see cref="BoolValue"/> for booleans, <see cref="ArrayValue"/> for arrays,
    /// <see cref="ObjectValue"/> for objects, or <see cref="StringValue"/> with an empty
    /// string for null values.
    /// </returns>
    private static TemplateValue ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => new StringValue(element.GetString()!),
            JsonValueKind.Number => new NumberValue(element.GetDecimal()),
            JsonValueKind.True => new BoolValue(true),
            JsonValueKind.False => new BoolValue(false),
            JsonValueKind.Array => ConvertJsonArray(element),
            JsonValueKind.Object => ConvertJsonObject(element),
            JsonValueKind.Null => new StringValue(string.Empty),
            _ => new StringValue(element.GetRawText())
        };
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> of kind <see cref="JsonValueKind.Array"/>
    /// to an <see cref="ArrayValue"/>.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <returns>The converted <see cref="ArrayValue"/>.</returns>
    private static ArrayValue ConvertJsonArray(JsonElement element)
    {
        var items = new List<TemplateValue>();

        foreach (var item in element.EnumerateArray())
        {
            items.Add(ConvertJsonElement(item));
        }

        return new ArrayValue(items);
    }
}
