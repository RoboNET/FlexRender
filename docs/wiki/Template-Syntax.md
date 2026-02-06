# Template Syntax

FlexRender templates are YAML files that define image layouts using a tree of typed elements. This page covers the template structure, all 8 element types, common properties, and supported units.

For template expressions (variables, loops, conditionals), see [[Template-Expressions]].
For flexbox layout properties, see [[Flexbox-Layout]].
For a complete property reference with examples for every element type, see [[Element-Reference]].
For visual examples with rendered images, see [[Visual-Reference]].

## Template Structure

```yaml
template:                     # Required: metadata
  name: "my-template"         # Template name (string)
  version: 1                  # Template version (int)

fonts:                        # Optional: font definitions
  default: "assets/fonts/Inter-Regular.ttf"
  bold: "assets/fonts/Inter-Bold.ttf"

canvas:                       # Required: canvas configuration
  fixed: width                # Which dimension is fixed
  width: 300                  # Canvas width in pixels
  background: "#ffffff"       # Background color

layout:                       # Required: array of elements
  - type: text
    content: "Hello!"
```

## Canvas Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `fixed` | FixedDimension | `width` | Which dimension is fixed: `width`, `height`, `both`, `none` |
| `width` | int | `300` | Canvas width in pixels |
| `height` | int | `0` | Canvas height (0 = auto when not fixed) |
| `background` | string | `"#ffffff"` | Background color in hex |
| `rotate` | string | `"none"` | Post-render rotation |

### Fixed Dimension

- `width` -- width is fixed, height expands to fit content (default)
- `height` -- height is fixed, width expands to fit content
- `both` -- both dimensions are fixed
- `none` -- both dimensions expand to fit content

### Canvas Rotation

| Value | Effect |
|-------|--------|
| `"none"` | No rotation |
| `"left"` | 270 degrees (90 CCW) -- swaps width/height |
| `"right"` | 90 degrees CW -- swaps width/height |
| `"flip"` | 180 degrees |
| `"<number>"` | Arbitrary degrees (e.g., `"45"`) |

Rotation is applied **after** rendering. For thermal printers: use `"right"` to rotate a wide receipt into a tall image.

## Fonts

Fonts are defined as key-value pairs. The key is a reference name used in `font:` properties, and the value is the font source:

```yaml
fonts:
  default: "assets/fonts/Inter-Regular.ttf"     # Local file
  bold: "assets/fonts/Inter-Bold.ttf"            # Local file
  icon: "embedded://MyApp.Fonts.icons.ttf"       # Embedded resource
  remote: "https://example.com/font.ttf"         # HTTP URL
```

## Color Format

Colors are specified in hex format:

- `#rrggbb` -- 6-digit hex (e.g., `"#ff0000"` for red)
- `#rgb` -- 3-digit hex shorthand (e.g., `"#f00"` for red)

---

## Element Types

### text

Renders text content with font, size, color, alignment, and wrapping options.

```yaml
- type: text
  content: "Hello, {{name}}!"
  font: bold
  size: 1.2em
  color: "#000000"
  align: center
  wrap: true
  overflow: ellipsis
  maxLines: 2
  lineHeight: "1.5"
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `content` | string | `""` | Text content, may contain `{{variable}}` expressions |
| `font` | string | `"main"` | Font reference name from `fonts` section |
| `size` | string | `"1em"` | Font size (px, em, %) |
| `color` | string | `"#000000"` | Text color in hex |
| `align` | TextAlign | `left` | Text alignment: `left`, `center`, `right` |
| `wrap` | bool | `true` | Whether text wraps to multiple lines |
| `overflow` | TextOverflow | `ellipsis` | Overflow handling: `ellipsis`, `clip`, `visible` |
| `maxLines` | int? | `null` | Maximum number of lines (null = unlimited) |
| `lineHeight` | string | `""` | Line height for multi-line text |

**lineHeight values:**

| Format | Example | Behavior |
|--------|---------|----------|
| Empty string | `""` | Use font-defined default spacing |
| Plain number | `"1.8"` | Multiplier of fontSize |
| Pixel units | `"24px"` | Absolute pixel value |
| Em units | `"2em"` | Relative to element's fontSize |

---

### flex

A container that lays out children using the flexbox algorithm. See [[Flexbox-Layout]] for detailed layout properties.

```yaml
- type: flex
  direction: row
  wrap: wrap
  gap: 10
  justify: space-between
  align: center
  padding: "20"
  background: "#f5f5f5"
  children:
    - type: text
      content: "Left"
    - type: text
      content: "Right"
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `direction` | FlexDirection | `column` | Main axis: `row`, `column`, `row-reverse`, `column-reverse` |
| `wrap` | FlexWrap | `nowrap` | Wrapping: `nowrap`, `wrap`, `wrap-reverse` |
| `gap` | string | `"0"` | Gap shorthand (sets both rowGap and columnGap) |
| `columnGap` | string? | `null` | Gap between items along the main axis |
| `rowGap` | string? | `null` | Gap between wrapped lines |
| `justify` | JustifyContent | `start` | Main axis alignment |
| `align` | AlignItems | `stretch` | Cross axis alignment |
| `alignContent` | AlignContent | `start` | Wrapped lines alignment |
| `overflow` | Overflow | `visible` | Content clipping: `visible`, `hidden` |
| `children` | element[] | `[]` | Child elements |

---

### image

Renders an image from a file, URL, base64 data, or embedded resource.

```yaml
# From file
- type: image
  src: "logo.png"
  width: 100
  height: 50

# From HTTP
- type: image
  src: "https://example.com/logo.png"

# From embedded resource
- type: image
  src: "embedded://MyApp.Resources.logo.png"

# From base64
- type: image
  src: "data:image/png;base64,iVBOR..."
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `src` | string | `""` | Image source: file path, `http://`, `embedded://`, base64 data URL |
| `width` | int? | `null` | Image container width (null = natural width) |
| `height` | int? | `null` | Image container height (null = natural height) |
| `fit` | ImageFit | `contain` | How the image fits within bounds |

**Image fit modes:**

| Mode | Description |
|------|-------------|
| `fill` | Stretch to fill bounds (may distort) |
| `contain` | Scale to fit within bounds preserving ratio (may have empty space) |
| `cover` | Scale to cover bounds preserving ratio (may be cropped) |
| `none` | Use image's natural size without scaling |

---

### qr

Renders a QR code. Requires the `FlexRender.QrCode` package and `.WithQr()` on the builder.

```yaml
- type: qr
  data: "{{paymentUrl}}"
  size: 120
  errorCorrection: M
  foreground: "#000000"
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `data` | string | `""` | Data to encode in the QR code |
| `size` | int | `100` | QR code size in pixels (width = height) |
| `errorCorrection` | ErrorCorrectionLevel | `M` | Error correction level |
| `foreground` | string | `"#000000"` | Foreground (module) color in hex |

**Error correction levels:**

| Level | Recovery Capacity |
|-------|------------------|
| `L` | ~7% recovery |
| `M` | ~15% recovery (default) |
| `Q` | ~25% recovery |
| `H` | ~30% recovery |

---

### barcode

Renders a barcode. Requires the `FlexRender.Barcode` package and `.WithBarcode()` on the builder.

```yaml
- type: barcode
  data: "{{sku}}"
  format: code128
  width: 200
  height: 80
  showText: true
  foreground: "#000000"
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `data` | string | `""` | Data to encode |
| `format` | BarcodeFormat | `code128` | Barcode format |
| `width` | int | `200` | Barcode width in pixels |
| `height` | int | `80` | Barcode height in pixels |
| `showText` | bool | `true` | Show encoded text below barcode |
| `foreground` | string | `"#000000"` | Bar color in hex |

**Barcode formats:**

| Format | Description |
|--------|-------------|
| `code128` | Alphanumeric, high density |
| `code39` | Alphanumeric, widely supported |
| `ean13` | 13 digits, retail |
| `ean8` | 8 digits, compact retail |
| `upc` | 12 digits, North American retail (UPC-A) |

---

### separator

Renders a horizontal or vertical line (solid, dashed, or dotted).

```yaml
- type: separator
  style: dashed
  color: "#cccccc"
  thickness: 1
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `orientation` | SeparatorOrientation | `horizontal` | Direction: `horizontal`, `vertical` |
| `style` | SeparatorStyle | `dotted` | Line style: `dotted`, `dashed`, `solid` |
| `thickness` | float | `1` | Line thickness in pixels |
| `color` | string | `"#000000"` | Line color in hex |

Horizontal separators stretch to full width and use thickness as height. Vertical separators stretch to full height and use thickness as width.

---

### each

Iterates over an array in the data, creating child elements for each item. See [[Template-Expressions]] for details.

```yaml
- type: each
  array: items
  as: item
  children:
    - type: text
      content: "{{@index}}. {{item.name}}: {{item.price}}"
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `array` | string | Yes | Path to array in data (e.g., `"items"`, `"order.lines"`) |
| `as` | string | No | Variable name for current item |
| `children` | element[] | Yes | Template elements to render per item |

---

### if

Conditional rendering based on data values. Supports 13 comparison operators. See [[Template-Expressions]] for details.

```yaml
- type: if
  condition: isPremium
  then:
    - type: text
      content: "Premium member"
  else:
    - type: text
      content: "Standard member"
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `condition` | string | Yes | Path to value for condition check |
| `then` | element[] | Yes | Elements when condition is true |
| `elseIf` | if element | No | Nested else-if condition |
| `else` | element[] | No | Elements when all conditions are false |

---

## Common Properties

All elements inherit these properties from `TemplateElement`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `padding` | string | `"0"` | Inner spacing (px, %, em, CSS shorthand) |
| `margin` | string | `"0"` | Outer spacing (px, %, em, auto, CSS shorthand) |
| `background` | string? | `null` | Background color in hex (null = transparent) |
| `rotate` | string | `"none"` | Element rotation (none/left/right/flip/degrees) |
| `display` | Display | `flex` | Display mode: `flex`, `none` |
| `grow` | float | `0` | Flex grow factor |
| `shrink` | float | `1` | Flex shrink factor |
| `basis` | string | `"auto"` | Flex basis (px, %, em, auto) |
| `alignSelf` | AlignSelf | `auto` | Per-item alignment override |
| `order` | int | `0` | Display order |
| `width` | string? | `null` | Explicit width (px, %, em, auto) |
| `height` | string? | `null` | Explicit height (px, %, em, auto) |
| `minWidth` | string? | `null` | Minimum width constraint |
| `maxWidth` | string? | `null` | Maximum width constraint |
| `minHeight` | string? | `null` | Minimum height constraint |
| `maxHeight` | string? | `null` | Maximum height constraint |
| `position` | Position | `static` | Positioning mode: `static`, `relative`, `absolute` |
| `top` | string? | `null` | Top inset for positioned elements |
| `right` | string? | `null` | Right inset for positioned elements |
| `bottom` | string? | `null` | Bottom inset for positioned elements |
| `left` | string? | `null` | Left inset for positioned elements |
| `aspectRatio` | float? | `null` | Width/height ratio constraint |

## Units

All size-based properties support the following units:

| Unit | Syntax | Description | Example |
|------|--------|-------------|---------|
| **px** | `"100"`, `"100px"` | Absolute pixels (default if no suffix) | `width: "100"` |
| **%** | `"50%"` | Percentage of parent container size | `width: "50%"` |
| **em** | `"1.5em"` | Relative to current font size | `size: "1.5em"` |
| **auto** | `"auto"`, `null` | Automatic sizing (context-dependent) | `width: "auto"` |

Plain numbers without suffix are treated as pixels. Empty or whitespace strings resolve to `auto`. Parsing is case-insensitive for unit suffixes.

## CSS Shorthand for Padding and Margin

Both `padding` and `margin` accept 1 to 4 space-separated values:

| Format | Meaning |
|--------|---------|
| `"20"` | All sides = 20 |
| `"20 40"` | Top/Bottom = 20, Left/Right = 40 |
| `"20 40 30"` | Top = 20, Left/Right = 40, Bottom = 30 |
| `"20 40 30 10"` | Top = 20, Right = 40, Bottom = 30, Left = 10 |

Each value can use a different unit: `"10px 5% 2em 20"`.

Margin additionally supports `auto` values for centering elements within flex containers. See [[Flexbox-Layout]] for auto margin details.

---

## Complete Example

A receipt template demonstrating multiple element types:

```yaml
template:
  name: "receipt"
  version: 1

fonts:
  default: "assets/fonts/Inter-Regular.ttf"
  bold: "assets/fonts/Inter-Bold.ttf"

canvas:
  fixed: width
  width: 320
  background: "#ffffff"

layout:
  - type: flex
    padding: "24 20"
    gap: 12
    children:
      # Header
      - type: text
        content: "{{shopName}}"
        font: bold
        size: 1.5em
        align: center
        color: "#1a1a1a"

      - type: separator
        style: dashed
        color: "#cccccc"

      # Items (dynamic loop)
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
                color: "#333333"
              - type: text
                content: "{{item.price}} $"
                color: "#333333"

      - type: separator
        style: solid
        color: "#1a1a1a"

      # Total
      - type: flex
        direction: row
        justify: space-between
        children:
          - type: text
            content: "TOTAL"
            font: bold
            size: 1.2em
          - type: text
            content: "{{total}} $"
            font: bold
            size: 1.2em

      # QR code
      - type: flex
        align: center
        children:
          - type: qr
            data: "{{paymentUrl}}"
            size: 120
            errorCorrection: M

      - type: text
        content: "Thank you for your purchase!"
        size: 0.85em
        align: center
        color: "#666666"
```

## See Also

- [[Element-Reference]] -- complete property reference for all element types
- [[Template-Expressions]] -- variables, loops, conditionals
- [[Flexbox-Layout]] -- layout engine details
- [[Render-Options]] -- rendering and format options
