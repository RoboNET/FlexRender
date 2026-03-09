using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Content.Ndc;
using FlexRender.Parsing;
using FlexRender.Yaml;

namespace FlexRender.Playground;

/// <summary>
/// C# API surface exported to JavaScript via JSExport for the WASM playground.
/// </summary>
internal static partial class PlaygroundApi
{
    private static IFlexRender? _render;
    private static MemoryResourceLoader? _memoryLoader;
    private static TemplateParser? _parser;

    /// <summary>
    /// Creates the FlexRender pipeline with an in-memory resource loader, Skia backend, and NDC support.
    /// Must be called once before any other method.
    /// </summary>
    [JSExport]
    public static void Initialize()
    {
        try
        {
            _memoryLoader = new MemoryResourceLoader();
            _parser = new TemplateParser();

            // Load embedded default font (WASM has no system fonts)
            LoadEmbeddedFont("Inter-Regular.ttf");

            var builder = new FlexRenderBuilder()
                .WithNdc()
                .WithSkia();

            // Insert memory loader at highest priority so uploaded files win
            builder.ResourceLoaders.Insert(0, _memoryLoader);

            _render = builder.Build();
            Console.WriteLine("FlexRender engine initialized successfully");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FlexRender initialization failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Renders a YAML template to PNG bytes.
    /// </summary>
    /// <param name="yaml">YAML template string.</param>
    /// <param name="dataJson">Optional JSON object with template data, or null.</param>
    /// <returns>PNG image bytes, or an empty array on error.</returns>
    [JSExport]
    public static byte[] RenderToPng(string yaml, string? dataJson)
    {
        try
        {
            if (_render is null)
            {
                Console.Error.WriteLine("PlaygroundApi not initialized. Call Initialize() first.");
                return [];
            }

            ObjectValue? data = null;
            if (!string.IsNullOrWhiteSpace(dataJson))
            {
                data = ParseJsonData(dataJson);
            }

            var result = _render.RenderYaml(yaml, data, ImageFormat.Png, _parser)
                .GetAwaiter()
                .GetResult();
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"RenderToPng error: {ex}");
            return [];
        }
    }

    /// <summary>
    /// Validates a YAML template and returns a JSON array of errors.
    /// </summary>
    /// <param name="yaml">YAML template string.</param>
    /// <returns>JSON string: <c>[]</c> on success, or <c>[{"message":"...","line":0}]</c> on error.</returns>
    [JSExport]
    public static string Validate(string yaml)
    {
        try
        {
            _parser ??= new TemplateParser();
            _parser.Parse(yaml);
            return "[]";
        }
        catch (TemplateParseException ex)
        {
            var errors = new[]
            {
                new { message = ex.Message, line = ex.Line ?? 0 }
            };
            return JsonSerializer.Serialize(errors);
        }
        catch (Exception ex)
        {
            var errors = new[]
            {
                new { message = ex.Message, line = 0 }
            };
            return JsonSerializer.Serialize(errors);
        }
    }

    /// <summary>
    /// Stores a font in the in-memory resource loader.
    /// </summary>
    /// <param name="name">Font file name (e.g. "Roboto-Regular.ttf").</param>
    /// <param name="data">Raw font bytes.</param>
    [JSExport]
    public static void LoadFont(string name, byte[] data)
    {
        try
        {
            _memoryLoader?.AddResource(name, data);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"LoadFont error: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores an image in the in-memory resource loader.
    /// </summary>
    /// <param name="path">Image path as referenced in the template (e.g. "logo.png").</param>
    /// <param name="data">Raw image bytes.</param>
    [JSExport]
    public static void LoadImage(string path, byte[] data)
    {
        try
        {
            _memoryLoader?.AddResource(path, data);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"LoadImage error: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores NDC or other content data in the in-memory resource loader.
    /// </summary>
    /// <param name="path">Content path as referenced in the template.</param>
    /// <param name="data">Raw content bytes.</param>
    [JSExport]
    public static void LoadContent(string path, byte[] data)
    {
        try
        {
            _memoryLoader?.AddResource(path, data);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"LoadContent error: {ex.Message}");
        }
    }

    private static void LoadEmbeddedFont(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            Console.Error.WriteLine($"Embedded font not found: {resourceName}");
            return;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        _memoryLoader!.AddResource(resourceName, ms.ToArray());
        Console.WriteLine($"Loaded embedded font: {resourceName}");
    }

    /// <summary>
    /// Parses a JSON string into an <see cref="ObjectValue"/> for template data binding.
    /// Mirrors the logic from <c>FlexRender.Cli.DataLoader</c>.
    /// </summary>
    private static ObjectValue ParseJsonData(string json)
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
