# Flexbox Layout

FlexRender implements a CSS-compatible flexbox layout engine based on the [Yoga](https://yogalayout.dev/) reference implementation. The engine uses a two-pass algorithm: intrinsic measurement (bottom-up) followed by layout computation (top-down).

> **Visual Learner?** Check out the [[Visual-Reference]] page for interactive examples with side-by-side comparisons of all flex properties.

## How It Works

### Two-Pass Layout Engine

1. **Pass 1 -- Intrinsic Measurement** (`MeasureAllIntrinsics`): bottom-up traversal computes `IntrinsicSize` (MinWidth, MaxWidth, MinHeight, MaxHeight) for every element. Uses a `TextMeasurer` delegate for content-based text sizing.

2. **Pass 2 -- Layout** (`ComputeLayout`): top-down traversal assigns positions and sizes, producing a `LayoutNode` tree with (X, Y, Width, Height).

---

## Direction

The `direction` property sets the main axis for child layout:

| Value | Main Axis | Cross Axis |
|-------|-----------|------------|
| `column` (default) | Vertical (top to bottom) | Horizontal |
| `row` | Horizontal (left to right) | Vertical |
| `column-reverse` | Vertical (bottom to top) | Horizontal |
| `row-reverse` | Horizontal (right to left) | Vertical |

```yaml
# Horizontal layout
- type: flex
  direction: row
  children:
    - type: text
      content: "Left"
    - type: text
      content: "Center"
    - type: text
      content: "Right"

# Reversed column (bottom to top)
- type: flex
  direction: column-reverse
  children:
    - type: text
      content: "This appears at the bottom"
    - type: text
      content: "This appears at the top"
```

---

## Justify Content

Controls alignment of items along the **main axis** using remaining free space.

| Value | Description |
|-------|-------------|
| `start` (default) | Items packed at start |
| `center` | Items centered |
| `end` | Items packed at end |
| `space-between` | Equal space between items, no space at edges |
| `space-around` | Equal space around items (half-size at edges) |
| `space-evenly` | Equal space everywhere (including edges) |

```yaml
# Space between items
- type: flex
  direction: row
  justify: space-between
  width: "300"
  children:
    - type: text
      content: "Left"
    - type: text
      content: "Right"

# Center items
- type: flex
  direction: row
  justify: center
  children:
    - type: text
      content: "Centered content"
```

**Overflow fallback:** when children overflow the container (`freeSpace < 0`), distribution-based values fall back to `start`:

| Original | Fallback |
|----------|----------|
| `space-between` | `start` |
| `space-around` | `start` |
| `space-evenly` | `start` |

---

## Align Items

Controls alignment of items along the **cross axis** within a flex line.

| Value | Description |
|-------|-------------|
| `stretch` (default) | Items stretch to fill the cross axis |
| `start` | Items at cross-axis start |
| `center` | Items centered on cross axis |
| `end` | Items at cross-axis end |
| `baseline` | Items aligned by text baselines |

```yaml
# Vertically center items in a row
- type: flex
  direction: row
  align: center
  height: "100"
  children:
    - type: text
      content: "Vertically centered"
      size: 1.5em
    - type: text
      content: "Also centered"
      size: 0.8em
```

### Align Self

Override the parent's `align` for a specific child:

```yaml
- type: flex
  direction: row
  align: stretch
  height: "100"
  children:
    - type: text
      content: "Stretched (default)"
    - type: text
      content: "Centered"
      alignSelf: center
    - type: text
      content: "At the end"
      alignSelf: end
```

| Value | Description |
|-------|-------------|
| `auto` (default) | Use parent's `align` value |
| `start` | Align to cross-axis start |
| `center` | Center on cross axis |
| `end` | Align to cross-axis end |
| `stretch` | Stretch to fill cross axis |
| `baseline` | Align by text baseline |

---

## Align Content

Controls distribution of **flex lines** along the cross axis in a multi-line (wrapping) container. Only has effect when `wrap` is `wrap` or `wrap-reverse` and there are multiple lines.

| Value | Description |
|-------|-------------|
| `start` (default) | Lines packed at start |
| `center` | Lines centered |
| `end` | Lines at end |
| `stretch` | Lines stretch to fill |
| `space-between` | Space between lines |
| `space-around` | Space around lines |
| `space-evenly` | Even distribution |

```yaml
- type: flex
  direction: row
  wrap: wrap
  alignContent: space-between
  width: "300"
  height: "200"
  children:
    # Items that wrap to multiple lines
    - type: text
      width: "150"
      content: "Item 1"
    - type: text
      width: "150"
      content: "Item 2"
    - type: text
      width: "150"
      content: "Item 3"
```

---

## Flex Item Properties

### Grow

Determines how much remaining free space the item receives. Default is `0` (no growth).

```yaml
- type: flex
  direction: row
  width: "300"
  children:
    - type: text
      grow: 1
      content: "Takes remaining space"
    - type: text
      width: "100"
      content: "Fixed 100px"
```

Multiple items with grow share space proportionally:

```yaml
- type: flex
  direction: row
  width: "300"
  children:
    - type: text
      grow: 1
      content: "1/3"    # Gets 1/3 of free space
    - type: text
      grow: 2
      content: "2/3"    # Gets 2/3 of free space
```

**Factor flooring:** when `0 < totalGrowFactors < 1`, the total is floored to `1.0` to ensure all free space is distributed.

### Shrink

Determines how much an item shrinks when children overflow the container. Default is `1`.

```yaml
- type: flex
  direction: row
  width: "200"
  children:
    - type: text
      shrink: 0           # Will NOT shrink
      width: "150"
      content: "Fixed"
    - type: text
      shrink: 1           # Absorbs all overflow
      width: "150"
      content: "Shrinks"
```

Shrink uses a basis-scaled formula (CSS spec): items with larger basis shrink more in absolute pixels than items with smaller basis, even at the same shrink factor.

### Basis

Sets the initial main-axis size before grow/shrink distribution. Default is `"auto"`.

```yaml
- type: flex
  direction: row
  width: "300"
  children:
    - type: text
      basis: "100"
      grow: 1
      content: "Starts at 100px, grows"
    - type: text
      basis: "50%"
      content: "50% of container"
```

**Resolution priority:**
1. If `basis` is explicitly set (not `auto`) -- use the basis value
2. If `basis` is `auto` and a main-axis dimension is set (e.g., `width` for row) -- use that dimension
3. Otherwise -- use the intrinsic (measured) size

---

## Gap

Adds spacing between children. Gap is NOT added before the first child or after the last.

```yaml
# Uniform gap
- type: flex
  gap: 10
  children:
    - type: text
      content: "A"
    - type: text
      content: "B"
    - type: text
      content: "C"
```

### Row Gap and Column Gap

Separate gap values for wrapped layouts:

```yaml
- type: flex
  direction: row
  wrap: wrap
  columnGap: 10      # Gap between items in a row
  rowGap: 20         # Gap between wrapped lines
  width: "300"
  children:
    - type: text
      width: "140"
      content: "Item 1"
    - type: text
      width: "140"
      content: "Item 2"
    - type: text
      width: "140"
      content: "Item 3"
```

**Gap-to-axis mapping:**

| Direction | Main-axis gap | Cross-axis gap |
|-----------|---------------|----------------|
| `row` / `row-reverse` | `columnGap` (or `gap`) | `rowGap` (or `gap`) |
| `column` / `column-reverse` | `rowGap` (or `gap`) | `columnGap` (or `gap`) |

---

## Wrapping

### wrap

```yaml
- type: flex
  direction: row
  wrap: wrap
  width: "300"
  children:
    - type: text
      width: "150"
      content: "Item 1"
    - type: text
      width: "150"
      content: "Item 2"
    - type: text
      width: "150"
      content: "Item 3"    # Wraps to new line
```

| Value | Description |
|-------|-------------|
| `nowrap` (default) | All items on a single line; may overflow or shrink |
| `wrap` | Items wrap to additional lines when they exceed the container |
| `wrap-reverse` | Items wrap, but lines are placed in reverse cross-axis order |

The line-breaking algorithm is greedy (matches Yoga): items are added to the current line until the next item would cause overflow, then a new line starts.

**Resource limit:** `MaxFlexLines` (default: 1000) prevents resource exhaustion from templates with thousands of tiny wrapped items.

---

## Min/Max Constraints

Constrain element sizes after flex resolution:

```yaml
- type: text
  grow: 1
  minWidth: "50"
  maxWidth: "200"
  content: "Clamped between 50-200px"
```

| Property | Description |
|----------|-------------|
| `minWidth` | Minimum width (px, %, em) |
| `maxWidth` | Maximum width (px, %, em) |
| `minHeight` | Minimum height (px, %, em) |
| `maxHeight` | Maximum height (px, %, em) |

**ClampSize rule** (CSS spec): if `min > max`, then `min` takes priority.

When min/max constraints cause items to freeze at their constrained sizes, remaining free space is redistributed among unfrozen items (iterative freeze algorithm).

---

## Auto Margins

Margins support the `auto` keyword for centering and pushing elements:

```yaml
# Center horizontally
- type: text
  margin: "0 auto"
  width: "100"
  content: "Centered"

# Push to the right
- type: text
  margin: "0 0 0 auto"
  content: "Right-aligned"

# Center both axes
- type: text
  margin: "auto"
  content: "Centered everywhere"
```

**Behavior:**
- Auto margins on the main axis consume free space **before** `justify-content` is applied
- When any item has an auto margin on the main axis, `justify-content` is effectively overridden
- Auto margins on the cross axis override `align-items`/`align-self` for that item
- If free space is zero or negative, auto margins resolve to `0`

---

## Positioning

### Static (default)

Normal flex flow. Inset properties (`top`, `right`, `bottom`, `left`) are ignored.

### Relative

Element is positioned in normal flex flow first, then offset by inset values. It still occupies space in the flow.

```yaml
- type: text
  position: relative
  left: "5"
  top: "3"
  content: "Offset 5px right, 3px down"
```

- `left` defined: shift right by `left` value
- `right` defined (no `left`): shift left by `right` value
- `top` defined: shift down by `top` value
- `bottom` defined (no `top`): shift up by `bottom` value
- `left` takes priority over `right`; `top` takes priority over `bottom`

### Absolute

Removed from the flex flow entirely. Positioned relative to the direct parent container.

```yaml
- type: flex
  width: "300"
  height: "200"
  children:
    - type: text
      content: "Normal flow"
    - type: text
      position: absolute
      top: "10"
      right: "10"
      content: "Top-right corner"
      background: "#ff0000"
      color: "#ffffff"
      padding: "2 8"
```

Absolute children are excluded from flex line calculation, flex basis computation, and justify-content/align-items distribution.

When both opposing insets are defined (e.g., `left` + `right`) and no explicit dimension is set, the child's size is computed from the insets.

---

## Overflow

Controls rendering of content that exceeds the container's bounds. This is a **renderer concern only** -- it does not affect layout calculations.

```yaml
- type: flex
  overflow: hidden
  width: "100"
  height: "100"
  children:
    - type: text
      content: "This long text will be clipped at the container bounds"
```

| Value | Description |
|-------|-------------|
| `visible` (default) | Content renders outside bounds |
| `hidden` | Content is clipped at the container's edges |

Only `FlexElement` supports the `overflow` property. `overflow: scroll` is not supported.

---

## Aspect Ratio

Enforces a width-to-height ratio. When one dimension is known, the other is computed automatically:

```yaml
# 16:9 container
- type: flex
  width: "320"
  aspectRatio: 1.7778
  background: "#000000"

# Square element
- type: flex
  width: "100"
  aspectRatio: 1
  background: "#f0f0f0"
```

---

## Display

Controls whether an element participates in layout:

| Value | Description |
|-------|-------------|
| `flex` (default) | Element participates in layout |
| `none` | Element is removed from layout flow entirely |

```yaml
- type: text
  display: none
  content: "This is hidden"
```

---

## Order

The `order` property controls the visual display order of flex items. Items are sorted by their `order` value before layout -- lower values appear first. Items with the same `order` value preserve their source (document) order (stable sort). The default is `0`. Negative values are supported.

Absolute-positioned children are excluded from order sorting and always render after flow children.

```yaml
# Source order: A, B, C -- display order: B (0), C (1), A (2)
- type: flex
  direction: row
  children:
    - type: text
      content: "A"
      order: 2
    - type: text
      content: "B"
      order: 0
    - type: text
      content: "C"
      order: 1
```

```yaml
# Negative order: B (-1) moves to front
- type: flex
  direction: row
  children:
    - type: text
      content: "A"
      order: 0
    - type: text
      content: "B"
      order: -1
    - type: text
      content: "C"
      order: 0
```

---

## Borders

All elements support CSS-like border properties. Borders consume space in the layout (they increase the element's total size alongside padding).

**Shorthand format:** `"width style color"` (e.g., `"2 solid #333"`)

```yaml
# Uniform border on all sides
- type: flex
  border: "2 solid #333333"
  padding: "16"
  children:
    - type: text
      content: "Bordered container"

# Per-side borders with different colors
- type: flex
  border-top: "3 solid #3498db"
  border-right: "2 dashed #e74c3c"
  border-bottom: "3 solid #2ecc71"
  border-left: "2 dashed #f39c12"
  padding: "16"
  children:
    - type: text
      content: "Different sides"

# Rounded corners
- type: flex
  border: "2 solid #3498db"
  border-radius: "12"
  padding: "16"
  children:
    - type: text
      content: "Rounded"
```

**CSS cascade order:**
1. `border` shorthand sets all four sides
2. `border-width`, `border-color`, `border-style` override individual properties on all sides
3. `border-top`, `border-right`, `border-bottom`, `border-left` override specific sides

---

## Layout Enums Reference

| Enum | Values |
|------|--------|
| `FlexDirection` | `row`, `column`, `row-reverse`, `column-reverse` |
| `FlexWrap` | `nowrap`, `wrap`, `wrap-reverse` |
| `JustifyContent` | `start`, `center`, `end`, `space-between`, `space-around`, `space-evenly` |
| `AlignItems` | `start`, `center`, `end`, `stretch`, `baseline` |
| `AlignContent` | `start`, `center`, `end`, `stretch`, `space-between`, `space-around`, `space-evenly` |
| `AlignSelf` | `auto`, `start`, `center`, `end`, `stretch`, `baseline` |
| `Display` | `flex`, `none` |
| `Position` | `static`, `relative`, `absolute` |
| `Overflow` | `visible`, `hidden` |

## RTL (Right-to-Left) Support

FlexRender supports RTL layout for Arabic, Hebrew, and other right-to-left scripts.

### Canvas Direction

Set `text-direction: rtl` on the canvas to make the entire template RTL:

```yaml
canvas:
  width: 400
  text-direction: rtl
```

### Per-Element Override

Individual elements can override the inherited direction:

```yaml
canvas:
  text-direction: rtl
layout:
  - type: flex
    text-direction: ltr  # Override to LTR for this subtree
    children: [...]
```

### How Direction Affects Layout

| Layout | LTR | RTL |
|--------|-----|-----|
| `row` | Left to right | Right to left |
| `row-reverse` | Right to left | Left to right |
| `column` | No change | No change |
| `column-reverse` | No change | No change |

### Logical Text Alignment

Use `start` and `end` for direction-aware alignment:

| Value | LTR resolves to | RTL resolves to |
|-------|-----------------|-----------------|
| `start` | `left` | `right` |
| `end` | `right` | `left` |
| `left` | `left` | `left` |
| `right` | `right` | `right` |

### HarfBuzz Text Shaping

For proper Arabic and Hebrew glyph shaping (ligatures, contextual forms), use the optional `FlexRender.HarfBuzz` package:

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia.WithHarfBuzz())
    .Build();
```

Without HarfBuzz, text renders left-to-right glyph-by-glyph. With HarfBuzz, text is shaped correctly with proper glyph substitution and positioning.

---

## Known Differences from CSS/Yoga

| Area | FlexRender | CSS/Yoga |
|------|-----------|----------|
| `order` | Items sorted by `order` (stable sort, absolute children excluded) | Items sorted by `order` |
| `display` | Only `flex` and `none` | Full CSS display values |
| `overflow` | Only `visible` and `hidden` | Also `scroll`, `auto` |
| Borders | Shorthand and per-side borders with radius. Borders affect sizing. | Borders affect sizing |
| RTL / writing modes | LTR and RTL via `text-direction` property | Full bidirectional support |
| Containing block | Direct parent | Nearest positioned ancestor |
| `min-content` / `max-content` | Not supported | Intrinsic sizing keywords |

## See Also

- [[Visual-Reference]] -- interactive property examples with rendered images
- [[Element-Reference]] -- complete property reference for all element types
- [[Template-Syntax]] -- element types and properties
- [[Render-Options]] -- rendering options
- [[API-Reference]] -- ResourceLimits for MaxFlexLines
