# Architecture: FlexRender Layout Engine Expansion

Total: ~53 story points across 6 phases.

## Phase 0: Refactoring (SP: 5)

### 0a. Move flex-item properties to base TemplateElement (SP: 3)

**Problem**: Flex-item properties (Grow, Shrink, Basis, AlignSelf, Order, Width, Height) are duplicated across 6 concrete element types. Adding min/max + display + position would mean 60+ new property definitions.

**Solution**: Move to base `TemplateElement`:
```csharp
TemplateElement (abstract)
  // Existing:
  + Padding: string
  + Margin: string            // Will become 4-side support in Phase 1b

  // Move from concrete classes:
  + Width: string?
  + Height: string?
  + Grow: float
  + Shrink: float = 1f
  + Basis: string = "auto"
  + AlignSelf: AlignSelf = Auto
  + Order: int

  // New (Phase 1-5):
  + MinWidth: string?         // Phase 2
  + MaxWidth: string?         // Phase 2
  + MinHeight: string?        // Phase 2
  + MaxHeight: string?        // Phase 2
  + Display: Display = Flex   // Phase 1
  + Position: Position = Static  // Phase 4
  + Top/Right/Bottom/Left: string?  // Phase 4
  + AspectRatio: float?       // Phase 4
```

**CRITICAL: ImageElement/BarcodeElement Width/Height mapping**:
- `ImageElement.ImageWidth: int?` = image content size (YAML `width` maps here)
- `TemplateElement.Width: string?` = flex layout override
- Parser MUST NOT map YAML `width`/`height` to base `Width`/`Height` for Image/Barcode
- Layout engine already handles fallback: `context.ResolveWidth(image.Width) ?? image.ImageWidth ?? context.ContainerWidth`

**Migration**: One commit. All tests pass through LayoutEngine, not typed property access.

### 0b. Remove unused FlexContainerProperties/FlexItemProperties (SP: 1)

Dead code. Only in own files + own tests + llms-full.txt. Not used in LayoutEngine, TemplateParser, or SkiaRenderer.

### 0c. ResourceLimits injection in LayoutEngine (SP: 1)

```csharp
public sealed class LayoutEngine
{
    private readonly ResourceLimits _limits;
    public LayoutEngine(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
    }
}
```

Update SkiaRenderer: `new LayoutEngine(_limits)` (one call site).

---

## Phase 1: Quick Wins (SP: 10)

### 1a. align-self (SP: 2)

AlignSelf property already exists on elements but LayoutEngine ignores it.

```csharp
// In LayoutColumnFlex/LayoutRowFlex, replace direct flex.Align with:
var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);

private static AlignItems GetEffectiveAlign(TemplateElement element, AlignItems parentAlign)
{
    return element.AlignSelf switch
    {
        AlignSelf.Auto => parentAlign,
        AlignSelf.Start => AlignItems.Start,
        AlignSelf.Center => AlignItems.Center,
        AlignSelf.End => AlignItems.End,
        AlignSelf.Stretch => AlignItems.Stretch,
        _ => parentAlign
    };
}
```

### 1b. margin 4-side (SP: 3)

Current: Margin is uniform string ("10"), parsed as one float via UnitParser.
Change: Reuse PaddingParser for margin ("10 20" or "10 20 30 40" format).
Margin now returns PaddingValues (Top, Right, Bottom, Left).

### 1c. display:none (SP: 2)

```csharp
public enum Display { Flex = 0, None = 1 }
```
- LayoutEngine: skip children with `Display == None`
- MeasureIntrinsic: return `IntrinsicSize(0,0,0,0)` for Display.None
- SkiaRenderer.RenderNode: skip nodes with Display.None

### 1d. reverse directions (SP: 2)

Add `RowReverse = 2`, `ColumnReverse = 3` to FlexDirection enum.
```csharp
var isReverse = flex.Direction is FlexDirection.RowReverse or FlexDirection.ColumnReverse;
var isColumn = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;
var children = isReverse ? node.Children.AsEnumerable().Reverse() : node.Children;
```

### 1e. justify-content overflow fallback (SP: 1)

When `freeSpace < 0`, SpaceBetween/SpaceAround/SpaceEvenly fallback to FlexStart:
```csharp
var effectiveJustify = freeSpace < 0 ? flex.Justify switch
{
    JustifyContent.SpaceBetween => JustifyContent.Start,
    JustifyContent.SpaceAround => JustifyContent.Start,
    JustifyContent.SpaceEvenly => JustifyContent.Start,
    _ => flex.Justify
} : flex.Justify;
```

---

## Phase 2: Core Flexbox (SP: 13)

### 2a. Flex resolution algorithm (SP: 8)

**Two-pass algorithm** (CSS Flexbox spec Section 9.7, Yoga-style):

```
ResolveFlexibleLengths(items, availableSpace):
  1. Determine hypothetical main size for each item (basis or width/height or intrinsic)
  2. Determine grow vs shrink mode

  Loop:
  3. Freeze items: grow=0 items, items >= max, items <= min
  4. Calculate remaining free space (excluding frozen)
  5. Distribute: grow proportional to flex-grow, shrink proportional to flex-shrink * flex-basis
  6. Clamp to min/max
  7. If any clamped -> freeze, goto 4. If none -> done.
```

**Shrink formula** (CSS spec compliant):
```csharp
var scaledShrinkFactor = GetFlexShrink(child.Element) * basis;
var shrinkAmount = overflow * scaledShrinkFactor / totalScaledShrinkFactors;
var newSize = Math.Max(0, basis - shrinkAmount);
```

**ClampSize** (min wins over max per CSS spec):
```csharp
private static float ClampSize(float value, float min, float max)
{
    var effectiveMax = Math.Max(min, max); // min wins
    return Math.Clamp(value, min, effectiveMax);
}
```

### 2b. min/max AST properties + parser (SP: 5)

New properties on TemplateElement: MinWidth, MaxWidth, MinHeight, MaxHeight (all `string?`).
Parse in YAML parser, resolve in LayoutEngine, integrate with IntrinsicSize.

---

## Phase 3: Wrapping (SP: 11)

### 3a. flex-wrap + align-content (SP: 9)

**New struct**:
```csharp
internal readonly record struct FlexLine(int StartIndex, int Count, float MainAxisSize, float CrossAxisSize);
```

**Algorithm**:
1. Line Breaking: greedy, new line when `sizeConsumed + itemSize + margin + gap > availableSpace`
2. Per line: apply flex-grow/shrink, justify-content, align-items
3. Apply align-content: distribute lines across cross axis (7 values: Start, Center, End, Stretch, SpaceBetween, SpaceAround, SpaceEvenly)
4. WrapReverse: invert cross-axis positions

**ResourceLimit**: `MaxFlexLines = 1000` to prevent DoS.

### 3b. row-gap/column-gap (SP: 2)

Add RowGap, ColumnGap on FlexElement. With wrap: rowGap between lines, columnGap between items.

---

## Phase 4: Advanced (SP: 11)

### 4a. position absolute/relative (SP: 5)

```csharp
public enum Position { Static = 0, Relative = 1, Absolute = 2 }
```
- Absolute: removed from flex flow, positioned after normal layout
- Relative: layout normally, then offset by top/left

### 4b. overflow:hidden (SP: 3)

```csharp
public enum Overflow { Visible = 0, Hidden = 1 }
```
SkiaRenderer: `canvas.Save()` + `canvas.ClipRect(bounds)` + render + `canvas.Restore()`

### 4c. aspect-ratio (SP: 3)

`AspectRatio: float?` on TemplateElement. If one dimension known: compute other via ratio. Applied after min/max clamping.

---

## Phase 5: Auto Margins (SP: 3)

### Parsing
Extend PaddingParser with `ParseMargin()` supporting "auto" token.

### Data types
```csharp
public readonly record struct MarginValue(float? Pixels, bool IsAuto)
{
    public static MarginValue Auto => new(null, true);
    public static MarginValue Fixed(float px) => new(px, false);
}

public readonly record struct MarginValues(
    MarginValue Top, MarginValue Right, MarginValue Bottom, MarginValue Left);
```

### Algorithm in LayoutEngine
Insert BEFORE justify-content in LayoutColumnFlex/LayoutRowFlex:
1. Count auto margins on main axis
2. If any: `spacerPerAuto = freeSpace / autoCount`, justify-content IGNORED
3. Cross axis: both auto = center; one auto = push to opposite side
4. Fallback: negative freeSpace -> auto margins = 0

### Files
1. `Layout/Units/MarginValues.cs` -- new file
2. `Layout/Units/PaddingParser.cs` -- add ParseMargin()
3. `Layout/LayoutEngine.cs` -- auto margin logic before justify-content
4. YAML parser -- support "auto" in margin strings

---

## Constraints (all phases)

1. **AOT compatible** -- no reflection, Type.GetType(), dynamic
2. **sealed classes** -- all new classes sealed
3. **Null checks** -- ArgumentNullException.ThrowIfNull()
4. **ResourceLimits** -- preserved, new MaxFlexLines
5. **Switch-based dispatch** -- for container-specific properties
6. **XML docs** -- on all public APIs
7. **Snapshot tests** -- for each new layout feature
8. **Backward compatibility** -- existing YAML templates unchanged

## C# Review Results (all APPROVED)

| Decision | Verdict | Risk |
|----------|---------|------|
| Flex-item props in base class | APPROVED | Low (keep Image/Barcode Width/Height mapping) |
| Display/Position/Overflow enums | APPROVED | Low |
| FlexLine readonly record struct | APPROVED | None |
| Remove FlexContainerProperties/FlexItemProperties | SAFE | None (dead code) |
| MaxFlexLines = 1000 | APPROVED | None |
| ClampSize order (min wins over max) | CORRECT per CSS spec | None |
