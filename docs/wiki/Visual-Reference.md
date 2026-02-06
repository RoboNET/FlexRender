# Visual Reference

A comprehensive visual guide with rendered examples for all FlexRender properties and element types. Each section provides side-by-side comparisons showing how different property values affect the final rendered output.

## Table of Contents

- [Canvas Settings](#canvas-settings)
  - [fixed -- Canvas Sizing Mode](#fixed--canvas-sizing-mode)
  - [background -- Canvas Background Color](#background--canvas-background-color)
  - [rotate -- Canvas Rotation](#rotate--canvas-rotation)
  - [All Canvas Properties](#all-canvas-properties)
- [Flex Layout](#flex-layout)
  - [direction](#direction)
  - [justify](#justify)
  - [align](#align)
  - [wrap](#wrap)
  - [position](#position)
- [Element Types](#element-types)
  - [text](#text)
  - [separator](#separator)
  - [image](#image)
  - [qr](#qr)
  - [barcode](#barcode)
- [See Also](#see-also)

---

## Canvas Settings

The `canvas` section in a template defines the rendering surface: its dimensions, sizing behavior, background color, and post-render rotation. These settings control the overall output image before any layout elements are placed.

### fixed -- Canvas Sizing Mode

The `fixed` property determines which canvas dimensions are locked to explicit values and which adjust automatically to fit content.

| Value | Description | Visual Example |
|-------|-------------|----------------|
| `width` | Width is fixed; height grows or shrinks to fit content. This is the **default**. | ![fixed: width](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-fixed-width.png) |
| `height` | Height is fixed; width grows or shrinks to fit the widest content. | ![fixed: height](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-fixed-height.png) |
| `both` | Both width and height are fixed. Content that overflows is clipped. | ![fixed: both](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-fixed-both.png) |
| `none` | Neither dimension is fixed. Both width and height adjust to fit content exactly. | ![fixed: none](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-fixed-none.png) |

### background -- Canvas Background Color

The `background` property sets the base color of the canvas behind all rendered content. Accepts hex color strings.

![Canvas Background](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-background.png)

### rotate -- Canvas Rotation

The `rotate` property applies a post-render rotation to the entire canvas. The layout is computed in the original orientation, then the final image is rotated. For 90-degree and 270-degree rotations, the output image dimensions are swapped.

| Value | Degrees | Description | Visual Example |
|-------|---------|-------------|----------------|
| `none` | 0 | No rotation (default). | ![rotate: none](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-rotate.png) |
| `right` | 90 | Rotate 90 degrees clockwise. Width and height are swapped in the output. | ![rotate: right](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-rotate-right.png) |
| `flip` | 180 | Rotate 180 degrees. The image appears upside down. | ![rotate: flip](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-rotate-flip.png) |
| `left` | 270 | Rotate 270 degrees clockwise (90 degrees counter-clockwise). Width and height are swapped. | ![rotate: left](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/canvas-rotate-left.png) |

> **Note:** The `rotate` value is applied **after** the entire layout is rendered. All element sizes, positions, and text wrapping are computed in the pre-rotation coordinate system. The `rotate` property also accepts arbitrary numeric degree values (e.g., `45`), though named values cover the most common use cases. For thermal printers, use `rotate: right` to convert a wide horizontal layout into a tall vertical image suitable for paper roll printing.

### All Canvas Properties

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `fixed` | enum | Sizing mode: `width`, `height`, `both`, `none` | `width` |
| `width` | integer | Canvas width in pixels | `300` |
| `height` | integer | Canvas height in pixels | `0` (auto) |
| `background` | color | Background color in hex format (e.g., `#ffffff`) | `#ffffff` |
| `rotate` | string | Post-render rotation: `none`, `left`, `right`, `flip`, or numeric degrees | `none` |

---

## Flex Layout

### direction

The `direction` property defines the main axis direction in which flex items are laid out within a flex container. It determines whether items flow horizontally or vertically, and in what order.

| Value | Description | Visual Example |
|-------|-------------|----------------|
| `row` | Items are laid out horizontally from left to right. This is the default behavior. The main axis runs horizontally. | ![direction: row](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/direction-row.png) |
| `column` | Items are laid out vertically from top to bottom. The main axis runs vertically. | ![direction: column](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/direction-column.png) |
| `row-reverse` | Items are laid out horizontally from right to left. The main axis runs horizontally in reverse. | ![direction: row-reverse](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/direction-row-reverse.png) |
| `column-reverse` | Items are laid out vertically from bottom to top. The main axis runs vertically in reverse. | ![direction: column-reverse](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/direction-column-reverse.png) |

### justify

The `justify` property defines how flex items are aligned along the main axis of a flex container. It distributes extra free space when items don't fill the entire container width (for `direction: row`) or height (for `direction: column`).

| Value | Description | Visual Example |
|-------|-------------|----------------|
| `start` | Items are packed toward the start of the flex container's main axis. This is the default behavior. | ![justify: start](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/justify-start.png) |
| `center` | Items are centered along the main axis, with equal space on both sides. | ![justify: center](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/justify-center.png) |
| `end` | Items are packed toward the end of the flex container's main axis. | ![justify: end](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/justify-end.png) |
| `space-between` | Items are evenly distributed with the first item at the start and the last item at the end. Remaining space is distributed evenly between items. | ![justify: space-between](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/justify-space-between.png) |
| `space-around` | Items are evenly distributed with equal space around each item. Note that the space at the edges is half the space between items. | ![justify: space-around](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/justify-space-around.png) |
| `space-evenly` | Items are evenly distributed with equal space between all items and at the edges. | ![justify: space-evenly](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/justify-space-evenly.png) |

### align

The `align` property defines how flex items are aligned along the cross axis of a flex container. For `direction: row`, the cross axis is vertical. For `direction: column`, the cross axis is horizontal.

| Value | Description | Visual Example |
|-------|-------------|----------------|
| `stretch` | Items are stretched to fill the container's cross-axis. This is the default behavior. Items without an explicit cross-axis size will expand to fill available space. | ![align: stretch](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/align-stretch.png) |
| `start` | Items are aligned at the start of the cross axis. Items maintain their intrinsic cross-axis size. | ![align: start](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/align-start.png) |
| `center` | Items are centered along the cross axis, with equal space above and below (for row) or left and right (for column). | ![align: center](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/align-center.png) |
| `end` | Items are aligned at the end of the cross axis. Items are positioned at the bottom (for row) or right (for column). | ![align: end](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/align-end.png) |
| `baseline` | Items are aligned such that their text baselines line up. Particularly useful when items contain text of different sizes. | ![align: baseline](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/align-baseline.png) |

### wrap

The `wrap` property controls whether flex items are forced onto a single line or can wrap onto multiple lines within a flex container. It determines how items behave when they exceed the container's main axis size.

| Value | Description | Visual Example |
|-------|-------------|----------------|
| `nowrap` | All items are laid out on a single line, even if they overflow the container. This is the default behavior. Items may shrink to fit. | ![wrap: nowrap](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/wrap-nowrap.png) |
| `wrap` | Items wrap onto multiple lines from top to bottom (for `row`) or left to right (for `column`). New lines are added in the natural direction. | ![wrap: wrap](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/wrap-wrap.png) |
| `wrap-reverse` | Items wrap onto multiple lines from bottom to top (for `row`) or right to left (for `column`). New lines are added in reverse order. | ![wrap: wrap-reverse](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/wrap-wrap-reverse.png) |

### position

The `position` property controls how an element is positioned within its container. FlexRender supports three positioning modes matching CSS behavior: `static` (default), `relative`, and `absolute`.

#### static (default)

Element follows normal flex flow. No offset properties are applied.

#### relative

Element is offset from its normal flow position. Siblings are not affected.

| Example | Description | Visual |
|---------|-------------|--------|
| Offset | Middle box shifted with `top: 15, left: 20` from normal position | ![relative offset](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-relative-offset.png) |

#### absolute

Element is removed from flex flow and positioned relative to its containing flex parent's padding box.

| Example | Description | Visual |
|---------|-------------|--------|
| Top/Left | Box pinned to `top: 10, left: 10` | ![absolute top-left](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-absolute-top-left.png) |
| Bottom/Right | Box pinned to `bottom: 10, right: 10` | ![absolute bottom-right](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-absolute-bottom-right.png) |
| Centered | Box centered via equal insets on all sides | ![absolute centered](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-absolute-center.png) |
| Flow Exclusion | Absolute elements excluded from flex flow -- A, C, D stack normally | ![flow exclusion](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-flow-exclusion.png) |
| Inset Sizing | Width/height computed from opposing insets (no explicit size) | ![inset sizing](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-inset-sizing.png) |

#### Practical Patterns

| Pattern | Description | Visual |
|---------|-------------|--------|
| Badge | Star badge overlaid on product card with `top: 8, right: 8` | ![badge](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-badge.png) |
| Text Overlay | Dark bar with text at bottom of image using `bottom: 0, left: 0, right: 0` | ![overlay](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-overlay.png) |
| Floating Label | Input label floats above border with `top: -8, left: 12` | ![floating label](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/position-floating-label.png) |

#### Inset Properties

| Property | Type | Description |
|----------|------|-------------|
| `top` | string | Offset from top edge of containing block's padding box |
| `right` | string | Offset from right edge |
| `bottom` | string | Offset from bottom edge |
| `left` | string | Offset from left edge |

**Priority rules:**
- `left` takes priority over `right` when both are specified (for `position: relative`)
- `top` takes priority over `bottom` when both are specified (for `position: relative`)
- For `position: absolute`, opposing insets (`left` + `right` or `top` + `bottom`) without explicit size compute the element's width/height (inset sizing)

---

## Element Types

### text

The `text` element is used to display styled text content in your templates.

#### Text Alignment

The `align` property controls horizontal text alignment within its container.

**Values:** `left`, `center`, `right`

![Text Alignment](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/text-align.png)

#### Text Wrapping

The `wrap` property controls whether text wraps to multiple lines or overflows.

**Values:** `true`, `false`

![Text Wrapping](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/text-wrap.png)

#### Max Lines & Overflow

The `maxLines` property limits text to a specific number of lines. Use with `overflow: ellipsis` to show truncation.

**Values:** Any positive integer

![Max Lines](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/text-maxLines.png)

#### Line Height

The `lineHeight` property controls vertical spacing between lines of text.

**Values:** Decimal numbers (e.g., `1.0`, `1.5`, `2.0`)

![Line Height](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/text-lineHeight.png)

#### All Text Properties

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `content` | string | Text to display | (required) |
| `align` | enum | Horizontal alignment: `left`, `center`, `right` | `left` |
| `size` | length | Font size (e.g., `14px`, `1em`) | `14px` |
| `color` | color | Text color | `#000000` |
| `font` | string | Font variant (e.g., `default`, `bold`, `semibold`) | `default` |
| `wrap` | boolean | Enable text wrapping | `false` |
| `maxLines` | integer | Maximum number of lines | (unlimited) |
| `overflow` | enum | Overflow behavior: `clip`, `ellipsis` | `clip` |
| `lineHeight` | number | Line height multiplier | `1.0` |
| `background` | color | Background color | (transparent) |
| `padding` | length | Internal spacing | `0` |

### separator

The `separator` element creates horizontal or vertical dividing lines between content sections.

#### Styles

The `style` property controls the visual appearance of the line.

**Values:** `solid`, `dashed`, `dotted`

![Separator Styles](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/separator-styles.png)

#### Orientation

The `orientation` property controls whether the separator is horizontal or vertical.

**Values:** `horizontal`, `vertical`

![Separator Orientation](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/separator-orientation.png)

> **Note:** Vertical separators work best inside flex containers with `direction: row` and `align: stretch`.

#### Thickness

The `thickness` property controls the width of the separator line.

**Values:** Any positive number (in pixels)

![Separator Thickness](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/separator-thickness.png)

#### All Separator Properties

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `style` | enum | Line style: `solid`, `dashed`, `dotted` | `solid` |
| `orientation` | enum | Direction: `horizontal`, `vertical` | `horizontal` |
| `thickness` | number | Line thickness in pixels | `1` |
| `color` | color | Line color | `#000000` |
| `height` | length | Fixed height (for vertical separators) | (auto) |

### image

The `image` element displays images from HTTP URLs or local file paths.

#### Fit Modes

The `fit` property controls how images are sized and positioned within their container.

##### Contain

Scales the image to fit inside the container while maintaining aspect ratio. The entire image is visible, and letterboxing may occur.

![Image Fit: Contain](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/image-contain.png)

##### Cover

Scales the image to fill the entire container while maintaining aspect ratio. The image may be cropped to fit.

![Image Fit: Cover](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/image-cover.png)

##### Fill

Stretches the image to fill the container exactly. Aspect ratio is NOT maintained and the image may appear distorted.

![Image Fit: Fill](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/image-fill.png)

##### None

Displays the image at its natural size without any scaling. The image may overflow the container.

![Image Fit: None](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/image-none.png)

#### All Image Properties

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `src` | string | Image URL (HTTP/HTTPS) or local file path | (required) |
| `fit` | enum | Fit mode: `contain`, `cover`, `fill`, `none` | `contain` |
| `width` | length | Container width | (auto) |
| `height` | length | Container height | (auto) |

### qr

The `qr` element generates QR codes dynamically from text data.

#### Error Correction Levels

The `errorCorrection` property controls the amount of redundancy in the QR code, allowing it to remain readable even if partially damaged.

**Values:** `L` (Low ~7%), `M` (Medium ~15%), `Q` (Quartile ~25%), `H` (High ~30%)

![QR Code Error Correction](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/qr-errorCorrection.png)

#### Custom Colors

QR codes support custom foreground and background colors.

![QR Code Colors](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/qr-colors.png)

#### All QR Code Properties

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `data` | string | Text content to encode | (required) |
| `size` | number | QR code size in pixels | `100` |
| `errorCorrection` | enum | Error correction: `L`, `M`, `Q`, `H` | `M` |
| `foreground` | color | QR code foreground color | `#000000` |
| `background` | color | QR code background color | `#ffffff` |

### barcode

The `barcode` element generates linear barcodes (Code 128) dynamically from text data.

#### Show Text Label

The `showText` property controls whether the human-readable barcode value is displayed below the barcode.

**Values:** `true`, `false`

![Barcode Show Text](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/barcode-showText.png)

#### Custom Colors

Barcodes support custom foreground and background colors.

![Barcode Colors](https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/visual-docs/output/barcode-colors.png)

#### All Barcode Properties

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `data` | string | Text content to encode | (required) |
| `format` | enum | Barcode format (currently only `code128`) | `code128` |
| `width` | number | Barcode width in pixels | `200` |
| `height` | number | Barcode height in pixels | `50` |
| `showText` | boolean | Display human-readable text below barcode | `true` |
| `foreground` | color | Barcode line color | `#000000` |
| `background` | color | Barcode background color | `#ffffff` |

---

## See Also

- [[Flexbox-Layout]] - Complete flexbox layout engine documentation
- [[Element-Reference]] - Detailed property reference for all element types
- [[Template-Syntax]] - Template structure, canvas settings, and element types
