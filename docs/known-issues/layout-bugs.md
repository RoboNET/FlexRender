# Layout Bug History

This document tracks layout bugs that have been identified and fixed in the
FlexRender layout engine. All bugs listed here are now **RESOLVED**.

Each bug had corresponding unit tests in `tests/FlexRender.Tests/Layout/FlexLayoutBugTests.cs`
that were initially skipped and are now passing.

---

## Bug #1: align-items: end is ignored when container height is auto

**Status:** ✅ Fixed in commit XXXX
**Test:** `AlignItemsEnd_AutoHeightRow_ChildrenAlignedToBottom`, `AlignItemsEnd_AutoHeightRow_LayoutPositions`

### Description

When a row flex container has no explicit `height` (auto-sized from content),
the `align: end` property is completely ignored. All children are placed at
Y=0 (top-aligned), regardless of the `align` setting.

Per the CSS Flexbox specification, an auto-height row container should:
1. Determine its height from the tallest child.
2. Apply `align-items` to position each child relative to that computed height.

### Reproduction (YAML)

```yaml
canvas:
  fixed: both
  width: 180
  height: 80
  background: "#FFFFFF"
layout:
  - type: flex
    direction: row
    align: end
    width: 180
    # No height -- auto-sized
    children:
      - type: flex
        width: 60
        height: 40
        background: "#FF0000"
      - type: flex
        width: 60
        height: 60
        background: "#00FF00"
      - type: flex
        width: 60
        height: 80
        background: "#0000FF"
```

### Expected Behavior

Container auto-height resolves to 80px (tallest child). Children are aligned
to the bottom:

| Child | Height | Expected Y |
|-------|--------|------------|
| Red   | 40px   | 40         |
| Green | 60px   | 20         |
| Blue  | 80px   | 0          |

### Actual Behavior

All children have Y=0. The layout engine does not apply cross-axis alignment
when the container height is auto-computed.

### Root Cause

The layout engine computed the auto-height in a first pass, but did not
perform a second pass to re-position children based on the resolved container
height. The cross-axis alignment logic only ran when an explicit height was set.

The `hasExplicitHeight` guard prevented alignment logic from executing for
auto-sized containers.

### Fix

Modified `RowFlexLayoutStrategy.cs` to compute `crossAxisSize` from the tallest
child when `hasExplicitHeight` is false. This allows the alignment logic to
execute correctly even for auto-height containers.

The fix involved 6 changes:
1. Compute crossAxisSize from children after layout
2. Remove hasExplicitHeight guard from align-items logic
3. Remove hasExplicitHeight guard from stretch logic
4. Remove hasExplicitHeight guard from auto margin logic
5-6. Apply same fixes to ApplyRowCrossAxisMargins method

---

## Bug #2: align-items: center is ignored when container height is auto

**Status:** ✅ Fixed in commit XXXX (same fix as Bug #1)
**Test:** `AlignItemsCenter_AutoHeightRow_ChildrenVerticallyCentered`, `AlignItemsCenter_AutoHeightRow_LayoutPositions`

### Description

Same root cause as Bug #1. When a row flex container had auto height,
`align-items: center` was ignored and all children were placed at Y=0
(top-aligned).

### Reproduction (YAML)

```yaml
canvas:
  fixed: both
  width: 180
  height: 80
  background: "#FFFFFF"
layout:
  - type: flex
    direction: row
    align: center
    width: 180
    # No height -- auto-sized
    children:
      - type: flex
        width: 60
        height: 40
        background: "#FF0000"
      - type: flex
        width: 60
        height: 60
        background: "#00FF00"
      - type: flex
        width: 60
        height: 80
        background: "#0000FF"
```

### Expected Behavior

Container auto-height resolves to 80px. Children are vertically centered:

| Child | Height | Expected Y         |
|-------|--------|---------------------|
| Red   | 40px   | (80 - 40) / 2 = 20 |
| Green | 60px   | (80 - 60) / 2 = 10 |
| Blue  | 80px   | (80 - 80) / 2 = 0  |

### Actual Behavior (Before Fix)

All children had Y=0. Same as Bug #1.

### Fix

Fixed by the same changes as Bug #1 in `RowFlexLayoutStrategy.cs`.

---

## Bug #3: Vertical separator without explicit height renders as tiny dot

**Status:** ✅ Fixed in commit XXXX (same fix as Bug #1)
**Test:** `VerticalSeparator_InRowWithExplicitHeight_StretchesToContainerHeight`, `VerticalSeparator_InAutoHeightRow_StretchesToContentHeight`, `VerticalSeparator_InRowWithExplicitHeight_LayoutStretchesHeight`, `VerticalSeparator_InAutoHeightRow_LayoutStretchesToSiblingHeight`

### Description

A vertical `SeparatorElement` placed inside a row flex container does not
stretch to the container height when it has no explicit `height`. Instead, it
renders as a tiny dot or minimal-height line (using its thickness as height).

The default `align-items` value for flex containers is `stretch`, which means
items without an explicit cross-axis dimension should stretch to fill the
container. This works for `FlexElement` children but not for `SeparatorElement`.

### Reproduction (YAML)

```yaml
canvas:
  fixed: both
  width: 200
  height: 100
  background: "#FFFFFF"
layout:
  - type: flex
    direction: row
    height: 100
    width: 200
    children:
      - type: flex
        width: 80
        height: 80
        background: "#FF0000"
      - type: separator
        orientation: vertical
        thickness: 2
        color: "#000000"
        style: solid
        # No height -- should stretch to 100px
      - type: flex
        width: 80
        height: 80
        background: "#0000FF"
```

### Expected Behavior

The separator should stretch to the container cross-axis size (100px height in
a row container), rendering as a visible 2px-wide by 100px-tall black line.

### Actual Behavior (Before Fix)

The separator had a height of approximately 2px (its thickness value), rendering
as a barely visible dot at the top of the container.

### Root Cause

The separator was properly requesting to stretch, but the stretch logic was
disabled by the `hasExplicitHeight` guard. This affected all elements including
separators.

### Fix

Fixed by the same changes as Bug #1 in `RowFlexLayoutStrategy.cs`. Removing the
`hasExplicitHeight` guards from the stretch logic allows separators to stretch
correctly to the container's computed cross-axis size.

---

---

## Bug #4: Image without explicit size does not inherit container dimensions

**Status:** ✅ Fixed in commit XXXX
**Test:** `ComputeLayout_ImageWithoutSize_UsesContainerDimensions`, `ComputeLayout_ImageWithoutSize_InSizedContainer`

### Description

When an image element has no explicit `ImageWidth`/`ImageHeight` (width/height attributes),
but is placed inside a flex container with explicit dimensions (e.g., 200×200),
the image should use the container dimensions for layout and apply the `fit` mode correctly.

### Reproduction (YAML)

```yaml
layout:
  - type: flex
    width: "200"
    height: "200"
    children:
      - type: image
        src: "test.png"  # 300×200 image
        fit: contain
        # NO width or height specified
```

### Expected Behavior

The image should:
1. Use container dimensions (200×200) from layout engine
2. Apply `fit: contain` to scale the 300×200 image to fit within 200×200 with letterboxing

### Actual Behavior (Before Fix)

The image used its intrinsic size (300×200), ignoring:
- Container dimensions
- The `fit` parameter

The image overflowed the 200×200 container.

### Root Cause

1. `LayoutEngine` computed dimensions with fallback: `Width ?? ImageWidth ?? ContainerWidth`
2. But `ImageProvider.Generate()` only used: `ImageWidth ?? intrinsicWidth`
3. The layout-computed dimensions were not passed to the rendering layer

### Fix

Modified three files to pass layout-computed dimensions to ImageProvider:

1. **LayoutEngine.cs (line 534)** - Use `ContainerHeight` instead of `DefaultTextHeight`:
   ```csharp
   var contentHeight = context.ResolveHeight(image.Height) ?? image.ImageHeight ?? context.ContainerHeight;
   ```

2. **ImageProvider.cs (lines 53, 185-188)** - Added optional `layoutWidth`/`layoutHeight` parameters:
   ```csharp
   public static SKBitmap Generate(..., int? layoutWidth = null, int? layoutHeight = null)
   {
       var targetWidth = layoutWidth ?? element.ImageWidth ?? source.Width;
       var targetHeight = layoutHeight ?? element.ImageHeight ?? source.Height;
   }
   ```

3. **RenderingEngine.cs (lines 269-275)** - Pass computed dimensions from layout:
   ```csharp
   using (var bitmap = ImageProvider.Generate(
       image, imageCache, renderOptions.Antialiasing,
       layoutWidth: (int)width,
       layoutHeight: (int)height))
   ```

**Priority order for image dimensions:**
1. Layout-computed dimensions (from LayoutNode)
2. Explicit ImageWidth/ImageHeight
3. Intrinsic image size

---

## Bug #5: QR and Barcode without explicit size do not inherit container dimensions

**Status:** ✅ Fixed
**Test:** `ComputeLayout_QrWithoutSize_UsesContainerDimensions`, `ComputeLayout_BarcodeWithoutSize_UsesContainerDimensions`

### Description

Similar to Bug #4, QR and Barcode elements without explicit size properties did not inherit
container dimensions. Instead, they used hardcoded defaults (QR: 100×100, Barcode: 200×80),
preventing them from automatically filling sized containers.

### Reproduction (YAML)

```yaml
layout:
  - type: flex
    width: "150"
    height: "150"
    background: "#f0f0f0"
    children:
      - type: qr
        data: "https://example.com"
        # NO size specified
```

### Expected Behavior

The QR code should use container dimensions (150×150) when `size` is not specified.

### Actual Behavior (Before Fix)

The QR code used its default size (100×100), leaving white space in the 150×150 container.

### Root Cause

1. QR `Size` property had a non-nullable default: `int Size { get; set; } = 100;`
2. Barcode `BarcodeWidth/Height` had non-nullable defaults: `int BarcodeWidth { get; set; } = 200;`
3. The layout engine couldn't distinguish between "user set Size=100" and "default Size=100"
4. Container dimension fallbacks in `LayoutEngine.cs` were never reached

### Fix

Applied the same pattern as Bug #4:

1. **Changed AST properties to nullable:**
   ```csharp
   // QrElement.cs
   public int? Size { get; set; }  // was: int Size { get; set; } = 100;

   // BarcodeElement.cs
   public int? BarcodeWidth { get; set; }  // was: int BarcodeWidth { get; set; } = 200;
   public int? BarcodeHeight { get; set; } // was: int BarcodeHeight { get; set; } = 80;
   ```

2. **Updated LayoutEngine.cs** to use container dimensions as fallbacks:
   ```csharp
   // QR: Priority: flex Width/Height > Size > container defaults
   var contentWidth = context.ResolveWidth(qr.Width) ?? (float?)qr.Size ?? context.ContainerWidth;

   // Barcode: Priority: flex Width/Height > BarcodeWidth/Height > container defaults
   var contentWidth = context.ResolveWidth(barcode.Width) ?? (float?)barcode.BarcodeWidth ?? context.ContainerWidth;
   ```

3. **Updated providers** to add sensible defaults when properties are null:
   ```csharp
   // QrProvider.cs
   var targetSize = layoutWidth ?? element.Size ?? 100;

   // BarcodeProvider.cs
   var targetWidth = layoutWidth ?? element.BarcodeWidth ?? 200;
   var targetHeight = layoutHeight ?? element.BarcodeHeight ?? 80;
   ```

4. **Updated IntrinsicMeasurer.cs** to handle nullable sizes

**Priority order:**
1. Layout-computed dimensions (from container)
2. Explicit Size/BarcodeWidth/BarcodeHeight
3. Provider-level defaults (100px for QR, 200×80 for Barcode)

---

## Summary Table

| Bug | Issue | Container Dims | Status |
|-----|-------|----------------|--------|
| #1  | align-items: end ignored  | auto height | ✅ Fixed |
| #2  | align-items: center ignored | auto height | ✅ Fixed |
| #3  | Vertical separator too small | explicit or auto | ✅ Fixed |
| #4  | Image without size doesn't inherit | explicit | ✅ Fixed |
| #5  | QR/Barcode without size don't inherit | explicit | ✅ Fixed |

**Bugs #1-3** were resolved by a single fix to `RowFlexLayoutStrategy.cs` that enables
cross-axis alignment and stretching to work correctly with auto-height containers.

**Bugs #4-5** were resolved by making size properties nullable and adding container dimension
fallbacks in `LayoutEngine.cs`, enabling automatic dimension inheritance from parent containers.
