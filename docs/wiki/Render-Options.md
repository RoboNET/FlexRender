# Render Options

FlexRender separates configuration into two levels:

- **Engine-level options** (`FlexRenderOptions` / Builder) -- configured once at build time, affect how the engine is constructed
- **Per-call options** (`RenderOptions`, `PngOptions`, `JpegOptions`, `BmpOptions`) -- can differ between consecutive render calls on the same `IFlexRender` instance

## Engine-Level vs Per-Call Options

| Engine-Level (FlexRenderOptions) | Per-Call (method parameters) |
|----------------------------------|------------------------------|
| DefaultFontFamily | Antialiasing (on/off) |
| BaseFontSize | SubpixelText (on/off) |
| BasePath | FontHinting level |
| EnableCaching | TextRendering mode |
| ResourceLimits | JPEG quality (1-100) |
| Content providers (QR, Barcode) | PNG compression level (0-100) |
| Resource loaders | BMP color mode |

**The rule:** if a setting affects how the engine is constructed or initialized (font loading, resource resolution), it is engine-level. If a setting can reasonably differ between two consecutive render calls on the same instance, it is per-call.

---

## Backend Compatibility

`RenderOptions` apply to both the Skia and ImageSharp backends. However, the ImageSharp backend ignores Skia-specific options that have no equivalent in the SixLabors rendering pipeline:

| Property | Skia | ImageSharp |
|----------|------|------------|
| `Antialiasing` | Supported | Supported |
| `SubpixelText` | Supported | Ignored |
| `FontHinting` | Supported | Ignored |
| `TextRendering` | Supported | Ignored |
| `Culture` | Supported | Supported |

---

## RenderOptions

Per-call rendering options that apply to all output formats. Defined in `FlexRender.Core`.

```csharp
public sealed record RenderOptions
{
    public static readonly RenderOptions Default = new();
    public static readonly RenderOptions Deterministic = new()
    {
        SubpixelText = false,
        FontHinting = FontHinting.None,
        TextRendering = TextRendering.Grayscale
    };

    public bool Antialiasing { get; init; } = true;
    public bool SubpixelText { get; init; } = true;
    public FontHinting FontHinting { get; init; } = FontHinting.Normal;
    public TextRendering TextRendering { get; init; } = TextRendering.SubpixelLcd;
}
```

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Antialiasing` | bool | `true` | Antialiasing for shapes, separators, image scaling |
| `SubpixelText` | bool | `true` | Subpixel text positioning for smoother spacing |
| `FontHinting` | FontHinting | `Normal` | Font hinting level for grid-fitting glyphs |
| `TextRendering` | TextRendering | `SubpixelLcd` | Text edge rendering mode |

### RenderOptions.Default

The default preset enables all visual quality features:
- Antialiasing enabled
- Subpixel text positioning enabled
- Normal font hinting
- Subpixel LCD text rendering

Best for on-screen display and high-quality output.

### RenderOptions.Deterministic

Produces cross-platform identical output, suitable for snapshot testing and CI:
- Antialiasing enabled (shapes are still smooth)
- Subpixel text positioning **disabled** (glyphs snap to pixel boundaries)
- Font hinting **None** (no platform-dependent grid-fitting)
- Text rendering **Grayscale** (platform-independent smooth text)

```csharp
// Snapshot testing
byte[] png = await render.RenderToPng(template, data,
    renderOptions: RenderOptions.Deterministic);
```

### Migration from DeterministicRendering

The old engine-level `FlexRenderOptions.DeterministicRendering` property is now deprecated. Migrate to the per-call `RenderOptions.Deterministic`:

```csharp
// OLD (deprecated)
var render = new FlexRenderBuilder()
    .WithSkia()
    .Build();
// Had to set DeterministicRendering at engine level

// NEW (per-call)
byte[] png = await render.RenderToPng(template, data,
    renderOptions: RenderOptions.Deterministic);
// Same engine can produce both deterministic and non-deterministic output
```

---

## FontHinting

Controls how font glyphs are adjusted to fit the pixel grid.

```csharp
public enum FontHinting
{
    None = 0,     // No hinting -- platform-consistent, use for snapshot tests
    Slight = 1,   // Minimal grid-fitting, preserves glyph shape
    Normal = 2,   // Default platform behavior
    Full = 3      // Maximum grid-fitting, crispest at small sizes
}
```

| Value | Use Case |
|-------|----------|
| `None` | Snapshot tests, CI -- identical output across platforms |
| `Slight` | Good balance of consistency and readability |
| `Normal` | Default -- best platform-native text appearance |
| `Full` | Small text sizes where crisp edges matter |

---

## TextRendering

Controls how text edges are antialiased.

```csharp
public enum TextRendering
{
    Aliased = 0,      // No antialiasing -- jagged text, monochrome targets
    Grayscale = 1,    // Grayscale AA -- platform-independent smooth text
    SubpixelLcd = 2   // Subpixel LCD AA -- sharpest on LCDs, platform-dependent
}
```

| Value | Appearance | Platform Consistency |
|-------|-----------|---------------------|
| `Aliased` | Jagged, no smoothing | Consistent |
| `Grayscale` | Smooth, gray anti-aliased edges | Consistent |
| `SubpixelLcd` | Sharpest on LCD displays | Platform-dependent |

---

## PngOptions

Options for PNG image encoding. PNG is always lossless.

```csharp
public sealed record PngOptions
{
    public static readonly PngOptions Default = new();

    /// <summary>Compression level (0-100). Throws ArgumentOutOfRangeException if out of range.</summary>
    public int CompressionLevel { get; init; } = 100;  // Validated: 0-100
}
```

| Property | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| `CompressionLevel` | int | `100` | 0-100 | 0 = no compression (fastest, largest). 100 = max compression (slowest, smallest) |

```csharp
// Fast encoding, larger file
byte[] png = await render.RenderToPng(template, data,
    new PngOptions { CompressionLevel = 0 });

// Maximum compression (default)
byte[] png = await render.RenderToPng(template, data);
```

> **Validation:** `CompressionLevel` is validated on construction. Values outside 0-100 throw `ArgumentOutOfRangeException` immediately, not at render time.

> **Note:** Unlike JPEG quality, higher `CompressionLevel` values mean more compression effort, not higher visual quality. PNG is always lossless regardless of this setting.

---

## JpegOptions

Options for JPEG image encoding. JPEG uses lossy compression.

```csharp
public sealed record JpegOptions
{
    public static readonly JpegOptions Default = new();

    /// <summary>Quality (1-100). Throws ArgumentOutOfRangeException if out of range.</summary>
    public int Quality { get; init; } = 90;  // Validated: 1-100
}
```

| Property | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| `Quality` | int | `90` | 1-100 | 1 = lowest quality (smallest). 100 = highest quality (largest) |

```csharp
// High quality
byte[] jpeg = await render.RenderToJpeg(template, data,
    new JpegOptions { Quality = 95 });

// Lower quality, smaller file
byte[] jpeg = await render.RenderToJpeg(template, data,
    new JpegOptions { Quality = 60 });
```

> **Validation:** `Quality` is validated on construction. Values outside 1-100 throw `ArgumentOutOfRangeException` immediately.

Values above 95 produce diminishing returns in quality with significant size increase.

---

## BmpOptions

Options for BMP image encoding. BMP supports multiple color depth modes.

```csharp
public sealed record BmpOptions
{
    public static readonly BmpOptions Default = new();
    public BmpColorMode ColorMode { get; init; } = BmpColorMode.Bgra32;
}
```

### BmpColorMode

| Value | Bits/Pixel | Size Reduction | Description |
|-------|-----------|---------------|-------------|
| `Bgra32` | 32 | -- | Full color with alpha (default) |
| `Rgb24` | 24 | 25% smaller | Full color without alpha |
| `Rgb565` | 16 | 50% smaller | Reduced color, thermal printers |
| `Grayscale8` | 8 | 75% smaller | 256 shades of gray |
| `Grayscale4` | 4 | 87% smaller | 16 shades of gray |
| `Monochrome1` | 1 | 96% smaller | Black and white, ideal for thermal printers |

```csharp
// Monochrome for thermal printer
byte[] bmp = await render.RenderToBmp(template, data,
    new BmpOptions { ColorMode = BmpColorMode.Monochrome1 });

// Grayscale
byte[] bmp = await render.RenderToBmp(template, data,
    new BmpOptions { ColorMode = BmpColorMode.Grayscale8 });
```

---

## Usage Examples

### Default PNG rendering

```csharp
var render = new FlexRenderBuilder()
    .WithSkia()
    .Build();

byte[] png = await render.RenderToPng(template, data);
```

### JPEG with custom quality

```csharp
byte[] jpeg = await render.RenderToJpeg(template, data,
    new JpegOptions { Quality = 75 });
```

### BMP monochrome for thermal printer (no antialiasing)

```csharp
byte[] bmp = await render.RenderToBmp(template, data,
    new BmpOptions { ColorMode = BmpColorMode.Monochrome1 },
    new RenderOptions { Antialiasing = false });
```

### Deterministic rendering for snapshot tests

```csharp
byte[] png = await render.RenderToPng(template, data,
    renderOptions: RenderOptions.Deterministic);
```

### Multiple renders with shared options

```csharp
var thermalOptions = new RenderOptions { Antialiasing = false };
var bmpMono = new BmpOptions { ColorMode = BmpColorMode.Monochrome1 };
var bmpGray = new BmpOptions { ColorMode = BmpColorMode.Grayscale8 };

// Same render options, different format options
byte[] label = await render.RenderToBmp(labelTemplate, data, bmpMono, thermalOptions);
byte[] receipt = await render.RenderToBmp(receiptTemplate, data, bmpGray, thermalOptions);
```

### File rendering with options (extension methods)

```csharp
// From FlexRender.Yaml
byte[] png = await render.RenderFileToPng("template.yaml", data);
byte[] jpeg = await render.RenderFileToJpeg("template.yaml", data,
    new JpegOptions { Quality = 85 },
    new RenderOptions { Antialiasing = false });
byte[] bmp = await render.RenderFileToBmp("template.yaml", data,
    new BmpOptions { ColorMode = BmpColorMode.Monochrome1 });
```

### Aliased text for very low resolution

```csharp
byte[] png = await render.RenderToPng(template, data,
    renderOptions: new RenderOptions
    {
        Antialiasing = false,
        TextRendering = TextRendering.Aliased,
        FontHinting = FontHinting.Full
    });
```

---

## Legacy Render Methods

The original `Render()` methods with `ImageFormat` parameter continue to work:

```csharp
// Still works
byte[] png = await render.Render(template, data, ImageFormat.Png);
byte[] jpeg = await render.Render(template, data, ImageFormat.Jpeg);
```

These methods use default format options. For full control over encoding parameters, use the format-specific methods (`RenderToPng`, `RenderToJpeg`, `RenderToBmp`, `RenderToRaw`).

## See Also

- [[API-Reference]] -- full interface and method signatures
- [[Getting-Started]] -- output formats overview
- [[CLI-Reference]] -- CLI format options
