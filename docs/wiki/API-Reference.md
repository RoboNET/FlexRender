# API Reference

Complete API documentation for FlexRender. For render options and format-specific settings, see [[Render-Options]].

## IFlexRender

The core interface for rendering templates to images. Defined in `FlexRender.Core`.

```csharp
public interface IFlexRender : IDisposable
{
    // --- Generic methods (backward compatible) ---
    Task<byte[]> Render(Template layoutTemplate, ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png, CancellationToken cancellationToken = default);
    Task Render(Stream output, Template layoutTemplate, ObjectValue? data = null,
        ImageFormat format = ImageFormat.Png, CancellationToken cancellationToken = default);

    // --- PNG ---
    Task<byte[]> RenderToPng(Template layoutTemplate, ObjectValue? data = null,
        PngOptions? options = null, RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);
    Task RenderToPng(Stream output, Template layoutTemplate, ObjectValue? data = null,
        PngOptions? options = null, RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    // --- JPEG ---
    Task<byte[]> RenderToJpeg(Template layoutTemplate, ObjectValue? data = null,
        JpegOptions? options = null, RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);
    Task RenderToJpeg(Stream output, Template layoutTemplate, ObjectValue? data = null,
        JpegOptions? options = null, RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    // --- BMP ---
    Task<byte[]> RenderToBmp(Template layoutTemplate, ObjectValue? data = null,
        BmpOptions? options = null, RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);
    Task RenderToBmp(Stream output, Template layoutTemplate, ObjectValue? data = null,
        BmpOptions? options = null, RenderOptions? renderOptions = null,
        CancellationToken cancellationToken = default);

    // --- Raw ---
    Task<byte[]> RenderToRaw(Template layoutTemplate, ObjectValue? data = null,
        RenderOptions? renderOptions = null, CancellationToken cancellationToken = default);
    Task RenderToRaw(Stream output, Template layoutTemplate, ObjectValue? data = null,
        RenderOptions? renderOptions = null, CancellationToken cancellationToken = default);
}
```

### Parameter Convention

All format-specific methods follow the same parameter order:

```
(Template, data?, formatOptions?, renderOptions?, cancellationToken)
```

For stream overloads, `Stream output` comes first (before Template).

Pass `null` for any options parameter to use defaults.

---

## FlexRenderBuilder

Builder for configuring and creating `IFlexRender` instances. Defined in `FlexRender.Core`.

### Methods

| Method | Description |
|--------|-------------|
| `WithSkia(Action<SkiaBuilder>?)` | Configure Skia renderer |
| `WithImageSharp(Action<ImageSharpBuilder>?)` | Configure ImageSharp renderer (pure .NET, no native deps) |
| `WithSvg(Action<SvgBuilder>?)` | Configure SVG output renderer |
| `WithBasePath(string)` | Base path for resolving relative file paths |
| `WithLimits(Action<ResourceLimits>)` | Configure resource limits |
| `WithEmbeddedLoader(Assembly)` | Add embedded resource loader for `embedded://` URIs |
| `WithFilter(string, ITemplateFilter)` | Register a custom template filter for inline expressions |
| `WithoutDefaultLoaders()` | Remove default File and Base64 loaders (sandboxed mode) |
| `WithoutDefaultFilters()` | Remove all 8 built-in filters, leaving only custom-registered filters |
| `Build()` | Create the configured `IFlexRender` instance |

### Usage

```csharp
// Minimal
var render = new FlexRenderBuilder()
    .WithSkia()
    .Build();

// Full configuration
var render = new FlexRenderBuilder()
    .WithHttpLoader(configure: opts => {
        opts.Timeout = TimeSpan.FromSeconds(60);
        opts.MaxResourceSize = 20 * 1024 * 1024;
    })
    .WithEmbeddedLoader(typeof(Program).Assembly)
    .WithBasePath("./templates")
    .WithLimits(limits => limits.MaxRenderDepth = 200)
    .WithSkia(skia => skia
        .WithQr()
        .WithBarcode())
    .Build();

// Sandboxed (no file system access)
var render = new FlexRenderBuilder()
    .WithoutDefaultLoaders()
    .WithEmbeddedLoader(typeof(Program).Assembly)
    .WithSkia()
    .Build();
```

The builder can only be built once. Creating a second instance requires a new `FlexRenderBuilder`.

---

## SkiaBuilder

Configures Skia-specific rendering options and content providers.

| Method | Description | Package |
|--------|-------------|---------|
| `WithQr()` | Enable QR code support | FlexRender.QrCode.Skia.Render or FlexRender.QrCode |
| `WithBarcode()` | Enable barcode support | FlexRender.Barcode.Skia.Render or FlexRender.Barcode |
| `WithSvgElement()` | Enable inline SVG element support | FlexRender.SvgElement.Skia.Render or FlexRender.SvgElement |
| `WithHarfBuzz()` | Enable HarfBuzz text shaping for Arabic/Hebrew | FlexRender.HarfBuzz |

```csharp
builder.WithSkia(skia => skia
    .WithQr()
    .WithBarcode()
    .WithSvgElement());
```

If a QR code or barcode element is encountered in a template and the corresponding provider is not configured, an `InvalidOperationException` is thrown at render time.

---

## ImageSharpBuilder

Configures ImageSharp-specific rendering options and content providers. Pure .NET, no native dependencies.

| Method | Description | Package |
|--------|-------------|---------|
| `WithQr()` | Enable QR code support | FlexRender.QrCode.ImageSharp.Render or FlexRender.QrCode |
| `WithBarcode()` | Enable barcode support | FlexRender.Barcode.ImageSharp.Render or FlexRender.Barcode |

```csharp
builder.WithImageSharp(imageSharp => imageSharp
    .WithQr()
    .WithBarcode());
```

> **Note:** ImageSharp does not support SVG elements or HarfBuzz text shaping.

---

## SvgElement (in templates)

The `FlexRender.SvgElement.Skia.Render` or `FlexRender.SvgElement` package adds `type: svg` support to templates for Skia rendering.

### Registration

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia.WithSvgElement().WithQr().WithBarcode())
    .Build();
```

---

## SVG Output Renderer

The `FlexRender.Svg.Render` package provides the SVG renderer. The `FlexRender.Svg` meta-package includes the renderer plus SVG-specific providers.

### Registration

```csharp
// SVG output only
var render = new FlexRenderBuilder()
    .WithSvg()
    .Build();

// SVG + raster output
var render = new FlexRenderBuilder()
    .WithSvg(svg => svg
        .WithSkia(skia => skia.WithQr().WithBarcode())
        .WithSvgElementSvg()
        .WithQrSvg()
        .WithBarcodeSvg())
    .Build();
```

### Usage

```csharp
// Render to SVG string
string svg = await render.RenderSvgAsync("template.yaml", data);

// Render SVG to file
await render.RenderSvgToFileAsync("template.yaml", data, "output.svg");
```

### SvgBuilder Methods

| Method | Description |
|--------|-------------|
| `WithSkia(Action<SkiaBuilder>?)` | Use Skia as raster fallback for SVG output |
| `WithRasterBackend(Func<FlexRenderBuilder, IFlexRender>)` | Use any `IFlexRender` implementation as raster fallback |
| `WithQrSvg()` | Enable SVG-native QR codes (FlexRender.QrCode.Svg.Render) |
| `WithBarcodeSvg()` | Enable SVG-native barcodes (FlexRender.Barcode.Svg.Render) |
| `WithSvgElementSvg()` | Enable SVG-native SVG elements (FlexRender.SvgElement.Svg.Render) |

If both `WithSkia` and `WithRasterBackend` are called, Skia takes precedence.

```csharp
// SVG output with ImageSharp as raster backend (no native dependencies)
var render = new FlexRenderBuilder()
    .WithSvg(svg => svg.WithRasterBackend(
        ImageSharpFlexRenderBuilderExtensions.CreateRendererFactory()))
    .Build();
```

---

## ImageSharp Backend

The `FlexRender.ImageSharp.Render` package provides a pure .NET rendering backend with zero native dependencies. The `FlexRender.ImageSharp` meta-package includes the renderer plus QR/barcode providers. Uses SixLabors.ImageSharp, SixLabors.ImageSharp.Drawing, and SixLabors.Fonts.

### Registration

```csharp
var render = new FlexRenderBuilder()
    .WithImageSharp(imageSharp => imageSharp.WithQr().WithBarcode())
    .Build();

// Or with configuration callback (reserved for future options)
var render = new FlexRenderBuilder()
    .WithImageSharp(imageSharp => { /* config */ })
    .Build();
```

### Limitations

- QR/barcode support requires `FlexRender.QrCode.ImageSharp.Render` and `FlexRender.Barcode.ImageSharp.Render` (or meta packages)
- Does **not** support SVG elements
- **Not** AOT-compatible (SixLabors.ImageSharp uses reflection internally)
- Ignores Skia-specific render options (`FontHinting`, `SubpixelText`, `TextRendering`)

### When to Use

- Environments where native SkiaSharp dependencies are problematic (e.g., certain cloud functions, ARM platforms)
- Projects that need pure managed .NET without P/Invoke
- Templates that only use text, images, flex layout, separators, and tables

---

## Dependency Injection

Extension methods for `IServiceCollection`. Defined in `FlexRender.DependencyInjection`.

### AddFlexRender (simple)

```csharp
services.AddFlexRender(builder => builder
    .WithBasePath("./templates")
    .WithSkia(skia => skia.WithQr().WithBarcode()));
```

### AddFlexRender (with service provider)

```csharp
services.AddFlexRender((sp, builder) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    builder
        .WithBasePath(config["FlexRender:BasePath"] ?? "./templates")
        .WithLimits(limits => limits.MaxRenderDepth = 200)
        .WithSkia(skia => skia.WithQr().WithBarcode());
});
```

Both overloads register `IFlexRender` as a **singleton**. The instance is created lazily when first resolved.

### Usage in services

```csharp
public class ReceiptService(IFlexRender render)
{
    public async Task<byte[]> Generate(ReceiptData data)
    {
        var values = MapToObjectValue(data);
        return await render.RenderFile("receipt.yaml", values);
    }
}
```

---

## Extension Methods (FlexRender.Yaml)

Convenience methods that handle YAML parsing internally. Defined in `FlexRender.Yaml`.

### Existing methods

| Method | Description |
|--------|-------------|
| `RenderYaml(yaml, data?, format?, parser?, ct)` | Render from YAML string to `byte[]` |
| `RenderYaml(output, yaml, data?, format?, parser?, ct)` | Render from YAML string to `Stream` |
| `RenderFile(path, data?, format?, parser?, ct)` | Render from YAML file to `byte[]` |
| `RenderFile(output, path, data?, format?, parser?, ct)` | Render from YAML file to `Stream` |

### Format-specific file methods

| Method | Description |
|--------|-------------|
| `RenderFileToPng(path, data?, options?, renderOptions?, parser?, ct)` | File to PNG `byte[]` |
| `RenderFileToJpeg(path, data?, options?, renderOptions?, parser?, ct)` | File to JPEG `byte[]` |
| `RenderFileToBmp(path, data?, options?, renderOptions?, parser?, ct)` | File to BMP `byte[]` |
| `RenderFileToRaw(path, data?, renderOptions?, parser?, ct)` | File to Raw `byte[]` |

### Format-specific YAML string methods

| Method | Description |
|--------|-------------|
| `RenderYamlToPng(yaml, data?, options?, renderOptions?, parser?, ct)` | YAML string to PNG `byte[]` |
| `RenderYamlToJpeg(yaml, data?, options?, renderOptions?, parser?, ct)` | YAML string to JPEG `byte[]` |
| `RenderYamlToBmp(yaml, data?, options?, renderOptions?, parser?, ct)` | YAML string to BMP `byte[]` |
| `RenderYamlToRaw(yaml, data?, renderOptions?, parser?, ct)` | YAML string to Raw `byte[]` |

All methods accept an optional `TemplateParser` parameter for parser reuse across multiple calls.

---

## FlexRenderOptions

Engine-level configuration. Configured via the builder.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Limits` | ResourceLimits | `new()` | Resource limits (read-only, configure via `WithLimits`) |
| `BasePath` | string? | `null` | Base path for resolving relative file paths |
| `DefaultFontFamily` | string | `"Arial"` | Default font family when none specified |
| `BaseFontSize` | float | `12` | Base font size in points |
| `MaxImageSize` | int | 10 MB | Delegates to `Limits.MaxImageSize` |
| `EnableCaching` | bool | `true` | Resource caching toggle |
| `DeterministicRendering` | bool | `false` | **Deprecated.** Use `RenderOptions.Deterministic` instead |
| `EmbeddedResourceAssemblies` | List\<Assembly\> | `[]` | Assemblies for `embedded://` resource loading |

---

## ResourceLimits

Security limits for the rendering pipeline. All defaults are safe-by-default values.

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `MaxTemplateFileSize` | long | 1 MB | Maximum YAML template file size |
| `MaxDataFileSize` | long | 10 MB | Maximum JSON data file size |
| `MaxTemplateNestingDepth` | int | 100 | Maximum nesting depth for control flow |
| `MaxRenderDepth` | int | 100 | Maximum recursion depth for render tree |
| `MaxImageSize` | int | 10 MB | Maximum image loading size |
| `MaxFlexLines` | int | 1000 | Maximum flex lines when wrapping |

All properties validate that values are positive and throw `ArgumentOutOfRangeException` on invalid input.

```csharp
builder.WithLimits(limits =>
{
    limits.MaxRenderDepth = 200;
    limits.MaxTemplateFileSize = 2 * 1024 * 1024;
    limits.MaxFlexLines = 2000;
});
```

---

## ImageFormat

```csharp
public enum ImageFormat
{
    Png,   // Lossless, supports transparency
    Jpeg,  // Lossy compression
    Bmp,   // Uncompressed bitmap
    Raw    // Raw BGRA8888 pixel data (4 bytes/pixel)
}
```

Used with the legacy `Render()` methods. For format-specific options, use the dedicated `RenderToPng`/`RenderToJpeg`/`RenderToBmp`/`RenderToRaw` methods.

---

## RenderOptions, PngOptions, JpegOptions, BmpOptions

See [[Render-Options]] for detailed documentation of all per-call option types.

**Quick reference:**

| Type | Key Property | Default |
|------|-------------|---------|
| `RenderOptions` | `Antialiasing`, `SubpixelText`, `FontHinting`, `TextRendering`, `Culture` | All enabled, Normal hinting, SubpixelLcd, null |

### RenderOptions.Culture

Controls the `CultureInfo` used by built-in filters (`currency`, `currencySymbol`, `number`, `format`) for number and date formatting. When `null` (default), the culture resolution order is:

1. `RenderOptions.Culture` (highest priority -- per-call override)
2. `template.culture` from YAML (template-level default)
3. `CultureInfo.InvariantCulture` (fallback)

```csharp
// Per-call culture override
byte[] png = await render.RenderToPng(template, data,
    renderOptions: new RenderOptions { Culture = new CultureInfo("ru-RU") });
```
| `RenderOptions.Deterministic` | Preset for snapshot tests | SubpixelText=false, Hinting=None, Grayscale |
| `PngOptions` | `CompressionLevel` | 100 (validated: 0-100 on init) |
| `JpegOptions` | `Quality` | 90 (validated: 1-100 on init) |
| `BmpOptions` | `ColorMode` | Bgra32 |

---

## BmpColorMode

```csharp
public enum BmpColorMode
{
    Bgra32 = 0,       // 32-bit with alpha (default)
    Rgb24 = 1,        // 24-bit without alpha (25% smaller)
    Rgb565 = 2,       // 16-bit (50% smaller)
    Grayscale8 = 3,   // 8-bit grayscale (75% smaller)
    Grayscale4 = 4,   // 4-bit grayscale (87% smaller)
    Monochrome1 = 5   // 1-bit black/white (96% smaller)
}
```

---

## FontHinting

```csharp
public enum FontHinting
{
    None = 0,    // No hinting -- platform-consistent
    Slight = 1,  // Minimal grid-fitting
    Normal = 2,  // Default platform behavior
    Full = 3     // Maximum grid-fitting
}
```

---

## TextRendering

```csharp
public enum TextRendering
{
    Aliased = 0,      // No antialiasing
    Grayscale = 1,    // Grayscale AA -- platform-independent
    SubpixelLcd = 2   // Subpixel LCD AA -- sharpest, platform-dependent
}
```

---

## TemplateValue Hierarchy

AOT-compatible data types for template variables. All are sealed classes with implicit conversions.

```csharp
// String
TemplateValue str = "hello";                    // implicit from string
TemplateValue str = new StringValue("hello");

// Number
TemplateValue num = 42;                         // implicit from int
TemplateValue num = 3.14;                       // implicit from double

// Boolean
TemplateValue flag = true;                      // implicit from bool

// Null
TemplateValue nil = NullValue.Instance;

// Array
var array = new ArrayValue("a", "b", "c");
int count = array.Count;
TemplateValue first = array[0];

// Object
var obj = new ObjectValue
{
    ["name"] = "John",
    ["age"] = 30,
    ["active"] = true,
    ["tags"] = new ArrayValue("admin", "user"),
    ["address"] = new ObjectValue
    {
        ["city"] = "Moscow",
        ["zip"] = "123456"
    }
};
```

| Type | C# Class | Description |
|------|----------|-------------|
| String | `StringValue` | String value, implicit from `string` |
| Number | `NumberValue` | Numeric value, implicit from `int`, `double` |
| Boolean | `BoolValue` | Boolean value, implicit from `bool` |
| Null | `NullValue` | Null sentinel (`NullValue.Instance`) |
| Array | `ArrayValue` | Implements `IReadOnlyList<TemplateValue>` |
| Object | `ObjectValue` | Dictionary-like, `StringComparer.OrdinalIgnoreCase` |

---

## Template Caching Pattern

Parse templates once at startup, render many times per request:

```csharp
// At startup
var parser = new TemplateParser();
var templates = new Dictionary<string, Template>();
templates["receipt"] = await parser.ParseFileAsync("receipt.yaml");
templates["label"] = await parser.ParseFileAsync("label.yaml");

// Per request
var data = new ObjectValue { ["name"] = "Customer" };
byte[] receipt = await render.Render(templates["receipt"], data);

// With format-specific methods
byte[] label = await render.RenderToPng(templates["label"], data,
    renderOptions: RenderOptions.Deterministic);
```

Caching works because `type: each` and `type: if` elements are expanded at render time (by `TemplateExpander`), not at parse time. The same parsed template can be rendered with different data.

---

## Resource Loading

Resources (images, fonts) are loaded through `IResourceLoader` implementations using chain of responsibility:

| Loader | URI Scheme | Priority | Description |
|--------|------------|----------|-------------|
| `Base64ResourceLoader` | `data:` | High (0-99) | Base64-encoded data |
| `EmbeddedResourceLoader` | `embedded://` | High (0-99) | Assembly embedded resources |
| `FileResourceLoader` | File paths | Normal (100-199) | Local file system |
| `HttpResourceLoader` | `http://`, `https://` | Low (200+) | Remote resources |

File and Base64 loaders are included by default. Use `WithoutDefaultLoaders()` for sandboxed operation. HTTP loader requires `WithHttpLoader()`.

## See Also

- [[Render-Options]] -- detailed per-call options documentation
- [[Getting-Started]] -- setup and first template
- [[Contributing]] -- architecture and project structure
