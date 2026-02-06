# Test Plan: Flexbox Expansion

Total: ~87 new tests (71 unit + 10 snapshot + 6 integration)

## Current Coverage

Already tested:
- FlexDirection: Row, Column
- JustifyContent: all 6 values
- AlignItems: Start, Center, End, Stretch
- Gap: px, %
- Padding: uniform, 2/3/4-value
- Margin: uniform
- Grow/Shrink: proportional distribution
- Percent widths
- Nested flex containers (2-3 levels)

## Phase 1 Tests

### align-self (LayoutEngineAlignSelfTests.cs)
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

### margin 4-side -- extend LayoutEnginePaddingMarginTests.cs
- 4-side margin parsing
- Margin in column direction
- Margin in row direction
- Margin with gap interaction

### display:none
```
ComputeLayout_DisplayNone_ChildSkippedInLayout
ComputeLayout_DisplayNone_OtherChildrenNotAffected
MeasureIntrinsic_DisplayNone_ReturnsZero
```

### reverse directions (LayoutEngineDirectionTests.cs)
```
ComputeLayout_RowReverse_ChildrenOrderedRightToLeft
ComputeLayout_RowReverse_FirstChildAtRightEdge
ComputeLayout_RowReverse_WithGap_GapBetweenReversedItems
ComputeLayout_ColumnReverse_ChildrenOrderedBottomToTop
ComputeLayout_ColumnReverse_FirstChildAtBottom
ComputeLayout_ColumnReverse_WithGap_GapBetweenReversedItems
```

### overflow fallback
```
ComputeLayout_SpaceBetween_NegativeFreeSpace_FallbackToStart
ComputeLayout_SpaceAround_NegativeFreeSpace_FallbackToStart
ComputeLayout_SpaceEvenly_NegativeFreeSpace_FallbackToStart
```

## Phase 2 Tests

### flex-basis (LayoutEngineFlexBasisTests.cs)
```
ComputeLayout_Column_FlexBasisPixels_SetsInitialSize
ComputeLayout_Column_FlexBasisPercent_CalculatesFromParent
ComputeLayout_Column_FlexBasisAuto_UsesContentSize
ComputeLayout_Column_FlexBasisWithGrow_GrowsFromBasis
ComputeLayout_Column_FlexBasisWithShrink_ShrinksFromBasis
ComputeLayout_Row_FlexBasisPixels_SetsInitialWidth
ComputeLayout_Row_FlexBasis0_WithGrow_EqualDistribution
```

### min/max (LayoutEngineMinMaxTests.cs)
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

## Phase 3 Tests

### flex-wrap (LayoutEngineWrapTests.cs)
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

### align-content (LayoutEngineAlignContentTests.cs)
```
ComputeLayout_RowWrap_AlignContentStart_LinesPackedAtStart
ComputeLayout_RowWrap_AlignContentCenter_LinesCentered
ComputeLayout_RowWrap_AlignContentEnd_LinesPackedAtEnd
ComputeLayout_RowWrap_AlignContentStretch_LinesStretchToFill
ComputeLayout_RowWrap_AlignContentSpaceBetween_SpaceDistributedBetweenLines
ComputeLayout_RowWrap_AlignContentSpaceAround_SpaceDistributedAroundLines
ComputeLayout_RowWrap_AlignContentSpaceEvenly_EvenDistribution
```

## Phase 4 Tests

### position absolute/relative
- Absolute removed from flow, positioned by insets
- Relative: offset from normal position

### overflow:hidden
- SkiaRenderer clipping tests (snapshot)

### aspect-ratio
```
ComputeLayout_AspectRatio_WidthDefined_CalculatesHeight
ComputeLayout_AspectRatio_HeightDefined_CalculatesWidth
ComputeLayout_AspectRatio_WithGrow_MaintainsRatio
```

## Phase 5 Tests

### auto margins
```
ComputeLayout_AutoMarginLeft_PushesChildToRight
ComputeLayout_AutoMarginBothSides_CentersChild
ComputeLayout_AutoMargins_OverridesJustifyContent
ComputeLayout_AutoMargins_NegativeFreeSpace_MarginIsZero
ComputeLayout_AutoMarginCrossAxis_CentersVertically
```

## Snapshot Tests (new golden images)

| Test | Description |
|------|-------------|
| flex_row_wrap_basic | 5 items wrap to 2 lines |
| flex_row_wrap_gap | wrap with gap |
| flex_row_wrap_align_content_center | centered lines |
| flex_align_self_mixed | mixed align-self values |
| flex_row_reverse | RowReverse direction |
| flex_column_reverse | ColumnReverse direction |
| flex_basis_grow | basis + grow distribution |
| flex_min_max_width | items with min/max constraints |
| flex_absolute_position | absolute positioned overlay |
| flex_overflow_hidden | clipped overflow |

## Conventions

- Framework: xUnit with [Fact] and [Theory]/[InlineData]
- Naming: MethodUnderTest_Scenario_ExpectedResult
- Class naming: {ClassName}Tests
- Pattern: Arrange-Act-Assert
- Templates: programmatic creation (not YAML parsing) for unit tests
- Layout assertions: Assert.Equal(expected, actual, precision)
- Snapshots: SnapshotTestBase with AssertSnapshot(), golden images in Snapshots/golden/
- UPDATE_SNAPSHOTS=true dotnet test to regenerate

## Regression

ALL existing 100+ tests must pass unchanged after every phase.
