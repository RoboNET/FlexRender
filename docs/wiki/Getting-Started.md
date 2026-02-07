# Getting Started

This guide walks you through installing FlexRender, creating your first template, and rendering it using code, dependency injection, or the CLI.

## Installation

### All-in-one (recommended)

The `FlexRender` meta-package includes all sub-packages:

```bash
dotnet add package FlexRender
```

> **Linux / Docker users:** SkiaSharp requires native libraries. Add the native assets package to avoid `DllNotFoundException: libSkiaSharp`:
> ```bash
> dotnet add package SkiaSharp.NativeAssets.Linux
> # For minimal containers without fontconfig/freetype:
> dotnet add package SkiaSharp.NativeAssets.Linux.NoDependencies
> # For HarfBuzz text shaping (Arabic, Hebrew):
> dotnet add package HarfBuzzSharp.NativeAssets.Linux
> ```

### Individual packages

Install only what you need:

| Package | Description | External Dependencies |
|---------|-------------|----------------------|
| `FlexRender.Core` | Layout engine, template processing | None |
| `FlexRender.Yaml` | YAML template parser | YamlDotNet |
| `FlexRender.Skia.Render` | SkiaSharp renderer | SkiaSharp |
| `FlexRender.Skia` | Skia backend meta-package (renderer + providers) | SkiaSharp |
| `FlexRender.ImageSharp.Render` | Pure .NET renderer (no native deps) | SixLabors.ImageSharp, SixLabors.Fonts, SixLabors.ImageSharp.Drawing |
| `FlexRender.ImageSharp` | ImageSharp backend meta-package (renderer + providers) | SixLabors.ImageSharp, SixLabors.Fonts, SixLabors.ImageSharp.Drawing |
| `FlexRender.Svg.Render` | SVG output renderer | None |
| `FlexRender.Svg` | SVG backend meta-package (renderer + providers) | None |
| `FlexRender.QrCode.Skia.Render` | QR codes for Skia | QRCoder |
| `FlexRender.QrCode.Svg.Render` | QR codes for SVG | QRCoder |
| `FlexRender.QrCode.ImageSharp.Render` | QR codes for ImageSharp | QRCoder |
| `FlexRender.QrCode` | QR meta-package (all renderers) | QRCoder |
| `FlexRender.Barcode.Skia.Render` | Barcodes for Skia | None |
| `FlexRender.Barcode.Svg.Render` | Barcodes for SVG | None |
| `FlexRender.Barcode.ImageSharp.Render` | Barcodes for ImageSharp | None |
| `FlexRender.Barcode` | Barcode meta-package (all renderers) | None |
| `FlexRender.SvgElement.Skia.Render` | SVG elements for Skia | Svg.Skia |
| `FlexRender.SvgElement.Svg.Render` | SVG elements for SVG output | None |
| `FlexRender.SvgElement` | SvgElement meta-package (all renderers) | Svg.Skia |
| `FlexRender.HarfBuzz` | HarfBuzz text shaping for Arabic/Hebrew | SkiaSharp.HarfBuzz |
| `FlexRender.Http` | HTTP/HTTPS resource loading | None |
| `FlexRender.DependencyInjection` | Microsoft DI integration | Microsoft.Extensions.DI |

```bash
# Example: core + YAML parser + Skia renderer
dotnet add package FlexRender.Core
dotnet add package FlexRender.Yaml
dotnet add package FlexRender.Skia.Render
```

### CLI tool

```bash
dotnet tool install -g flexrender-cli
```

See [[CLI-Reference]] for full CLI documentation.

## Your First Template

Create a file `hello.yaml`:

```yaml
template:
  name: "hello"
  version: 1
  # culture: "ru-RU"          # Optional: culture for number/date formatting

canvas:
  fixed: width
  width: 300
  background: "#ffffff"

layout:
  - type: flex
    padding: 20
    gap: 10
    children:
      - type: text
        content: "Hello, {{name}}!"
        size: 1.5em
        align: center
        color: "#1a1a1a"

      - type: text
        content: "Welcome to FlexRender"
        size: 0.9em
        align: center
        color: "#666666"
```

## Three Rendering Approaches

### 1. Code (FlexRenderBuilder)

```csharp
using FlexRender;
using FlexRender.Configuration;
using FlexRender.Yaml;

// Build renderer with Skia backend (full features)
var render = new FlexRenderBuilder()
    .WithBasePath("./templates")
    .WithSkia(skia => skia
        .WithQr()
        .WithBarcode())
    .Build();

// Prepare data
var data = new ObjectValue
{
    ["name"] = "World"
};

// Render to PNG bytes (extension method from FlexRender.Yaml)
byte[] png = await render.RenderFile("hello.yaml", data);
await File.WriteAllBytesAsync("hello.png", png);
```

**Alternative: ImageSharp backend (pure .NET, no native deps):**

```csharp
var render = new FlexRenderBuilder()
    .WithBasePath("./templates")
    .WithImageSharp(imageSharp => imageSharp.WithQr().WithBarcode())
    .Build();

byte[] png = await render.RenderFile("hello.yaml", data);
```

> **Note:** ImageSharp supports QR codes and barcodes via `FlexRender.QrCode.ImageSharp.Render` and `FlexRender.Barcode.ImageSharp.Render`. SVG elements are not supported in ImageSharp.

### 2. Dependency Injection

```csharp
// Program.cs -- register IFlexRender as singleton
services.AddFlexRender(builder => builder
    .WithBasePath("/app/templates")
    .WithSkia(skia => skia
        .WithQr()
        .WithBarcode()));

// In your service -- inject IFlexRender
public class GreetingService(IFlexRender render)
{
    public async Task<byte[]> GenerateGreeting(string name)
    {
        var data = new ObjectValue { ["name"] = name };
        return await render.RenderFile("hello.yaml", data);
    }
}
```

With service provider access for configuration:

```csharp
services.AddFlexRender((sp, builder) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    builder
        .WithBasePath(config["FlexRender:BasePath"] ?? "./templates")
        .WithSkia(skia => skia.WithQr().WithBarcode());
});
```

### 3. CLI

```bash
# Render template to PNG
flexrender render hello.yaml -d data.json -o hello.png

# Watch mode -- auto re-render on file changes
flexrender watch hello.yaml -d data.json -o preview.png
```

## Renderer Configuration

FlexRender supports three rendering backends. Choose based on your deployment environment and requirements.

### Skia Backend

Native rendering via SkiaSharp. Best quality, widest feature set.

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia
        .WithQr()        // QR code support
        .WithBarcode()   // Barcode support
        .WithSvgElement()) // Inline SVG elements
    .Build();
```

- **Formats:** PNG, JPEG, BMP, Raw
- **Requires:** `SkiaSharp.NativeAssets.Linux` on Linux/Docker
- **Optional:** `.WithHarfBuzz()` for Arabic/Hebrew text shaping
- **Best for:** Desktop apps, servers with native library support

### ImageSharp Backend

Pure .NET rendering — zero native dependencies.

```csharp
var render = new FlexRenderBuilder()
    .WithImageSharp(imageSharp => imageSharp
        .WithQr()        // QR code support
        .WithBarcode())  // Barcode support
    .Build();
```

- **Formats:** PNG, JPEG, BMP, Raw
- **No native dependencies** — pure managed .NET
- **Best for:** Containers, serverless, ARM platforms, environments where SkiaSharp is unavailable
- **Limitations:** No SVG element support, no HarfBuzz text shaping

### SVG Backend

Vector SVG output with optional raster fallback.

```csharp
// SVG-only (vector output, no native dependencies)
var render = new FlexRenderBuilder()
    .WithSvg(svg => svg
        .WithQrSvg()           // Vector QR codes
        .WithBarcodeSvg()      // Vector barcodes
        .WithSvgElementSvg())  // Inline SVG elements
    .Build();
```

```csharp
// SVG + Skia raster fallback (also supports PNG/JPEG)
var render = new FlexRenderBuilder()
    .WithSvg(svg => svg
        .WithQrSvg()
        .WithBarcodeSvg()
        .WithSkia(skia => skia
            .WithQr()
            .WithBarcode()))
    .Build();
```

- **SVG output:** Scalable vector graphics, smallest file size
- **SVG-native providers:** QR codes and barcodes rendered as `<path>` elements
- **Optional raster backend:** Add `.WithSkia()` for PNG/JPEG output alongside SVG
- **Best for:** Web, print, documents where vector graphics are preferred

### Renderer Comparison

See the [showcase-capabilities template](../../examples/showcase-capabilities.yaml) for a side-by-side comparison of all three backends rendering the same template. Pre-rendered outputs:

- [Skia output (PNG)](../../examples/output/showcase-capabilities-skia.png) -- gradients, shadows, SVG elements
- [ImageSharp output (PNG)](../../examples/output/showcase-capabilities-imagesharp.png) -- pure .NET rendering
- [SVG output](../../examples/output/showcase-capabilities.svg) -- vector graphics

## Output Formats

FlexRender supports four output formats, each with per-call options:

| Format | Extension | Description | Options |
|--------|-----------|-------------|---------|
| PNG | `.png` | Lossless compression, transparency | CompressionLevel (0-100) |
| JPEG | `.jpg` | Lossy compression, smaller files | Quality (1-100) |
| BMP | `.bmp` | Uncompressed bitmap, 6 color modes | ColorMode |
| Raw | `.raw` | Raw BGRA pixel data (4 bytes/pixel) | -- |

### Format-specific rendering

Use the new format-specific methods for full control over encoding options:

```csharp
// Default PNG
byte[] png = await render.RenderToPng(template, data);

// JPEG with custom quality
byte[] jpeg = await render.RenderToJpeg(template, data,
    new JpegOptions { Quality = 75 });

// BMP monochrome for thermal printer
byte[] bmp = await render.RenderToBmp(template, data,
    new BmpOptions { ColorMode = BmpColorMode.Monochrome1 },
    new RenderOptions { Antialiasing = false });

// Raw BGRA pixel data
byte[] raw = await render.RenderToRaw(template, data);

// With culture-specific formatting (e.g., Russian number/date formatting)
byte[] localized = await render.RenderToPng(template, data,
    renderOptions: new RenderOptions { Culture = new CultureInfo("ru-RU") });
```

Convenience extension methods from `FlexRender.Yaml`:

```csharp
byte[] png = await render.RenderFileToPng("template.yaml", data);
byte[] jpeg = await render.RenderFileToJpeg("template.yaml", data,
    new JpegOptions { Quality = 85 });
```

See [[Render-Options]] for detailed format and rendering options.

### Legacy format parameter (still works)

```csharp
byte[] png = await render.RenderFile("template.yaml", data, ImageFormat.Png);
byte[] jpeg = await render.RenderFile("template.yaml", data, ImageFormat.Jpeg);
```

## Template Caching

For high-throughput scenarios, parse templates once and render many times:

```csharp
// Parse once (at startup)
var parser = new TemplateParser();
_templates["receipt"] = await parser.ParseFileAsync("receipt.yaml");

// Render many times (per request)
byte[] png = await render.Render(_templates["receipt"], data);
```

Template caching works because `type: each` and `type: if` elements are expanded at render time, not parse time. The same parsed template can be rendered with different data.

## Sandboxed Mode

For environments where file system access should be restricted:

```csharp
var render = new FlexRenderBuilder()
    .WithoutDefaultLoaders()                      // Remove file and base64 loaders
    .WithEmbeddedLoader(typeof(Program).Assembly)  // Only embedded resources
    .WithSkia()
    .Build();
```

## Next Steps

- [[Template-Syntax]] -- learn all element types and properties
- [[Template-Expressions]] -- variables, loops, conditionals
- [[Flexbox-Layout]] -- master the layout engine
- [[Render-Options]] -- per-call rendering and format options
- [[API-Reference]] -- full API documentation
