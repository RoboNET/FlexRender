using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Content.Ndc;
using FlexRender.Layout;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Barcode;
using FlexRender.QrCode;
using FlexRender.Rendering;
using FlexRender.Skia;
using FlexRender.Yaml;
using SkiaSharp;

namespace FlexRender.Playground;

/// <summary>
/// C# API surface exported to JavaScript via JSExport for the WASM playground.
/// </summary>
internal static partial class PlaygroundApi
{
    private static IFlexRender? _render;
    private static MemoryResourceLoader? _memoryLoader;
    private static TemplateParser? _parser;
    private static string? _lastError;

    /// <summary>
    /// Returns the last error message from any API call, or null if no error occurred.
    /// </summary>
    [JSExport]
    public static string? GetLastError() => _lastError;

    /// <summary>
    /// Creates the FlexRender pipeline with an in-memory resource loader, Skia backend, and NDC support.
    /// Must be called once before any other method.
    /// </summary>
    [JSExport]
    public static void Initialize()
    {
        _memoryLoader = new MemoryResourceLoader();
        _parser = new TemplateParser();

        var builder = new FlexRenderBuilder()
            .WithNdc()
            .WithSkia(skia => skia.WithQr().WithBarcode());

        // Insert memory loader at highest priority so uploaded files win
        builder.ResourceLoaders.Insert(0, _memoryLoader);

        _render = builder.Build();

        // Enable layout diagnostics for the playground inspector
        if (_render is SkiaRender skiaRender)
            skiaRender.EnableDiagnostics = true;
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
                return [];

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
            _lastError = ex.Message;
            return [];
        }
    }

    /// <summary>
    /// Renders a YAML template to PNG bytes with a debug overlay showing layout boundaries,
    /// element type colors, and per-glyph text boundaries.
    /// </summary>
    /// <param name="yaml">YAML template string.</param>
    /// <param name="dataJson">Optional JSON object with template data, or null.</param>
    /// <returns>PNG image bytes with debug overlay, or an empty array on error.</returns>
    [JSExport]
    public static byte[] RenderDebugPng(string yaml, string? dataJson)
    {
        try
        {
            if (_render is not SkiaRender skiaRender)
                return [];

            _parser ??= new TemplateParser();
            var template = _parser.Parse(yaml);

            ObjectValue data = new();
            if (!string.IsNullOrWhiteSpace(dataJson))
            {
                data = ParseJsonData(dataJson);
            }

            // Render normal PNG
            var pngBytes = skiaRender.RenderToPng(template, data)
                .GetAwaiter()
                .GetResult();

            // Compute layout for debug overlay
            var root = skiaRender.ComputeLayout(template, data)
                .GetAwaiter()
                .GetResult();

            var size = skiaRender.Measure(template, data)
                .GetAwaiter()
                .GetResult();

            var bitmapWidth = Math.Max(1, (int)Math.Ceiling(size.Width));
            var bitmapHeight = Math.Max(1, (int)Math.Ceiling(size.Height));

            using var bitmap = new SKBitmap(bitmapWidth, bitmapHeight);
            using var canvas = new SKCanvas(bitmap);

            // Draw the rendered PNG onto the canvas
            using var rendered = SKBitmap.Decode(pngBytes);
            if (rendered is not null)
            {
                canvas.DrawBitmap(rendered, 0, 0);
            }

            // Draw debug overlay
            DrawDebugOverlay(canvas, root, 0, 0, skiaRender.FontManager);

            // Encode to PNG
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded is null)
                return [];

            using var ms = new MemoryStream();
            encoded.SaveTo(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
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
            _lastError = ex.Message;
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
            _lastError = ex.Message;
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
            _lastError = ex.Message;
        }
    }

    /// <summary>
    /// Stores a resource (font, image, content, or any file) in the in-memory VFS.
    /// </summary>
    /// <param name="path">Resource path as referenced in the template.</param>
    /// <param name="data">Raw resource bytes.</param>
    [JSExport]
    public static void LoadResource(string path, byte[] data)
    {
        try
        {
            _memoryLoader?.AddResource(path, data);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
        }
    }

    /// <summary>
    /// Removes a resource from the in-memory VFS.
    /// </summary>
    /// <param name="path">Resource path to remove.</param>
    [JSExport]
    public static void RemoveResource(string path)
    {
        try
        {
            _memoryLoader?.RemoveResource(path);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
        }
    }

    /// <summary>
    /// Lists all resource paths currently stored in the in-memory VFS.
    /// </summary>
    /// <returns>JSON array of resource path strings.</returns>
    [JSExport]
    public static string ListResources()
    {
        try
        {
            var paths = _memoryLoader?.ListResources() ?? [];
            return JsonSerializer.Serialize(paths);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return "[]";
        }
    }

    /// <summary>
    /// Computes the layout tree for a YAML template and returns it as a JSON string.
    /// </summary>
    /// <param name="yaml">YAML template string.</param>
    /// <param name="dataJson">Optional JSON object with template data, or null.</param>
    /// <returns>JSON string representing the layout tree, or an empty object on error.</returns>
    [JSExport]
    public static string GetLayout(string yaml, string? dataJson)
    {
        try
        {
            if (_render is not SkiaRender skiaRender)
                return "{}";

            _parser ??= new TemplateParser();
            var template = _parser.Parse(yaml);

            ObjectValue data = new();
            if (!string.IsNullOrWhiteSpace(dataJson))
            {
                data = ParseJsonData(dataJson);
            }

            var layoutNode = skiaRender.ComputeLayout(template, data)
                .GetAwaiter()
                .GetResult();

            var json = SerializeLayoutNode(layoutNode);
            return json.ToJsonString();
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return "{}";
        }
    }

    private static JsonObject SerializeLayoutNode(LayoutNode node)
    {
        var obj = new JsonObject
        {
            ["type"] = node.Element.Type.ToString().ToLowerInvariant(),
            ["x"] = Math.Round(node.X, 1),
            ["y"] = Math.Round(node.Y, 1),
            ["w"] = Math.Round(node.Width, 1),
            ["h"] = Math.Round(node.Height, 1)
        };

        // Computed properties
        if (node.ComputedFontSize > 0)
        {
            obj["fontSize"] = Math.Round(node.ComputedFontSize, 1);
            obj["fontSizeExact"] = node.ComputedFontSize;
        }

        if (node.Baseline > 0)
            obj["baseline"] = Math.Round(node.Baseline, 1);

        if (node.ComputedLineHeight > 0)
            obj["lineHeight"] = Math.Round(node.ComputedLineHeight, 1);

        if (node.TextLines is not null)
            obj["textLines"] = node.TextLines.Count;

        if (node.Direction != TextDirection.Ltr)
            obj["direction"] = node.Direction.ToString().ToLowerInvariant();

        // Diagnostic fields for text debugging
        if (node.Diagnostics is { } diag)
        {
            if (diag.ContentWidth > 0)
                obj["contentW"] = Math.Round(diag.ContentWidth, 2);
            if (diag.IntrinsicWidth > 0)
                obj["intrinsicW"] = Math.Round(diag.IntrinsicWidth, 2);
            if (diag.ShapedWidth > 0)
                obj["shapedW"] = Math.Round(diag.ShapedWidth, 2);
            if (!string.IsNullOrEmpty(diag.ResolvedTypeface))
                obj["resolvedTypeface"] = diag.ResolvedTypeface;
        }

        // Element-specific properties
        SerializeElementProperties(obj, node.Element);

        var children = new JsonArray();
        foreach (var child in node.Children)
        {
            children.Add(SerializeLayoutNode(child));
        }

        obj["children"] = children;
        return obj;
    }

    /// <summary>
    /// Adds element-specific properties to the JSON object based on the element type.
    /// </summary>
    /// <param name="obj">The JSON object to add properties to.</param>
    /// <param name="element">The template element.</param>
    private static void SerializeElementProperties(JsonObject obj, TemplateElement element)
    {
        switch (element)
        {
            case FlexElement f:
                obj["direction"] = f.Direction.ToString().ToLowerInvariant();
                if (!string.IsNullOrEmpty(f.FontSize.Value))
                    obj["fontSizeSpec"] = f.FontSize.Value;
                if (f.Align.Value != AlignItems.Stretch)
                    obj["align"] = f.Align.Value.ToString().ToLowerInvariant();
                if (f.Justify.Value != JustifyContent.Start)
                    obj["justify"] = f.Justify.Value.ToString().ToLowerInvariant();
                break;

            case TextElement t:
                obj["content"] = Truncate(t.Content.Value, 50);
                if (!string.IsNullOrEmpty(t.Size.Value))
                    obj["size"] = t.Size.Value;
                if (!string.IsNullOrEmpty(t.Font.Value))
                    obj["font"] = t.Font.Value;
                if (!string.IsNullOrEmpty(t.FontFamily.Value))
                    obj["fontFamily"] = t.FontFamily.Value;
                if (t.FontWeight.Value != Parsing.Ast.FontWeight.Normal)
                    obj["fontWeight"] = t.FontWeight.Value.ToString().ToLowerInvariant();
                if (t.FontStyle.Value != Parsing.Ast.FontStyle.Normal)
                    obj["fontStyle"] = t.FontStyle.Value.ToString().ToLowerInvariant();
                if (!string.IsNullOrEmpty(t.Color.Value))
                    obj["color"] = t.Color.Value;

                // Safely resolve typeface name for diagnostics.
                // Only access native SKTypeface properties when the base font was loaded from
                // a real file/resource — system fallback typefaces crash in WASM (no system fonts).
                if (_render is SkiaRender skiaRender)
                {
                    var baseFontName = !string.IsNullOrEmpty(t.Font.Value) ? t.Font.Value : "main";
                    if (skiaRender.FontManager.IsFileLoaded(baseFontName))
                    {
                        var typeface = skiaRender.FontManager.GetTypeface(
                            t.Font.Value, t.FontFamily.Value, t.FontWeight.Value, t.FontStyle.Value);
                        obj["resolvedTypeface"] = typeface.FamilyName;
                    }
                }

                break;

            case ImageElement:
                obj["indicator"] = "image";
                break;

            case QrElement:
                obj["indicator"] = "qr";
                break;

            case BarcodeElement:
                obj["indicator"] = "barcode";
                break;

            case SeparatorElement:
                obj["indicator"] = "separator";
                break;

            case SvgElement:
                obj["indicator"] = "svg";
                break;

            case TableElement:
                obj["indicator"] = "table";
                break;

            case EachElement:
                obj["indicator"] = "each";
                break;

            case IfElement:
                obj["indicator"] = "if";
                break;

            case ContentElement:
                obj["indicator"] = "content";
                break;
        }
    }

    /// <summary>
    /// Draws debug overlay rectangles on the canvas showing element boundaries
    /// with colors coded by element type.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="node">The current layout node.</param>
    /// <param name="offsetX">The X offset from parent.</param>
    /// <param name="offsetY">The Y offset from parent.</param>
    /// <param name="fontManager">The font manager for creating fonts to measure glyph widths.</param>
    private static void DrawDebugOverlay(SKCanvas canvas, LayoutNode node, float offsetX, float offsetY, FontManager fontManager)
    {
        var x = node.X + offsetX;
        var y = node.Y + offsetY;

        // Choose color based on element type
        var color = node.Element switch
        {
            FlexElement => SKColors.Blue,
            TextElement => SKColors.Green,
            QrElement => SKColors.Purple,
            BarcodeElement => SKColors.Orange,
            ImageElement => SKColors.Cyan,
            _ => SKColors.Gray
        };

        // Draw fill for flex containers
        if (node.Element is FlexElement)
        {
            using var fillPaint = new SKPaint
            {
                Color = color.WithAlpha(30),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(x, y, node.Width, node.Height, fillPaint);
        }

        // Draw stroke
        using var strokePaint = new SKPaint
        {
            Color = color.WithAlpha(180),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        canvas.DrawRect(x, y, node.Width, node.Height, strokePaint);

        // Draw per-glyph boundaries for text elements
        if (node.Element is TextElement text && !string.IsNullOrEmpty(text.Content.Value))
        {
            DrawGlyphBoundaries(canvas, text, x, y, node, fontManager);
        }

        // Recursively draw children
        foreach (var child in node.Children)
        {
            DrawDebugOverlay(canvas, child, x, y, fontManager);
        }
    }

    /// <summary>
    /// Draws per-glyph boundary rectangles for a text element.
    /// Spaces are highlighted with a distinct color to make whitespace visible.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="text">The text element.</param>
    /// <param name="x">The absolute X position of the text element.</param>
    /// <param name="y">The absolute Y position of the text element.</param>
    /// <param name="node">The layout node for the text element.</param>
    /// <param name="fontManager">The font manager for creating fonts.</param>
    private static void DrawGlyphBoundaries(
        SKCanvas canvas,
        TextElement text,
        float x,
        float y,
        LayoutNode node,
        FontManager fontManager)
    {
        // Skip glyph boundaries if font is not file-loaded (WASM: system fallback has invalid native handle)
        var baseFontName = !string.IsNullOrEmpty(text.Font.Value) ? text.Font.Value : "main";
        if (!fontManager.IsFileLoaded(baseFontName))
            return;

        var fontSize = node.ComputedFontSize > 0 ? node.ComputedFontSize : 16f;
        var typeface = fontManager.GetTypeface(text.Font.Value, text.FontFamily.Value, text.FontWeight.Value, text.FontStyle.Value);
        using var font = new SKFont(typeface, FontSizeResolver.Resolve(text.Size.Value, fontSize));

        var content = text.Content.Value;

        // Compute per-character advance widths using cumulative measurement.
        // GetGlyphWidths returns ink bounds (visual shape width), not advance width,
        // so spaces would appear zero-width. Cumulative MeasureText gives correct advances.
        var advances = new float[content.Length];
        float prevWidth = 0;
        for (var i = 0; i < content.Length; i++)
        {
            var cumWidth = font.MeasureText(content.AsSpan(0, i + 1));
            advances[i] = cumWidth - prevWidth;
            prevWidth = cumWidth;
        }

        using var glyphStroke = new SKPaint
        {
            Color = SKColors.Red.WithAlpha(100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f
        };
        using var spaceFill = new SKPaint
        {
            Color = SKColors.Yellow.WithAlpha(60),
            Style = SKPaintStyle.Fill
        };

        var glyphX = x;
        for (var i = 0; i < advances.Length; i++)
        {
            var w = advances[i];

            // Highlight spaces with yellow fill
            if (content[i] == ' ')
            {
                canvas.DrawRect(glyphX, y, w, node.Height, spaceFill);
            }

            canvas.DrawRect(glyphX, y, w, node.Height, glyphStroke);
            glyphX += w;
        }
    }

    /// <summary>
    /// Returns a JSON diagnostic report of font loading status, including registered font paths,
    /// resolved typeface family names, and all resources in the in-memory VFS.
    /// </summary>
    /// <returns>JSON string with font diagnostics, or an error JSON on failure.</returns>
    [JSExport]
    public static string GetFontDiagnostics()
    {
        try
        {
            if (_render is not SkiaRender skiaRender)
            {
                return JsonSerializer.Serialize(new { error = "Not initialized or not using SkiaRender" });
            }

            var fontManager = skiaRender.FontManager;
            var registeredPaths = fontManager.RegisteredFontPaths;

            var fonts = new JsonArray();
            foreach (var (name, path) in registeredPaths)
            {
                var info = fontManager.GetTypefaceInfo(name);
                var fontObj = new JsonObject
                {
                    ["name"] = name,
                    ["path"] = path,
                    ["fileLoaded"] = fontManager.IsFileLoaded(name)
                };

                if (info is var (familyName, isFixedPitch))
                {
                    fontObj["familyName"] = familyName;
                    fontObj["isFixedPitch"] = isFixedPitch;
                }
                else
                {
                    fontObj["familyName"] = "(system fallback - not inspectable in WASM)";
                }

                fonts.Add(fontObj);
            }

            var resources = _memoryLoader?.ListResources() ?? [];

            var result = new JsonObject
            {
                ["registeredFontCount"] = registeredPaths.Count,
                ["fonts"] = fonts,
                ["memoryResourceCount"] = resources.Count,
                ["memoryResources"] = JsonSerializer.SerializeToNode(resources)
            };

            return result.ToJsonString();
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
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

    /// <summary>
    /// Truncates a string to the specified maximum length, appending ellipsis if needed.
    /// </summary>
    /// <param name="s">The string to truncate.</param>
    /// <param name="max">The maximum length.</param>
    /// <returns>The truncated string with ellipsis if it exceeded the limit.</returns>
    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 3)] + "...";
}
