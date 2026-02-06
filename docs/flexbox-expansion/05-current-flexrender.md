# Current FlexRender Layout Engine Analysis

## Two-Pass Algorithm

**Pass 1 -- Intrinsic Measurement (MeasureAllIntrinsics)**:
- Bottom-up traversal
- Computes IntrinsicSize(MinWidth, MaxWidth, MinHeight, MaxHeight) for each element
- Uses ReferenceEqualityComparer.Instance for dictionary
- Dispatch via switch on concrete types: TextElement, QrElement, BarcodeElement, ImageElement, SeparatorElement, FlexElement
- Text: TextMeasurer delegate for real font metrics; fallback: fontSize * 1.4
- Flex containers: aggregate children by direction (column: sum heights/max widths; row: sum widths/max heights) + gap + padding + margin

**Pass 2 -- Layout (ComputeLayout)**:
- Top-down traversal
- Each element gets LayoutContext(ContainerWidth, ContainerHeight, FontSize, IntrinsicSizes)
- Result: LayoutNode tree (Element, X, Y, Width, Height) with relative positions
- Root: virtual FlexElement { Direction = Column }

## Supported Units

Via Unit/UnitParser:
- **px** (or plain number) -- absolute pixels
- **%** -- percent of parent
- **em** -- relative to font size
- **auto** -- automatic (returns null)

PaddingParser: CSS shorthand 1, 2, 3, or 4 values with any units.

## AST Element Hierarchy

```
TemplateElement (abstract)
  +-- TextElement      (sealed)
  +-- ImageElement     (sealed)
  +-- QrElement        (sealed)
  +-- BarcodeElement   (sealed)
  +-- SeparatorElement (sealed)
  +-- FlexElement      (sealed)
  +-- EachElement      (sealed) -- expanded before layout
  +-- IfElement        (sealed) -- expanded before layout
```

## Key Methods in LayoutEngine

| Method | Lines | Role |
|--------|-------|------|
| ComputeLayout(Template) | 36-145 | Entry point |
| MeasureAllIntrinsics(element) | 161-166 | Pass 1 |
| MeasureIntrinsic(element, sizes) | 174-189 | Dispatch by type |
| MeasureFlexIntrinsic(flex, sizes) | 305-394 | Measure flex container |
| LayoutElement(element, context) | 434-446 | Pass 2 dispatch |
| LayoutFlexElement(flex, context) | 448-502 | Layout flex container |
| LayoutColumnFlex(node, flex, context, padding, gap) | 695-818 | Column flex algorithm |
| LayoutRowFlex(node, flex, context, padding, gap) | 820-943 | Row flex algorithm |
| GetFlexGrow(element) | 945-966 | Switch dispatch |
| GetFlexShrink(element) | 976-997 | Switch dispatch |

## Critical Gaps (defined but NOT implemented)

1. **flex-wrap** -- Enum FlexWrap exists (NoWrap, Wrap, WrapReverse), layout doesn't wrap
2. **flex-basis** -- Property exists on all elements, NEVER READ by LayoutEngine
3. **align-self** -- Enum and property exist, layout only uses container-level flex.Align
4. **align-content** -- Enum exists, no implementation (needs wrap)
5. **order** -- Property exists, children processed in document order

## Architectural Issues

1. **Flex-item property duplication**: Grow, Shrink, Basis, AlignSelf, Order, Width, Height duplicated across 6 element types
2. **Switch dispatch**: GetFlexGrow, GetFlexShrink, HasExplicitWidth, HasExplicitHeight each have 6-branch switch
3. **FlexContainerProperties/FlexItemProperties**: defined as record structs, never used anywhere
4. **Margin**: uniform only (single float via UnitParser), unlike padding (4-side via PaddingParser)
5. **Shrink formula**: simple weighted shrink, NOT scaled by basis (incorrect per CSS spec)
