# FlexRender

[![NuGet](https://img.shields.io/nuget/v/FlexRender.svg)](https://www.nuget.org/packages/FlexRender)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexRender.svg)](https://www.nuget.org/packages/FlexRender)
[![CI](https://github.com/RoboNET/FlexRender/actions/workflows/ci.yml/badge.svg)](https://github.com/RoboNET/FlexRender/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modular .NET library for rendering images from YAML templates with a full CSS flexbox layout engine. Render-backend agnostic with SkiaSharp as the default backend. Fully AOT-compatible with zero reflection.

## Why FlexRender?

- **YAML-first** -- define complex image layouts in readable YAML, no design tools needed
- **Full CSS Flexbox** -- direction, wrapping, justify, align, grow/shrink/basis, min/max constraints, auto margins, positioning
- **RTL Support** -- right-to-left layout with `text-direction: rtl`, logical alignment (`start`/`end`), HarfBuzz text shaping for Arabic/Hebrew
- **Template engine** -- variables (`{{name}}`), inline expressions (`{{price * qty | currency}}`), loops (`type: each`), conditionals (`type: if` with 13 operators)
- **Inline expressions** -- arithmetic (`+`, `-`, `*`, `/`), null coalescing (`??`), 8 built-in filters enabled by default (`currency`, `currencySymbol`, `number`, `upper`, `lower`, `trim`, `truncate`, `format`)
- **Rich content types** -- text, images, SVG, QR codes, barcodes, separators, tables
- **Visual effects** -- opacity, box-shadow, linear and radial gradient backgrounds
- **Multiple output formats** -- PNG, JPEG, BMP (6 color modes), Raw pixels, with per-call format options
- **AOT-ready** -- no reflection, no `dynamic`, works with Native AOT publishing
- **Modular packages** -- install only what you need, from zero-dependency Core to the all-in-one meta-package
- **CLI tool** -- render, validate, watch, and debug templates from the command line

## Feature Highlights

| Feature | Description |
|---------|-------------|
| Flexbox layout | Row/column, wrapping, gap, justify, align, grow/shrink/basis |
| RTL layout | Right-to-left with `text-direction: rtl`, logical `start`/`end` alignment, HarfBuzz shaping |
| Template expressions | `{{variable}}`, dot notation, array indexing, arithmetic, null coalescing |
| Inline filters | `{{value \| currency}}`, 8 built-in enabled by default: currency, currencySymbol, number, upper, lower, trim, truncate, format |
| Control flow | `type: each` loops with @index/@first/@last, `type: if` with 13 operators |
| Tables | `type: table` with dynamic/static rows, column definitions, header styling |
| Visual effects | `opacity`, `box-shadow`, `linear-gradient()`, `radial-gradient()` backgrounds |
| QR codes | Configurable error correction, colors |
| Barcodes | Code128, Code39, EAN-13, EAN-8, UPC-A |
| Image loading | File, HTTP, Base64, embedded resources |
| Output formats | PNG, JPEG (quality 1-100), BMP (6 color modes), Raw BGRA |
| Render options | Per-call antialiasing, font hinting, text rendering mode |
| ImageSharp backend | Pure .NET renderer via `FlexRender.ImageSharp` (meta) or `FlexRender.ImageSharp.Render`, zero native dependencies |
| Template caching | Parse once, render many times with different data |
| CLI tool | render, validate, info, watch, debug-layout commands |

## Examples

| Receipt | Dynamic Receipt | Ticket | Label |
|---------|-----------------|--------|-------|
| ![Receipt](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/output/receipt.png) | ![Dynamic](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/output/receipt-dynamic.png) | ![Ticket](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/output/ticket.png) | ![Label](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/output/label.png) |

## Quick Install

```bash
# All-in-one package
dotnet add package FlexRender

# CLI tool
dotnet tool install -g flexrender-cli
```

## Quick Example

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
        content: "Hello, {{name}}!"
        font: bold
        size: 1.5em
        align: center
```

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia.WithQr().WithBarcode())
    .Build();

var data = new ObjectValue { ["name"] = "World" };
byte[] png = await render.RenderFile("template.yaml", data);
```

## Documentation

| Page | Description |
|------|-------------|
| [[Getting-Started]] | Installation, first template, rendering approaches |
| [[Template-Syntax]] | Canvas, all 10 element types, common properties, units |
| [[Element-Reference]] | Complete property reference for all 10 element types with examples |
| [[Visual-Reference]] | Interactive visual examples for all properties and elements |
| [[Template-Expressions]] | Variables, loops, conditionals with 13 operators |
| [[Flexbox-Layout]] | Direction, justify, align, wrapping, grow/shrink, positioning |
| [[Render-Options]] | Per-call options: antialiasing, font hinting, format-specific settings |
| [[CLI-Reference]] | Commands, options, AOT publishing |
| [[API-Reference]] | IFlexRender, builder, DI, extension methods, types |
| [[Contributing]] | Build, test, architecture, coding conventions |

## For LLM Agents

This project includes optimized documentation for AI coding assistants:

- [`llms.txt`](https://github.com/RoboNET/FlexRender/blob/main/llms.txt) -- concise project overview (~450 lines)
- [`llms-full.txt`](https://github.com/RoboNET/FlexRender/blob/main/llms-full.txt) -- comprehensive reference (~1250 lines)
- [`AGENTS.md`](https://github.com/RoboNET/FlexRender/blob/main/AGENTS.md) -- build commands, coding conventions, contributor guidelines

## Package Structure

```
FlexRender.Core          (0 external deps)
  ^          ^        ^        ^
  |          |        |        |
FlexRender.Yaml  FlexRender.Http  FlexRender.Skia.Render  FlexRender.ImageSharp.Render  FlexRender.Svg.Render
                                    ^           ^                     ^                     ^
                                    |           |                     |                     |
                      Qr/Bar/SvgElement providers per renderer (Skia/Svg/ImageSharp)
                                    |           |                     |
                     FlexRender.QrCode / FlexRender.Barcode / FlexRender.SvgElement (meta)
                                    |           |
                     FlexRender.Skia / FlexRender.ImageSharp / FlexRender.Svg (backend meta)
                                    |
               FlexRender.DependencyInjection
                                    |
                           FlexRender (meta-package)
```

## License

[MIT](https://github.com/RoboNET/FlexRender/blob/main/LICENSE)
