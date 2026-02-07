# CLI Reference

The `flexrender` CLI tool renders YAML templates to images, validates templates, and provides debugging tools.

## Installation

### dotnet tool (requires .NET SDK)

```bash
dotnet tool install -g flexrender-cli
```

### Native AOT binary (no .NET required)

Download pre-built binaries from [GitHub Releases](https://github.com/RoboNET/FlexRender/releases) for:
- `osx-arm64` (Apple Silicon)
- `linux-x64`
- `win-x64`

### Build from source

```bash
dotnet publish src/FlexRender.Cli -c Release -r <RID> /p:PublishAot=true
```

Common RIDs: `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `win-x64`.

The resulting binary is a single self-contained executable with no external dependencies (except native SkiaSharp libs).

### Distribution channels summary

| Channel | Install | Requires .NET? | Use case |
|---------|---------|---------------|----------|
| dotnet tool | `dotnet tool install -g flexrender-cli` | Yes | .NET developers |
| GitHub Release | Download binary from Releases | No | CI, Docker, non-.NET |
| Build from source | `dotnet publish /p:PublishAot=true -r <RID>` | Build-time only | Custom builds |

---

## Commands

### render

Renders a YAML template to an image file.

```bash
flexrender render <template> [options]
```

| Option | Short | Description |
|--------|-------|-------------|
| `--data <file>` | `-d` | JSON data file for template variables |
| `--output <file>` | `-o` | Output file path (format inferred from extension) |
| `--format <fmt>` | `-f` | Output format: `png`, `jpeg`, `bmp`, `raw` |
| `--quality <n>` | `-q` | JPEG quality (1-100, default: 90) |
| `--bmp-color <mode>` | | BMP color mode (see below) |
| `--base-path <dir>` | | Base path for resolving relative file references |

**Examples:**

```bash
# Basic render
flexrender render receipt.yaml -d data.json -o receipt.png

# JPEG with custom quality
flexrender render receipt.yaml -d data.json -o receipt.jpg --quality 85

# BMP monochrome for thermal printer
flexrender render receipt.yaml -d data.json -o receipt.bmp --bmp-color monochrome1

# Explicit format (when extension doesn't match)
flexrender render template.yaml -d data.json -o output.bin --format raw

# With base path for fonts/images
flexrender render templates/receipt.yaml --base-path templates -o output.png
```

**BMP color modes:**

| Value | Description |
|-------|-------------|
| `bgra32` | 32-bit with alpha (default) |
| `rgb24` | 24-bit without alpha |
| `rgb565` | 16-bit |
| `grayscale8` | 8-bit grayscale |
| `grayscale4` | 4-bit grayscale |
| `monochrome1` | 1-bit black/white |

---

### validate

Validates a YAML template without rendering.

```bash
flexrender validate <template>
```

**Example:**

```bash
flexrender validate receipt.yaml
# Output: Template "receipt" v1 is valid (12 elements)
```

---

### info

Shows template metadata and structure.

```bash
flexrender info <template>
```

**Example:**

```bash
flexrender info receipt.yaml
# Output: template info including name, version, canvas settings, element count
```

---

### watch

Watches a template and data file for changes, automatically re-rendering on save.

```bash
flexrender watch <template> [options]
```

| Option | Short | Description |
|--------|-------|-------------|
| `--data <file>` | `-d` | JSON data file to watch |
| `--output <file>` | `-o` | Output file path |

**Example:**

```bash
flexrender watch receipt.yaml -d data.json -o preview.png
# Re-renders whenever receipt.yaml or data.json changes
```

---

### debug-layout

Renders a template with layout debugging information (element bounds, hierarchy).

```bash
flexrender debug-layout <template> [options]
```

| Option | Short | Description |
|--------|-------|-------------|
| `--data <file>` | `-d` | JSON data file |

**Example:**

```bash
flexrender debug-layout receipt.yaml -d data.json
```

---

## Global Options

These options apply to all commands:

| Option | Short | Description |
|--------|-------|-------------|
| `--verbose` | `-v` | Verbose output |
| `--fonts <dir>` | | Custom fonts directory |
| `--scale <float>` | | Scale factor (e.g., `2.0` for retina) |
| `--backend <name>` | `-b` | Rendering backend: `skia` (default) or `imagesharp` |

The `--scale` option uses invariant culture -- always use `.` as the decimal separator (e.g., `--scale 2.0`).

### Backend Selection

| Backend | Features | Native Deps |
|---------|----------|-------------|
| `skia` (default) | Full features: QR codes, barcodes, SVG elements, HarfBuzz | Requires SkiaSharp native libs |
| `imagesharp` | Pure .NET rendering, QR/barcode support (if packages installed), no SVG elements | None |

> **Note:** The CLI references `FlexRender.QrCode` and `FlexRender.Barcode`, so the ImageSharp backend includes QR/barcode support out of the box. SVG elements are still Skia/SVG-only.

```bash
# Default (Skia backend)
flexrender render receipt.yaml -d data.json -o receipt.png

# Render with ImageSharp backend (no native dependencies)
flexrender render receipt.yaml -d data.json -o receipt.png --backend imagesharp
```

---

## Working Directory

The CLI resolves all relative paths (template, data, fonts, image `src`) from the **current working directory**. When running examples, `cd` into the `examples/` directory first:

```bash
cd examples/
flexrender render receipt.yaml -d receipt-data.json -o output/receipt.png
```

---

## Run from Source (Development)

```bash
dotnet run --project src/FlexRender.Cli -- render template.yaml -d data.json -o output.png
dotnet run --project src/FlexRender.Cli -- validate template.yaml
dotnet run --project src/FlexRender.Cli -- info template.yaml
dotnet run --project src/FlexRender.Cli -- watch template.yaml -d data.json -o preview.png
dotnet run --project src/FlexRender.Cli -- debug-layout template.yaml -d data.json
```

---

## AOT Publishing

The CLI is fully AOT-compatible (`IsAotCompatible=true` on all projects, no reflection). Build standalone native binaries:

### macOS (Apple Silicon)

```bash
dotnet publish src/FlexRender.Cli -c Release -r osx-arm64 /p:PublishAot=true
```

### macOS (Intel)

```bash
dotnet publish src/FlexRender.Cli -c Release -r osx-x64 /p:PublishAot=true
```

### Linux (x64)

```bash
dotnet publish src/FlexRender.Cli -c Release -r linux-x64 /p:PublishAot=true
```

### Linux (ARM64)

```bash
dotnet publish src/FlexRender.Cli -c Release -r linux-arm64 /p:PublishAot=true
```

### Windows (x64)

```bash
dotnet publish src/FlexRender.Cli -c Release -r win-x64 /p:PublishAot=true
```

The output is a single self-contained executable in `src/FlexRender.Cli/bin/Release/net10.0/<RID>/publish/`.

## See Also

- [[Getting-Started]] -- first steps with FlexRender
- [[Template-Syntax]] -- YAML template reference
- [[Render-Options]] -- format and rendering options
