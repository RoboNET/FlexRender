# Flexbox Expansion: API Reference

Comprehensive property reference for the FlexRender flexbox layout system.
Covers all phases (0-5) of the flexbox expansion.

**Status legend**: Implemented | Planned

---

## Table of Contents

1. [Units](#1-units)
2. [Base Element Properties (Phase 0)](#2-base-element-properties-phase-0)
3. [Flex Container Properties](#3-flex-container-properties)
4. [Flex Item Properties](#4-flex-item-properties)
5. [Layout Enums](#5-layout-enums)
6. [Phase 1: Quick Wins](#6-phase-1-quick-wins)
7. [Phase 2: Core Flexbox](#7-phase-2-core-flexbox)
8. [Phase 3: Wrapping](#8-phase-3-wrapping)
9. [Phase 4: Advanced Features](#9-phase-4-advanced-features)
10. [Phase 5: Auto Margins](#10-phase-5-auto-margins)
11. [Resource Limits](#11-resource-limits)
12. [Known Limitations](#12-known-limitations)

---

## 1. Units

All size-based properties support the following unit types via `UnitParser`:

| Unit | Syntax | Description | Example |
|------|--------|-------------|---------|
| **px** | `"100"`, `"100px"` | Absolute pixels (default if no suffix) | `width: "100"` |
| **%** | `"50%"` | Percentage of parent container size | `width: "50%"` |
| **em** | `"1.5em"` | Relative to current font size | `width: "2em"` |
| **auto** | `"auto"`, `null` | Automatic sizing (context-dependent) | `width: "auto"` |

**Parsing rules**:
- Empty or whitespace strings resolve to `auto`
- Plain numbers without suffix are treated as pixels
- Parsing is case-insensitive for unit suffixes
- Invalid values resolve to `auto`

---

## 2. Base Element Properties (Phase 0)

**Status**: Implemented

All properties below are defined on `TemplateElement` (abstract base class) and inherited by every element type: `TextElement`, `FlexElement`, `ImageElement`, `QrElement`, `BarcodeElement`, `SeparatorElement`.

Source: `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`

### display

| Attribute | Value |
|-----------|-------|
| **Type** | `Display` enum |
| **Default** | `Display.Flex` (= 0) |
| **Values** | `Flex`, `None` |

```yaml
- type: text
  display: none       # removed from layout flow
  content: "Hidden"
```

**Behavior**: `display: none` removes the element entirely from the layout flow. The element occupies no space and is not rendered. Other children are laid out as if the hidden element does not exist.

**Limitation**: Only `Flex` and `None` are supported. CSS `display: block`, `inline`, `grid`, etc. are not available.

---

### padding

| Attribute | Value |
|-----------|-------|
| **Type** | `string` |
| **Default** | `"0"` |
| **Units** | px, %, em |

```yaml
# Uniform padding
- type: flex
  padding: "10"

# Vertical / Horizontal
- type: flex
  padding: "10 20"

# Top / Horizontal / Bottom
- type: flex
  padding: "10 20 30"

# Top / Right / Bottom / Left
- type: flex
  padding: "10 20 30 40"
```

**Behavior**: CSS shorthand notation. Supports 1, 2, 3, or 4 values. Parsed by `PaddingParser` into `PaddingValues(Top, Right, Bottom, Left)`. Padding is inside the element's bounds and reduces available space for children.

---

### margin

| Attribute | Value |
|-----------|-------|
| **Type** | `string` |
| **Default** | `"0"` |
| **Units** | px, %, em (Phase 5 adds `auto`) |

```yaml
# Uniform margin
- type: text
  margin: "10"

# 4-side margin (Phase 1)
- type: text
  margin: "10 20 30 40"
```

**Behavior**: CSS shorthand notation with 1, 2, 3, or 4 values (same as padding). Margin is outside the element and creates space between siblings.

**Phase 5 addition**: `auto` keyword for automatic margin distribution (see [Phase 5: Auto Margins](#10-phase-5-auto-margins)).

---

### background

| Attribute | Value |
|-----------|-------|
| **Type** | `string?` |
| **Default** | `null` (transparent) |

```yaml
- type: flex
  background: "#FF0000"
```

**Behavior**: Fills the element's bounds with the specified color before rendering children. Supports hex color format.

---

### rotate

| Attribute | Value |
|-----------|-------|
| **Type** | `string` |
| **Default** | `"none"` |

```yaml
- type: text
  rotate: "90"
  content: "Rotated"
```

**Behavior**: Rotates the element by the specified degrees. Applied during rendering.

---

## 3. Flex Container Properties

**Status**: Implemented

Defined only on `FlexElement`. These properties control the layout behavior of the container for its children.

Source: `src/FlexRender.Core/Parsing/Ast/FlexElement.cs`

### direction

| Attribute | Value |
|-----------|-------|
| **Type** | `FlexDirection` enum |
| **Default** | `FlexDirection.Column` (= 1) |
| **Values** | `Row` (0), `Column` (1), `RowReverse` (2), `ColumnReverse` (3) |

```yaml
- type: flex
  direction: row
  children:
    - type: text
      content: "Left"
    - type: text
      content: "Right"
```

**Behavior**:
- `row` -- main axis horizontal (left to right), cross axis vertical
- `column` -- main axis vertical (top to bottom), cross axis horizontal
- `rowReverse` -- main axis horizontal (right to left)
- `columnReverse` -- main axis vertical (bottom to top)

**Note**: `RowReverse` and `ColumnReverse` reverse the order in which children are placed along the main axis. The starting edge becomes the end edge.

---

### wrap

| Attribute | Value |
|-----------|-------|
| **Type** | `FlexWrap` enum |
| **Default** | `FlexWrap.NoWrap` (= 0) |
| **Values** | `NoWrap` (0), `Wrap` (1), `WrapReverse` (2) |
| **Status** | Planned (Phase 3) |

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
      content: "Item 3"   # Wraps to new line
```

**Behavior**:
- `noWrap` -- all items on a single line (default). Items may overflow or shrink.
- `wrap` -- items wrap to additional lines when they exceed the container's main axis size. New lines are added in the cross-axis direction.
- `wrapReverse` -- items wrap, but lines are placed in reverse cross-axis order. After normal layout, each child's cross position is flipped: `newPos = containerCrossSize - oldPos - childSize`.

**Line breaking algorithm** (greedy):
- Items are added to the current line until the next item would cause overflow
- Line break threshold: `lineMainSize + gap + childMainSize + childMarginMain > availableMainSize`
- Line cross-axis size = maximum child cross-axis size (including margins) in that line

**WrapReverse formula**: Uses FULL container cross dimension (including padding) for the flip, not inner dimension. This ensures symmetric positioning within the padding area.

---

### gap

| Attribute | Value |
|-----------|-------|
| **Type** | `string` |
| **Default** | `"0"` |
| **Units** | px, %, em |

```yaml
- type: flex
  direction: row
  gap: "10"
  children:
    - type: text
      content: "A"
    - type: text
      content: "B"
```

**Behavior**: Adds uniform spacing between children along the main axis. Gap is NOT added before the first child or after the last child. Equivalent to setting both `rowGap` and `columnGap` to the same value.

---

### rowGap / columnGap

| Attribute | Value |
|-----------|-------|
| **Type** | `string?` |
| **Default** | `null` (falls back to `gap`) |
| **Units** | px, %, em |
| **Status** | Planned (Phase 3) |

```yaml
- type: flex
  direction: row
  wrap: wrap
  columnGap: "10"    # Gap between items in a row (main axis)
  rowGap: "20"       # Gap between wrapped lines (cross axis)
```

**Behavior**: Separate gap values for the two axes. If not specified, falls back to the `gap` property.

**Gap-to-axis mapping**:

| Direction | Main-axis gap | Cross-axis gap |
|-----------|---------------|----------------|
| `row` / `rowReverse` | `columnGap` (or `gap`) | `rowGap` (or `gap`) |
| `column` / `columnReverse` | `rowGap` (or `gap`) | `columnGap` (or `gap`) |

---

### justify

| Attribute | Value |
|-----------|-------|
| **Type** | `JustifyContent` enum |
| **Default** | `JustifyContent.Start` (= 0) |
| **Values** | `Start` (0), `Center` (1), `End` (2), `SpaceBetween` (3), `SpaceAround` (4), `SpaceEvenly` (5) |

```yaml
- type: flex
  direction: row
  justify: spaceBetween
  width: "300"
  children:
    - type: text
      content: "Left"
    - type: text
      content: "Right"
```

**Behavior**: Controls alignment of items along the main axis using remaining free space.

| Value | Formula | Description |
|-------|---------|-------------|
| `start` | `leadingDim = 0` | Items packed at start |
| `center` | `leadingDim = freeSpace / 2` | Items centered |
| `end` | `leadingDim = freeSpace` | Items packed at end |
| `spaceBetween` | `between = freeSpace / (count - 1)` | Equal space between items |
| `spaceAround` | `leading = freeSpace / (2*count)`, `between = leading*2` | Equal space around items |
| `spaceEvenly` | `leading = freeSpace / (count + 1)`, `between = leading` | Equal space everywhere |

**Overflow fallback** (Phase 1): When `freeSpace < 0`, distribution-based values fall back to `start`:
- `spaceBetween` -> `start`
- `spaceAround` -> `start`
- `spaceEvenly` -> `start`

---

### align

| Attribute | Value |
|-----------|-------|
| **Type** | `AlignItems` enum |
| **Default** | `AlignItems.Stretch` (= 3) |
| **Values** | `Start` (0), `Center` (1), `End` (2), `Stretch` (3), `Baseline` (4) |

```yaml
- type: flex
  direction: row
  align: center
  height: "100"
  children:
    - type: text
      content: "Vertically centered"
```

**Behavior**: Controls alignment of items along the cross axis within a flex line.

| Value | Formula | Description |
|-------|---------|-------------|
| `start` | `crossPos = 0` | Items at cross-axis start |
| `center` | `crossPos = (lineHeight - childHeight) / 2` | Items centered |
| `end` | `crossPos = lineHeight - childHeight` | Items at cross-axis end |
| `stretch` | Child expands to fill `lineHeight` | Items stretch to fill (only if no explicit cross-axis size) |
| `baseline` | Aligned by text baselines | Fallback to `start` in column direction |

---

### alignContent

| Attribute | Value |
|-----------|-------|
| **Type** | `AlignContent` enum |
| **Default** | `AlignContent.Stretch` (= 3) |
| **Values** | `Start` (0), `Center` (1), `End` (2), `Stretch` (3), `SpaceBetween` (4), `SpaceAround` (5), `SpaceEvenly` (6, planned) |
| **Status** | Enum exists; `SpaceEvenly` planned in Phase 3 |

```yaml
- type: flex
  direction: row
  wrap: wrap
  alignContent: spaceBetween
  width: "300"
  height: "200"
  children:
    # ... items that wrap to multiple lines
```

**Behavior**: Controls distribution of flex lines along the cross axis in a multi-line (wrapping) container. Only has effect when `wrap` is `wrap` or `wrapReverse` AND there are multiple lines.

| Value | Formula | Description |
|-------|---------|-------------|
| `start` | `leadingCrossDim = 0` | Lines packed at start |
| `center` | `leadingCrossDim = crossFreeSpace / 2` | Lines centered |
| `end` | `leadingCrossDim = crossFreeSpace` | Lines at end |
| `stretch` | `extraPerLine = crossFreeSpace / lineCount` | Lines expand to fill |
| `spaceBetween` | `between = crossFreeSpace / (lineCount - 1)` | Space between lines |
| `spaceAround` | `leading = crossFreeSpace / (2*lineCount)`, `between = leading*2` | Space around lines |
| `spaceEvenly` | `leading = crossFreeSpace / (lineCount + 1)`, `between = leading` | Even distribution |

**Overflow fallback** (planned, Phase 3): When `crossFreeSpace < 0`:
- `spaceBetween` -> `start`
- `stretch` -> `start`
- `spaceAround` -> `start`
- `spaceEvenly` -> `start`
- `center` and `end` remain unchanged (allow overflow at start)

---

### overflow

| Attribute | Value |
|-----------|-------|
| **Type** | `Overflow` enum |
| **Default** | `Overflow.Visible` (= 0) |
| **Values** | `Visible` (0), `Hidden` (1) |
| **Status** | Planned (Phase 4) |

```yaml
- type: flex
  overflow: hidden
  width: "100"
  height: "100"
  children:
    - type: text
      content: "This long text will be clipped at the container bounds"
```

**Behavior**: Controls rendering of content that exceeds the container's bounds.
- `visible` -- content renders outside bounds (default)
- `hidden` -- content is clipped at the container's edges using `SKCanvas.ClipRect()`

**Important**: `overflow` is a **renderer concern only**. It does NOT affect layout calculations. Child positions and sizes are computed identically regardless of overflow setting. Only the rendering step applies clipping.

**Limitation**: `overflow: scroll` is not supported. Only `FlexElement` supports the `overflow` property.

---

## 4. Flex Item Properties

**Status**: Implemented (Phase 0 moved to base class, Phase 2 adds flex-basis/min/max)

These properties are on `TemplateElement` and apply when the element is a child of a flex container.

### grow

| Attribute | Value |
|-----------|-------|
| **Type** | `float` |
| **Default** | `0f` |
| **Constraint** | Must be >= 0 |

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

**Behavior**: Determines how much of the remaining free space the item receives. If all items have `grow: 0`, free space is not distributed (controlled by `justify`). If one or more items have `grow > 0`, free space is distributed proportionally to their grow factors.

**Formula**: `childSize = flexBasis + (freeSpace / totalGrowFactors) * growFactor`

**Factor flooring** (Phase 2): When `0 < totalGrowFactors < 1`, the total is floored to `1`. This prevents fractional grow factors from leaving undistributed space. For example, with `grow: 0.2, 0.2, 0.4` (total = 0.8), the total is treated as 1.0, distributing ALL free space proportionally.

---

### shrink

| Attribute | Value |
|-----------|-------|
| **Type** | `float` |
| **Default** | `1f` |
| **Constraint** | Must be >= 0 |

```yaml
- type: flex
  direction: row
  width: "200"
  children:
    - type: text
      shrink: 0         # Will NOT shrink
      width: "150"
      content: "Fixed"
    - type: text
      shrink: 1         # Absorbs all overflow
      width: "150"
      content: "Shrinks"
```

**Behavior**: Determines how much an item shrinks when children overflow the container. An item with `shrink: 0` will never shrink below its flex basis.

**CSS-spec formula** (basis-scaled, Phase 2):
```
scaledShrinkFactor = flexShrink * flexBasis
shrinkAmount = overflow * (scaledShrinkFactor / totalScaledShrinkFactors)
childSize = flexBasis - shrinkAmount
```

Items with larger basis shrink more (in absolute pixels) than items with smaller basis, even at the same shrink factor. This matches the CSS specification and Yoga reference.

**Limitation**: Before Phase 2, shrink used a simpler weighted formula (not scaled by basis).

---

### basis

| Attribute | Value |
|-----------|-------|
| **Type** | `string` |
| **Default** | `"auto"` |
| **Units** | px, %, em, auto |
| **Status** | Implemented (Phase 2) |

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

**Behavior**: Sets the initial main-axis size of the item before grow/shrink distribution.

**Resolution priority** (matches Yoga):
1. If `basis` is explicitly set (not `auto`) -> use basis value (clamped to padding floor)
2. If `basis` is `auto` and main-axis dimension is set (e.g., `width` for row) -> use that dimension
3. Otherwise -> use intrinsic (measured) size

**Padding floor**: Flex basis can never go below the element's total padding on the main axis.

**Limitation**: Before Phase 2, `basis` existed as a property but was never read by the layout engine.

---

### alignSelf

| Attribute | Value |
|-----------|-------|
| **Type** | `AlignSelf` enum |
| **Default** | `AlignSelf.Auto` (= 0) |
| **Values** | `Auto` (0), `Start` (1), `Center` (2), `End` (3), `Stretch` (4), `Baseline` (5) |
| **Status** | Implemented (Phase 1) |

```yaml
- type: flex
  direction: row
  align: stretch     # Container default
  height: "100"
  children:
    - type: text
      alignSelf: center    # Overrides parent's stretch
      content: "Centered"
    - type: text
      content: "Stretched"  # Uses parent's stretch
```

**Behavior**: Overrides the parent container's `align` (align-items) for this specific item.
- `auto` -- use the parent's `align` value
- All other values -- override the parent's alignment for this child only

**Yoga correspondence**: `resolveChildAlignment()` -- if align-self == Auto, use parent's align-items.

---

### order

| Attribute | Value |
|-----------|-------|
| **Type** | `int` |
| **Default** | `0` |
| **Status** | Property exists, NOT implemented in layout |

```yaml
- type: text
  order: 1
  content: "Displayed second"
```

**Behavior**: Currently, children are always processed in document order regardless of the `order` property. The property exists on the AST but is not read by the layout engine.

**Limitation**: `order` is defined but not implemented. Children are laid out in the order they appear in the YAML template.

---

### width / height

| Attribute | Value |
|-----------|-------|
| **Type** | `string?` |
| **Default** | `null` (auto) |
| **Units** | px, %, em |

```yaml
- type: text
  width: "200"
  height: "50"
  content: "Fixed size"

- type: text
  width: "50%"
  content: "Half of parent"
```

**Behavior**: Sets the explicit main-axis or cross-axis size of the element. When `null`, the element uses its intrinsic (content-based) size.

**Note for images/barcodes**: For `ImageElement` and `BarcodeElement`, the YAML `width`/`height` properties map to content-specific properties (`ImageWidth`/`BarcodeWidth`), NOT the base flex `Width`/`Height`. This preserves backward compatibility.

---

### minWidth / maxWidth / minHeight / maxHeight

| Attribute | Value |
|-----------|-------|
| **Type** | `string?` |
| **Default** | `null` (no constraint) |
| **Units** | px, %, em |
| **Status** | Implemented (Phase 2) |

```yaml
- type: text
  grow: 1
  minWidth: "50"
  maxWidth: "200"
  content: "Clamped between 50-200px"
```

**Behavior**: Constrains the element's size after flex resolution. Applied via `ClampSize(value, min, max)`.

**ClampSize formula** (CSS spec: min wins over max):
```
result = Math.Clamp(value, min, Math.Max(min, max))
```

If `min > max`, then `min` takes priority.

**Iterative freeze algorithm** (Phase 2): When min/max constraints cause items to freeze at their constrained sizes, the remaining free space is redistributed among unfrozen items. This iterates until no more items are frozen or all items are frozen.

---

### aspectRatio

| Attribute | Value |
|-----------|-------|
| **Type** | `float?` |
| **Default** | `null` (no aspect ratio) |
| **Status** | Planned (Phase 4) |

```yaml
- type: flex
  aspectRatio: 1.5    # width / height = 1.5
  width: "150"
  # height computed as 150 / 1.5 = 100
```

**Behavior**: When one dimension is known and the other is not, computes the missing dimension using the ratio (width / height).

**Application points** (per Yoga reference):
1. When explicit width is set but height is not: `height = width / aspectRatio`
2. When explicit height is set but width is not: `width = height * aspectRatio`
3. After flex resolution (main-axis size determined by grow/shrink): compute cross-axis size
4. After cross-axis stretch: compute main-axis size if aspect ratio set

**Known issue** (yoga-c-researcher finding): The initial implementation plan applies aspect ratio only at explicit dimensions. Yoga also applies it after flex resolution and stretch. The implementation should cover all application points.

**Limitation**: Margin is not subtracted from the dimension before computing the ratio. Yoga uses content size (excluding margins) for aspect ratio calculation.

---

### position

| Attribute | Value |
|-----------|-------|
| **Type** | `Position` enum |
| **Default** | `Position.Static` (= 0) |
| **Values** | `Static` (0), `Relative` (1), `Absolute` (2) |
| **Status** | Planned (Phase 4) |

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
    - type: text
      position: relative
      left: "5"
      top: "3"
      content: "Offset 5px right, 3px down"
```

**Behavior**:

**Static** (default): Normal flex flow. Inset properties (`top`/`right`/`bottom`/`left`) are ignored.

**Relative**: The element is positioned in normal flex flow first, then offset by the inset values. It still occupies space in the flow. Other children are NOT affected by the offset.
- `left` defined: shift right by `left` value
- `right` defined (and no `left`): shift left by `right` value (negative offset)
- `top` defined: shift down by `top` value
- `bottom` defined (and no `top`): shift up by `bottom` value (negative offset)
- `left` takes priority over `right`; `top` takes priority over `bottom`

**Absolute**: Removed from the flex flow entirely. Other children are laid out as if this element does not exist. The element is positioned relative to the nearest positioned ancestor (simplified to direct parent in FlexRender).
- If `left` defined: `X = padding.Left + left`
- If `right` defined (no `left`): `X = containerWidth - padding.Right - childWidth - right`
- If neither defined: positioned by parent's `justify-content` (main axis) / `align-items` (cross axis)

**Inset-based sizing** (planned): When both opposing insets are defined (e.g., `left` + `right`) and no explicit dimension is set, the child's size is computed: `width = containerWidth - paddingLeft - paddingRight - left - right`.

---

### top / right / bottom / left

| Attribute | Value |
|-----------|-------|
| **Type** | `string?` |
| **Default** | `null` |
| **Units** | px, %, em |
| **Status** | Planned (Phase 4) |

```yaml
- type: text
  position: absolute
  top: "20"
  left: "30"
  content: "At (30, 20)"
```

**Behavior**: Inset properties for positioned elements. Only have effect when `position` is `relative` or `absolute`. See `position` property for detailed behavior.

---

## 5. Layout Enums

Source: `src/FlexRender.Core/Layout/FlexEnums.cs`

### FlexDirection

| Value | Int | Description |
|-------|-----|-------------|
| `Row` | 0 | Main axis horizontal (left to right) |
| `Column` | 1 | Main axis vertical (top to bottom) |
| `RowReverse` | 2 | Main axis horizontal (right to left) |
| `ColumnReverse` | 3 | Main axis vertical (bottom to top) |

### FlexWrap

| Value | Int | Description |
|-------|-----|-------------|
| `NoWrap` | 0 | All items on one line |
| `Wrap` | 1 | Items wrap to new lines |
| `WrapReverse` | 2 | Items wrap in reverse cross-axis order |

### JustifyContent

| Value | Int | Description |
|-------|-----|-------------|
| `Start` | 0 | Items at start |
| `Center` | 1 | Items centered |
| `End` | 2 | Items at end |
| `SpaceBetween` | 3 | Space between items |
| `SpaceAround` | 4 | Space around items |
| `SpaceEvenly` | 5 | Even space everywhere |

### AlignItems

| Value | Int | Description |
|-------|-----|-------------|
| `Start` | 0 | Items at cross-axis start |
| `Center` | 1 | Items centered |
| `End` | 2 | Items at cross-axis end |
| `Stretch` | 3 | Items stretch to fill |
| `Baseline` | 4 | Aligned by baselines |

### AlignContent

| Value | Int | Description |
|-------|-----|-------------|
| `Start` | 0 | Lines at start |
| `Center` | 1 | Lines centered |
| `End` | 2 | Lines at end |
| `Stretch` | 3 | Lines stretch to fill |
| `SpaceBetween` | 4 | Space between lines |
| `SpaceAround` | 5 | Space around lines |
| `SpaceEvenly` | 6 | Even distribution (planned, Phase 3) |

### AlignSelf

| Value | Int | Description |
|-------|-----|-------------|
| `Auto` | 0 | Use parent's align-items |
| `Start` | 1 | Align to start |
| `Center` | 2 | Center |
| `End` | 3 | Align to end |
| `Stretch` | 4 | Stretch to fill |
| `Baseline` | 5 | Align by baseline |

### Display

| Value | Int | Description |
|-------|-----|-------------|
| `Flex` | 0 | Participates in layout |
| `None` | 1 | Hidden, removed from flow |

### Position (planned, Phase 4)

| Value | Int | Description |
|-------|-----|-------------|
| `Static` | 0 | Normal flow (default) |
| `Relative` | 1 | Offset from normal position |
| `Absolute` | 2 | Removed from flow |

### Overflow (planned, Phase 4)

| Value | Int | Description |
|-------|-----|-------------|
| `Visible` | 0 | Content visible outside bounds |
| `Hidden` | 1 | Content clipped at bounds |

---

## 6. Phase 1: Quick Wins

**Status**: Implemented

### Features

| Feature | Description | YAML Property |
|---------|-------------|---------------|
| FlexDirection reverse | `RowReverse`, `ColumnReverse` directions | `direction: rowReverse` |
| AlignSelf | Per-item cross-axis alignment override | `alignSelf: center` |
| Display: None | Remove element from layout flow | `display: none` |
| 4-side margins | CSS shorthand for individual margin sides | `margin: "10 20 30 40"` |
| Overflow fallback | SpaceBetween/SpaceAround/SpaceEvenly -> Start when freeSpace < 0 | (automatic) |

### Overflow Fallback Detail

When the total size of children exceeds the container (negative free space), distribution-based justify-content values produce nonsensical results (negative spacing). FlexRender follows the Yoga/CSS behavior and falls back:

| Original Value | Fallback (when freeSpace < 0) |
|----------------|-------------------------------|
| `spaceBetween` | `start` |
| `spaceAround` | `start` |
| `spaceEvenly` | `start` |
| `start`, `center`, `end` | (unchanged) |

---

## 7. Phase 2: Core Flexbox

**Status**: Implemented

### Features

| Feature | Description |
|---------|-------------|
| flex-basis resolution | Priority: explicit basis > main-axis dimension > intrinsic |
| Basis-scaled shrink | Shrink proportional to `shrink * basis` (CSS spec) |
| Factor flooring | `totalGrowFactors` in (0,1) floored to 1 |
| Min/Max constraints | `minWidth`, `maxWidth`, `minHeight`, `maxHeight` |
| Iterative freeze algorithm | Two-pass min/max constraint resolution |

### Flex-Basis Resolution

```
ResolveFlexBasis(element, layoutNode, isColumn, context):
  1. If basis != "auto" -> parse and resolve (px/%, em) -> clamp to padding floor
  2. If basis == "auto" and main-axis dimension is set -> use Width (row) or Height (column)
  3. Otherwise -> use intrinsic size from IntrinsicSizes dictionary
```

### Two-Pass Iterative Freeze Algorithm

More CSS-spec-compliant than Yoga's simplified two-pass:

```
1. Compute flex basis for each item
2. Calculate free space = availableSize - sum(basis)
3. For each item:
   a. Compute target size = basis + (freeSpace * factor / totalFactors)
   b. Clamp to [min, max]
   c. If clamped (target != unclamped): freeze at clamped size
4. If any items were frozen: recalculate free space excluding frozen items, repeat step 3
5. Continue until no more items are frozen or all items are frozen
```

### Factor Flooring

From Yoga (`FlexLine.cpp:104-112`): When `0 < totalGrowFactors < 1`, the total is floored to `1.0`. This prevents fractional grow factors from distributing less space than available.

Example: Three children with `grow: 0.2, 0.2, 0.4` (total = 0.8). Without flooring, only 80% of free space would be distributed. With flooring (total treated as 1.0), ALL free space is distributed proportionally.

---

## 8. Phase 3: Wrapping

**Status**: Planned

### Features

| Feature | Description | Task |
|---------|-------------|------|
| `rowGap` / `columnGap` | Separate gap axes | 3.1 |
| `AlignContent.SpaceEvenly` | Missing enum value | 3.2 |
| `FlexWrap.Wrap` | Line breaking | 3.5 |
| `FlexWrap.WrapReverse` | Reversed line order | 3.6 |
| `AlignContent` distribution | Multi-line cross-axis layout | 3.6 |
| `MaxFlexLines` limit | DoS protection | 3.4 |

### Line Breaking Algorithm

The line-breaking algorithm is greedy (matches Yoga):

```
CalculateFlexLines(children, availableMainSize, isColumn, gap, context):
  for each child:
    skip display:none and position:absolute children
    childMainSize = ClampSize(ResolveFlexBasis(child), min, max)
    childMarginMain = child margin on main axis
    totalChild = childMainSize + childMarginMain
    gapBefore = (itemsInLine > 0) ? gap : 0

    if itemsInLine > 0 AND lineMainSize + gapBefore + totalChild > availableMainSize:
      finalize line, start new line
    else:
      accumulate into current line
```

**Critical**: Line breaking uses `flexBasisWithMinAndMaxConstraints` (clamped flex basis), NOT the final layout size. Child margins on the main axis are included in the threshold.

### Per-Line Flex Resolution

Each line is resolved independently:
1. Compute remaining free space for the line: `freeSpace = availableMainSize - line.sizeConsumed`
2. Apply the iterative freeze algorithm from Phase 2 to the line's items only
3. Apply justify-content for the line
4. Factor flooring is applied per-line

### FlexLine Struct

```csharp
internal readonly record struct FlexLine(
    int StartIndex,
    int Count,
    float MainAxisSize,
    float CrossAxisSize);
```

### Align-Content Overflow Fallback

Same pattern as justify-content overflow fallback, applied to the cross axis:

| Original Value | Fallback (when crossFreeSpace < 0) |
|----------------|-------------------------------------|
| `spaceBetween` | `start` |
| `stretch` | `start` |
| `spaceAround` | `start` |
| `spaceEvenly` | `start` |
| `start`, `center`, `end` | (unchanged) |

### Resource Limit: MaxFlexLines

| Property | Default | Purpose |
|----------|---------|---------|
| `MaxFlexLines` | 1000 | Maximum flex lines in a wrapping container |

Prevents DoS via templates with thousands of tiny items creating excessive line count. Throws `InvalidOperationException` when exceeded. Configurable via `ResourceLimits.MaxFlexLines`.

---

## 9. Phase 4: Advanced Features

**Status**: Planned

### Features

| Feature | Description | Task |
|---------|-------------|------|
| `Position.Relative` | Offset from normal position | 4.3 |
| `Position.Absolute` | Removed from flow, positioned by insets | 4.3 |
| Inset properties | `top`, `right`, `bottom`, `left` | 4.2 |
| Inset-based sizing | Both opposing insets -> compute size | 4.3 |
| `Overflow.Hidden` | Clip rendering at container bounds | 4.4 |
| `AspectRatio` | Width/height ratio constraint | 4.5 |

### Absolute Positioning: Exclusion from Flex Flow

Absolute children are completely excluded from:
- Flex line calculation (line breaking skips them)
- Flex basis computation
- Container intrinsic sizing (they don't contribute to container's auto size)
- Justify-content and align-items distribution

After normal flex layout is complete, absolute children are positioned separately using their inset values.

### Absolute Positioning: Fallback Without Insets

When an absolute child has no insets defined, Yoga uses the parent's `justify-content` (main axis) and `align-items` / `align-self` (cross axis) to position it:

| justify-content | Main-axis position |
|-----------------|-------------------|
| `start`, `spaceBetween` | Padding start |
| `end` | Container end - child size - padding end |
| `center`, `spaceAround`, `spaceEvenly` | Centered in content box |

### Relative Positioning: Priority

When both opposing insets are defined:
- `left` takes priority over `right`
- `top` takes priority over `bottom`

### Containing Block

FlexRender simplifies containing block resolution: absolute children are always positioned relative to their **direct parent**. Yoga resolves to the nearest ancestor with `positionType != Static`, which is more complex but rarely needed for document rendering.

---

## 10. Phase 5: Auto Margins

**Status**: Planned

### Features

| Feature | Description | Task |
|---------|-------------|------|
| `MarginValue` / `MarginValues` types | Support for `auto` keyword in margins | 5.2 |
| Margin auto parsing | `PaddingParser.ParseMargin()` handles `"auto"` | 5.3 |
| Auto margin distribution | Consumes free space before justify-content | 5.4 |

### YAML Syntax

```yaml
# Center a child horizontally (row container)
- type: text
  margin: "auto"         # All sides auto -> centered both axes
  content: "Centered"

# Push child to right
- type: text
  margin: "0 0 0 auto"   # Left auto -> pushes to right
  content: "Right-aligned"

# Mixed
- type: text
  margin: "0 auto"       # Horizontal auto -> centered horizontally
  content: "H-centered"
```

### Behavior

Auto margins consume remaining free space **before** `justify-content` is applied. When any item has an auto margin on the main axis, justify-content is effectively overridden.

**Main axis auto margins**:
1. Count total auto margins across all items on the main axis
2. If `freeSpace > 0` and `autoCount > 0`: each auto margin = `freeSpace / autoCount`
3. If `freeSpace <= 0`: all auto margins resolve to `0` (no effect)
4. Justify-content is completely ignored when auto margins are present

**Cross axis auto margins**:
- Both auto (e.g., `margin-top: auto; margin-bottom: auto`): child is centered (like `align-self: center`)
- Only start auto: child pushed to end
- Only end auto: child stays at start
- If `crossFreeSpace <= 0`: auto margins resolve to `0`

### MarginValue Type

```csharp
public readonly record struct MarginValue(float? Pixels, bool IsAuto)
{
    public static MarginValue Auto => new(null, true);
    public static MarginValue Fixed(float px) => new(px, false);
    public float ResolvedPixels => Pixels ?? 0f;
}
```

### MarginValues Type

```csharp
public readonly record struct MarginValues(
    MarginValue Top, MarginValue Right, MarginValue Bottom, MarginValue Left)
{
    public bool HasAuto => Top.IsAuto || Right.IsAuto || Bottom.IsAuto || Left.IsAuto;
    public int MainAxisAutoCount(bool isColumn);
    public int CrossAxisAutoCount(bool isColumn);
}
```

---

## 11. Resource Limits

Source: `src/FlexRender.Core/Configuration/ResourceLimits.cs`

| Property | Default | Phase | Purpose |
|----------|---------|-------|---------|
| `MaxTemplateFileSize` | 1 MB | existing | YAML template file size |
| `MaxDataFileSize` | 10 MB | existing | JSON data file size |
| `MaxTemplateNestingDepth` | 100 | existing | Expression nesting |
| `MaxRenderDepth` | 100 | existing | Render tree recursion |
| `MaxImageSize` | 10 MB | existing | Image loading |
| `MaxFlexLines` | 1000 | Phase 3 | Flex lines in wrapping containers |

```yaml
# Configuration via builder:
```
```csharp
var render = new FlexRenderBuilder()
    .WithLimits(limits =>
    {
        limits.MaxFlexLines = 2000;
        limits.MaxRenderDepth = 200;
    })
    .Build();
```

---

## 12. Known Limitations

### Differences from CSS/Yoga

| Area | FlexRender | CSS/Yoga | Notes |
|------|-----------|----------|-------|
| `order` | Property exists, not read by layout | Items sorted by `order` before layout | Children always in document order |
| `display` | Only `flex` and `none` | Full CSS display values | No `block`, `inline`, `grid` |
| `overflow` | Only `visible` and `hidden` | Also `scroll`, `auto` | `scroll` affects auto-sizing in Yoga |
| Borders | No border support | Borders affect sizing and inset positioning | Padding serves as border equivalent for inset calculations |
| RTL / writing modes | LTR only | Full bidirectional support | Physical properties only (`left`/`right`), no logical (`start`/`end`) |
| Containing block | Direct parent only | Nearest positioned ancestor | Simplified; sufficient for document rendering |
| Aspect ratio application | Explicit dimensions only (Phase 4 initial) | Also after flex resolution and stretch | Planned improvement |
| `baseline` alignment | Fallback to `start` in column direction | Baseline calculation from text metrics | Baseline requires font metric integration |
| `flex-wrap` + `align-self` | Not yet combined | Each child aligned within its line | Planned in Phase 3 |
| `min-content` / `max-content` | Not supported | Intrinsic sizing keywords | FlexRender uses its own intrinsic measurement |
| `gap` units | px, %, em | Also `calc()`, CSS variables | No `calc()` support |

### AOT Compatibility

All types and properties are AOT-compatible:
- No reflection usage
- No `dynamic` keyword
- Pattern matching via `switch` on concrete types
- `GeneratedRegex` for all regex patterns
- All classes are `sealed` unless designed for inheritance

### Element-Specific Width/Height

For `ImageElement` and `BarcodeElement`, the YAML `width`/`height` properties map to content-specific properties (`ImageWidth`/`ImageHeight`, `BarcodeWidth`/`BarcodeHeight`), not the base flex `Width`/`Height`. The flex-level width/height must be set separately if needed. This preserves backward compatibility with pre-flexbox templates.
