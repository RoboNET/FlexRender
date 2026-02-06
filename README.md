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
- **Rich Content** -- text, images, QR codes (`FlexRender.QrCode`), barcodes (`FlexRender.Barcode`), separators
- **HarfBuzz Shaping** -- optional `FlexRender.HarfBuzz` package for Arabic/Hebrew glyph shaping
- **Multiple Formats** -- PNG, JPEG (quality 1-100), BMP (6 color modes), Raw pixels
- **Per-Call Options** -- antialiasing, font hinting, text rendering mode per render call
- **AOT Compatible** -- no reflection, works with Native AOT publishing
- **CLI Tool** -- render, validate, watch, and debug templates from the command line

## Examples

| Receipt | Dynamic Receipt | Ticket | Label |
|---------|-----------------|--------|-------|
| ![Receipt](examples/output/receipt.png) | ![Dynamic](examples/output/receipt-dynamic.png) | ![Ticket](examples/output/ticket.png) | ![Label](examples/output/label.png) |

<details>
<summary>Feature Showcase (click to expand)</summary>

![Showcase](examples/output/showcase.png)

</details>

## Installation

```bash
# All-in-one package
dotnet add package FlexRender

# CLI tool
dotnet tool install -g flexrender-cli
```

> **Linux / Docker:** Add `SkiaSharp.NativeAssets.Linux` to avoid `DllNotFoundException: libSkiaSharp`.

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
                content: "{{item.price}} $"

      - type: text
        content: "Total: {{total}} $"
        font: bold
        align: right
```

**2. Render with code:**

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

**3. Or use the CLI:**

```bash
flexrender render receipt.yaml -d data.json -o receipt.png
```

## Documentation

| Page | Description |
|------|-------------|
| [Getting Started](https://github.com/RoboNET/FlexRender/wiki/Getting-Started) | Installation, first template, rendering approaches |
| [Template Syntax](https://github.com/RoboNET/FlexRender/wiki/Template-Syntax) | Canvas, all 8 element types, common properties |
| [Element Reference](https://github.com/RoboNET/FlexRender/wiki/Element-Reference) | Complete property reference for all element types |
| [Template Expressions](https://github.com/RoboNET/FlexRender/wiki/Template-Expressions) | Variables, loops, conditionals |
| [Flexbox Layout](https://github.com/RoboNET/FlexRender/wiki/Flexbox-Layout) | Direction, justify, align, wrapping, grow/shrink |
| [Render Options](https://github.com/RoboNET/FlexRender/wiki/Render-Options) | Per-call antialiasing, font hinting, format options |
| [CLI Reference](https://github.com/RoboNET/FlexRender/wiki/CLI-Reference) | Commands, options, AOT publishing |
| [API Reference](https://github.com/RoboNET/FlexRender/wiki/API-Reference) | IFlexRender, builder, DI, types |
| [Contributing](https://github.com/RoboNET/FlexRender/wiki/Contributing) | Build, test, architecture, conventions |

### For LLM Agents

- [`llms.txt`](llms.txt) -- concise project overview (~200 lines)
- [`llms-full.txt`](llms-full.txt) -- comprehensive reference (~600 lines)
- [`AGENTS.md`](AGENTS.md) -- build commands, coding conventions

## License

MIT
