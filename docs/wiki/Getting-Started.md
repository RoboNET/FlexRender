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
> ```

### Individual packages

Install only what you need:

| Package | Description | External Dependencies |
|---------|-------------|----------------------|
| `FlexRender.Core` | Layout engine, template processing | None |
| `FlexRender.Yaml` | YAML template parser | YamlDotNet |
| `FlexRender.Skia` | SkiaSharp renderer | SkiaSharp |
| `FlexRender.QrCode` | QR code support | QRCoder |
| `FlexRender.Barcode` | Barcode support | None |
| `FlexRender.Http` | HTTP/HTTPS resource loading | None |
| `FlexRender.DependencyInjection` | Microsoft DI integration | Microsoft.Extensions.DI |

```bash
# Example: core + YAML parser + Skia renderer
dotnet add package FlexRender.Core
dotnet add package FlexRender.Yaml
dotnet add package FlexRender.Skia
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

// Build renderer with fluent API
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
