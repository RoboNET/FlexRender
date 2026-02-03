# FlexRender

[![NuGet](https://img.shields.io/nuget/v/FlexRender.svg)](https://www.nuget.org/packages/FlexRender)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FlexRender.svg)](https://www.nuget.org/packages/FlexRender)
[![CI](https://github.com/RoboNET/FlexRender/actions/workflows/ci.yml/badge.svg)](https://github.com/RoboNET/FlexRender/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library for rendering images from YAML templates with flexbox-like layout system. Perfect for generating receipts, labels, tickets, and other structured documents.

## Features

- **YAML Templates** - Define layouts in readable YAML format
- **Flexbox Layout** - Row/column direction, wrap, justify, align, gap
- **Template Engine** - Variables, loops (`type: each`), conditionals (`type: if`)
- **Multiple Content Types** - Text, images, QR codes, barcodes
- **Output Formats** - PNG, JPEG, BMP
- **CLI Tool** - Render templates from command line
- **AOT Compatible** - No reflection, works with Native AOT
- **Modular Architecture** - Install only what you need

## Examples

| Receipt | Dynamic Receipt | Ticket | Label |
|---------|-----------------|--------|-------|
| ![Receipt](examples/output/receipt.png) | ![Dynamic](examples/output/receipt-dynamic.png) | ![Ticket](examples/output/ticket.png) | ![Label](examples/output/label.png) |

<details>
<summary>Feature Showcase (click to expand)</summary>

![Showcase](examples/output/showcase.png)

</details>

All examples are in the [`examples/`](examples/) directory. Render them with the CLI:

```bash
flexrender render examples/receipt.yaml -d examples/receipt-data.json -o examples/output/receipt.png
flexrender render examples/ticket.yaml -d examples/ticket-data.json -o examples/output/ticket.png
flexrender render examples/receipt-dynamic.yaml -d examples/receipt-dynamic-data.json -o examples/output/receipt-dynamic.png
```

The `receipt-dynamic.yaml` demonstrates AST-level control flow with `type: each` for item lists and `type: if` for conditional sections.

## Installation

### All-in-one (recommended)

```bash
dotnet add package FlexRender
```

### Individual packages

| Package | Description |
|---------|-------------|
| `FlexRender.Core` | Layout engine, 0 external dependencies |
| `FlexRender.Yaml` | YAML template parser |
| `FlexRender.Skia` | SkiaSharp renderer |
| `FlexRender.QrCode` | QR code support |
| `FlexRender.Barcode` | Barcode support |
| `FlexRender.DependencyInjection` | Microsoft DI integration |

```bash
# Example: install only the core engine and YAML parser
dotnet add package FlexRender.Core
dotnet add package FlexRender.Yaml

# Or install the Skia renderer
dotnet add package FlexRender.Skia
```

### Linux / Docker

SkiaSharp requires native libraries on Linux. If you get `DllNotFoundException: libSkiaSharp`, add the native assets package:

```bash
# Standard Linux (requires system fontconfig/freetype)
dotnet add package SkiaSharp.NativeAssets.Linux

# Minimal containers without system libs
dotnet add package SkiaSharp.NativeAssets.Linux.NoDependencies
```

### CLI tool

```bash
dotnet tool install -g FlexRender.Cli
```

### Dependency Injection

```csharp
// Register all FlexRender services
services.AddFlexRender();
```

## Quick Start

### 1. Create a template (receipt.yaml)

```yaml
template:
  name: "receipt"
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
        content: "{{shopName}}"
        font: bold
        size: 1.5em
        align: center

      - type: flex
        gap: 4
        children:
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
        size: 1.2em
        align: right

      - type: qr
        data: "{{paymentUrl}}"
        size: 100
```

### 2. Render with code

```csharp
using FlexRender.Parsing;
using FlexRender.Rendering;
using FlexRender.Values;

var parser = new TemplateParser();
var renderer = new SkiaRenderer();

var template = parser.ParseFile("receipt.yaml");

var data = new ObjectValue
{
    ["shopName"] = "My Shop",
    ["total"] = 1500,
    ["paymentUrl"] = "https://pay.example.com/123",
    ["items"] = new ArrayValue(new TemplateValue[]
    {
        new ObjectValue { ["name"] = "Product 1", ["price"] = 500 },
        new ObjectValue { ["name"] = "Product 2", ["price"] = 1000 }
    })
};

// Render to bitmap (async API)
using var bitmap = await renderer.Render(template, data);

// Save to file
using var image = SKImage.FromBitmap(bitmap);
using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
using var stream = File.OpenWrite("receipt.png");
pngData.SaveTo(stream);
```

### 3. Or use CLI

```bash
flexrender render receipt.yaml -d data.json -o receipt.png
```

## Template Syntax

### Canvas Settings

```yaml
canvas:
  fixed: width          # or height - which dimension is fixed
  size: 300             # size in pixels
  background: "#ffffff" # background color
```

### Text Element

```yaml
- type: text
  content: "Hello {{name}}!"
  font: main            # font reference
  size: 1.2em           # pixels, em, or percentage
  color: "#000000"
  align: center         # left/center/right
  wrap: true
  overflow: ellipsis    # ellipsis/clip/visible
  maxLines: 2
  rotate: none          # none/left/right/flip or degrees
```

### Flex Container

```yaml
- type: flex
  direction: row        # row/column
  wrap: wrap            # nowrap/wrap/wrap-reverse
  gap: 10
  padding: 5%
  justify: space-between  # start/center/end/space-between/space-around/space-evenly
  align: center           # start/center/end/stretch/baseline
  children:
    - type: text
      content: "Item"
      flex:
        grow: 1
        shrink: 0
        basis: auto
```

### QR Code

```yaml
- type: qr
  data: "{{url}}"
  size: 100
  errorCorrection: M    # L/M/Q/H
  foreground: "#000000"
  background: "#ffffff"
```

### Barcode

```yaml
- type: barcode
  data: "{{ean13}}"
  format: ean13         # ean13/ean8/code128/code39/upc
  width: 200
  height: 80
  showText: true
  foreground: "#000000"
  background: "#ffffff"
```

### Image

```yaml
- type: image
  src: "logo.png"       # or base64 from data
  width: 80%
  height: auto
  fit: contain          # contain/cover/fill/none
```

## Template Expressions

### Variable Substitution

```yaml
# Simple variable
content: "Hello {{name}}"

# Nested access
content: "City: {{user.address.city}}"

# Array index
content: "First: {{items[0].name}}"
```

### Loops (type: each)

```yaml
# Iterate over array
- type: each
  array: items          # path to array in data
  as: item              # variable name for current item
  children:
    - type: text
      content: "{{item.name}}: {{item.price}}"

# Loop variables: @index, @first, @last
- type: each
  array: items
  as: item
  children:
    - type: text
      content: "{{@index}}. {{item.name}}"
```

### Conditionals (type: if)

```yaml
# Simple truthy check
- type: if
  condition: discount
  then:
    - type: text
      content: "Discount: {{discount}}%"

# With else branch
- type: if
  condition: premium
  then:
    - type: text
      content: "Premium member"
  else:
    - type: text
      content: "Regular member"

# Equality check
- type: if
  condition: status
  equals: "paid"
  then:
    - type: text
      content: "Payment received"
      color: "#22c55e"

# Chained else-if
- type: if
  condition: status
  equals: "paid"
  then:
    - type: text
      content: "PAID"
      color: "#22c55e"
  elseIf:
    condition: status
    equals: "pending"
    then:
      - type: qr
        data: "{{paymentUrl}}"
        size: 100
  else:
    - type: text
      content: "Payment required"
      color: "#ef4444"
```

## CLI Commands

```bash
# Render template
flexrender render template.yaml -d data.json -o output.png
flexrender render template.yaml -d data.json -o output.jpg --quality 90
flexrender render template.yaml -d data.json -o output.bmp

# With base path for resolving relative fonts/images
flexrender render templates/receipt.yaml --base-path templates -o output.png

# Validate template
flexrender validate template.yaml

# Show template info
flexrender info template.yaml

# Debug layout - shows element bounds and hierarchy
flexrender debug-layout template.yaml -d data.json

# Watch mode - re-render on changes
flexrender watch template.yaml -d data.json -o preview.png

# Global options
--verbose, -v       # Verbose output
--fonts <dir>       # Custom fonts directory
--scale <float>     # Scale factor (e.g., 2.0 for retina)
--base-path <dir>   # Base path for resolving relative file references
```

## API Reference

### TemplateParser

```csharp
var parser = new TemplateParser();

// Parse from string
Template template = parser.Parse(yamlString);

// Parse from file (with 1MB size limit)
Template template = parser.ParseFile("template.yaml");

// Check supported element types
IReadOnlyCollection<string> types = parser.SupportedElementTypes;
// Returns: ["text", "qr", "barcode", "image", "flex", "separator", "each", "if"]
```

### TemplateExpander

Templates with `type: each` and `type: if` are automatically expanded during rendering.
For manual expansion (useful for template caching):

```csharp
var expander = new TemplateExpander();

// Expand control flow elements with data
Template expanded = expander.Expand(template, data);

// The expanded template has no Each/If elements - they're replaced with concrete elements
// This allows parsing once and rendering with different data
```

### SkiaRenderer

```csharp
using var renderer = new SkiaRenderer();

// Set base font size (default: 12)
renderer.BaseFontSize = 14f;

// Measure required size
SKSize size = renderer.Measure(template, data);

// Render to canvas
renderer.Render(canvas, template, data);
renderer.Render(canvas, template, data, offset: new SKPoint(10, 10));

// Render to bitmap
renderer.Render(bitmap, template, data);

// Async render via ILayoutRenderer<SKBitmap>
using var bitmap = await renderer.Render(template, data);
```

### TemplateValue Types

```csharp
// String
TemplateValue str = "hello";           // implicit conversion
TemplateValue str = new StringValue("hello");

// Number
TemplateValue num = 42;                // implicit from int
TemplateValue num = 3.14;              // implicit from double
TemplateValue num = new NumberValue(42);

// Boolean
TemplateValue flag = true;             // implicit conversion
TemplateValue flag = new BoolValue(true);

// Null
TemplateValue nil = NullValue.Instance;

// Array
var array = new ArrayValue(new TemplateValue[] { "a", "b", "c" });
int count = array.Count;
TemplateValue first = array[0];

// Object
var obj = new ObjectValue
{
    ["name"] = "John",
    ["age"] = 30,
    ["active"] = true
};
TemplateValue name = obj["name"];
bool has = obj.ContainsKey("name");
```

## License

MIT
