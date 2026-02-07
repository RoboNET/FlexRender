# FlexRender

[![NuGet](https://img.shields.io/nuget/v/FlexRender.svg)](https://www.nuget.org/packages/FlexRender)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexRender.svg)](https://www.nuget.org/packages/FlexRender)
[![CI](https://github.com/RoboNET/FlexRender/actions/workflows/ci.yml/badge.svg)](https://github.com/RoboNET/FlexRender/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library for rendering images from YAML templates with a full CSS flexbox layout engine. Perfect for generating receipts, labels, tickets, and structured documents.

## Features

- **YAML Templates** -- define complex image layouts in readable YAML format
- **Full CSS Flexbox** -- direction, wrapping, justify, align, grow/shrink/basis, min/max constraints, auto margins
- **RTL Support** -- right-to-left layout with `text-direction: rtl`, logical alignment (`start`/`end`), row mirroring
- **Template Engine** -- variables (`{{name}}`), loops (`type: each`), conditionals (`type: if` with 13 operators)
- **Inline Expressions** -- arithmetic (`+`, `-`, `*`, `/`), null coalesce (`??`), unary negation, parentheses
- **Filters** -- `| currency`, `| number`, `| format`, `| upper`, `| lower`, `| trim`, `| truncate`
- **Tables** -- `type: table` with dynamic rows from data arrays, column alignment, header styling, row/column gaps
- **Visual Effects** -- `box-shadow`, `background: linear-gradient(...)`, `opacity` on any element
- **Rich Content** -- text, images, SVG, QR codes (`FlexRender.QrCode.*`), barcodes (`FlexRender.Barcode.*`), separators
- **HarfBuzz Shaping** -- optional `FlexRender.HarfBuzz` package for Arabic/Hebrew glyph shaping
- **SVG Output** -- render templates to SVG vector format via `FlexRender.Svg` (meta) or `FlexRender.Svg.Render`
- **ImageSharp Backend** -- pure .NET rendering via `FlexRender.ImageSharp` (meta) or `FlexRender.ImageSharp.Render`, zero native dependencies
- **Multiple Formats** -- PNG, JPEG (quality 1-100), BMP (6 color modes), Raw pixels
- **Per-Call Options** -- antialiasing, font hinting, text rendering mode per render call
- **AOT Compatible** -- no reflection, works with Native AOT publishing
- **CLI Tool** -- render, validate, watch, and debug templates from the command line

## Examples

| Receipt | Dynamic Receipt | Ticket | Label |
|---------|-----------------|--------|-------|
| ![Receipt](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/receipt.png) | ![Dynamic](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/receipt-dynamic.png) | ![Ticket](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/ticket.png) | ![Label](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/label.png) |

| Table Invoice | Expressions | Visual Effects |
|---------------|-------------|----------------|
| ![Table Invoice](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/table-invoice.png) | ![Expressions](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/expressions-demo.png) | ![Visual Effects](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/visual-effects.png) |

<details>
<summary>All Features Showcase (click to expand)</summary>

![Showcase](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/showcase-all-features.png)

</details>

<details>
<summary>Renderer Comparison (click to expand)</summary>

| Skia | ImageSharp | SVG |
|------|------------|-----|
| ![Skia](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/showcase-capabilities-skia.png) | ![ImageSharp](https://raw.githubusercontent.com/RoboNET/FlexRender/main/examples/output/showcase-capabilities-imagesharp.png) | [SVG output](examples/output/showcase-capabilities.svg) |
| Native rendering, gradients, shadows, SVG elements | Pure .NET, zero native deps | Vector output, scalable |

</details>

## Installation

```bash
# All-in-one meta package (all backends)
dotnet add package FlexRender

# Skia backend (meta: renderer + providers)
dotnet add package FlexRender.Skia

# SVG backend (meta: renderer + providers)
dotnet add package FlexRender.Svg

# Pure .NET backend (meta: renderer + providers, no native dependencies)
dotnet add package FlexRender.ImageSharp

# Render-only packages (no providers)
dotnet add package FlexRender.Skia.Render
dotnet add package FlexRender.Svg.Render
dotnet add package FlexRender.ImageSharp.Render

# Optional providers (pick the renderer you use)
dotnet add package FlexRender.QrCode.Skia.Render
dotnet add package FlexRender.QrCode.Svg.Render
dotnet add package FlexRender.QrCode.ImageSharp.Render
dotnet add package FlexRender.Barcode.Skia.Render
dotnet add package FlexRender.Barcode.Svg.Render
dotnet add package FlexRender.Barcode.ImageSharp.Render
dotnet add package FlexRender.SvgElement.Skia.Render
dotnet add package FlexRender.SvgElement.Svg.Render

# Meta packages (all renderers for a feature)
dotnet add package FlexRender.QrCode
dotnet add package FlexRender.Barcode
dotnet add package FlexRender.SvgElement

# CLI tool
dotnet tool install -g flexrender-cli
```

> **Linux / Docker:** The Skia backend requires native libraries. Add `SkiaSharp.NativeAssets.Linux` to avoid `DllNotFoundException: libSkiaSharp`. For HarfBuzz text shaping, also add `HarfBuzzSharp.NativeAssets.Linux`. The ImageSharp backend has no native dependencies.

## Quick Start

**1. Create a template** (`receipt.yaml`):

```yaml
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
        content: "{{shopName}}"
        font: bold
        size: 1.5em
        align: center

      - type: each
        array: items
        as: item
        children:
          - type: flex
            direction: row
            justify: space-between
            children:
              - type: text
                content: "{{item.name}}"
              - type: text
                content: "{{item.price | currency}} $"

      - type: text
        content: "Total: {{total | currency}} $"
        font: bold
        align: right
```

**2. Render with code (Skia backend):**

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia.WithQr().WithBarcode())
    .Build();

var data = new ObjectValue
{
    ["shopName"] = "My Shop",
    ["total"] = 1500,
    ["items"] = new ArrayValue(
        new ObjectValue { ["name"] = "Product 1", ["price"] = 500 },
        new ObjectValue { ["name"] = "Product 2", ["price"] = 1000 })
};

byte[] png = await render.RenderFile("receipt.yaml", data);
```

> For ImageSharp QR/barcode support, install `FlexRender.ImageSharp` (meta) or `FlexRender.QrCode.ImageSharp.Render` and `FlexRender.Barcode.ImageSharp.Render`.

**Or with ImageSharp (pure .NET, no native deps):**

```csharp
var render = new FlexRenderBuilder()
    .WithImageSharp(imageSharp => imageSharp.WithQr().WithBarcode())
    .Build();

byte[] png = await render.RenderFile("receipt.yaml", data);
```

**3. Or use the CLI:**

```bash
flexrender render receipt.yaml -d data.json -o receipt.png

# Use ImageSharp backend (no native dependencies)
flexrender render receipt.yaml -d data.json -o receipt.png --backend imagesharp
```

## Documentation

| Page | Description |
|------|-------------|
| [Getting Started](https://github.com/RoboNET/FlexRender/wiki/Getting-Started) | Installation, first template, rendering approaches |
| [Template Syntax](https://github.com/RoboNET/FlexRender/wiki/Template-Syntax) | Canvas, all element types, tables, common properties |
| [Element Reference](https://github.com/RoboNET/FlexRender/wiki/Element-Reference) | Complete property reference for all element types |
| [Template Expressions](https://github.com/RoboNET/FlexRender/wiki/Template-Expressions) | Variables, loops, conditionals, inline expressions, filters |
| [Flexbox Layout](https://github.com/RoboNET/FlexRender/wiki/Flexbox-Layout) | Direction, justify, align, wrapping, grow/shrink |
| [Render Options](https://github.com/RoboNET/FlexRender/wiki/Render-Options) | Per-call antialiasing, font hinting, format options |
| [CLI Reference](https://github.com/RoboNET/FlexRender/wiki/CLI-Reference) | Commands, options, AOT publishing |
| [API Reference](https://github.com/RoboNET/FlexRender/wiki/API-Reference) | IFlexRender, builder, DI, types |
| [Visual Reference](https://github.com/RoboNET/FlexRender/wiki/Visual-Reference) | Visual effects, gradients, shadows, opacity |
| [Contributing](https://github.com/RoboNET/FlexRender/wiki/Contributing) | Build, test, architecture, conventions |

### For LLM Agents

- [`llms.txt`](llms.txt) -- concise project overview (~450 lines)
- [`llms-full.txt`](llms-full.txt) -- comprehensive reference (~1250 lines)
- [`AGENTS.md`](AGENTS.md) -- build commands, coding conventions

## License

MIT
