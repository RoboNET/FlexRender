using System.Text.Json;

namespace FlexRender.Cli;

/// <summary>
/// Loads JSON data files into TemplateValue objects.
/// </summary>
public static class DataLoader
{
    /// <summary>
    /// Maximum allowed file size for JSON data files (10 MB).
    /// </summary>
    public const long MaxFileSize = 10 * 1024 * 1024;

    /// <summary>
    /// Loads a JSON file and converts it to an ObjectValue.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <param name="maxFileSize">Maximum allowed file size in bytes. Defaults to <see cref="MaxFileSize"/> (10 MB).</param>
    /// <returns>ObjectValue representing the JSON data.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the file size exceeds the maximum allowed size.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static ObjectValue LoadFromFile(string path, long maxFileSize = MaxFileSize)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Data file not found: {path}", path);
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > maxFileSize)
        {
            throw new InvalidOperationException(
                $"Data file size ({fileInfo.Length} bytes) exceeds maximum allowed size ({maxFileSize} bytes)");
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Parses a JSON string and converts it to an ObjectValue.
    /// </summary>
    /// <param name="json">JSON string to parse.</param>
    /// <returns>ObjectValue representing the JSON data.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static ObjectValue LoadFromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Root element must be a JSON object");
        }

        return (ObjectValue)ConvertElement(root);
    }

    private static TemplateValue ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => new StringValue(element.GetString()!),
            JsonValueKind.Number => new NumberValue(element.GetDecimal()),
            JsonValueKind.True => new BoolValue(true),
            JsonValueKind.False => new BoolValue(false),
            JsonValueKind.Null => NullValue.Instance,
            JsonValueKind.Array => ConvertArray(element),
            JsonValueKind.Object => ConvertObject(element),
            _ => NullValue.Instance
        };
    }

    private static ArrayValue ConvertArray(JsonElement element)
    {
        var items = new List<TemplateValue>();
        foreach (var item in element.EnumerateArray())
        {
            items.Add(ConvertElement(item));
        }
        return new ArrayValue(items);
    }

    private static ObjectValue ConvertObject(JsonElement element)
    {
        var obj = new ObjectValue();
        foreach (var property in element.EnumerateObject())
        {
            obj[property.Name] = ConvertElement(property.Value);
        }
        return obj;
    }
}
