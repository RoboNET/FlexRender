# SkiaRenderer Analysis & Yoga Fixture Reference

## Part 1: SkiaRenderer Analysis

### File: `src/FlexRender.Skia/Rendering/SkiaRenderer.cs` (1117 lines)

### Architecture Overview

SkiaRenderer is `internal sealed`, implements `IDisposable` + `IAsyncDisposable`. It orchestrates the full pipeline:

```
Template -> TemplateExpander -> ProcessTemplate -> LayoutEngine.ComputeLayout -> RenderNode tree traversal
```

### Key Methods

#### `RenderNode` (lines 322-347) -- Recursive Layout Tree Traversal

```csharp
private void RenderNode(SKCanvas canvas, LayoutNode node, float offsetX, float offsetY,
    IReadOnlyDictionary<string, SKBitmap>? imageCache, int depth = 0)
```

- Depth guard: throws if `depth > _limits.MaxRenderDepth`
- Accumulates absolute position: `x = node.X + offsetX`, `y = node.Y + offsetY`
- Calls `DrawElement()` for current node
- Recursively renders `node.Children`
- **No clipping logic exists** -- all children render without clip bounds

**Changes needed for expansion:**

1. **display:none** -- Add early return: `if (node.Element.Display == Display.None) return;`
2. **overflow:hidden** -- Add before children traversal:
   ```csharp
   if (element is FlexElement { Overflow: Overflow.Hidden })
   {
       canvas.Save();
       canvas.ClipRect(new SKRect(x, y, x + node.Width, y + node.Height));
   }
   // render children
   // canvas.Restore() if clipped
   ```
3. **position:absolute** -- No change needed in renderer; layout engine places absolute children at correct (X, Y). RenderNode already uses node.X/Y which would contain the absolute coordinates.

#### `DrawElement` (lines 359-412) -- Element Type Dispatch

```csharp
private void DrawElement(SKCanvas canvas, TemplateElement element, float x, float y,
    float width, float height, IReadOnlyDictionary<string, SKBitmap>? imageCache)
```

- Draws background first (if `element.Background` is set)
- Switch dispatch on concrete types: TextElement, QrElement, BarcodeElement, ImageElement, SeparatorElement, FlexElement
- **FlexElement case is empty** -- just a container, children rendered by RenderNode recursion
- QR/Barcode render via `IContentProvider.Generate()` -> `canvas.DrawBitmap()`
- Image renders via `ImageProvider.Generate()` -> `canvas.DrawBitmap()`
- Text renders via `TextRenderer.DrawText()` with bounds rect

**No changes needed** for flexbox expansion (no new element types).

#### `ProcessElement` (lines 574-688) -- Template Expression Processing

- Creates new instances of each element type, processing `{{expressions}}` in string properties
- Copies ALL flex-item properties (Grow, Shrink, Basis, AlignSelf, Order, Width, Height, Padding, Margin)
- **After Phase 0 refactoring** (move props to base class): ProcessElement could be simplified with a helper that copies base properties

#### `ProcessFlexElement` (lines 690-721)

- Copies all container properties (Direction, Wrap, Justify, Align, AlignContent, Gap)
- Recursively processes children via `ProcessElement()`
- **New properties to copy**: RowGap, ColumnGap (Phase 3b), Overflow (Phase 4b)

### Current Clipping State

**No clipping exists anywhere in the renderer.** The only `Save()`/`Restore()` usage is:
1. `RenderToCanvas` (line 202/220) -- for offset translation, not clipping
2. `RotateBitmap` (line 486-496) -- for rotation transform

### Canvas State Management

SkiaSharp uses a stack-based model:
- `canvas.Save()` -- push state
- `canvas.ClipRect(rect)` -- restrict drawing area
- `canvas.Restore()` -- pop state, remove clip

This maps directly to `overflow:hidden` implementation.

### Thread Safety

- SkiaRenderer stores no mutable state between renders (except `_fontManager` which is concurrent)
- Image cache is passed as parameter, not stored on instance
- Layout engine is created fresh or is stateless

---

## Part 2: Yoga Test Fixtures Analysis

Source: `facebook/yoga/gentest/fixtures/` (24 HTML fixture files)

### Fixture Files Mapped to FlexRender Phases

#### Phase 1 Fixtures

| Fixture File | Test IDs Relevant | FlexRender Feature |
|---|---|---|
| `YGAlignSelfTest.html` | `align_self_center`, `align_self_flex_end`, `align_self_flex_start`, `align_self_flex_end_override_flex_start` | **1a. align-self** |
| `YGMarginTest.html` | `margin_start`, `margin_top`, `margin_end`, `margin_bottom`, `margin_and_flex_row`, `margin_and_flex_column`, `margin_and_stretch_row`, `margin_and_stretch_column`, `margin_with_sibling_row`, `margin_with_sibling_column` | **1b. margin 4-side** |
| `YGDisplayTest.html` | `display_none`, `display_none_fixed_size`, `display_none_with_margin`, `display_none_with_child`, `display_none_with_position` | **1c. display:none** |
| `YGFlexDirectionTest.html` | `flex_direction_column_reverse`, `flex_direction_row_reverse`, `flex_direction_row_reverse_margin_*`, `flex_direction_column_reverse_margin_*`, `flex_direction_row_reverse_padding_*`, `flex_direction_column_reverse_padding_*` | **1d. reverse directions** |
| `YGJustifyContentTest.html` | (overflow cases) | **1e. overflow fallback** |

#### Phase 2 Fixtures

| Fixture File | Test IDs Relevant | FlexRender Feature |
|---|---|---|
| `YGFlexTest.html` | `flex_basis_flex_grow_column`, `flex_basis_flex_grow_row`, `flex_basis_flex_shrink_column`, `flex_basis_flex_shrink_row`, `flex_shrink_to_zero`, `flex_basis_overrides_main_size`, `flex_grow_less_than_factor_one` | **2a. flex-basis + flex resolution** |
| `YGMinMaxDimensionTest.html` | `max_width`, `max_height`, `min_width_overrides_width`, `max_width_overrides_width`, `min_height_overrides_height`, `max_height_overrides_height`, `child_min_max_width_flexing`, `flex_grow_within_constrained_min_row`, `flex_grow_within_max_width`, `min_max_percent_no_width_height` | **2b. min/max** |

#### Phase 3 Fixtures

| Fixture File | Test IDs Relevant | FlexRender Feature |
|---|---|---|
| `YGFlexWrapTest.html` | `wrap_column`, `wrap_row`, `wrap_row_align_items_flex_end`, `wrap_row_align_items_center`, `flex_wrap_children_with_min_main_overriding_flex_basis`, `flex_wrap_align_stretch_fits_one_row`, `wrap_reverse_row_align_content_*`, `wrap_reverse_column_fixed_size` | **3a. flex-wrap** |
| `YGAlignContentTest.html` | `align_content_flex_start_wrap`, `align_content_flex_end_wrap`, `align_content_center_wrap`, `align_content_space_between_wrap`, `align_content_space_around_wrap`, `align_content_space_evenly_wrap`, `align_content_stretch`, `align_content_stretch_row`, `align_content_stretch_row_with_*` | **3a. align-content** |
| `YGGapTest.html` | `column_gap_flexible`, `column_gap_inflexible`, `column_row_gap_wrapping`, `column_gap_justify_*`, `column_gap_wrap_align_*` | **3b. row-gap/column-gap** |

#### Phase 4 Fixtures

| Fixture File | Test IDs Relevant | FlexRender Feature |
|---|---|---|
| `YGAbsolutePositionTest.html` | `absolute_layout_width_height_start_top`, `absolute_layout_width_height_end_bottom`, `absolute_layout_start_top_end_bottom`, `absolute_layout_align_items_and_justify_content_center`, `absolute_layout_percentage_bottom_based_on_parent_height`, `absolute_layout_within_border`, `absolute_layout_padding_*` | **4a. position absolute/relative** |
| `YGAspectRatioTest.html` | Most tests disabled (`data-disabled="true"`) due to Web inconsistency. `zero_aspect_ratio_behaves_like_auto` is useful. | **4c. aspect-ratio** |

#### Phase 5 Fixtures

| Fixture File | Test IDs Relevant | FlexRender Feature |
|---|---|---|
| `YGMarginTest.html` | `margin_auto_bottom`, `margin_auto_top`, `margin_auto_bottom_and_top`, `margin_auto_bottom_and_top_justify_center`, `margin_auto_multiple_children_column`, `margin_auto_multiple_children_row`, `margin_auto_left_and_right_column`, `margin_auto_left_and_right`, `margin_auto_left_and_right_stretch`, `margin_auto_top_and_bottom_stretch`, `margin_auto_left_right_child_bigger_than_parent`, `margin_auto_overflowing_container` | **5. auto margins** |

### Key Expected Values from Yoga Fixtures

#### align-self (Phase 1a)

| Test ID | Container | Child | Expected Position |
|---|---|---|---|
| `align_self_center` | 100x100, column | 10x10, align-self:center | x=45, y=0 |
| `align_self_flex_end` | 100x100, column | 10x10, align-self:flex-end | x=90, y=0 |
| `align_self_flex_start` | 100x100, column | 10x10, align-self:flex-start | x=0, y=0 |
| `align_self_flex_end_override_flex_start` | 100x100, column, align-items:flex-start | 10x10, align-self:flex-end | x=90, y=0 |

#### flex-direction reverse (Phase 1d)

| Test ID | Container | Children | Expected |
|---|---|---|---|
| `flex_direction_column_reverse` | 100x100, column-reverse | 3 x h:10 | child[0]: y=90, child[1]: y=80, child[2]: y=70 |
| `flex_direction_row_reverse` | 100x100, row-reverse | 3 x w:10 | child[0]: x=90, child[1]: x=80, child[2]: x=70 |

#### display:none (Phase 1c)

| Test ID | Expected |
|---|---|
| `display_none` | visible child gets width=100 (full container, grow:1) |
| `display_none_fixed_size` | visible child gets width=100 (hidden 20x20 ignored) |
| `display_none_with_margin` | margin on hidden element ignored, visible gets full width |
| `display_none_with_child` | hidden element and its children skipped, 2 visible get 50px each |

#### flex-basis (Phase 2a)

| Test ID | Expected |
|---|---|
| `flex_basis_flex_grow_column` | child[0]: basis=50, grows to 75 (50 + 50/2); child[1]: grows to 25 (0 + 50/2) |
| `flex_basis_flex_grow_row` | same but horizontal |
| `flex_basis_flex_shrink_column` | child[0]: 100 basis shrinks; child[1]: 50 basis stays |
| `flex_basis_overrides_main_size` | child with height:20 but basis:50 uses 50 as starting point |
| `flex_grow_less_than_factor_one` | fractional grow: total grow=0.8 (<1), only 80% of free space distributed |

#### min/max (Phase 2b)

| Test ID | Expected |
|---|---|
| `max_width` | child width clamped to 50 (not stretched to 100) |
| `max_height` | child height clamped to 50 |
| `min_width_overrides_width` | container 50px wide but min-width=100 -> actual=100 |
| `max_width_overrides_width` | container 200px wide but max-width=100 -> actual=100 |
| `child_min_max_width_flexing` | child[0]: min-width:60 wins over basis:0, child[1]: max-width:20 caps flex growth |

#### wrap (Phase 3a)

| Test ID | Expected |
|---|---|
| `wrap_row` | 4 items @ 30px in 100px -> line 1: [0,1,2] (90px), line 2: [3] (30px) |
| `wrap_column` | 4 items @ 30px high in 100px -> line 1: [0,1,2] (90px), line 2: [3] |
| `wrap_row_align_items_flex_end` | items aligned to bottom of each line |
| `wrap_row_align_items_center` | items centered within each line |

#### align-content (Phase 3a)

| Test ID | Lines | Expected Cross Position |
|---|---|---|
| `align_content_flex_start_wrap` | 2 lines in 120px container | lines packed at top |
| `align_content_flex_end_wrap` | 2 lines | lines packed at bottom |
| `align_content_center_wrap` | 2 lines | lines centered |
| `align_content_space_between_wrap` | 2 lines | first at top, last at bottom |
| `align_content_space_around_wrap` | 2 lines | equal space around each line |
| `align_content_space_evenly_wrap` | 2 lines | equal space between all slots |
| `align_content_stretch` | multiple lines | lines stretch to fill available cross space |

#### auto margins (Phase 5)

| Test ID | Expected |
|---|---|
| `margin_auto_bottom` | child pushed to top, sibling at bottom |
| `margin_auto_top` | child pushed down by auto margin |
| `margin_auto_bottom_and_top` | child centered vertically |
| `margin_auto_left_and_right` | child centered horizontally in cross axis |
| `margin_auto_multiple_children_column` | free space distributed between auto margins |
| `margin_auto_left_right_child_bigger_than_parent` | auto margin becomes 0 when negative free space |
| `margin_auto_overflowing_container` | auto margin becomes 0 when children overflow |

---

## Part 3: Renderer Changes Per Phase

### Phase 1 Changes

| Sub-phase | SkiaRenderer Change | Scope |
|---|---|---|
| 1a. align-self | None | Layout engine only |
| 1b. margin 4-side | None | Layout engine only |
| 1c. display:none | Add `if (Display.None) return` in `RenderNode` | 2 lines in RenderNode |
| 1d. reverse directions | None | Layout engine only |
| 1e. overflow fallback | None | Layout engine only |

### Phase 2 Changes

| Sub-phase | SkiaRenderer Change | Scope |
|---|---|---|
| 2a. flex resolution | None | Layout engine only |
| 2b. min/max | Copy new props in `ProcessElement` | ~24 lines (4 props x 6 types) |

### Phase 3 Changes

| Sub-phase | SkiaRenderer Change | Scope |
|---|---|---|
| 3a. flex-wrap + align-content | None | Layout engine only |
| 3b. row-gap/column-gap | Copy RowGap, ColumnGap in `ProcessFlexElement` | 2 lines |

### Phase 4 Changes

| Sub-phase | SkiaRenderer Change | Scope |
|---|---|---|
| 4a. position absolute/relative | Copy Position, Top/Right/Bottom/Left in ProcessElement | ~30 lines |
| 4b. overflow:hidden | Add Save/ClipRect/Restore around children in RenderNode | ~10 lines |
| 4c. aspect-ratio | Copy AspectRatio in ProcessElement | ~6 lines |

### Phase 5 Changes

| Sub-phase | SkiaRenderer Change | Scope |
|---|---|---|
| 5. auto margins | None (margin already copied, "auto" is a string value) | 0 lines |

---

## Part 4: Fixtures NOT Relevant for FlexRender

| Fixture | Reason |
|---|---|
| `YGBorderTest.html` | FlexRender has no border-width layout support (not planned) |
| `YGRoundingTest.html` | Pixel rounding -- not critical for first pass |
| `YGPercentageTest.html` | Mostly already supported via Unit/UnitParser |
| `YGDimensionTest.html` | Basic sizing -- already tested |
| `YGStaticPositionTest.html` | CSS `position:static` containing blocks -- advanced, Phase 4+ |
| `YGIntrinsicSizeTest.html` | Already handled by MeasureAllIntrinsics |
| `YGBoxSizingTest.html` | `box-sizing: border-box` -- not planned |
| `YGSizeOverflowTest.html` | Size overflow edge cases -- low priority |
| `YGAutoTest.html` | Auto dimensions -- mostly covered by existing behavior |
| `YGAndroidNewsFeed.html` | Android-specific integration test |

## Part 5: Recommended Test Scenarios from Fixtures

### Priority 1 (Phase 1 -- must have)

1. **align-self override**: parent align-items:start, child align-self:end -> child at cross-axis end
2. **align-self auto**: falls back to parent align-items
3. **column-reverse basic**: 3 children stacked from bottom
4. **row-reverse basic**: 3 children from right to left
5. **reverse with gap**: gap applied between reversed items
6. **reverse with padding**: padding still at container edges, items reversed within
7. **display:none in flex grow**: hidden child's grow ignored, visible child takes all space
8. **display:none with margins**: margins on hidden child ignored

### Priority 2 (Phase 2 -- must have)

1. **flex-basis + grow**: basis as starting size, remaining space distributed by grow
2. **flex-basis overrides main size**: basis wins over width/height
3. **shrink scaled by basis**: larger basis shrinks more (CSS-correct formula)
4. **min-width stops shrink**: item with min-width doesn't shrink below minimum
5. **max-width caps grow**: item with max-width doesn't grow beyond maximum
6. **two-pass resolution**: multiple constrained items, frozen items excluded from redistribution

### Priority 3 (Phase 3 -- must have)

1. **basic row wrap**: items exceeding container width wrap to next line
2. **wrap with gap**: gap between items AND between lines (row-gap/column-gap)
3. **wrap line height**: line height = tallest child in that line
4. **align-content center**: wrapped lines centered in container
5. **align-content stretch**: lines stretch to fill available cross space
6. **align-content space-between**: first line at top, last at bottom
7. **wrap-reverse**: lines in reverse order on cross axis

### Priority 4 (Phase 4-5 -- important)

1. **absolute removed from flow**: other children positioned as if absolute doesn't exist
2. **absolute with insets**: top/left/right/bottom position absolute child
3. **absolute with align-items/justify-content**: positions absolute child without insets
4. **overflow hidden clips children**: child extending beyond parent bounds is clipped
5. **auto margin centering**: margin-left:auto + margin-right:auto centers on cross axis
6. **auto margin overrides justify**: auto margins consume space before justify-content
7. **auto margin negative space**: becomes 0 when container overflows
