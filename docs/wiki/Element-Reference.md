# Element Reference

Complete reference for all FlexRender element types. Every supported property is documented with type, default value, valid values, and examples.

> **Want to see it in action?** The [[Visual-Reference]] page has rendered examples for every element type and property.

For layout concepts (justify, align, grow/shrink), see [[Flexbox-Layout]].
For template expressions (variables, loops, conditionals), see [[Template-Expressions]].
For rendering options (antialiasing, format settings), see [[Render-Options]].

---

## Common Properties (TemplateElement)

All 9 element types (`flex`, `text`, `image`, `qr`, `barcode`, `separator`, `table`, `each`, `if`) inherit these properties from the base `TemplateElement` class. You can use any of them on any element.

### Size Properties

Control the explicit dimensions and constraints of an element. All size properties accept values in `px`, `%`, `em`, or `auto`. Plain numbers without a suffix are treated as pixels.

| Property | YAML Name | Type | Default | Valid Values | Description |
|----------|-----------|------|---------|--------------|-------------|
| Width | `width` | string? | `null` | px, %, em, auto | Explicit width. `null` means auto-sized by content or flex. |
| Height | `height` | string? | `null` | px, %, em, auto | Explicit height. `null` means auto-sized by content or flex. |
| MinWidth | `minWidth` or `min-width` | string? | `null` | px, %, em | Minimum width constraint. If `min > max`, min wins (CSS spec). |
| MaxWidth | `maxWidth` or `max-width` | string? | `null` | px, %, em | Maximum width constraint. |
| MinHeight | `minHeight` or `min-height` | string? | `null` | px, %, em | Minimum height constraint. If `min > max`, min wins (CSS spec). |
| MaxHeight | `maxHeight` or `max-height` | string? | `null` | px, %, em | Maximum height constraint. |

```yaml
# Fixed 200px width
- type: text
  width: "200"
  content: "Fixed width box"

# Percentage of parent
- type: text
  width: "50%"
  content: "Half the parent width"

# Em-based sizing (relative to font size)
- type: text
  width: "20em"
  content: "Width relative to font size"

# Auto width (default behavior)
- type: text
  width: "auto"
  content: "Sized to content"

# Constrained: will grow but never exceed 300px or shrink below 100px
- type: text
  grow: 1
  minWidth: "100"
  maxWidth: "300"
  content: "Constrained text"
```

### Spacing Properties

Padding adds space inside the element; margin adds space outside.

| Property | YAML Name | Type | Default | Valid Values | Description |
|----------|-----------|------|---------|--------------|-------------|
| Padding | `padding` | string | `"0"` | CSS shorthand (1-4 values), each in px, %, em | Inner spacing between the element boundary and its content. |
| Margin | `margin` | string | `"0"` | CSS shorthand (1-4 values), each in px, %, em, auto | Outer spacing around the element. Supports `auto` for centering. |

Both `padding` and `margin` accept CSS shorthand notation with 1 to 4 space-separated values:

| Format | Meaning | Example |
|--------|---------|---------|
| 1 value | All sides | `"20"` -- 20px on all sides |
| 2 values | Vertical Horizontal | `"10 20"` -- 10px top/bottom, 20px left/right |
| 3 values | Top Horizontal Bottom | `"10 20 30"` -- 10px top, 20px left/right, 30px bottom |
| 4 values | Top Right Bottom Left | `"10 20 30 40"` -- clockwise from top |

Each value can use a different unit:

```yaml
# Uniform padding
- type: flex
  padding: "16"
  children:
    - type: text
      content: "16px padding on all sides"

# Vertical and horizontal padding
- type: flex
  padding: "24 20"
  children:
    - type: text
      content: "24px top/bottom, 20px left/right"

# Three values
- type: flex
  padding: "10 20 30"
  children:
    - type: text
      content: "10px top, 20px sides, 30px bottom"

# Four values with mixed units
- type: flex
  padding: "10px 5% 2em 20"
  children:
    - type: text
      content: "Mixed units"

# Auto margin for horizontal centering
- type: text
  width: "200"
  margin: "0 auto"
  content: "Horizontally centered"

# Auto margin to push element to the right
- type: text
  margin: "0 0 0 auto"
  content: "Pushed to the right"

# Auto margin for full centering (both axes)
- type: text
  margin: "auto"
  content: "Centered on both axes"
```

**Auto margins:** When `auto` is used on the main axis, free space is distributed to auto margins **before** `justify` is applied. This means `auto` margins effectively override `justify`. On the cross axis, `auto` margins override `align`/`alignSelf`. If free space is zero or negative, auto margins resolve to `0`.

### Flex Item Properties

Control how the element behaves as a child within a `flex` container.

| Property | YAML Name | Type | Default | Valid Values | Description |
|----------|-----------|------|---------|--------------|-------------|
| Grow | `grow` | float | `0` | >= 0 | How much free space the item absorbs. `0` means no growth. |
| Shrink | `shrink` | float | `1` | >= 0 | How much the item shrinks when overflowing. `0` means it will not shrink. |
| Basis | `basis` | string | `"auto"` | px, %, em, auto | Initial main-axis size before grow/shrink. |
| AlignSelf | `alignSelf` | AlignSelf | `auto` | auto, start, center, end, stretch, baseline | Override parent's `align` for this item. |
| Order | `order` | int | `0` | any integer | Display order for sorting. Lower values appear first. Negative values allowed. |
| Display | `display` | Display | `flex` | flex, none | `none` removes the element from layout entirely. |

```yaml
# Grow to fill remaining space
- type: flex
  direction: row
  width: "400"
  children:
    - type: text
      width: "100"
      content: "Fixed 100px"
    - type: text
      grow: 1
      content: "Fills remaining 300px"

# Proportional growth
- type: flex
  direction: row
  width: "300"
  children:
    - type: text
      grow: 1
      content: "1/3 of space"
    - type: text
      grow: 2
      content: "2/3 of space"

# Prevent shrinking
- type: flex
  direction: row
  width: "200"
  children:
    - type: text
      shrink: 0
      width: "150"
      content: "Will not shrink"
    - type: text
      shrink: 1
      width: "150"
      content: "Absorbs overflow"

# Flex basis
- type: flex
  direction: row
  width: "400"
  children:
    - type: text
      basis: "200"
      grow: 1
      content: "Starts at 200px, grows"
    - type: text
      basis: "50%"
      content: "50% of container"

# AlignSelf override
- type: flex
  direction: row
  align: stretch
  height: "100"
  children:
    - type: text
      content: "Stretched (default)"
    - type: text
      alignSelf: center
      content: "Centered"
    - type: text
      alignSelf: end
      content: "Bottom"

# Hide element with display: none
- type: text
  display: none
  content: "This element is invisible and takes no space"
```

**Basis resolution priority:**
1. If `basis` is explicitly set (not `auto`) -- use the basis value
2. If `basis` is `auto` and a main-axis dimension is set (e.g., `width` for row) -- use that dimension
3. Otherwise -- use the intrinsic (measured) content size

### Positioning Properties

Control how the element is placed relative to normal flex flow.

| Property | YAML Name | Type | Default | Valid Values | Description |
|----------|-----------|------|---------|--------------|-------------|
| Position | `position` | Position | `static` | static, relative, absolute | Positioning mode. |
| Top | `top` | string? | `null` | px, %, em | Top inset offset. |
| Right | `right` | string? | `null` | px, %, em | Right inset offset. |
| Bottom | `bottom` | string? | `null` | px, %, em | Bottom inset offset. |
| Left | `left` | string? | `null` | px, %, em | Left inset offset. |

**static** -- Normal flex flow. Inset properties are ignored.

```yaml
# Static positioning (default)
- type: text
  content: "Normal flow"
```

**relative** -- Element is laid out in normal flow, then shifted by inset values. It still occupies its original space.

```yaml
# Offset 10px right and 5px down from normal position
- type: text
  position: relative
  left: "10"
  top: "5"
  content: "Shifted from normal position"
```

**absolute** -- Removed from flex flow entirely. Positioned relative to the direct parent container. Does not affect siblings.

```yaml
# Badge in the top-right corner of a container
- type: flex
  width: "300"
  height: "200"
  background: "#f5f5f5"
  children:
    - type: text
      content: "Card content"
    - type: text
      position: absolute
      top: "8"
      right: "8"
      content: "NEW"
      background: "#ef4444"
      color: "#ffffff"
      padding: "2 8"
      size: 0.7em
```

When both opposing insets are set (e.g., `left` + `right`) without an explicit dimension, the element's size is computed from the insets. When both are set and a dimension is also set, `left` takes priority over `right` and `top` takes priority over `bottom`.

### Visual Properties

| Property | YAML Name | Type | Default | Valid Values | Description |
|----------|-----------|------|---------|--------------|-------------|
| Background | `background` | string? | `null` | Hex color (#rgb or #rrggbb) or CSS gradient | Background color or gradient. `null` means transparent. |
| Opacity | `opacity` | float | `1.0` | 0.0 to 1.0 | Element opacity. 0.0 is fully transparent, 1.0 is fully opaque. Affects the element and all its children. |
| BoxShadow | `box-shadow` or `boxShadow` | string? | `null` | `"offsetX offsetY blurRadius color"` | Box shadow rendered behind the element. |
| Rotate | `rotate` | string | `"none"` | none, left, right, flip, or degrees (e.g., "45") | Element rotation applied after rendering. |
| AspectRatio | `aspectRatio` or `aspect-ratio` | float? | `null` | Any positive float | Width/height ratio. When one dimension is known, the other is computed. |
| TextDirection | `text-direction` | TextDirection? | `null` | ltr, rtl, null | Text direction override. `null` inherits from parent or canvas. |

```yaml
# Background color
- type: flex
  background: "#e8f5e9"
  padding: "16"
  children:
    - type: text
      content: "Green background"

# 3-digit hex shorthand
- type: flex
  background: "#f00"
  padding: "8"
  children:
    - type: text
      content: "Red background"
      color: "#fff"

# Rotation: 90 degrees clockwise
- type: text
  rotate: "right"
  content: "Rotated 90 degrees CW"

# Rotation: arbitrary degrees
- type: text
  rotate: "15"
  content: "Tilted 15 degrees"
  font: bold
  size: 1.5em

# Rotation: 180 degrees (upside down)
- type: text
  rotate: "flip"
  content: "Flipped"

# Aspect ratio: 16:9 container
- type: flex
  width: "320"
  aspectRatio: 1.7778
  background: "#000000"

# Aspect ratio: square element
- type: flex
  width: "100"
  aspect-ratio: 1.0
  background: "#f0f0f0"

# Semi-transparent overlay
- type: flex
  opacity: 0.5
  background: "#000000"
  padding: "16"
  children:
    - type: text
      content: "50% transparent"
      color: "#ffffff"

# Box shadow
- type: flex
  box-shadow: "4 4 8 #00000040"
  background: "#ffffff"
  padding: "16"
  children:
    - type: text
      content: "Shadowed card"

# Linear gradient background
- type: flex
  background: "linear-gradient(to right, #ff0000, #0000ff)"
  padding: "20"
  children:
    - type: text
      content: "Gradient background"
      color: "#ffffff"

# Radial gradient background
- type: flex
  background: "radial-gradient(#ffffff, #000000)"
  padding: "20"
  width: "200"
  height: "200"
```

| Rotate Value | Effect |
|--------------|--------|
| `"none"` | No rotation (default) |
| `"left"` | 270 degrees (90 CCW), swaps width and height |
| `"right"` | 90 degrees CW, swaps width and height |
| `"flip"` | 180 degrees |
| `"<number>"` | Arbitrary degrees, e.g. `"45"`, `"15"`, `"270"` |

### Border Properties

Add visible borders around any element. Borders consume space in layout (like padding). All border properties are inherited from the base `TemplateElement` class and available on every element type.

| Property | YAML Name | Type | Default | Description |
|----------|-----------|------|---------|-------------|
| Border | `border` | string? | `null` | Shorthand for all sides: `"width style color"` (e.g., `"2 solid #333"`). |
| BorderTop | `border-top` | string? | `null` | Per-side shorthand for top: `"width style color"`. Overrides `border`. |
| BorderRight | `border-right` | string? | `null` | Per-side shorthand for right side. Overrides `border`. |
| BorderBottom | `border-bottom` | string? | `null` | Per-side shorthand for bottom side. Overrides `border`. |
| BorderLeft | `border-left` | string? | `null` | Per-side shorthand for left side. Overrides `border`. |
| BorderWidth | `border-width` | string? | `null` | Width override for all sides (px, em). Overrides shorthand width. |
| BorderColor | `border-color` | string? | `null` | Color override for all sides. Overrides shorthand color. |
| BorderStyle | `border-style` | string? | `null` | Style override: `solid`, `dashed`, `dotted`, `none`. Overrides shorthand style. |
| BorderRadius | `border-radius` | string? | `null` | Corner rounding radius (px, em, %). |

**CSS cascade order:**
1. `border` shorthand sets all four sides
2. `border-width`, `border-color`, `border-style` override individual properties on all sides
3. `border-top`, `border-right`, `border-bottom`, `border-left` override specific sides

```yaml
# Uniform solid border
- type: flex
  border: "2 solid #333333"
  padding: "16"
  children:
    - type: text
      content: "Bordered box"

# Per-side borders
- type: flex
  border-top: "3 solid #3498db"
  border-bottom: "1 dashed #cccccc"
  padding: "16"
  children:
    - type: text
      content: "Top and bottom borders"

# Rounded corners
- type: flex
  border: "2 solid #3498db"
  border-radius: "12"
  padding: "16"
  children:
    - type: text
      content: "Rounded box"
```

---

## flex

A container that arranges its children using the CSS flexbox algorithm. Flex containers can be nested to create complex layouts. The default direction is `column` (top to bottom).

### Minimal Example

```yaml
- type: flex
  children:
    - type: text
      content: "First"
    - type: text
      content: "Second"
```

### Container Properties

These properties are specific to `flex` elements and control how children are arranged.

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| Direction | `direction` | FlexDirection | `column` | row, column, row-reverse, column-reverse | No | Main axis direction. |
| Wrap | `wrap` | FlexWrap | `nowrap` | nowrap, wrap, wrap-reverse | No | Whether items wrap to new lines. |
| Gap | `gap` | string | `"0"` | px, %, em | No | Shorthand for both `rowGap` and `columnGap`. |
| ColumnGap | `columnGap` or `column-gap` | string? | `null` | px, %, em | No | Gap between items on the main axis (overrides `gap`). |
| RowGap | `rowGap` or `row-gap` | string? | `null` | px, %, em | No | Gap between wrapped lines (overrides `gap`). |
| Justify | `justify` | JustifyContent | `start` | start, center, end, space-between, space-around, space-evenly | No | Main axis alignment of children. |
| Align | `align` | AlignItems | `stretch` | start, center, end, stretch, baseline | No | Cross axis alignment of children. |
| AlignContent | `alignContent` or `align-content` | AlignContent | `start` | start, center, end, stretch, space-between, space-around, space-evenly | No | Alignment of wrapped lines along cross axis. Only effective when `wrap` is `wrap` or `wrap-reverse`. |
| Overflow | `overflow` | Overflow | `visible` | visible, hidden | No | Content clipping. `hidden` clips children at container bounds. |
| Children | `children` | element[] | `[]` | Array of elements | No | Child elements to lay out. |

```yaml
# Row direction
- type: flex
  direction: row
  gap: 10
  children:
    - type: text
      content: "Left"
    - type: text
      content: "Right"

# Wrapping with separate row/column gaps
- type: flex
  direction: row
  wrap: wrap
  columnGap: 10
  rowGap: 20
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
      content: "Item 3 (wraps)"

# Justify: space items evenly
- type: flex
  direction: row
  justify: space-between
  children:
    - type: text
      content: "Left"
    - type: text
      content: "Center"
    - type: text
      content: "Right"

# Align children to center
- type: flex
  direction: row
  align: center
  height: "80"
  background: "#f5f5f5"
  children:
    - type: text
      size: 1.5em
      content: "Large"
    - type: text
      size: 0.8em
      content: "Small"

# Overflow hidden clips content
- type: flex
  overflow: hidden
  width: "100"
  height: "50"
  children:
    - type: text
      content: "This very long text will be clipped at the container boundary"
```

**Gap-to-axis mapping:** The `gap` shorthand sets both row and column gaps. When `columnGap` or `rowGap` is also set, it overrides the shorthand for that axis.

| Direction | Main-axis gap | Cross-axis gap |
|-----------|---------------|----------------|
| `row` / `row-reverse` | `columnGap` (or `gap`) | `rowGap` (or `gap`) |
| `column` / `column-reverse` | `rowGap` (or `gap`) | `columnGap` (or `gap`) |

**Justify overflow fallback:** When children overflow (`freeSpace < 0`), distribution modes fall back to `start`:

| Original | Fallback |
|----------|----------|
| `space-between` | `start` |
| `space-around` | `start` |
| `space-evenly` | `start` |

### Complete Example: Product Card Layout

```yaml
- type: flex
  background: "#ffffff"
  padding: "20"
  gap: 12
  width: "300"
  children:
    # Header row with title and price
    - type: flex
      direction: row
      justify: space-between
      align: center
      children:
        - type: text
          content: "{{product.name}}"
          font: bold
          size: 1.2em
          color: "#1a1a1a"
        - type: text
          content: "{{product.price}} $"
          font: bold
          size: 1.2em
          color: "#cc0000"

    # Description
    - type: text
      content: "{{product.description}}"
      size: 0.9em
      color: "#666666"
      maxLines: 3
      overflow: ellipsis

    - type: separator
      style: dashed
      color: "#eeeeee"

    # Tags row
    - type: flex
      direction: row
      gap: 8
      wrap: wrap
      children:
        - type: text
          content: "In Stock"
          size: 0.75em
          color: "#22c55e"
          background: "#f0fdf4"
          padding: "2 8"
        - type: text
          content: "Free Shipping"
          size: 0.75em
          color: "#3b82f6"
          background: "#eff6ff"
          padding: "2 8"
```

### Complete Example: Navigation Bar

```yaml
- type: flex
  direction: row
  justify: space-between
  align: center
  background: "#1a1a2e"
  padding: "12 24"
  children:
    # Logo
    - type: text
      content: "MyStore"
      font: bold
      size: 1.3em
      color: "#ffffff"

    # Navigation links
    - type: flex
      direction: row
      gap: 24
      children:
        - type: text
          content: "Home"
          color: "#ffffff"
          size: 0.9em
        - type: text
          content: "Products"
          color: "#8888aa"
          size: 0.9em
        - type: text
          content: "About"
          color: "#8888aa"
          size: 0.9em

    # Cart badge
    - type: flex
      direction: row
      gap: 4
      align: center
      children:
        - type: text
          content: "Cart ({{cartCount}})"
          color: "#ffffff"
          size: 0.9em
```

### Complete Example: Grid with Wrapping

```yaml
- type: flex
  direction: row
  wrap: wrap
  gap: 12
  padding: "16"
  width: "340"
  children:
    - type: flex
      width: "100"
      height: "100"
      background: "#e8f5e9"
      padding: "8"
      align: center
      justify: center
      children:
        - type: text
          content: "Cell 1"
          align: center
    - type: flex
      width: "100"
      height: "100"
      background: "#e3f2fd"
      padding: "8"
      align: center
      justify: center
      children:
        - type: text
          content: "Cell 2"
          align: center
    - type: flex
      width: "100"
      height: "100"
      background: "#fff3e0"
      padding: "8"
      align: center
      justify: center
      children:
        - type: text
          content: "Cell 3"
          align: center
    - type: flex
      width: "100"
      height: "100"
      background: "#fce4ec"
      padding: "8"
      align: center
      justify: center
      children:
        - type: text
          content: "Cell 4"
          align: center
    - type: flex
      width: "100"
      height: "100"
      background: "#f3e5f5"
      padding: "8"
      align: center
      justify: center
      children:
        - type: text
          content: "Cell 5"
          align: center
    - type: flex
      width: "100"
      height: "100"
      background: "#e0f7fa"
      padding: "8"
      align: center
      justify: center
      children:
        - type: text
          content: "Cell 6"
          align: center
```

---

## text

Renders text content with font styling, alignment, wrapping, overflow handling, and line height control. Supports `{{variable}}` expressions in the `content` property.

### Minimal Example

```yaml
- type: text
  content: "Hello, World!"
```

### Properties

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| Content | `content` | string | `""` | Any string, may contain `{{variable}}` expressions | No | The text to render. |
| Font | `font` | string | `"main"` | Any key defined in the `fonts` section | No | Font reference name. Falls back to `"default"` if `"main"` is not defined. |
| Size | `size` | string | `"1em"` | px, em, % | No | Font size. |
| Color | `color` | string | `"#000000"` | Hex color (#rgb or #rrggbb) | No | Text color. |
| Align | `align` | TextAlign | `left` | left, center, right, start (logical), end (logical) | No | Horizontal text alignment within the element. `start`/`end` resolve based on text direction. |
| Wrap | `wrap` | bool | `true` | true, false | No | Whether text wraps to multiple lines when exceeding width. |
| Overflow | `overflow` | TextOverflow | `ellipsis` | ellipsis, clip, visible | No | How overflowing text is handled when `maxLines` is reached or `wrap` is `false`. |
| MaxLines | `maxLines` | int? | `null` | Any positive integer, or null for unlimited | No | Maximum number of lines. Text beyond this limit is truncated per `overflow`. |
| LineHeight | `lineHeight` | string | `""` | Multiplier, px, em, or empty string | No | Line spacing for multi-line text. |

```yaml
# Font size in pixels
- type: text
  size: 14px
  content: "14 pixel text"

# Font size in em (relative to parent/default)
- type: text
  size: 1.5em
  content: "1.5x the base font size"

# Font size as percentage
- type: text
  size: 80%
  content: "80% of the base font size"

# Right-aligned price
- type: text
  align: right
  content: "99.99 $"
  font: bold

# Center-aligned heading
- type: text
  align: center
  font: bold
  size: 1.5em
  content: "Section Title"
  color: "#1a1a1a"

# Disable wrapping for single-line labels
- type: text
  wrap: false
  content: "This will not wrap regardless of width"

# Truncate with ellipsis after 2 lines
- type: text
  maxLines: 2
  overflow: ellipsis
  content: "This is a long product description that may span multiple lines. After the second line, the remaining text is replaced with an ellipsis character."

# Clip overflow (hard cut, no ellipsis)
- type: text
  maxLines: 1
  overflow: clip
  content: "Text that is clipped without any ellipsis indicator"

# Visible overflow (text renders beyond bounds)
- type: text
  overflow: visible
  content: "Text may render outside its allocated area"
```

**lineHeight values:**

| Format | Example | Behavior |
|--------|---------|----------|
| Empty string | `""` | Use font's default line spacing |
| Plain number | `"1.5"` | Multiplier of the font size |
| Pixel units | `"24px"` | Fixed pixel spacing between lines |
| Em units | `"2em"` | Relative to the element's font size |

```yaml
# Default line height (font-defined spacing)
- type: text
  content: "Default spacing between lines when text wraps to multiple lines."
  wrap: true

# 1.8x line height multiplier
- type: text
  lineHeight: "1.8"
  content: "Increased line spacing for better readability in dense paragraphs of text."
  wrap: true

# Fixed 30px line height
- type: text
  lineHeight: "30px"
  content: "Fixed 30-pixel spacing regardless of font size."
  wrap: true

# 2em line height (double the font size)
- type: text
  lineHeight: "2em"
  content: "Double-spaced text using em units."
  wrap: true
```

### Complete Example: Multi-line Truncated Description

```yaml
- type: flex
  width: "280"
  background: "#f8f8f8"
  padding: "16"
  gap: 8
  children:
    - type: text
      content: "Product Description"
      font: bold
      size: 1.1em
      color: "#1a1a1a"
    - type: text
      content: "{{product.longDescription}}"
      size: 0.9em
      color: "#555555"
      wrap: true
      maxLines: 4
      overflow: ellipsis
      lineHeight: "1.6"
```

### Complete Example: Styled Receipt Header

```yaml
- type: flex
  gap: 4
  align: center
  children:
    - type: text
      content: "{{shopName}}"
      font: bold
      size: 1.5em
      align: center
      color: "#1a1a1a"
    - type: text
      content: "{{address}}"
      size: 0.85em
      align: center
      color: "#888888"
    - type: text
      content: "Tel: {{phone}}"
      size: 0.75em
      align: center
      color: "#aaaaaa"
```

---

## image

Renders an image from a local file, HTTP URL, embedded resource, or base64 data URI. The `width` and `height` properties on `image` elements set the image container dimensions (not the flex layout size). Use `fit` to control how the image is scaled within its container.

### Minimal Example

```yaml
- type: image
  src: "logo.png"
```

### Properties

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| Src | `src` | string | `""` | File path, http://, embedded://, data:image/... | No (but empty renders nothing) | Image source URI. |
| ImageWidth | `width` | int? | `null` | Any positive integer (pixels) | No | Image container width. `null` uses the image's natural width. |
| ImageHeight | `height` | int? | `null` | Any positive integer (pixels) | No | Image container height. `null` uses the image's natural height. |
| Fit | `fit` | ImageFit | `contain` | fill, contain, cover, none | No | How the image scales within its container. |

**Important:** The `width` and `height` YAML keys on an `image` element map to `ImageWidth`/`ImageHeight` (image container size), not to the base `Width`/`Height` flex layout properties. To set flex layout dimensions on an image, wrap it in a `flex` container.

**Image source URI schemes:**

| Scheme | Example | Description |
|--------|---------|-------------|
| File (relative) | `"logo.png"` | Relative to the working directory. |
| File (absolute) | `"/images/logo.png"` | Absolute file path. |
| HTTP/HTTPS | `"https://example.com/img.png"` | Remote image. Requires `FlexRender.Http` package and `.WithHttpLoader()`. |
| Embedded | `"embedded://MyApp.Resources.logo.png"` | .NET embedded resource. |
| Base64 | `"data:image/png;base64,iVBOR..."` | Inline base64-encoded image data. |

```yaml
# Local file
- type: image
  src: "assets/images/logo.png"
  width: 200
  height: 60

# HTTP image
- type: image
  src: "https://example.com/product-photo.jpg"
  width: 300
  height: 200
  fit: cover

# Base64 inline image
- type: image
  src: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUg..."
  width: 50
  height: 50

# Embedded resource
- type: image
  src: "embedded://MyApp.Assets.icon.png"
  width: 32
  height: 32
```

**Image fit modes:**

| Mode | Description | Use Case |
|------|-------------|----------|
| `contain` | Scale to fit within bounds, preserving aspect ratio. May leave empty space. | Default. Logos, icons where full visibility matters. |
| `cover` | Scale to cover bounds, preserving aspect ratio. May crop edges. | Profile photos, backgrounds. |
| `fill` | Stretch to fill bounds exactly. May distort the image. | When exact dimensions are required. |
| `none` | Use the image's natural size. No scaling. | Pixel-art, screenshots at native resolution. |

```yaml
# fit: contain -- full image visible, may have letterboxing
- type: image
  src: "photo.jpg"
  width: 200
  height: 200
  fit: contain

# fit: cover -- fills container, may crop
- type: image
  src: "photo.jpg"
  width: 200
  height: 200
  fit: cover

# fit: fill -- stretches to exact dimensions
- type: image
  src: "banner.png"
  width: 400
  height: 80
  fit: fill

# fit: none -- natural size, no scaling
- type: image
  src: "pixel-art.png"
  fit: none
```

---

## qr

Renders a QR code image from string data. Requires the `FlexRender.QrCode` NuGet package and `.WithQr()` on the builder configuration.

### Minimal Example

```yaml
- type: qr
  data: "https://example.com"
```

### Properties

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| Data | `data` | string | `""` | Any string, may contain `{{variable}}` | No (but empty renders nothing) | Data to encode in the QR code. |
| Size | `size` | int | `100` | Any positive integer (pixels) | No | QR code dimensions (width = height). |
| ErrorCorrection | `errorCorrection` | ErrorCorrectionLevel | `M` | L, M, Q, H | No | Error correction level. Higher levels allow more damage recovery but create denser codes. |
| Foreground | `foreground` | string | `"#000000"` | Hex color (#rgb or #rrggbb) | No | Module (dark square) color. |
| Background | `background` | string | `"#ffffff"` | Hex color (#rgb or #rrggbb) | No | Background color. Overrides the base class `background`. |

**Error correction levels:**

| Level | Recovery Capacity | Best For |
|-------|-------------------|----------|
| `L` (Low) | ~7% damage recovery | Maximum data density, clean environments |
| `M` (Medium) | ~15% damage recovery | General purpose (default) |
| `Q` (Quartile) | ~25% damage recovery | Industrial use, moderate wear expected |
| `H` (High) | ~30% damage recovery | Harsh environments, expected damage or partial obstruction |

```yaml
# Basic QR code with default settings
- type: qr
  data: "https://example.com/pay/{{orderId}}"
  size: 120

# High error correction for printed labels
- type: qr
  data: "{{ticketId}}"
  size: 160
  errorCorrection: H

# Custom colors (inverted)
- type: qr
  data: "{{profileUrl}}"
  size: 100
  foreground: "#ffffff"
  background: "#1a1a2e"

# Small QR code with minimal error correction
- type: qr
  data: "{{shortUrl}}"
  size: 80
  errorCorrection: L
```

### Complete Example: Payment QR Section

```yaml
- type: flex
  align: center
  gap: 8
  padding: "16"
  children:
    - type: text
      content: "Scan to Pay"
      font: bold
      size: 1.1em
      align: center
    - type: qr
      data: "https://pay.example.com/{{orderId}}"
      size: 140
      errorCorrection: M
    - type: text
      content: "{{orderId}}"
      size: 0.7em
      color: "#aaaaaa"
      align: center
```

---

## barcode

Renders a 1D barcode image. Requires the `FlexRender.Barcode` NuGet package and `.WithBarcode()` on the builder configuration.

### Minimal Example

```yaml
- type: barcode
  data: "1234567890"
```

### Properties

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| Data | `data` | string | `""` | String matching the format's requirements | No (but empty renders nothing) | Data to encode. |
| Format | `format` | BarcodeFormat | `code128` | code128, code39, ean13, ean8, upc | No | Barcode symbology. |
| BarcodeWidth | `width` | int | `200` | Any positive integer (pixels) | No | Barcode image width. |
| BarcodeHeight | `height` | int | `80` | Any positive integer (pixels) | No | Barcode image height. |
| ShowText | `showText` | bool | `true` | true, false | No | Whether to render the encoded data as text below the barcode. |
| Foreground | `foreground` | string | `"#000000"` | Hex color (#rgb or #rrggbb) | No | Bar color. |
| Background | `background` | string | `"#ffffff"` | Hex color (#rgb or #rrggbb) | No | Background color. Overrides the base class `background`. |

**Important:** Like `image`, the `width` and `height` YAML keys on a `barcode` element map to `BarcodeWidth`/`BarcodeHeight` (barcode image size), not to the base flex layout properties.

**Barcode formats and data requirements:**

| Format | Name | Data Requirements | Use Case |
|--------|------|-------------------|----------|
| `code128` | Code 128 | Any ASCII characters (0-127) | General alphanumeric, high density |
| `code39` | Code 39 | A-Z, 0-9, space, `-`, `.`, `$`, `/`, `+`, `%` | Industrial, widely supported legacy |
| `ean13` | EAN-13 | Exactly 12 or 13 digits | International retail products |
| `ean8` | EAN-8 | Exactly 7 or 8 digits | Compact retail products |
| `upc` | UPC-A | Exactly 11 or 12 digits | North American retail (UPC-A) |

```yaml
# Code 128 (default, most versatile)
- type: barcode
  data: "SKU-2026-ABCDEF"
  format: code128
  width: 250
  height: 60

# EAN-13 retail barcode
- type: barcode
  data: "5901234123457"
  format: ean13
  width: 200
  height: 80

# EAN-8 compact barcode
- type: barcode
  data: "96385074"
  format: ean8
  width: 150
  height: 60

# UPC-A barcode
- type: barcode
  data: "012345678905"
  format: upc
  width: 200
  height: 80

# Code 39 without text label
- type: barcode
  data: "ITEM-42"
  format: code39
  width: 200
  height: 50
  showText: false

# Custom colored barcode
- type: barcode
  data: "{{sku}}"
  format: code128
  width: 180
  height: 40
  foreground: "#1a1a2e"
  background: "#f5f5f5"
```

### Complete Example: Product Label

```yaml
- type: flex
  padding: "12"
  gap: 8
  width: "200"
  align: center
  background: "#ffffff"
  children:
    - type: text
      content: "{{productName}}"
      font: bold
      size: 1.1em
      align: center
      maxLines: 2
      overflow: ellipsis
    - type: text
      content: "{{price}} $"
      font: bold
      size: 1.3em
      color: "#cc0000"
      align: center
    - type: barcode
      data: "{{sku}}"
      format: code128
      width: 180
      height: 40
      showText: true
```

---

## separator

Renders a horizontal or vertical line with configurable style, thickness, and color. Horizontal separators stretch to the full width of their parent; vertical separators stretch to the full height.

### Minimal Example

```yaml
- type: separator
```

### Properties

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| Orientation | `orientation` | SeparatorOrientation | `horizontal` | horizontal, vertical | No | Line direction. |
| Style | `style` | SeparatorStyle | `dotted` | dotted, dashed, solid | No | Line rendering style. |
| Thickness | `thickness` | float | `1` | > 0 (any positive float) | No | Line thickness in pixels. Values <= 0 cause a parse error. |
| Color | `color` | string | `"#000000"` | Hex color (#rgb or #rrggbb) | No | Line color. |

**Separator styles:**

| Style | Visual Description |
|-------|-------------------|
| `dotted` | A series of small dots (default). Subtle, non-intrusive divider. |
| `dashed` | A series of short dashes. Medium visual weight. |
| `solid` | A continuous unbroken line. Strong visual divider. |

```yaml
# Dotted separator (default style)
- type: separator
  style: dotted
  color: "#cccccc"

# Dashed separator
- type: separator
  style: dashed
  color: "#cccccc"
  thickness: 1

# Solid separator with increased thickness
- type: separator
  style: solid
  color: "#1a1a1a"
  thickness: 2

# Thick colored divider
- type: separator
  style: solid
  color: "#e74c3c"
  thickness: 3

# Vertical separator between columns
- type: flex
  direction: row
  height: "60"
  align: center
  gap: 16
  children:
    - type: text
      content: "Column A"
    - type: separator
      orientation: vertical
      style: solid
      color: "#cccccc"
      thickness: 1
      height: "40"
    - type: text
      content: "Column B"
```

Horizontal separators use `thickness` as their height and stretch to the container's full width. Vertical separators use `thickness` as their width and stretch to the container's full height (or use an explicit `height` if provided).

---

## table

Renders tabular data with configurable columns, optional header row, and support for both dynamic (data-driven) and static rows. During template expansion, the `table` element is expanded into a tree of `flex` and `text` elements -- no changes to the layout or rendering engines are needed.

### Minimal Example

```yaml
- type: table
  array: items
  as: item
  columns:
    - key: name
      label: "Name"
      grow: 1
    - key: price
      label: "Price"
      width: "80"
      align: right
```

### Table Properties

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| ArrayPath | `array` | string | `""` | Dot-notation path to array in data | No (use `rows` for static data) | Path to the data array for dynamic rows. |
| ItemVariable | `as` | string? | `null` | Any valid identifier | No | Variable name for the current item. |
| Columns | `columns` | column[] | -- | Array of column definitions | **Yes** | Column definitions. Must have at least one column. |
| Rows | `rows` | row[] | `[]` | Array of static row definitions | No | Static rows. Alternative to `array` for fixed data. |
| HeaderFont | `headerFont` or `header-font` | string? | `null` | Any font name from `fonts` section | No | Font for the header row. |
| HeaderColor | `headerColor` or `header-color` | string? | `null` | Hex color | No | Text color for the header row. |
| HeaderSize | `headerSize` or `header-size` | string? | `null` | px, em, % | No | Font size for the header row. |
| HeaderBackground | `headerBackground` or `header-background` | string? | `null` | Hex color | No | Background color for the header row. |

### Column Properties

| Property | YAML Name | Type | Default | Description |
|----------|-----------|------|---------|-------------|
| Key | `key` | string | -- | **Required.** Data field name to display in this column. |
| Label | `label` | string? | `null` | Header text. If null, no header cell is rendered for this column. |
| Width | `width` | string? | `null` | Explicit column width (px, %, em). |
| Grow | `grow` | float | `0` | Flex grow factor for the column. |
| Align | `align` | TextAlign | `left` | Text alignment: `left`, `center`, `right`. |
| Font | `font` | string? | `null` | Font override for cells in this column. |
| Color | `color` | string? | `null` | Text color override for cells in this column. |
| Size | `size` | string? | `null` | Font size override for cells in this column. |
| Format | `format` | string? | `null` | Format string for cell values. |

### Static Row Properties

| Property | YAML Name | Type | Default | Description |
|----------|-----------|------|---------|-------------|
| Values | `values` | map | -- | **Required.** Mapping of column keys to display values. Uses case-insensitive keys. |
| Font | `font` | string? | `null` | Font override for this entire row. |
| Color | `color` | string? | `null` | Text color override for this entire row. |
| Size | `size` | string? | `null` | Font size override for this entire row. |

```yaml
# Dynamic table with styled header
- type: table
  array: products
  as: product
  headerFont: bold
  headerColor: "#ffffff"
  headerBackground: "#333333"
  columns:
    - key: name
      label: "Product"
      grow: 1
    - key: quantity
      label: "Qty"
      width: "50"
      align: center
    - key: price
      label: "Price"
      width: "80"
      align: right
      format: "N2"

# Static table with explicit rows
- type: table
  columns:
    - key: label
      grow: 1
      font: bold
    - key: value
      width: "120"
      align: right
  rows:
    - values:
        label: "Subtotal"
        value: "85.00 $"
    - values:
        label: "Tax (10%)"
        value: "8.50 $"
    - values:
        label: "Total"
        value: "93.50 $"
      font: bold
      size: 1.2em
```

### Complete Example: Invoice Table

```yaml
- type: flex
  gap: 12
  padding: "20"
  width: "400"
  children:
    - type: text
      content: "Invoice #{{invoiceId}}"
      font: bold
      size: 1.3em

    - type: table
      array: lineItems
      as: line
      headerFont: bold
      headerBackground: "#f5f5f5"
      columns:
        - key: description
          label: "Description"
          grow: 1
        - key: qty
          label: "Qty"
          width: "40"
          align: center
        - key: unitPrice
          label: "Unit"
          width: "70"
          align: right
        - key: total
          label: "Total"
          width: "70"
          align: right
          font: bold

    - type: separator
      style: solid
      color: "#333333"

    - type: table
      columns:
        - key: label
          grow: 1
        - key: amount
          width: "70"
          align: right
          font: bold
      rows:
        - values:
            label: "Subtotal"
            amount: "{{subtotal}} $"
        - values:
            label: "Tax"
            amount: "{{tax}} $"
        - values:
            label: "Total"
            amount: "{{total}} $"
          font: bold
          size: 1.2em
```

---

## each (Control Flow)

Iterates over an array in the template data, rendering the `children` template once for each array item. This is the primary mechanism for dynamic, data-driven lists.

### Minimal Example

```yaml
- type: each
  array: items
  children:
    - type: text
      content: "{{name}}"
```

### Properties

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| ArrayPath | `array` | string | `""` | Dot-notation path to an array in data (e.g., `"items"`, `"order.lines"`) | **Yes** | Path to the array to iterate. Missing or non-array values produce no output. |
| ItemVariable | `as` | string? | `null` | Any valid identifier | No | Variable name for the current item. When set, item fields are accessed via `{{name.field}}`. |
| Children | `children` | element[] | `[]` | Array of template elements | **Yes** | Template rendered for each array item. |

**Loop variables:** Inside `each` children, these special variables are automatically available:

| Variable | Type | Description |
|----------|------|-------------|
| `{{@index}}` | int | Zero-based index of the current item (0, 1, 2, ...) |
| `{{@first}}` | bool | `true` for the first item only |
| `{{@last}}` | bool | `true` for the last item only |

```yaml
# Basic list with item variable
- type: each
  array: items
  as: item
  children:
    - type: text
      content: "{{item.name}}: {{item.price}} $"

# Numbered list using @index
- type: each
  array: tasks
  as: task
  children:
    - type: text
      content: "{{@index}}. {{task.title}}"

# Without 'as' -- item fields are at root scope
- type: each
  array: items
  children:
    - type: text
      content: "{{name}}: {{price}} $"
```

### Conditional Separator Between Items

Use `@last` to add a separator between items but not after the last one:

```yaml
- type: each
  array: items
  as: item
  children:
    - type: flex
      gap: 4
      children:
        - type: flex
          direction: row
          justify: space-between
          children:
            - type: text
              content: "{{item.name}}"
            - type: text
              content: "{{item.price}} $"
        - type: if
          condition: "@last"
          notEquals: true
          then:
            - type: separator
              style: dashed
              color: "#eeeeee"
```

### Nested Data Paths

Access nested arrays using dot notation:

```yaml
- type: each
  array: order.lineItems
  as: line
  children:
    - type: flex
      direction: row
      justify: space-between
      children:
        - type: text
          content: "{{line.product}} x{{line.quantity}}"
        - type: text
          content: "{{line.subtotal}} $"
```

### Complete Example: Dynamic Receipt Items

```yaml
- type: flex
  gap: 6
  children:
    - type: each
      array: items
      as: item
      children:
        - type: flex
          direction: row
          justify: space-between
          children:
            - type: flex
              direction: column
              gap: 2
              shrink: 1
              children:
                - type: text
                  content: "{{item.name}}"
                  size: 1em
                  color: "#333333"
                - type: if
                  condition: item.quantity
                  greaterThan: 1
                  then:
                    - type: text
                      content: "x{{item.quantity}}"
                      size: 0.8em
                      color: "#888888"
            - type: text
              content: "{{item.price}} $"
              size: 1em
              color: "#333333"
              align: right
```

---

## if (Control Flow)

Conditionally renders elements based on data values. Supports 13 comparison operators, else branches, and else-if chains. Only one operator key is allowed per `if` element.

### Minimal Example

```yaml
- type: if
  condition: isPremium
  then:
    - type: text
      content: "Premium Member"
```

### Properties

| Property | YAML Name | Type | Default | Valid Values | Required | Description |
|----------|-----------|------|---------|--------------|----------|-------------|
| ConditionPath | `condition` | string | `""` | Dot-notation path to value in data | **Yes** | Path to the value to test. |
| Operator | _(see table below)_ | ConditionOperator? | `null` (truthy) | One operator key per element | No | Comparison operator. Omit for truthy check. |
| ThenBranch | `then` | element[] | `[]` | Array of template elements | **Yes** | Elements rendered when condition is true. |
| ElseBranch | `else` | element[] | `[]` | Array of template elements | No | Elements rendered when all conditions are false. |
| ElseIf | `elseIf` | if element | `null` | Nested if definition (without `type:`) | No | Chained else-if condition. |

### All 13 Operators

| # | Operator | YAML Key | Value Type | Description | Example Value |
|---|----------|----------|------------|-------------|---------------|
| 1 | Truthy | _(none)_ | -- | Value exists and is non-empty, non-zero, non-false, non-null | -- |
| 2 | Equals | `equals` | any | Value strictly equals the operand | `"paid"`, `42`, `true` |
| 3 | NotEquals | `notEquals` | any | Value does not equal the operand | `"cancelled"` |
| 4 | In | `in` | string[] | Value is in the provided list | `["admin", "mod"]` |
| 5 | NotIn | `notIn` | string[] | Value is not in the provided list | `["banned", "deleted"]` |
| 6 | Contains | `contains` | string | Array value contains this element | `"urgent"` |
| 7 | GreaterThan | `greaterThan` | number | Numeric value > operand | `1000` |
| 8 | GreaterThanOrEqual | `greaterThanOrEqual` | number | Numeric value >= operand | `10` |
| 9 | LessThan | `lessThan` | number | Numeric value < operand | `5` |
| 10 | LessThanOrEqual | `lessThanOrEqual` | number | Numeric value <= operand | `2` |
| 11 | HasItems | `hasItems` | bool | Array is non-empty (true) or empty (false) | `true` |
| 12 | CountEquals | `countEquals` | int | Array length equals N | `1` |
| 13 | CountGreaterThan | `countGreaterThan` | int | Array length > N | `5` |

### Operator Examples

**Truthy** -- Value exists and is non-empty/non-zero/non-false:

```yaml
- type: if
  condition: discount
  then:
    - type: text
      content: "Discount: {{discount}}%"
      color: "#22c55e"
```

**Equals** -- String, number, or boolean equality:

```yaml
- type: if
  condition: status
  equals: "paid"
  then:
    - type: text
      content: "Payment received"
      color: "#22c55e"
```

**NotEquals:**

```yaml
- type: if
  condition: status
  notEquals: "cancelled"
  then:
    - type: text
      content: "Order is active"
```

**In** -- Value is in a list of strings:

```yaml
- type: if
  condition: role
  in: ["admin", "moderator"]
  then:
    - type: text
      content: "Staff Member"
      background: "#3b82f6"
      color: "#ffffff"
      padding: "2 8"
```

**NotIn:**

```yaml
- type: if
  condition: status
  notIn: ["cancelled", "refunded"]
  then:
    - type: text
      content: "Active Order"
```

**Contains** -- Check if an array field contains a value:

```yaml
- type: if
  condition: tags
  contains: "urgent"
  then:
    - type: text
      content: "URGENT"
      color: "#ef4444"
      font: bold
```

**GreaterThan:**

```yaml
- type: if
  condition: total
  greaterThan: 1000
  then:
    - type: text
      content: "Free shipping on orders over 1000 $!"
      color: "#22c55e"
```

**GreaterThanOrEqual:**

```yaml
- type: if
  condition: quantity
  greaterThanOrEqual: 10
  then:
    - type: text
      content: "Bulk discount: 15% off"
```

**LessThan:**

```yaml
- type: if
  condition: stock
  lessThan: 5
  then:
    - type: text
      content: "Low stock - only {{stock}} left!"
      color: "#ef4444"
```

**LessThanOrEqual:**

```yaml
- type: if
  condition: rating
  lessThanOrEqual: 2
  then:
    - type: text
      content: "Needs improvement"
      color: "#f59e0b"
```

**HasItems** -- Check if array is non-empty:

```yaml
- type: if
  condition: items
  hasItems: true
  then:
    - type: each
      array: items
      as: item
      children:
        - type: text
          content: "{{item.name}}"
  else:
    - type: text
      content: "No items in your cart"
      color: "#888888"
```

**CountEquals:**

```yaml
- type: if
  condition: items
  countEquals: 1
  then:
    - type: text
      content: "Single item order"
```

**CountGreaterThan:**

```yaml
- type: if
  condition: items
  countGreaterThan: 5
  then:
    - type: text
      content: "Bulk order -- special handling required"
      font: bold
```

### Else-If Chain Example

Chain multiple conditions for status-dependent rendering:

```yaml
- type: if
  condition: paymentStatus
  equals: "paid"
  then:
    - type: flex
      align: center
      gap: 4
      children:
        - type: text
          content: "PAID"
          font: bold
          size: 1.1em
          color: "#22c55e"
          align: center
        - type: text
          content: "Thank you for your payment!"
          size: 0.85em
          color: "#666666"
          align: center
  elseIf:
    condition: paymentStatus
    equals: "pending"
    then:
      - type: flex
        align: center
        gap: 8
        children:
          - type: qr
            data: "{{paymentUrl}}"
            size: 120
            errorCorrection: M
          - type: text
            content: "Scan to complete payment"
            size: 0.85em
            color: "#999999"
            align: center
  else:
    - type: text
      content: "Payment required at counter"
      size: 0.9em
      color: "#ef4444"
      align: center
```

### Combining Conditions (AND Logic)

Nest `if` elements for AND logic:

```yaml
# Show discount only for premium members with orders over 100 $
- type: if
  condition: isPremium
  then:
    - type: if
      condition: total
      greaterThan: 100
      then:
        - type: text
          content: "Premium member discount: 20% off!"
          color: "#22c55e"
          font: bold
```

---

## Property Name Variants

Several properties accept both camelCase and kebab-case names in YAML. The parser checks kebab-case first, then falls back to camelCase. Both forms are fully equivalent.

| camelCase | kebab-case | Element | Notes |
|-----------|------------|---------|-------|
| `minWidth` | `min-width` | All (TemplateElement) | Both forms accepted |
| `maxWidth` | `max-width` | All (TemplateElement) | Both forms accepted |
| `minHeight` | `min-height` | All (TemplateElement) | Both forms accepted |
| `maxHeight` | `max-height` | All (TemplateElement) | Both forms accepted |
| `aspectRatio` | `aspect-ratio` | All (TemplateElement) | Both forms accepted |
| `alignContent` | `align-content` | flex | Both forms accepted |
| `rowGap` | `row-gap` | flex | Both forms accepted |
| `columnGap` | `column-gap` | flex | Both forms accepted |
| `boxShadow` | `box-shadow` | All (TemplateElement) | Both forms accepted |
| `headerFont` | `header-font` | table | Both forms accepted |
| `headerColor` | `header-color` | table | Both forms accepted |
| `headerSize` | `header-size` | table | Both forms accepted |
| `headerBackground` | `header-background` | table | Both forms accepted |

The following properties are **camelCase only** (no kebab-case variant):

| Property | Element | YAML Name |
|----------|---------|-----------|
| `alignSelf` | All (TemplateElement) | `alignSelf` only |
| `maxLines` | text | `maxLines` only |
| `lineHeight` | text | `lineHeight` only |
| `showText` | barcode | `showText` only |
| `errorCorrection` | qr | `errorCorrection` only |
| `elseIf` | if | `elseIf` only |
| `notEquals` | if | `notEquals` only |
| `notIn` | if | `notIn` only |
| `greaterThan` | if | `greaterThan` only |
| `greaterThanOrEqual` | if | `greaterThanOrEqual` only |
| `lessThan` | if | `lessThan` only |
| `lessThanOrEqual` | if | `lessThanOrEqual` only |
| `hasItems` | if | `hasItems` only |
| `countEquals` | if | `countEquals` only |
| `countGreaterThan` | if | `countGreaterThan` only |

---

## Units Reference

All size-based properties (`width`, `height`, `padding`, `margin`, `gap`, `basis`, `minWidth`, etc.) accept these units:

| Unit | Syntax | Meaning | Example |
|------|--------|---------|---------|
| px | `"100"` or `"100px"` | Absolute pixels. Default if no suffix. | `width: "200"` |
| % | `"50%"` | Percentage of parent container size. | `width: "50%"` |
| em | `"1.5em"` | Relative to current font size. | `size: "1.5em"` |
| auto | `"auto"` or `null` | Automatic sizing (context-dependent). | `width: "auto"` |

Plain numbers (without a suffix) are treated as pixels. Empty or whitespace strings resolve to `auto`. Parsing is case-insensitive for unit suffixes.

---

## Color Format Reference

All color properties (`color`, `background`, `foreground`) accept hex format:

| Format | Example | Description |
|--------|---------|-------------|
| `#rrggbb` | `"#ff0000"` | 6-digit hex (red) |
| `#rgb` | `"#f00"` | 3-digit hex shorthand (red) |

Invalid color strings fall back to black (`#000000`) at render time.

---

## See Also

- [[Visual-Reference]] -- interactive visual examples for all properties and elements
- [[Template-Syntax]] -- template structure, canvas settings, fonts
- [[Template-Expressions]] -- variables (`{{name}}`), loops, conditionals
- [[Flexbox-Layout]] -- layout engine details, two-pass algorithm
- [[Render-Options]] -- per-call rendering options (antialiasing, font hinting, output format)
- [[API-Reference]] -- IFlexRender, builder pattern, dependency injection
- [[CLI-Reference]] -- render, validate, watch, debug-layout commands
