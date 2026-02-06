# AST Elements Detailed Map

Comprehensive analysis of all template elements and their flex-item properties for Phase 0 (move properties to base class).

## Base Class: TemplateElement

**Location**: `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`

### Current Properties (all elements inherit these)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Type` | `ElementType` | abstract | Element type enum |
| `Rotate` | `string` | `"none"` | Rotation transform |
| `Background` | `string?` | `null` | Background color (hex) |
| `Padding` | `string` | `"0"` | Inner spacing (px, %, em) |
| `Margin` | `string` | `"0"` | Outer spacing (px, %, em) |

**Note**: `Padding` and `Margin` are already in base class. They were added in the padding/margin expansion phase.

---

## Concrete Elements with Flex-Item Properties

All concrete elements (except control flow elements `EachElement`, `IfElement`) have **identical** flex-item properties.

### Common Flex-Item Properties Pattern

These 7 properties appear verbatim in **every** concrete element class:

```csharp
/// <summary>Flex grow factor.</summary>
public float Grow { get; set; }

/// <summary>Flex shrink factor.</summary>
public float Shrink { get; set; } = 1f;

/// <summary>Flex basis (px, %, em, auto).</summary>
public string Basis { get; set; } = "auto";

/// <summary>Self alignment override.</summary>
public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;

/// <summary>Display order.</summary>
public int Order { get; set; }

/// <summary>Width (px, %, em, auto).</summary>
public string? Width { get; set; }

/// <summary>Height (px, %, em, auto).</summary>
public string? Height { get; set; }
```

**Key observation**: Exact same names, types, defaults, and XML doc comments across all 6 element types.

---

## Element-by-Element Analysis

### 1. FlexElement

**Location**: `src/FlexRender.Core/Parsing/Ast/FlexElement.cs`

**Unique properties** (container-specific):
- `Direction` (FlexDirection): Column
- `Wrap` (FlexWrap): NoWrap
- `Gap` (string): "0"
- `Justify` (JustifyContent): Start
- `Align` (AlignItems): Stretch
- `AlignContent` (AlignContent): Stretch
- `Children` (IReadOnlyList<TemplateElement>): []

**Duplicated flex-item properties**:
```csharp
public float Grow { get; set; }                          // default: 0
public float Shrink { get; set; } = 1f;                  // default: 1
public string Basis { get; set; } = "auto";              // default: "auto"
public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;  // default: Auto
public int Order { get; set; }                           // default: 0
public string? Width { get; set; }                       // default: null
public string? Height { get; set; }                      // default: null
```

### 2. TextElement

**Location**: `src/FlexRender.Core/Parsing/Ast/TextElement.cs`

**Unique properties**:
- `Content` (string): ""
- `Font` (string): "main"
- `Size` (string): "1em"
- `Color` (string): "#000000"
- `Align` (TextAlign): Left
- `Wrap` (bool): true
- `Overflow` (TextOverflow): Ellipsis
- `MaxLines` (int?): null
- `LineHeight` (string): ""

**Duplicated flex-item properties**:
```csharp
public float Grow { get; set; }                          // default: 0
public float Shrink { get; set; } = 1f;                  // default: 1
public string Basis { get; set; } = "auto";              // default: "auto"
public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;  // default: Auto
public int Order { get; set; }                           // default: 0
public string? Width { get; set; }                       // default: null
public string? Height { get; set; }                      // default: null
```

### 3. ImageElement

**Location**: `src/FlexRender.Core/Parsing/Ast/ImageElement.cs`

**Unique properties**:
- `Src` (string): ""
- `ImageWidth` (int?): null
- `ImageHeight` (int?): null
- `Fit` (ImageFit): Contain

**Duplicated flex-item properties**:
```csharp
public float Grow { get; set; }                          // default: 0
public float Shrink { get; set; } = 1f;                  // default: 1
public string Basis { get; set; } = "auto";              // default: "auto"
public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;  // default: Auto
public int Order { get; set; }                           // default: 0
public string? Width { get; set; }                       // default: null
public string? Height { get; set; }                      // default: null
```

### 4. QrElement

**Location**: `src/FlexRender.Core/Parsing/Ast/QrElement.cs`

**Unique properties**:
- `Data` (string): ""
- `Size` (int): 100
- `ErrorCorrection` (ErrorCorrectionLevel): M
- `Foreground` (string): "#000000"

**Duplicated flex-item properties**:
```csharp
public float Grow { get; set; }                          // default: 0
public float Shrink { get; set; } = 1f;                  // default: 1
public string Basis { get; set; } = "auto";              // default: "auto"
public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;  // default: Auto
public int Order { get; set; }                           // default: 0
public string? Width { get; set; }                       // default: null
public string? Height { get; set; }                      // default: null
```

### 5. BarcodeElement

**Location**: `src/FlexRender.Core/Parsing/Ast/BarcodeElement.cs`

**Unique properties**:
- `Data` (string): ""
- `Format` (BarcodeFormat): Code128
- `BarcodeWidth` (int): 200
- `BarcodeHeight` (int): 80
- `ShowText` (bool): true
- `Foreground` (string): "#000000"

**Duplicated flex-item properties**:
```csharp
public float Grow { get; set; }                          // default: 0
public float Shrink { get; set; } = 1f;                  // default: 1
public string Basis { get; set; } = "auto";              // default: "auto"
public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;  // default: Auto
public int Order { get; set; }                           // default: 0
public string? Width { get; set; }                       // default: null
public string? Height { get; set; }                      // default: null
```

### 6. SeparatorElement

**Location**: `src/FlexRender.Core/Parsing/Ast/SeparatorElement.cs`

**Unique properties**:
- `Orientation` (SeparatorOrientation): Horizontal
- `Style` (SeparatorStyle): Dotted
- `Thickness` (float): 1f
- `Color` (string): "#000000"

**Duplicated flex-item properties**:
```csharp
public float Grow { get; set; }                          // default: 0
public float Shrink { get; set; } = 1f;                  // default: 1
public string Basis { get; set; } = "auto";              // default: "auto"
public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;  // default: Auto
public int Order { get; set; }                           // default: 0
public string? Width { get; set; }                       // default: null
public string? Height { get; set; }                      // default: null
```

**Note**: SeparatorElement declares these in slightly different order (Width/Height before Grow), but properties are identical.

---

## Control Flow Elements (NO flex-item properties)

These elements do NOT participate in flex layout and do NOT have flex-item properties:

### EachElement

**Location**: `src/FlexRender.Core/Parsing/Ast/EachElement.cs` (not read, but documented in AGENTS.md)

Properties:
- `Array` (string): path to data array
- `As` (string?): optional variable name
- `Children` (List<TemplateElement>)

### IfElement

**Location**: `src/FlexRender.Core/Parsing/Ast/IfElement.cs` (not read, but documented in AGENTS.md)

Properties:
- `Condition` (string): path to boolean/value
- Various condition operators (equals, notEquals, in, etc.)
- `Then` (List<TemplateElement>)
- `ElseIf` (IfElement?)
- `Else` (List<TemplateElement>?)

**These expand at template processing time and do NOT become layout nodes.**

---

## Units System

### Unit Type

**Location**: `src/FlexRender.Core/Layout/Units/Unit.cs`

```csharp
public readonly struct Unit
{
    public UnitType Type { get; }  // Pixels | Percent | Em | Auto
    public float Value { get; }

    public float? Resolve(float parentSize, float fontSize)
}
```

### UnitParser

**Location**: `src/FlexRender.Core/Layout/Units/UnitParser.cs`

Parses: `"100"`, `"50%"`, `"1.5em"`, `"auto"`, `"100px"`

### PaddingValues

**Location**: `src/FlexRender.Core/Layout/Units/PaddingValues.cs`

```csharp
public readonly record struct PaddingValues(float Top, float Right, float Bottom, float Left)
{
    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;
    public static readonly PaddingValues Zero;
    public static PaddingValues Uniform(float value);
    public PaddingValues ClampNegatives();
}
```

### PaddingParser

**Location**: `src/FlexRender.Core/Layout/Units/PaddingParser.cs`

Parses CSS-like shorthand:
- `"20"` → all sides = 20
- `"20 40"` → vertical=20, horizontal=40
- `"20 40 30"` → top=20, horizontal=40, bottom=30
- `"20 40 30 10"` → top=20, right=40, bottom=30, left=10

Methods:
- `Parse(string? value, float parentSize, float fontSize)` → PaddingValues
- `ParseAbsolute(string? value)` → PaddingValues (for intrinsic pass)

---

## Summary: Phase 0 Migration Plan

### Properties to Move to TemplateElement Base Class

**Add these 7 properties to `TemplateElement`** (currently duplicated in 6 classes):

```csharp
// In TemplateElement.cs

/// <summary>Flex grow factor.</summary>
public float Grow { get; set; }

/// <summary>Flex shrink factor.</summary>
public float Shrink { get; set; } = 1f;

/// <summary>Flex basis (px, %, em, auto).</summary>
public string Basis { get; set; } = "auto";

/// <summary>Self alignment override.</summary>
public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;

/// <summary>Display order.</summary>
public int Order { get; set; }

/// <summary>Width (px, %, em, auto).</summary>
public string? Width { get; set; }

/// <summary>Height (px, %, em, auto).</summary>
public string? Height { get; set; }
```

### Affected Files (delete duplicated properties)

1. `src/FlexRender.Core/Parsing/Ast/FlexElement.cs` — delete lines 37-56 (7 properties)
2. `src/FlexRender.Core/Parsing/Ast/TextElement.cs` — delete lines 105-124 (7 properties)
3. `src/FlexRender.Core/Parsing/Ast/ImageElement.cs` — delete lines 61-80 (7 properties)
4. `src/FlexRender.Core/Parsing/Ast/QrElement.cs` — delete lines 62-81 (7 properties)
5. `src/FlexRender.Core/Parsing/Ast/BarcodeElement.cs` — delete lines 76-95 (7 properties)
6. `src/FlexRender.Core/Parsing/Ast/SeparatorElement.cs` — delete lines 72-105 (7 properties, different order)

**Important**: Control flow elements (`EachElement`, `IfElement`) will inherit these properties but they are **never used** since these elements don't participate in layout. This is acceptable.

### Required Import Addition

All element files must have:
```csharp
using FlexRender.Layout;  // for AlignSelf enum
```

Currently **all 6 files already have this import**, so no changes needed.

### Testing Impact

**Zero breaking changes** expected:
- Properties move from child to parent
- Same names, types, defaults
- Existing code accessing `element.Grow`, `element.Width`, etc. continues to work
- LayoutEngine switch-based dispatch continues to work (queries properties on `element`, now resolved via inheritance)

**Test validation**:
- Run full test suite (`dotnet test FlexRender.slnx`)
- All existing tests should pass without modification
- Snapshot tests validate visual output unchanged

---

## Duplication Statistics

**Before Phase 0**:
- 7 flex-item properties × 6 element types = **42 duplicated property declarations**
- Total lines of duplicated code: ~126 lines (20 lines per element × 6)

**After Phase 0**:
- 7 properties in base class = **7 declarations total**
- Reduction: **35 declarations eliminated** (83% reduction)
- Lines of code saved: ~106 lines

**Maintenance benefit**: Adding a new flex-item property (e.g., `min-width`, `max-height`) requires changing **1 file** instead of 6.

---

## Existing Base Properties Analysis

Current `TemplateElement` has 5 properties:
1. `Type` (abstract) — discriminator, must remain
2. `Rotate` — rendering transform, appropriate for base
3. `Background` — rendering property, appropriate for base
4. `Padding` — layout property (added in padding/margin phase)
5. `Margin` — layout property (added in padding/margin phase)

**Observation**: Base class already mixes rendering properties (Rotate, Background) with layout properties (Padding, Margin). Adding 7 more layout properties is **consistent with existing design**.

---

## AlignSelf Enum

**Location**: `src/FlexRender.Core/Layout/AlignEnums.cs` (inferred)

```csharp
public enum AlignSelf
{
    Auto,    // default: inherit from parent align-items
    Start,
    Center,
    End,
    Stretch
}
```

Used in property: `public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;`

---

## Property Default Values Table

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `Grow` | `float` | `0` | C# default for float (no initializer needed) |
| `Shrink` | `float` | `1f` | Explicit initializer required |
| `Basis` | `string` | `"auto"` | Explicit initializer required |
| `AlignSelf` | `AlignSelf` | `AlignSelf.Auto` | Explicit initializer required |
| `Order` | `int` | `0` | C# default for int (no initializer needed) |
| `Width` | `string?` | `null` | C# default for nullable (no initializer needed) |
| `Height` | `string?` | `null` | C# default for nullable (no initializer needed) |

**4 properties need explicit initializers** when moved to base class.

---

## Phase 0 Checklist

- [ ] Add 7 properties to `TemplateElement` with correct defaults
- [ ] Delete duplicated properties from `FlexElement`
- [ ] Delete duplicated properties from `TextElement`
- [ ] Delete duplicated properties from `ImageElement`
- [ ] Delete duplicated properties from `QrElement`
- [ ] Delete duplicated properties from `BarcodeElement`
- [ ] Delete duplicated properties from `SeparatorElement`
- [ ] Run `dotnet build FlexRender.slnx` (must succeed)
- [ ] Run `dotnet test FlexRender.slnx` (all tests must pass)
- [ ] Visual inspection: no changes to snapshot test outputs
- [ ] Git commit: `refactor(ast): move flex-item properties to base class`

**Estimated effort**: 1-2 SP (mechanical refactoring, low risk)

---

**End of detailed map.**
