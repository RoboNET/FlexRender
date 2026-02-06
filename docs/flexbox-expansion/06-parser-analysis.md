# Parser Analysis: What Changes Per Phase

## Current Parser Architecture

### File: `src/FlexRender.Yaml/Parsing/TemplateParser.cs` (1028 lines)

### Element Parser Registry (line 52)
```csharp
_elementParsers = new Dictionary<string, Func<YamlMappingNode, TemplateElement>>
{
    ["text"] = ParseTextElement,
    ["flex"] = ParseFlexElement,
    ["qr"] = ParseQrElement,
    ["barcode"] = ParseBarcodeElement,
    ["image"] = ParseImageElement,
    ["separator"] = ParseSeparatorElement,
    ["each"] = ParseEachElement,
    ["if"] = ParseIfElement
};
```

### Key Methods

#### `ApplyFlexItemProperties` (line 332-408)
Applies flex-item properties to parsed elements via pattern-matched `switch`:
- Parses: `width`, `height`, `grow`, `shrink`, `basis`, `order`, `alignSelf`
- Enum parsing: `alignSelf` string -> `AlignSelf` enum (line 342-351)
- Switch on element type: FlexElement, TextElement, QrElement, BarcodeElement, ImageElement, SeparatorElement
- **Special case**: BarcodeElement and ImageElement do NOT map YAML `width`/`height` to base Width/Height (YAML width/height maps to BarcodeWidth/BarcodeHeight and ImageWidth/ImageHeight instead)
- This method is called at the end of EVERY element parser

#### `ParseFlexElement` (line 459-525)
Container properties parsed:
- `gap` -> string (default "0")
- `padding` -> string (default "0")
- `margin` -> string (default "0")
- `background` -> string? (no default)
- `rotate` -> string (default "none")
- `direction` -> FlexDirection enum (default "column")
- `wrap` -> FlexWrap enum (default "nowrap")
- `justify` -> JustifyContent enum (default "start")
- `align` -> AlignItems enum (default "stretch")
- Children parsed recursively via `ParseElements`
- `ApplyFlexItemProperties` called at end

**NOT parsed currently**: `alignContent`, `display`, `position`, `overflow`, `rowGap`, `columnGap`

#### Enum Parsing Pattern
All enums use the same pattern:
```csharp
var str = GetStringValue(node, "propertyName", "defaultValue");
element.Property = str.ToLowerInvariant() switch
{
    "value1" => EnumType.Value1,
    "value2" => EnumType.Value2,
    _ => EnumType.DefaultValue
};
```
This pattern is used for: direction, wrap, justify, align, alignSelf, textAlign, textOverflow, imageFit, barcodeFormat, separatorOrientation, separatorStyle, errorCorrection.

### AST Model Structure

**Base class** `TemplateElement` (abstract):
- `Rotate`, `Background`, `Padding`, `Margin` (common properties)
- Flex-item properties are **duplicated** on each concrete class (not on base)

**Concrete classes** with flex-item properties:
- `FlexElement` (+ container props: Direction, Wrap, Gap, Justify, Align, AlignContent, Children)
- `TextElement` (+ Content, Font, Size, Color, Align, Wrap, Overflow, MaxLines, LineHeight)
- `QrElement` (+ Data, Size, Foreground, Background, ErrorCorrection)
- `BarcodeElement` (+ Data, BarcodeWidth, BarcodeHeight, ShowText, Format, Foreground, Background)
- `ImageElement` (+ Src, ImageWidth, ImageHeight, Fit)
- `SeparatorElement` (+ Orientation, Style, Thickness, Color)

Each has: `Grow`, `Shrink`, `Basis`, `AlignSelf`, `Order`, `Width?`, `Height?`

### Layout Engine Dispatch Pattern
`LayoutEngine` also uses `switch` dispatch on element type for:
- `GetFlexGrow` (line 945)
- `GetFlexShrink` (line 976)
- `HasExplicitHeight` (line 999)
- `HasExplicitWidth` (line 1013)
- `MeasureIntrinsic` (line 174)
- `LayoutElement` (line 434)

---

## Changes Per Phase

### Phase 0: Refactoring

#### 0a. Move flex-item properties to base TemplateElement

**Parser changes** (`TemplateParser.cs`):
- `ApplyFlexItemProperties` (line 332-408): **SIMPLIFY** — remove entire switch block, apply directly to `element` base class:
  ```csharp
  element.Width = width;  // except Image/Barcode special case
  element.Height = height;
  element.Grow = grow;
  element.Shrink = shrink;
  element.Basis = basis;
  element.Order = order;
  element.AlignSelf = alignSelf;
  ```
  Still need switch for Image/Barcode Width/Height exclusion.

**AST changes** (`TemplateElement.cs`):
- Add: `Width`, `Height`, `Grow`, `Shrink`, `Basis`, `AlignSelf`, `Order` to base class
- Remove from: FlexElement, TextElement, QrElement, BarcodeElement, ImageElement, SeparatorElement

**LayoutEngine changes**:
- `GetFlexGrow`, `GetFlexShrink`, `HasExplicitWidth`, `HasExplicitHeight`: **SIMPLIFY** — remove switch, use `element.Grow` directly
- Keep Image/Barcode fallback logic in `HasExplicitWidth`

**Test changes**:
- All existing tests should pass unchanged (they use layout engine, not direct property access on specific types)
- Some AST tests in `Parsing/Ast/` may reference typed properties

#### 0b. Remove FlexContainerProperties/FlexItemProperties
- Delete: `FlexContainerProperties.cs`, `FlexItemProperties.cs`
- Delete: `FlexContainerPropertiesTests.cs`, `FlexItemPropertiesTests.cs`
- Update: `llms-full.txt`

#### 0c. ResourceLimits injection
- No parser changes
- LayoutEngine constructor change

---

### Phase 1: Quick Wins

#### 1a. align-self
**Parser**: NO changes (already parsed in `ApplyFlexItemProperties` line 341-351)
**LayoutEngine**: Add `GetEffectiveAlign()` method, use in `LayoutColumnFlex`/`LayoutRowFlex`
**Tests**: New `LayoutEngineAlignSelfTests.cs`

#### 1b. margin 4-side
**Parser**: Each element parser already reads `Margin = GetStringValue(node, "margin", "0")`
  - No parser changes needed (PaddingParser already supports multi-value strings)
**LayoutEngine**: Replace `UnitParser.Parse(margin).Resolve()` (single float) with `PaddingParser.Parse(margin)` (PaddingValues)
  - Affects: `LayoutTextElement`, `LayoutQrElement`, `LayoutBarcodeElement`, `LayoutImageElement`, `LayoutSeparatorElement`, `LayoutColumnFlex`, `LayoutRowFlex`
  - Affects: `ApplyPaddingAndMargin` in intrinsic measurement
**Tests**: Extend `LayoutEnginePaddingMarginTests.cs`

#### 1c. display:none
**AST**: Add `Display` property to `TemplateElement` base
  ```csharp
  public Display Display { get; set; } = Display.Flex;
  ```
**Enums**: New `Display` enum in `FlexEnums.cs`
**Parser** (`TemplateParser.cs`):
  - Add `display` parsing in `ApplyFlexItemProperties` (after Phase 0 refactoring):
    ```csharp
    var displayStr = GetStringValue(node, "display", "flex");
    element.Display = displayStr.ToLowerInvariant() switch {
        "flex" => Display.Flex,
        "none" => Display.None,
        _ => Display.Flex
    };
    ```
**LayoutEngine**: Skip `Display.None` elements in `LayoutElement`, `MeasureIntrinsic`
**Tests**: New tests in `LayoutEngineTests.cs` or separate file

#### 1d. reverse directions
**Enums** (`FlexEnums.cs`): Add `RowReverse = 2`, `ColumnReverse = 3`
**Parser** (`ParseFlexElement` line 470-476):
  ```csharp
  flex.Direction = directionStr.ToLowerInvariant() switch {
      "row" => FlexDirection.Row,
      "column" => FlexDirection.Column,
      "row-reverse" => FlexDirection.RowReverse,
      "column-reverse" => FlexDirection.ColumnReverse,
      _ => FlexDirection.Column
  };
  ```
**LayoutEngine**: Add reverse logic in `LayoutFlexElement` to choose column/row path and reverse children iteration
**Tests**: New `LayoutEngineDirectionTests.cs`

#### 1e. justify-content overflow fallback
**Parser**: NO changes
**LayoutEngine**: Add freeSpace < 0 check before justify switch in both `LayoutColumnFlex`/`LayoutRowFlex`
**Tests**: 3 new tests

---

### Phase 2: Core Flexbox

#### 2a. Flex resolution algorithm
**Parser**: NO changes
**LayoutEngine**: Major rewrite of grow/shrink logic in `LayoutColumnFlex`/`LayoutRowFlex`
  - New `ResolveFlexibleLengths` method with iterative freeze/distribute loop
  - Integrate min/max clamping

#### 2b. min/max properties
**AST** (`TemplateElement.cs`): Add `MinWidth`, `MaxWidth`, `MinHeight`, `MaxHeight` (all `string?`)
**Parser** (`ApplyFlexItemProperties`): Add parsing:
  ```csharp
  element.MinWidth = GetStringValue(node, "minWidth");
  element.MaxWidth = GetStringValue(node, "maxWidth");
  element.MinHeight = GetStringValue(node, "minHeight");
  element.MaxHeight = GetStringValue(node, "maxHeight");
  ```
**LayoutEngine**: Resolve and clamp in flex algorithm
**Tests**: New `LayoutEngineMinMaxTests.cs`

---

### Phase 3: Wrapping

#### 3a. flex-wrap + align-content
**Parser** (`ParseFlexElement`): `wrap` already parsed (line 478-485), `alignContent` needs to be added:
  ```csharp
  var alignContentStr = GetStringValue(node, "alignContent", "stretch");
  flex.AlignContent = alignContentStr.ToLowerInvariant() switch {
      "start" => AlignContent.Start,
      "center" => AlignContent.Center,
      "end" => AlignContent.End,
      "stretch" => AlignContent.Stretch,
      "space-between" => AlignContent.SpaceBetween,
      "space-around" => AlignContent.SpaceAround,
      _ => AlignContent.Stretch
  };
  ```
  Note: `AlignContent` property already exists on `FlexElement` but is never parsed from YAML.
**LayoutEngine**: New line-breaking algorithm, per-line flex resolution, align-content distribution
**Tests**: New `LayoutEngineWrapTests.cs`, `LayoutEngineAlignContentTests.cs`

#### 3b. row-gap/column-gap
**AST** (`FlexElement.cs`): Add `RowGap`, `ColumnGap` properties
**Parser** (`ParseFlexElement`): Add parsing:
  ```csharp
  flex.RowGap = GetStringValue(node, "rowGap");
  flex.ColumnGap = GetStringValue(node, "columnGap");
  ```
**LayoutEngine**: Use ColumnGap between items in line, RowGap between lines (fallback to Gap)

---

### Phase 4: Advanced

#### 4a. position absolute/relative
**Enums** (`FlexEnums.cs`): New `Position` enum
**AST** (`TemplateElement.cs`): Add `Position`, `Top`, `Right`, `Bottom`, `Left`
**Parser** (`ApplyFlexItemProperties`):
  ```csharp
  var posStr = GetStringValue(node, "position", "static");
  element.Position = posStr.ToLowerInvariant() switch {
      "static" => Position.Static,
      "relative" => Position.Relative,
      "absolute" => Position.Absolute,
      _ => Position.Static
  };
  element.Top = GetStringValue(node, "top");
  element.Right = GetStringValue(node, "right");
  element.Bottom = GetStringValue(node, "bottom");
  element.Left = GetStringValue(node, "left");
  ```
**LayoutEngine**: Two-pass for absolute positioning

#### 4b. overflow:hidden
**Enums** (`FlexEnums.cs`): New `Overflow` enum
**AST** (`FlexElement.cs`): Add `Overflow` property
**Parser** (`ParseFlexElement`):
  ```csharp
  var overflowStr = GetStringValue(node, "overflow", "visible");
  flex.Overflow = overflowStr.ToLowerInvariant() switch {
      "visible" => Overflow.Visible,
      "hidden" => Overflow.Hidden,
      _ => Overflow.Visible
  };
  ```
**SkiaRenderer**: `canvas.ClipRect()` for overflow:hidden

#### 4c. aspect-ratio
**AST** (`TemplateElement.cs`): Add `AspectRatio: float?`
**Parser** (`ApplyFlexItemProperties`):
  ```csharp
  element.AspectRatio = GetNullableFloatValue(node, "aspectRatio");
  ```
  Need new helper: `GetNullableFloatValue`
**LayoutEngine**: Compute missing dimension from ratio

---

### Phase 5: Auto Margins

**New types**: `MarginValue` (struct), `MarginValues` (struct) in `Layout/Units/`
**Parser**: Extend PaddingParser or create new `ParseMargin()` supporting "auto" token
  - "auto" is not a valid number, so current `UnitParser.Parse` would fail
  - Need special handling in parser to detect "auto" keyword
**LayoutEngine**: Auto margin logic before justify-content

---

## Summary: Parser Method Changes Per Phase

| Method | Phase 0 | Phase 1 | Phase 2 | Phase 3 | Phase 4 | Phase 5 |
|--------|---------|---------|---------|---------|---------|---------|
| `ApplyFlexItemProperties` | SIMPLIFY (remove switch) | +display | +minWidth/maxWidth/minHeight/maxHeight | - | +position/top/right/bottom/left, +aspectRatio | - |
| `ParseFlexElement` | - | +direction reverse cases | - | +alignContent, +rowGap/columnGap | +overflow | - |
| `ParseTextElement` | - | - | - | - | - | - |
| Other element parsers | - | - | - | - | - | - |
| YAML helpers | - | - | - | - | - | +GetNullableFloatValue |
| New parser code | - | - | - | - | - | +ParseMargin (auto support) |

## Files Affected Per Phase (Parser + AST)

| Phase | Files |
|-------|-------|
| 0 | `TemplateElement.cs`, `FlexElement.cs`, `TextElement.cs`, `QrElement.cs`, `BarcodeElement.cs`, `ImageElement.cs`, `SeparatorElement.cs`, `TemplateParser.cs`, `LayoutEngine.cs` (all dispatch switches) |
| 1a | `LayoutEngine.cs` only |
| 1b | `LayoutEngine.cs` only |
| 1c | `TemplateElement.cs` (+Display), `FlexEnums.cs` (+enum), `TemplateParser.cs`, `LayoutEngine.cs` |
| 1d | `FlexEnums.cs` (+enum values), `TemplateParser.cs`, `LayoutEngine.cs` |
| 1e | `LayoutEngine.cs` only |
| 2 | `TemplateElement.cs` (+min/max), `TemplateParser.cs`, `LayoutEngine.cs` |
| 3a | `TemplateParser.cs` (+alignContent), `LayoutEngine.cs` |
| 3b | `FlexElement.cs` (+rowGap/columnGap), `TemplateParser.cs`, `LayoutEngine.cs` |
| 4a | `FlexEnums.cs`, `TemplateElement.cs`, `TemplateParser.cs`, `LayoutEngine.cs` |
| 4b | `FlexEnums.cs`, `FlexElement.cs`, `TemplateParser.cs`, `SkiaRenderer.cs` |
| 4c | `TemplateElement.cs`, `TemplateParser.cs`, `LayoutEngine.cs` |
| 5 | New: `MarginValues.cs`. Modified: `PaddingParser.cs`, `TemplateParser.cs`, `LayoutEngine.cs` |
