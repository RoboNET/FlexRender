# Flexbox Expansion: TDD Implementation Plan

## Overview

This plan implements the flexbox expansion in 6 phases (0-5), broken into atomic tasks.
Each task follows TDD: write failing test(s), then implement to make them pass.
Each task is small enough for one agent turn.

**Branch**: `feat/flexbox-expansion`
**Test project**: `tests/FlexRender.Tests/`
**Run all tests**: `dotnet test FlexRender.slnx`
**Run filtered**: `dotnet test FlexRender.slnx --filter "ClassName"`

---

## Phase 0: Refactoring (Foundation)

### Task 0.1: Move flex-item properties to base TemplateElement

**Goal**: Eliminate 6-way property duplication. Move Grow, Shrink, Basis, AlignSelf, Order, Width, Height to `TemplateElement`.

**Files to modify**:
- `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs` -- add properties
- `src/FlexRender.Core/Parsing/Ast/FlexElement.cs` -- remove duplicated properties (keep container-only: Direction, Wrap, Gap, Justify, Align, AlignContent, Children)
- `src/FlexRender.Core/Parsing/Ast/TextElement.cs` -- remove Grow, Shrink, Basis, AlignSelf, Order, Width, Height
- `src/FlexRender.Core/Parsing/Ast/QrElement.cs` -- remove Grow, Shrink, Basis, AlignSelf, Order, Width, Height
- `src/FlexRender.Core/Parsing/Ast/BarcodeElement.cs` -- remove Grow, Shrink, Basis, AlignSelf, Order, Width, Height
- `src/FlexRender.Core/Parsing/Ast/ImageElement.cs` -- remove Grow, Shrink, Basis, AlignSelf, Order, Width, Height
- `src/FlexRender.Core/Parsing/Ast/SeparatorElement.cs` -- remove Width, Height, Grow, Shrink, Basis, Order, AlignSelf

**New properties on TemplateElement**:
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

**CRITICAL**: `using FlexRender.Layout;` must be added to TemplateElement.cs for AlignSelf.

**Files to modify (LayoutEngine simplification)**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs` -- simplify GetFlexGrow, GetFlexShrink, HasExplicitWidth, HasExplicitHeight

Simplified methods:
```csharp
private static float GetFlexGrow(TemplateElement element)
{
    if (element.Grow < 0f)
        throw new ArgumentException(
            $"Flex grow value cannot be negative. Got {element.Grow} for element of type {element.Type}.",
            nameof(element));
    return element.Grow;
}

private static float GetFlexShrink(TemplateElement element)
{
    if (element.Shrink < 0f)
        throw new ArgumentException(
            $"Flex shrink value cannot be negative. Got {element.Shrink} for element of type {element.Type}.",
            nameof(element));
    return element.Shrink;
}

private static bool HasExplicitHeight(TemplateElement element)
{
    return element switch
    {
        QrElement q => !string.IsNullOrEmpty(q.Height) || q.Size > 0,
        BarcodeElement b => !string.IsNullOrEmpty(b.Height) || b.BarcodeHeight > 0,
        ImageElement i => !string.IsNullOrEmpty(i.Height) || i.ImageHeight.HasValue,
        _ => !string.IsNullOrEmpty(element.Height)
    };
}

private static bool HasExplicitWidth(TemplateElement element)
{
    return element switch
    {
        QrElement q => !string.IsNullOrEmpty(q.Width) || q.Size > 0,
        BarcodeElement b => !string.IsNullOrEmpty(b.Width) || b.BarcodeWidth > 0,
        ImageElement i => !string.IsNullOrEmpty(i.Width) || i.ImageWidth.HasValue,
        _ => !string.IsNullOrEmpty(element.Width)
    };
}
```

**Files to modify (YAML parser)**:
- `src/FlexRender.Yaml/Parsing/TemplateParser.cs` -- simplify ApplyFlexItemProperties

Simplified parser:
```csharp
private static void ApplyFlexItemProperties(YamlMappingNode node, TemplateElement element)
{
    element.Grow = GetFloatValue(node, "grow", 0f);
    element.Shrink = GetFloatValue(node, "shrink", 1f);
    element.Basis = GetStringValue(node, "basis", "auto");
    element.Order = GetIntValue(node, "order", 0);

    var alignSelfStr = GetStringValue(node, "alignSelf", "auto");
    element.AlignSelf = alignSelfStr.ToLowerInvariant() switch
    {
        "auto" => AlignSelf.Auto,
        "start" => AlignSelf.Start,
        "center" => AlignSelf.Center,
        "end" => AlignSelf.End,
        "stretch" => AlignSelf.Stretch,
        "baseline" => AlignSelf.Baseline,
        _ => AlignSelf.Auto
    };

    // Width/Height: Barcode and Image YAML width/height map to content-specific properties,
    // NOT to the base flex Width/Height. This preserves backward compatibility.
    switch (element)
    {
        case BarcodeElement:
        case ImageElement:
            // width/height already parsed in element-specific parsers (BarcodeWidth/ImageWidth)
            break;
        default:
            element.Width = GetStringValue(node, "width");
            element.Height = GetStringValue(node, "height");
            break;
    }
}
```

**Test command**: `dotnet test FlexRender.slnx`
**Done when**: ALL existing tests pass (100+). No new tests needed -- this is purely a refactor.

---

### Task 0.2: Remove dead code FlexContainerProperties and FlexItemProperties

**Goal**: Remove unused record structs and their test files.

**Files to delete**:
- `src/FlexRender.Core/Layout/FlexContainerProperties.cs`
- `src/FlexRender.Core/Layout/FlexItemProperties.cs`
- `tests/FlexRender.Tests/Layout/FlexContainerPropertiesTests.cs`
- `tests/FlexRender.Tests/Layout/FlexItemPropertiesTests.cs`

**Files to update**:
- `llms-full.txt` -- remove references to FlexContainerProperties/FlexItemProperties

**Test command**: `dotnet test FlexRender.slnx`
**Done when**: Build and all tests pass.

---

### Task 0.3: Inject ResourceLimits into LayoutEngine

**Goal**: Prepare LayoutEngine for MaxFlexLines limit (Phase 3).

**Files to modify**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs`

Add constructor:
```csharp
public sealed class LayoutEngine
{
    private readonly ResourceLimits _limits;

    /// <summary>
    /// Creates a new LayoutEngine with the specified resource limits.
    /// </summary>
    /// <param name="limits">Resource limits for layout computation.</param>
    public LayoutEngine(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
    }

    /// <summary>
    /// Creates a new LayoutEngine with default resource limits.
    /// </summary>
    public LayoutEngine() : this(new ResourceLimits()) { }

    // ... existing code
}
```

**Files to modify (callers)**:
- `src/FlexRender.Skia/Rendering/SkiaRenderer.cs` -- pass `_limits` to `new LayoutEngine(_limits)`

**Test to write** (file: `tests/FlexRender.Tests/Layout/LayoutEngineConstructorTests.cs`):
```
LayoutEngine_DefaultConstructor_HasDefaultLimits
LayoutEngine_WithLimits_StoresLimits
LayoutEngine_NullLimits_ThrowsArgumentNullException
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineConstructorTests"`
**Done when**: New tests pass + all existing tests pass.

---

## Phase 1: Quick Wins

### Task 1.1: Write align-self tests

**Goal**: Write failing tests for align-self functionality.

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineAlignSelfTests.cs`

**Tests** (8 tests):
```
ComputeLayout_RowAlignItemsStretch_ChildAlignSelfStart_ChildNotStretched
ComputeLayout_RowAlignItemsStretch_ChildAlignSelfCenter_ChildCentered
ComputeLayout_RowAlignItemsStretch_ChildAlignSelfEnd_ChildAtEnd
ComputeLayout_RowAlignItemsStretch_ChildAlignSelfStretch_ChildStretched
ComputeLayout_RowAlignItemsCenter_ChildAlignSelfEnd_OverridesParent
ComputeLayout_ColumnAlignItemsStretch_ChildAlignSelfCenter_ChildCentered
ComputeLayout_AlignSelfAuto_UsesParentAlignItems
ComputeLayout_MultipleChildren_DifferentAlignSelf_EachPositionedCorrectly
```

**Test pattern** (each test):
```csharp
[Fact]
public void ComputeLayout_RowAlignItemsStretch_ChildAlignSelfStart_ChildNotStretched()
{
    // Arrange: row flex with height=100, align=stretch, child with alignSelf=start, height=30
    var flex = new FlexElement { Direction = FlexDirection.Row, Align = AlignItems.Stretch, Height = "100" };
    var child = new TextElement { Content = "test", Height = "30", AlignSelf = AlignSelf.Start };
    flex.AddChild(child);

    var template = new Template { Elements = { flex } };
    template.Canvas.Width = 400;
    template.Canvas.Height = 100;

    var engine = new LayoutEngine();
    // Act
    var root = engine.ComputeLayout(template);
    // Assert: child should NOT be stretched, should be at Y = padding.Top
    var flexNode = root.Children[0];
    var childNode = flexNode.Children[0];
    Assert.Equal(30f, childNode.Height, 0.1f);
    Assert.Equal(0f, childNode.Y, 0.1f);
}
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineAlignSelfTests"`
**Done when**: Tests compile but FAIL (align-self not implemented yet).

---

### Task 1.2: Implement align-self in LayoutEngine

**Goal**: Make align-self tests pass.

**Files to modify**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs`

**Changes**:

Add helper method:
```csharp
/// <summary>
/// Resolves the effective cross-axis alignment for a child element,
/// considering align-self override vs parent's align-items.
/// </summary>
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

In `LayoutColumnFlex`, replace `flex.Align` usage with per-child effective alignment:
```csharp
foreach (var child in node.Children)
{
    var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);

    child.X = childMarginX + effectiveAlign switch
    {
        AlignItems.Start => padding.Left,
        AlignItems.Center => padding.Left + (crossAxisSize - child.Width) / 2,
        AlignItems.End => padding.Left + crossAxisSize - child.Width,
        AlignItems.Stretch => padding.Left,
        _ => padding.Left
    };

    if (effectiveAlign == AlignItems.Stretch && !HasExplicitWidth(child.Element) && crossAxisSize > 0)
    {
        child.Width = crossAxisSize;
    }

    child.Y = y + childMarginY;
    y += child.Height + gap + childMarginY;
}
```

Same pattern in `LayoutRowFlex` for cross-axis (Y) alignment:
```csharp
var effectiveAlign = GetEffectiveAlign(child.Element, flex.Align);

child.Y = childMarginY + ((hasExplicitHeight && crossAxisSize > 0) ? effectiveAlign switch
{
    AlignItems.Start => padding.Top,
    AlignItems.Center => padding.Top + (crossAxisSize - child.Height) / 2,
    AlignItems.End => padding.Top + crossAxisSize - child.Height,
    AlignItems.Stretch => padding.Top,
    _ => padding.Top
} : padding.Top);

if (hasExplicitHeight && effectiveAlign == AlignItems.Stretch && !HasExplicitHeight(child.Element) && crossAxisSize > 0)
{
    child.Height = crossAxisSize;
}
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineAlignSelfTests"`
**Done when**: All 8 align-self tests pass + all existing tests pass.

---

### Task 1.3: Write margin 4-side tests

**Goal**: Write failing tests for 4-side margin support.

**File to modify**: `tests/FlexRender.Tests/Layout/LayoutEnginePaddingMarginTests.cs`

**Tests to add** (6 tests):
```
ComputeLayout_Margin4Side_AppliesIndividualMargins
ComputeLayout_Margin2Value_AppliesVerticalHorizontal
ComputeLayout_MarginColumn_AffectsYPositionAndXPosition
ComputeLayout_MarginRow_AffectsXPositionAndYPosition
ComputeLayout_MarginWithGap_BothApplied
ComputeLayout_MarginNegativeClamp_ClampsToZero
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEnginePaddingMarginTests"`
**Done when**: New tests compile but FAIL.

---

### Task 1.4: Implement margin 4-side support

**Goal**: Parse margin as PaddingValues (4-side) instead of uniform float.

**Files to modify**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs` -- change all margin parsing from UnitParser to PaddingParser

Replace in all Layout*Element methods:
```csharp
// OLD:
var margin = UnitParser.Parse(text.Margin).Resolve(context.ContainerWidth, context.FontSize) ?? 0f;
if (margin < 0) margin = 0;
// ... return new LayoutNode(text, margin, margin, ...)

// NEW:
var margin = PaddingParser.Parse(text.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();
// ... return new LayoutNode(text, margin.Left, margin.Top, ...)
```

Modify `LayoutColumnFlex` and `LayoutRowFlex`:
- `childMarginX = child.X` and `childMarginY = child.Y` already works, but the initial values are now per-side.
- In LayoutColumnFlex: `y += child.Height + gap + childMarginY` should account for marginBottom too.
  Need: `y += child.Height + gap + margin.Bottom` (top is in child.Y, bottom adds to spacing).

This requires storing margin in LayoutNode or adjusting the approach. Current approach stores marginX/marginY in initial X/Y. For 4-side:
- LayoutNode.X = margin.Left, LayoutNode.Y = margin.Top (set during Layout*Element)
- In LayoutColumnFlex: need margin.Bottom for spacing. Retrieve from element's Margin string.

**Better approach**: Parse margin in LayoutColumnFlex/LayoutRowFlex, not in Layout*Element:
```csharp
// In LayoutColumnFlex:
foreach (var child in node.Children)
{
    var childMargin = PaddingParser.Parse(child.Element.Margin, context.ContainerWidth, context.FontSize).ClampNegatives();

    child.X = childMargin.Left + effectiveAlign switch { ... };
    child.Y = y + childMargin.Top;
    y += child.Height + gap + childMargin.Top + childMargin.Bottom;
}
```

And in Layout*Element, stop setting X/Y to margin:
```csharp
// Was: return new LayoutNode(text, margin, margin, totalWidth, totalHeight);
// Now: return new LayoutNode(text, 0, 0, totalWidth, totalHeight);
```

**WARNING**: This changes how margin is stored. Layout*Element returns node with X=0, Y=0. Margin is applied in the flex layout pass. This is cleaner but needs careful review.

**Update IntrinsicSize margin handling**:
- `MeasureFlexIntrinsic`: change `ParseAbsolutePixelValue(flex.Margin, 0f)` to `PaddingParser.ParseAbsolute(flex.Margin).ClampNegatives()`
- `ApplyPaddingAndMargin`: change margin from uniform to PaddingValues
- `IntrinsicSize.WithMargin`: add overload for PaddingValues

**File to modify**: `src/FlexRender.Core/Layout/IntrinsicSize.cs`
```csharp
/// <summary>
/// Returns a new IntrinsicSize with non-uniform margin added.
/// </summary>
public IntrinsicSize WithMargin(PaddingValues margin)
{
    var h = Math.Max(0f, margin.Horizontal);
    var v = Math.Max(0f, margin.Vertical);
    return new IntrinsicSize(
        MinWidth + h,
        MaxWidth + h,
        MinHeight + v,
        MaxHeight + v);
}
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEnginePaddingMarginTests"`
**Done when**: All margin tests pass + all existing tests pass.

---

### Task 1.5: Write display:none tests

**Goal**: Write failing tests for display:none.

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineDisplayTests.cs`

**Tests** (4 tests):
```
ComputeLayout_DisplayNone_ChildSkippedInLayout
ComputeLayout_DisplayNone_OtherChildrenNotAffected
ComputeLayout_DisplayNone_GapNotAppliedForHiddenChild
MeasureIntrinsic_DisplayNone_ReturnsZero
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineDisplayTests"`
**Done when**: Tests compile but FAIL.

---

### Task 1.6: Implement display:none

**Goal**: Add Display enum and implement skipping in layout engine.

**Files to create**:
- None (add Display enum to FlexEnums.cs)

**Files to modify**:
- `src/FlexRender.Core/Layout/FlexEnums.cs` -- add Display enum:
```csharp
/// <summary>
/// Controls whether an element participates in layout.
/// </summary>
public enum Display
{
    /// <summary>Element participates in flex layout.</summary>
    Flex = 0,
    /// <summary>Element is hidden and removed from layout flow.</summary>
    None = 1
}
```

- `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs` -- add property:
```csharp
/// <summary>
/// Display mode. None removes the element from layout flow.
/// </summary>
public Display Display { get; set; } = Display.Flex;
```

- `src/FlexRender.Core/Layout/LayoutEngine.cs`:
  - `MeasureIntrinsic`: return `IntrinsicSize(0,0,0,0)` for `Display.None`
  - `MeasureFlexIntrinsic`: skip children with `Display.None`
  - `LayoutFlexElement`: skip children with `Display.None`
  - `LayoutColumnFlex`/`LayoutRowFlex`: skip children with `Display.None`

- `src/FlexRender.Skia/Rendering/SkiaRenderer.cs`:
  - `RenderNode`: skip nodes with `Display.None`

- `src/FlexRender.Yaml/Parsing/TemplateParser.cs`:
  - Parse `display` property in common properties or `ApplyFlexItemProperties`

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineDisplayTests"`
**Done when**: All display tests pass + all existing tests pass.

---

### Task 1.7: Write reverse direction tests

**Goal**: Write failing tests for RowReverse and ColumnReverse.

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineDirectionTests.cs`

**Tests** (6 tests):
```
ComputeLayout_RowReverse_ChildrenOrderedRightToLeft
ComputeLayout_RowReverse_FirstChildAtRightEdge
ComputeLayout_RowReverse_WithGap_GapBetweenReversedItems
ComputeLayout_ColumnReverse_ChildrenOrderedBottomToTop
ComputeLayout_ColumnReverse_FirstChildAtBottom
ComputeLayout_ColumnReverse_WithGap_GapBetweenReversedItems
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineDirectionTests"`
**Done when**: Tests compile but FAIL.

---

### Task 1.8: Implement reverse directions

**Goal**: Add RowReverse and ColumnReverse to FlexDirection and implement in layout.

**Files to modify**:
- `src/FlexRender.Core/Layout/FlexEnums.cs`:
```csharp
public enum FlexDirection
{
    Row = 0,
    Column = 1,
    RowReverse = 2,
    ColumnReverse = 3
}
```

- `src/FlexRender.Core/Layout/LayoutEngine.cs`:

In `LayoutFlexElement`, update direction check:
```csharp
var isColumn = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;
if (isColumn)
    LayoutColumnFlex(node, flex, innerContext, padding, gap);
else
    LayoutRowFlex(node, flex, innerContext, padding, gap);
```

In `MeasureFlexIntrinsic`, update direction check:
```csharp
var isColumn = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;
```

In `LayoutColumnFlex`, after computing positions, reverse if needed:
```csharp
if (flex.Direction == FlexDirection.ColumnReverse)
{
    var containerHeight = node.Height > 0 ? node.Height : y;
    foreach (var child in node.Children)
    {
        child.Y = containerHeight - child.Y - child.Height;
    }
}
```

In `LayoutRowFlex`, after computing positions, reverse if needed:
```csharp
if (flex.Direction == FlexDirection.RowReverse)
{
    var containerWidth = node.Width;
    foreach (var child in node.Children)
    {
        child.X = containerWidth - child.X - child.Width;
    }
}
```

- `src/FlexRender.Yaml/Parsing/TemplateParser.cs`:
  - Add parsing for `row-reverse` and `column-reverse` direction values

- `src/FlexRender.Core/Layout/LayoutEngine.cs`:
  - Update row width logic: for RowReverse, children still need `HasExplicitWidth` check with the same logic

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineDirectionTests"`
**Done when**: All 6 direction tests pass + all existing tests pass.

---

### Task 1.9: Write and implement justify-content overflow fallback

**Goal**: Space-distribution modes fall back to Start when freeSpace < 0.

**File to modify**: `tests/FlexRender.Tests/Layout/LayoutEngineTests.cs` (add tests to existing file)

**Tests** (3 tests):
```
ComputeLayout_SpaceBetween_NegativeFreeSpace_FallbackToStart
ComputeLayout_SpaceAround_NegativeFreeSpace_FallbackToStart
ComputeLayout_SpaceEvenly_NegativeFreeSpace_FallbackToStart
```

**Implementation in** `src/FlexRender.Core/Layout/LayoutEngine.cs`:

In both `LayoutColumnFlex` and `LayoutRowFlex`, before the justify-content switch:
```csharp
var effectiveJustify = freeSpace < 0 ? flex.Justify switch
{
    JustifyContent.SpaceBetween => JustifyContent.Start,
    JustifyContent.SpaceAround => JustifyContent.Start,
    JustifyContent.SpaceEvenly => JustifyContent.Start,
    _ => flex.Justify
} : flex.Justify;
```

Then use `effectiveJustify` instead of `flex.Justify` in the switch.

**Test command**: `dotnet test FlexRender.slnx --filter "ComputeLayout_Space.*NegativeFreeSpace"`
**Done when**: All 3 tests pass + all existing tests pass.

---

## Phase 2: Core Flexbox Algorithm

### Task 2.1: Write flex-basis tests

**Goal**: Write failing tests for flex-basis functionality.

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineFlexBasisTests.cs`

**Tests** (7 tests):
```
ComputeLayout_Column_FlexBasisPixels_SetsInitialSize
ComputeLayout_Column_FlexBasisPercent_CalculatesFromParent
ComputeLayout_Column_FlexBasisAuto_UsesContentSize
ComputeLayout_Column_FlexBasisWithGrow_GrowsFromBasis
ComputeLayout_Column_FlexBasisWithShrink_ShrinksFromBasis
ComputeLayout_Row_FlexBasisPixels_SetsInitialWidth
ComputeLayout_Row_FlexBasis0_WithGrow_EqualDistribution
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineFlexBasisTests"`
**Done when**: Tests compile but FAIL.

---

### Task 2.2: Write min/max sizing tests

**Goal**: Write failing tests for min/max width and height constraints.

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineMinMaxTests.cs`

**Tests** (10 tests):
```
ComputeLayout_MinWidth_ChildDoesNotShrinkBelowMinWidth
ComputeLayout_MaxWidth_ChildDoesNotGrowBeyondMaxWidth
ComputeLayout_MinHeight_ChildDoesNotShrinkBelowMinHeight
ComputeLayout_MaxHeight_ChildDoesNotGrowBeyondMaxHeight
ComputeLayout_MinWidth_WithFlexShrink_RespectsMinWidth
ComputeLayout_MaxWidth_WithFlexGrow_RespectsMaxWidth
ComputeLayout_MinMaxWidth_ClampsToRange
ComputeLayout_MinMaxHeight_ClampsToRange
ComputeLayout_MinWidth_PercentValue_CalculatesFromParent
ComputeLayout_TwoPassResolution_MultipleConstrainedItems_CorrectDistribution
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineMinMaxTests"`
**Done when**: Tests compile but FAIL.

---

### Task 2.3: Add min/max properties to AST and parser

**Goal**: Add MinWidth, MaxWidth, MinHeight, MaxHeight properties and YAML parsing.

**Files to modify**:
- `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`:
```csharp
/// <summary>Minimum width constraint (px, %, em).</summary>
public string? MinWidth { get; set; }

/// <summary>Maximum width constraint (px, %, em).</summary>
public string? MaxWidth { get; set; }

/// <summary>Minimum height constraint (px, %, em).</summary>
public string? MinHeight { get; set; }

/// <summary>Maximum height constraint (px, %, em).</summary>
public string? MaxHeight { get; set; }
```

- `src/FlexRender.Yaml/Parsing/TemplateParser.cs`:
  In `ApplyFlexItemProperties`, add:
```csharp
element.MinWidth = GetStringValue(node, "minWidth");
element.MaxWidth = GetStringValue(node, "maxWidth");
element.MinHeight = GetStringValue(node, "minHeight");
element.MaxHeight = GetStringValue(node, "maxHeight");
```

**Write parser tests**: `tests/FlexRender.Tests/Parsing/TemplateParserMinMaxTests.cs`
```
Parse_MinWidthProperty_ParsedCorrectly
Parse_MaxWidthProperty_ParsedCorrectly
Parse_MinHeightProperty_ParsedCorrectly
Parse_MaxHeightProperty_ParsedCorrectly
```

**Test command**: `dotnet test FlexRender.slnx --filter "TemplateParserMinMaxTests"`
**Done when**: Parser tests pass. Layout tests still fail (algorithm not implemented).

---

### Task 2.4: Implement flex resolution algorithm with basis and min/max

**Goal**: Replace current simple grow/shrink with CSS-spec-compliant two-pass algorithm.

**Files to modify**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs`

Add helper:
```csharp
/// <summary>
/// Clamps a value between min and max, where min wins over max per CSS spec.
/// </summary>
private static float ClampSize(float value, float min, float max)
{
    var effectiveMax = Math.Max(min, max);
    return Math.Clamp(value, min, effectiveMax);
}

/// <summary>
/// Resolves the flex basis for a child element.
/// Priority: explicit basis > main-axis dimension > intrinsic size.
/// </summary>
private static float ResolveFlexBasis(TemplateElement element, LayoutNode childNode,
    bool isColumn, LayoutContext context)
{
    var basisStr = element.Basis;
    if (!string.IsNullOrEmpty(basisStr) && basisStr != "auto")
    {
        var unit = UnitParser.Parse(basisStr);
        var resolved = isColumn
            ? unit.Resolve(context.ContainerHeight, context.FontSize)
            : unit.Resolve(context.ContainerWidth, context.FontSize);
        if (resolved.HasValue)
            return resolved.Value;
    }

    // auto: use main-axis dimension (Width for row, Height for column)
    return isColumn ? childNode.Height : childNode.Width;
}

/// <summary>
/// Resolves min/max constraints for a child on the main axis.
/// </summary>
private static (float min, float max) ResolveMinMax(TemplateElement element,
    bool isColumn, LayoutContext context)
{
    float min = 0f;
    float max = float.MaxValue;

    var minStr = isColumn ? element.MinHeight : element.MinWidth;
    var maxStr = isColumn ? element.MaxHeight : element.MaxWidth;

    if (!string.IsNullOrEmpty(minStr))
    {
        var unit = UnitParser.Parse(minStr);
        var parentSize = isColumn ? context.ContainerHeight : context.ContainerWidth;
        min = unit.Resolve(parentSize, context.FontSize) ?? 0f;
    }

    if (!string.IsNullOrEmpty(maxStr))
    {
        var unit = UnitParser.Parse(maxStr);
        var parentSize = isColumn ? context.ContainerHeight : context.ContainerWidth;
        max = unit.Resolve(parentSize, context.FontSize) ?? float.MaxValue;
    }

    return (min, max);
}
```

Replace grow/shrink distribution in `LayoutColumnFlex`:
```csharp
// NEW: Two-pass flex resolution algorithm
// Step 1: Determine hypothetical main size (flex basis) for each child
var itemCount = node.Children.Count;
Span<float> bases = itemCount <= 32 ? stackalloc float[itemCount] : new float[itemCount];
Span<float> sizes = itemCount <= 32 ? stackalloc float[itemCount] : new float[itemCount];
Span<bool> frozen = itemCount <= 32 ? stackalloc bool[itemCount] : new bool[itemCount];

for (var i = 0; i < itemCount; i++)
{
    var child = node.Children[i];
    bases[i] = ResolveFlexBasis(child.Element, child, isColumn: true, context);
    sizes[i] = bases[i];
    frozen[i] = false;
}

var totalBases = 0f;
for (var i = 0; i < itemCount; i++) totalBases += bases[i];
var freeSpace = availableHeight - totalBases - totalGaps;

// Step 2: Determine grow vs shrink mode
var isGrowing = freeSpace > 0;

// Step 3-7: Iterative resolution
for (var iteration = 0; iteration < itemCount + 1; iteration++)
{
    // Freeze items with grow=0 (if growing) or shrink=0 (if shrinking)
    // Also freeze items that would violate min/max
    var anyNewlyFrozen = false;

    var unfrozenFreeSpace = availableHeight - totalGaps;
    for (var i = 0; i < itemCount; i++)
    {
        unfrozenFreeSpace -= frozen[i] ? sizes[i] : bases[i];
    }

    var totalGrowFactors = 0f;
    var totalShrinkScaled = 0f;
    for (var i = 0; i < itemCount; i++)
    {
        if (frozen[i]) continue;
        var child = node.Children[i];
        totalGrowFactors += GetFlexGrow(child.Element);
        totalShrinkScaled += GetFlexShrink(child.Element) * bases[i];
    }

    for (var i = 0; i < itemCount; i++)
    {
        if (frozen[i]) continue;
        var child = node.Children[i];
        var (minSize, maxSize) = ResolveMinMax(child.Element, isColumn: true, context);

        float hypothetical;
        if (isGrowing)
        {
            var grow = GetFlexGrow(child.Element);
            hypothetical = grow > 0 && totalGrowFactors > 0
                ? bases[i] + unfrozenFreeSpace * grow / totalGrowFactors
                : bases[i];
        }
        else
        {
            var shrink = GetFlexShrink(child.Element);
            var scaledFactor = shrink * bases[i];
            hypothetical = totalShrinkScaled > 0 && scaledFactor > 0
                ? bases[i] + unfrozenFreeSpace * scaledFactor / totalShrinkScaled
                : bases[i];
        }

        var clamped = ClampSize(hypothetical, minSize, maxSize);
        if (Math.Abs(clamped - hypothetical) > 0.01f)
        {
            sizes[i] = clamped;
            frozen[i] = true;
            anyNewlyFrozen = true;
        }
        else
        {
            sizes[i] = hypothetical;
        }
    }

    if (!anyNewlyFrozen)
    {
        // All remaining items sized, done
        break;
    }
}

// Apply sizes to child nodes
for (var i = 0; i < itemCount; i++)
{
    node.Children[i].Height = Math.Max(0, sizes[i]);
}

// Recalculate freeSpace after flex resolution
var totalSized = 0f;
for (var i = 0; i < itemCount; i++) totalSized += sizes[i];
freeSpace = availableHeight - totalSized - totalGaps;
```

Same pattern for `LayoutRowFlex` but with Width instead of Height and isColumn=false.

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineFlexBasisTests|LayoutEngineMinMaxTests"`
**Done when**: All flex-basis and min/max tests pass + all existing tests pass.

---

## Phase 3: Wrapping

### Task 3.1: Add row-gap/column-gap to FlexElement and parser

**Goal**: Support separate row-gap and column-gap properties.

**Files to modify**:
- `src/FlexRender.Core/Parsing/Ast/FlexElement.cs`:
```csharp
/// <summary>Gap between items along main axis (alias for gap in non-wrap mode).</summary>
public string? ColumnGap { get; set; }

/// <summary>Gap between lines when wrapping.</summary>
public string? RowGap { get; set; }
```

- `src/FlexRender.Yaml/Parsing/TemplateParser.cs`: Parse `rowGap` and `columnGap` properties

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineGapTests.cs`

**Tests** (3 tests):
```
ComputeLayout_RowGap_AppliesGapBetweenRows
ComputeLayout_ColumnGap_AppliesGapBetweenColumns
ComputeLayout_GapFallback_UsedWhenRowColumnGapNotSet
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineGapTests"`
**Done when**: Parser tests pass. Gap resolution tests may fail until wrap implementation.

---

### Task 3.2: Add AlignContent.SpaceEvenly and write wrap tests

**Goal**: Add missing SpaceEvenly to AlignContent enum, write failing wrap tests.

**Files to modify**:
- `src/FlexRender.Core/Layout/FlexEnums.cs`:
```csharp
public enum AlignContent
{
    Start = 0,
    Center = 1,
    End = 2,
    Stretch = 3,
    SpaceBetween = 4,
    SpaceAround = 5,
    SpaceEvenly = 6  // NEW
}
```

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineWrapTests.cs`

**Tests** (10 tests):
```
ComputeLayout_RowWrap_ChildrenExceedWidth_WrapsToNewLine
ComputeLayout_RowWrap_AllChildrenFit_SingleLine
ComputeLayout_RowWrap_ThreeLines_CalculatesHeightCorrectly
ComputeLayout_RowWrap_WithGap_GapAppliedBetweenLinesAndItems
ComputeLayout_RowWrapReverse_WrapsInReverse
ComputeLayout_ColumnWrap_ChildrenExceedHeight_WrapsToNewColumn
ComputeLayout_ColumnWrapReverse_WrapsInReverse
ComputeLayout_RowWrap_DifferentHeightChildren_LineHeightIsMaxChild
ComputeLayout_RowWrap_SingleChild_NoWrap
ComputeLayout_RowWrap_EmptyChildren_NoWrap
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineWrapTests"`
**Done when**: Tests compile but FAIL (wrap not implemented).

---

### Task 3.3: Write align-content tests

**Goal**: Write failing tests for align-content (multi-line).

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineAlignContentTests.cs`

**Tests** (7 tests):
```
ComputeLayout_RowWrap_AlignContentStart_LinesPackedAtStart
ComputeLayout_RowWrap_AlignContentCenter_LinesCentered
ComputeLayout_RowWrap_AlignContentEnd_LinesPackedAtEnd
ComputeLayout_RowWrap_AlignContentStretch_LinesStretchToFill
ComputeLayout_RowWrap_AlignContentSpaceBetween_SpaceDistributedBetweenLines
ComputeLayout_RowWrap_AlignContentSpaceAround_SpaceDistributedAroundLines
ComputeLayout_RowWrap_AlignContentSpaceEvenly_EvenDistribution
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineAlignContentTests"`
**Done when**: Tests compile but FAIL.

---

### Task 3.4: Add MaxFlexLines to ResourceLimits

**Goal**: Add DoS protection limit for flex lines.

**File to modify**: `src/FlexRender.Core/Configuration/ResourceLimits.cs`:
```csharp
private int _maxFlexLines = 1000;

/// <summary>
/// Maximum number of flex lines allowed in a wrapping container.
/// Prevents denial-of-service via excessive line creation.
/// </summary>
/// <value>Default: 1000.</value>
/// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
public int MaxFlexLines
{
    get => _maxFlexLines;
    set
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        _maxFlexLines = value;
    }
}
```

**File to modify**: `tests/FlexRender.Tests/Configuration/ResourceLimitsTests.cs` -- add tests:
```
MaxFlexLines_Default_Is1000
MaxFlexLines_SetPositive_Succeeds
MaxFlexLines_SetZero_ThrowsArgumentOutOfRangeException
MaxFlexLines_SetNegative_ThrowsArgumentOutOfRangeException
```

**Test command**: `dotnet test FlexRender.slnx --filter "ResourceLimitsTests"`
**Done when**: Tests pass.

---

### Task 3.5: Implement flex-wrap in LayoutEngine (part 1: line breaking)

**Goal**: Implement line breaking for flex-wrap.

**Files to modify**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs`

Add internal struct:
```csharp
/// <summary>
/// Represents a single flex line in a wrapping container.
/// </summary>
internal readonly record struct FlexLine(int StartIndex, int Count, float MainAxisSize, float CrossAxisSize);
```

Add method for line breaking:
```csharp
/// <summary>
/// Breaks children into flex lines based on available space.
/// </summary>
private List<FlexLine> CalculateFlexLines(
    IReadOnlyList<LayoutNode> children,
    float availableMainSize,
    bool isColumn,
    float gap)
{
    var lines = new List<FlexLine>();
    var startIndex = 0;
    var lineMainSize = 0f;
    var lineCrossSize = 0f;
    var itemsInLine = 0;

    for (var i = 0; i < children.Count; i++)
    {
        var child = children[i];
        if (child.Element.Display == Display.None) continue;

        var childMainSize = isColumn ? child.Height : child.Width;
        var childCrossSize = isColumn ? child.Width : child.Height;
        var gapBefore = itemsInLine > 0 ? gap : 0f;

        if (itemsInLine > 0 && lineMainSize + gapBefore + childMainSize > availableMainSize)
        {
            lines.Add(new FlexLine(startIndex, itemsInLine, lineMainSize, lineCrossSize));
            if (lines.Count >= _limits.MaxFlexLines)
                throw new InvalidOperationException(
                    $"Maximum flex lines ({_limits.MaxFlexLines}) exceeded.");
            startIndex = i;
            lineMainSize = childMainSize;
            lineCrossSize = childCrossSize;
            itemsInLine = 1;
        }
        else
        {
            lineMainSize += gapBefore + childMainSize;
            lineCrossSize = Math.Max(lineCrossSize, childCrossSize);
            itemsInLine++;
        }
    }

    if (itemsInLine > 0)
    {
        lines.Add(new FlexLine(startIndex, itemsInLine, lineMainSize, lineCrossSize));
    }

    return lines;
}
```

Update `LayoutFlexElement` to call wrapping path when `flex.Wrap != FlexWrap.NoWrap`.

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineWrapTests"`
**Done when**: Basic wrap tests pass (line breaking works).

---

### Task 3.6: Implement flex-wrap (part 2: per-line layout + align-content)

**Goal**: Apply flex resolution per line, then apply align-content.

**Files to modify**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs`

Add method:
```csharp
/// <summary>
/// Lays out a wrapping flex container.
/// </summary>
private void LayoutWrappedFlex(LayoutNode node, FlexElement flex, LayoutContext context,
    PaddingValues padding, float mainGap, float crossGap)
{
    var isColumn = flex.Direction is FlexDirection.Column or FlexDirection.ColumnReverse;
    var availableMainSize = isColumn
        ? (node.Height > 0 ? node.Height - padding.Vertical : float.MaxValue)
        : node.Width - padding.Horizontal;

    var lines = CalculateFlexLines(node.Children, availableMainSize, isColumn, mainGap);

    // Per-line: apply flex grow/shrink, justify-content, and align-items
    // ... (apply per-line flex resolution)

    // Apply align-content to distribute lines on cross axis
    var totalLinesSize = lines.Sum(l => l.CrossAxisSize);
    var totalCrossGaps = crossGap * Math.Max(0, lines.Count - 1);
    var availableCrossSize = isColumn
        ? node.Width - padding.Horizontal
        : (node.Height > 0 ? node.Height - padding.Vertical : totalLinesSize + totalCrossGaps);
    var crossFreeSpace = availableCrossSize - totalLinesSize - totalCrossGaps;

    // Distribute cross free space per align-content
    // ... (Start, Center, End, Stretch, SpaceBetween, SpaceAround, SpaceEvenly)

    // Handle WrapReverse: invert cross-axis positions
    if (flex.Wrap == FlexWrap.WrapReverse)
    {
        var containerCrossSize = isColumn
            ? node.Width - padding.Horizontal
            : (node.Height > 0 ? node.Height - padding.Vertical : totalLinesSize + totalCrossGaps);
        foreach (var child in node.Children)
        {
            if (isColumn)
                child.X = containerCrossSize - child.X - child.Width + padding.Left;
            else
                child.Y = containerCrossSize - child.Y - child.Height + padding.Top;
        }
    }
}
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineWrapTests|LayoutEngineAlignContentTests"`
**Done when**: All wrap + align-content tests pass + all existing tests pass.

---

## Phase 4: Advanced Features

### Task 4.1: Write position absolute/relative tests

**Goal**: Write failing tests for CSS positioning.

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEnginePositionTests.cs`

**Tests** (8 tests):
```
ComputeLayout_PositionAbsolute_RemovedFromFlexFlow
ComputeLayout_PositionAbsolute_PositionedByInsets
ComputeLayout_PositionAbsolute_OtherChildrenUnaffected
ComputeLayout_PositionAbsolute_DefaultsToTopLeftCorner
ComputeLayout_PositionRelative_OffsetFromNormalPosition
ComputeLayout_PositionRelative_TopOffset_ShiftsDown
ComputeLayout_PositionRelative_LeftOffset_ShiftsRight
ComputeLayout_PositionRelative_DoesNotAffectOtherChildren
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEnginePositionTests"`
**Done when**: Tests compile but FAIL.

---

### Task 4.2: Add Position enum and inset properties

**Goal**: Add position-related types and properties.

**Files to modify**:
- `src/FlexRender.Core/Layout/FlexEnums.cs`:
```csharp
/// <summary>
/// CSS positioning mode for an element.
/// </summary>
public enum Position
{
    /// <summary>Normal flow (default).</summary>
    Static = 0,
    /// <summary>Offset from normal flow position.</summary>
    Relative = 1,
    /// <summary>Removed from flow, positioned relative to container.</summary>
    Absolute = 2
}
```

- `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`:
```csharp
/// <summary>Positioning mode.</summary>
public Position Position { get; set; } = Position.Static;

/// <summary>Top inset for positioned elements.</summary>
public string? Top { get; set; }

/// <summary>Right inset for positioned elements.</summary>
public string? Right { get; set; }

/// <summary>Bottom inset for positioned elements.</summary>
public string? Bottom { get; set; }

/// <summary>Left inset for positioned elements.</summary>
public string? Left { get; set; }
```

Note: `Top`, `Right`, `Bottom`, `Left` conflict with existing names -- use as-is since they are on TemplateElement which has no collisions.

- `src/FlexRender.Yaml/Parsing/TemplateParser.cs`: Parse position and inset properties

**Test command**: `dotnet test FlexRender.slnx`
**Done when**: Build passes with new properties.

---

### Task 4.3: Implement position absolute/relative

**Goal**: Implement positioning in LayoutEngine.

**Files to modify**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs`

**For absolute**: In `LayoutFlexElement`, before the flex layout loop:
```csharp
// Separate absolute children from flow
foreach (var child in flex.Children)
{
    if (child.Position == Position.Absolute)
        continue; // Skip in flex flow
    // ... normal layout
}
```

After normal layout, position absolute children:
```csharp
// Position absolute children
foreach (var child in node.Children)
{
    if (child.Element.Position != Position.Absolute) continue;

    var left = context.ResolveWidth(child.Element.Left);
    var top = context.ResolveHeight(child.Element.Top);
    var right = context.ResolveWidth(child.Element.Right);
    var bottom = context.ResolveHeight(child.Element.Bottom);

    if (left.HasValue) child.X = padding.Left + left.Value;
    else if (right.HasValue) child.X = node.Width - padding.Right - child.Width - right.Value;
    else child.X = padding.Left;

    if (top.HasValue) child.Y = padding.Top + top.Value;
    else if (bottom.HasValue) child.Y = node.Height - padding.Bottom - child.Height - bottom.Value;
    else child.Y = padding.Top;
}
```

**For relative**: After normal layout positioning:
```csharp
if (child.Element.Position == Position.Relative)
{
    var offsetX = context.ResolveWidth(child.Element.Left) ?? 0f;
    var offsetY = context.ResolveHeight(child.Element.Top) ?? 0f;
    child.X += offsetX;
    child.Y += offsetY;
}
```

In `LayoutColumnFlex`/`LayoutRowFlex`: skip absolute children in the flow:
```csharp
foreach (var child in node.Children)
{
    if (child.Element.Position == Position.Absolute) continue;
    // ... existing positioning logic
}
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEnginePositionTests"`
**Done when**: All 8 position tests pass + all existing tests pass.

---

### Task 4.4: Write and implement overflow:hidden tests

**Goal**: Add overflow clipping in SkiaRenderer.

**Files to modify**:
- `src/FlexRender.Core/Layout/FlexEnums.cs`:
```csharp
/// <summary>
/// Controls how content that overflows the element's bounds is handled.
/// </summary>
public enum Overflow
{
    /// <summary>Content is visible outside bounds.</summary>
    Visible = 0,
    /// <summary>Content is clipped at bounds.</summary>
    Hidden = 1
}
```

- `src/FlexRender.Core/Parsing/Ast/FlexElement.cs`:
```csharp
/// <summary>Overflow behavior for content exceeding container bounds.</summary>
public Overflow Overflow { get; set; } = Overflow.Visible;
```

- `src/FlexRender.Skia/Rendering/SkiaRenderer.cs`:
  In `RenderNode`, before rendering children of a FlexElement with Overflow.Hidden:
```csharp
var needsClip = node.Element is FlexElement { Overflow: Overflow.Hidden };
if (needsClip)
{
    canvas.Save();
    canvas.ClipRect(new SKRect(x, y, x + node.Width, y + node.Height));
}

foreach (var child in node.Children)
{
    RenderNode(canvas, child, x, y, imageCache, depth + 1);
}

if (needsClip)
{
    canvas.Restore();
}
```

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineOverflowTests.cs` (parser tests)
**Snapshot test** in `tests/FlexRender.Tests/Snapshots/VisualSnapshotTests.cs` (add overflow hidden test)

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineOverflowTests"`
**Done when**: Tests pass + snapshot generated.

---

### Task 4.5: Write and implement aspect-ratio tests

**Goal**: Add aspect-ratio support.

**File to modify**: `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`:
```csharp
/// <summary>Aspect ratio (width / height). When one dimension is known, the other is computed.</summary>
public float? AspectRatio { get; set; }
```

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineAspectRatioTests.cs`

**Tests** (3 tests):
```
ComputeLayout_AspectRatio_WidthDefined_CalculatesHeight
ComputeLayout_AspectRatio_HeightDefined_CalculatesWidth
ComputeLayout_AspectRatio_WithGrow_MaintainsRatio
```

**Implementation in** `src/FlexRender.Core/Layout/LayoutEngine.cs`:
After sizing a child (after flex resolution), apply aspect ratio:
```csharp
if (child.Element.AspectRatio.HasValue)
{
    var ratio = child.Element.AspectRatio.Value;
    if (ratio > 0)
    {
        if (HasExplicitWidth(child.Element) && !HasExplicitHeight(child.Element))
            child.Height = child.Width / ratio;
        else if (HasExplicitHeight(child.Element) && !HasExplicitWidth(child.Element))
            child.Width = child.Height * ratio;
    }
}
```

- `src/FlexRender.Yaml/Parsing/TemplateParser.cs`: Parse `aspectRatio` property

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineAspectRatioTests"`
**Done when**: All 3 tests pass + all existing tests pass.

---

## Phase 5: Auto Margins

### Task 5.1: Write auto margin tests

**Goal**: Write failing tests for margin:auto behavior.

**File to create**: `tests/FlexRender.Tests/Layout/LayoutEngineAutoMarginTests.cs`

**Tests** (5 tests):
```
ComputeLayout_AutoMarginLeft_PushesChildToRight
ComputeLayout_AutoMarginBothSides_CentersChild
ComputeLayout_AutoMargins_OverridesJustifyContent
ComputeLayout_AutoMargins_NegativeFreeSpace_MarginIsZero
ComputeLayout_AutoMarginCrossAxis_CentersVertically
```

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineAutoMarginTests"`
**Done when**: Tests compile but FAIL.

---

### Task 5.2: Add MarginValue and MarginValues types

**Goal**: Create types to represent auto margins.

**File to create**: `src/FlexRender.Core/Layout/Units/MarginValues.cs`
```csharp
namespace FlexRender.Layout.Units;

/// <summary>
/// Represents a single margin value that can be either a fixed pixel amount or auto.
/// </summary>
/// <param name="Pixels">The pixel value (null for auto).</param>
/// <param name="IsAuto">Whether this margin is auto-distributed.</param>
public readonly record struct MarginValue(float? Pixels, bool IsAuto)
{
    /// <summary>Creates an auto margin.</summary>
    public static MarginValue Auto => new(null, true);

    /// <summary>Creates a fixed margin.</summary>
    public static MarginValue Fixed(float px) => new(px, false);

    /// <summary>The resolved pixel value (0 for auto margins before distribution).</summary>
    public float ResolvedPixels => Pixels ?? 0f;
}

/// <summary>
/// Represents resolved margin values for all four sides, supporting auto margins.
/// </summary>
/// <param name="Top">Top margin.</param>
/// <param name="Right">Right margin.</param>
/// <param name="Bottom">Bottom margin.</param>
/// <param name="Left">Left margin.</param>
public readonly record struct MarginValues(
    MarginValue Top, MarginValue Right, MarginValue Bottom, MarginValue Left)
{
    /// <summary>Whether any side is auto.</summary>
    public bool HasAuto => Top.IsAuto || Right.IsAuto || Bottom.IsAuto || Left.IsAuto;

    /// <summary>Count of auto margins on main axis (horizontal for row, vertical for column).</summary>
    public int MainAxisAutoCount(bool isColumn) => isColumn
        ? (Top.IsAuto ? 1 : 0) + (Bottom.IsAuto ? 1 : 0)
        : (Left.IsAuto ? 1 : 0) + (Right.IsAuto ? 1 : 0);

    /// <summary>Count of auto margins on cross axis.</summary>
    public int CrossAxisAutoCount(bool isColumn) => isColumn
        ? (Left.IsAuto ? 1 : 0) + (Right.IsAuto ? 1 : 0)
        : (Top.IsAuto ? 1 : 0) + (Bottom.IsAuto ? 1 : 0);

    /// <summary>All sides zero, no auto.</summary>
    public static readonly MarginValues Zero = new(
        MarginValue.Fixed(0), MarginValue.Fixed(0),
        MarginValue.Fixed(0), MarginValue.Fixed(0));
}
```

**Test to write**: `tests/FlexRender.Tests/Layout/Units/MarginValuesTests.cs`
```
MarginValue_Auto_HasNoPixels
MarginValue_Fixed_HasPixels
MarginValues_HasAuto_TrueWhenAnyAuto
MarginValues_MainAxisAutoCount_Column_CountsTopBottom
MarginValues_MainAxisAutoCount_Row_CountsLeftRight
```

**Test command**: `dotnet test FlexRender.slnx --filter "MarginValuesTests"`
**Done when**: Type tests pass.

---

### Task 5.3: Add margin auto parsing

**Goal**: Extend PaddingParser to handle "auto" token in margins.

**Files to modify**:
- `src/FlexRender.Core/Layout/Units/PaddingParser.cs` -- add `ParseMargin` method:
```csharp
/// <summary>
/// Parses a margin string that may contain "auto" tokens.
/// </summary>
/// <param name="value">The margin string (e.g., "auto", "10 auto", "auto 20 auto 30").</param>
/// <param name="parentSize">Parent container size for percentage resolution.</param>
/// <param name="fontSize">Font size for em resolution.</param>
/// <returns>Resolved margin values with auto support.</returns>
public static MarginValues ParseMargin(string? value, float parentSize, float fontSize)
{
    if (string.IsNullOrWhiteSpace(value))
        return MarginValues.Zero;

    var tokens = SplitTokens(value);
    // ... parse each token, "auto" -> MarginValue.Auto, otherwise -> MarginValue.Fixed(resolved)
}
```

**Tests** in `tests/FlexRender.Tests/Layout/Units/PaddingParserTests.cs`:
```
ParseMargin_Auto_ReturnsAllAuto
ParseMargin_MixedAutoAndFixed_ParsesCorrectly
ParseMargin_FourValues_WithAuto_ParsesCorrectly
ParseMargin_NoAuto_ReturnsFixedValues
```

**Test command**: `dotnet test FlexRender.slnx --filter "PaddingParserTests"`
**Done when**: Parser tests pass.

---

### Task 5.4: Implement auto margins in LayoutEngine

**Goal**: Auto margins consume free space before justify-content.

**Files to modify**:
- `src/FlexRender.Core/Layout/LayoutEngine.cs`

In `LayoutColumnFlex` and `LayoutRowFlex`, before justify-content:
```csharp
// Auto margins consume free space BEFORE justify-content
var hasAutoMargins = false;
var totalAutoCount = 0;
foreach (var child in node.Children)
{
    if (child.Element.Position == Position.Absolute) continue;
    var childMargin = PaddingParser.ParseMargin(child.Element.Margin, ...);
    var autoCount = childMargin.MainAxisAutoCount(isColumn: true);
    if (autoCount > 0)
    {
        hasAutoMargins = true;
        totalAutoCount += autoCount;
    }
}

if (hasAutoMargins && freeSpace > 0)
{
    var spacePerAuto = freeSpace / totalAutoCount;
    // ... distribute space to auto margins, justify-content is IGNORED
}
else
{
    // ... normal justify-content logic
}
```

Cross-axis auto margins:
```csharp
var crossAutoCount = childMargin.CrossAxisAutoCount(isColumn: true);
if (crossAutoCount == 2 && crossFreeSpace > 0)
{
    // Center: both auto -> split equally
    var offset = crossFreeSpace / 2;
    child.X = padding.Left + offset;
}
else if (crossAutoCount == 1 && crossFreeSpace > 0)
{
    // One auto -> push to opposite side
    if (childMargin.Left.IsAuto)
        child.X = padding.Left + crossFreeSpace;
    // else Right is auto -> child stays at Left
}
```

Fallback: negative freeSpace -> auto margins = 0.

**Test command**: `dotnet test FlexRender.slnx --filter "LayoutEngineAutoMarginTests"`
**Done when**: All 5 auto margin tests pass + all existing tests pass.

---

## Phase 6: Snapshot Tests and Integration

### Task 6.1: Create snapshot tests for new features

**Goal**: Add golden image snapshot tests for visual verification.

**File to modify**: `tests/FlexRender.Tests/Snapshots/VisualSnapshotTests.cs`

**Snapshots to create** (YAML templates + golden images):

| Test Name | Description |
|-----------|-------------|
| `flex_align_self_mixed` | Mixed align-self values in row |
| `flex_row_reverse` | RowReverse direction |
| `flex_column_reverse` | ColumnReverse direction |
| `flex_basis_grow` | Basis + grow distribution |
| `flex_min_max_width` | Items with min/max constraints |
| `flex_row_wrap_basic` | 5 items wrap to 2 lines |
| `flex_row_wrap_gap` | Wrap with row/column gap |
| `flex_row_wrap_align_content_center` | Centered wrapped lines |
| `flex_absolute_position` | Absolute positioned overlay |
| `flex_overflow_hidden` | Clipped overflow content |

**Commands**:
```bash
# First run: generate golden images
UPDATE_SNAPSHOTS=true dotnet test FlexRender.slnx --filter "VisualSnapshotTests"
# Subsequent runs: compare against golden
dotnet test FlexRender.slnx --filter "VisualSnapshotTests"
```

**Done when**: All snapshots generated and tests pass.

---

### Task 6.2: Update documentation

**Goal**: Update llms.txt and llms-full.txt with new features.

**Files to modify**:
- `llms.txt` -- add new properties overview
- `llms-full.txt` -- add detailed property documentation
- `AGENTS.md` -- update Key Classes table if needed

**Done when**: Documentation reflects all new features.

---

## Execution Summary

| Phase | Tasks | Description |
|-------|-------|-------------|
| 0 | 0.1 - 0.3 | Refactoring: move properties, remove dead code, inject limits |
| 1 | 1.1 - 1.9 | Quick wins: align-self, margin 4-side, display:none, reverse, overflow fallback |
| 2 | 2.1 - 2.4 | Core: flex-basis, min/max, two-pass resolution algorithm |
| 3 | 3.1 - 3.6 | Wrapping: row/column gap, wrap, align-content |
| 4 | 4.1 - 4.5 | Advanced: position, overflow:hidden, aspect-ratio |
| 5 | 5.1 - 5.4 | Auto margins: types, parsing, distribution |
| 6 | 6.1 - 6.2 | Snapshots and documentation |

**Total tasks**: 27
**Total estimated tests**: ~87 new tests

## Regression Safety

After EVERY task:
```bash
dotnet test FlexRender.slnx
```
ALL existing 100+ tests must continue to pass. Zero regressions allowed.

## Conventions Checklist (every task)

- [ ] AOT safe -- no reflection, no `dynamic`, no `Type.GetType()`
- [ ] Classes are `sealed` unless designed for inheritance
- [ ] Null checks via `ArgumentNullException.ThrowIfNull()`
- [ ] ResourceLimits preserved -- never remove or weaken limits
- [ ] New element types follow switch-based dispatch pattern
- [ ] XML docs on all public API surface
- [ ] File-scoped namespaces
- [ ] `readonly record struct` for value types
- [ ] Test naming: `MethodUnderTest_Scenario_ExpectedResult`
