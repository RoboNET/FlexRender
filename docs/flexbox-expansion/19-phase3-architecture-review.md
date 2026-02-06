# Phase 3 Architecture Review

**Reviewer**: architect
**Date**: 2026-02-06
**Scope**: Tasks 3.1-3.6 (Wrapping + Align-Content)
**Status**: ✅ **APPROVED WITH NOTES**

---

## Executive Summary

Phase 3 implementation is **architecturally sound** with acceptable trade-offs. The wrapping implementation successfully extends Phase 2's flex algorithm while maintaining correctness and performance.

**Key metrics**:
- **File size**: 1907 lines (was ~1250 in Phase 2) — +650 lines
- **Test Status**: 1151 tests passing (1131 + 20 Phase 3)
- **Method sizes**:
  - `LayoutWrappedFlex`: ~234 lines
  - `ResolveFlexForLine`: ~197 lines
  - `CalculateFlexLines`: ~64 lines

**Verdict**: **APPROVED** with recommendations for future refactoring opportunities.

---

## 1. Architectural Questions — Answers

### Q1: Code Duplication — ResolveFlexForLine vs LayoutColumnFlex

**Finding**: ~80% algorithmic duplication between `ResolveFlexForLine` and `LayoutColumnFlex`/`LayoutRowFlex`.

**Analysis**:

**Duplicated logic** (lines 1437-1545):
- Span<float> bases/sizes/frozen allocation
- Margin calculation (totalMarginMain)
- Basis resolution loop
- Freeze items with grow=0 / shrink=0
- **Iterative freeze loop** (lines 1482-1545):
  - unfrozenFreeSpace calculation
  - totalGrowFactors / totalShrinkScaled accumulation
  - Factor flooring
  - Hypothetical size computation (grow vs shrink)
  - ClampSize + freeze condition

**Unique aspects**:
- `ResolveFlexForLine` operates on `List<LayoutNode> lineChildren` (subset)
- Single-line methods operate on `node.Children` (full list)
- Line-level doesn't include positioning (only sizing + justify on main axis)

**Verdict**: **ACCEPTABLE DUPLICATION**

**Reasoning**:
1. **Extraction complexity**: Abstracting the iterative loop would require:
   - Generic `IReadOnlyList<LayoutNode>` parameter
   - 6-8 parameters (bases, sizes, frozen, context, isColumn, availableSize, etc.)
   - Return type: tuple or custom struct
   - **Result**: More complex than current duplication

2. **Maintainability**: Current approach is **easier to understand**:
   - Single-line layout: complete algorithm in one place (LayoutColumnFlex)
   - Multi-line layout: subset algorithm clearly marked as "per-line"
   - Changes to core algorithm visible in 3 places (Column, Row, PerLine) — grep-friendly

3. **Performance**: No overhead from abstraction indirection

4. **Precedent**: Yoga C++ has similar duplication between single-line and multi-line paths

**Recommendation**: **Keep as-is for Phase 3**. Consider extraction in future refactoring if:
- Algorithm changes frequently (hasn't been the case)
- Code size becomes unmaintainable (not yet at that point)

---

### Q2: Method Size — LayoutWrappedFlex ~234 lines, LayoutRowFlex ~230 lines

**Analysis**:

| Method | Lines | Sections | Acceptable? |
|--------|-------|----------|-------------|
| `LayoutWrappedFlex` | 234 | 7 logical sections | ✅ YES |
| `LayoutRowFlex` | 230 | 6 logical sections | ✅ YES |
| `LayoutColumnFlex` | 235 | 6 logical sections | ✅ YES |
| `ResolveFlexForLine` | 197 | 5 logical sections | ✅ YES |

**LayoutWrappedFlex structure** (7 sections):
1. Setup (isColumn, availableMainSize)
2. **Line breaking** (CalculateFlexLines)
3. **Per-line flex resolution** (ResolveFlexForLine loop)
4. Cross-axis size calculation
5. **Align-content distribution** (overflow fallback, switch on AlignContent)
6. **Per-child cross-axis positioning** (align-items + align-self)
7. **WrapReverse** inversion

**Verdict**: **ACCEPTABLE**

**Reasoning**:
1. Each section has clear responsibility
2. Comments delineate sections ("Step 1: Line breaking", etc.)
3. No deep nesting (max 3-4 levels)
4. Alternative (splitting into 7 methods) would hurt readability:
   - Current: Linear flow, easy to follow wrap → resolve → distribute → position
   - Split: Jump between 7 methods, harder to understand dependencies

5. **Comparison**: Yoga's `calculateLayout` is 400+ lines. Our 234 is reasonable.

**Recommendation**: **Keep as-is**. If method grows beyond 300 lines, consider splitting.

---

### Q3: Structure — Partial class or extension for wrapping?

**Current**: Single `LayoutEngine.cs` file (1907 lines)

**Options considered**:

#### Option A: Partial class split
```
LayoutEngine.cs         (core methods, 900 lines)
LayoutEngine.Wrap.cs    (wrapping methods, 500 lines)
LayoutEngine.Helpers.cs (shared helpers, 500 lines)
```

**Pros**:
- Smaller files, easier to navigate
- Clear separation: single-line vs multi-line

**Cons**:
- 3 files instead of 1 (cognitive overhead)
- Partial classes hide method relationships
- Unclear where to add new methods (which file?)

#### Option B: Separate class `FlexWrapEngine`
```
LayoutEngine.cs (delegates to FlexWrapEngine when wrap != NoWrap)
FlexWrapEngine.cs (line breaking, per-line resolution)
```

**Pros**:
- Clear separation of concerns
- Testable in isolation

**Cons**:
- Need to share helpers (ClampSize, ResolveFlexBasis, ResolveMinMax)
- Would require static helpers or dependency injection
- **Overkill** for current complexity

#### Option C: Current structure (single file)

**Pros**:
- All layout logic in one place
- Easy to grep/search
- Clear method ordering (dispatch → single-line → multi-line → helpers)

**Cons**:
- 1907 lines (large but not unmanageable)

**Verdict**: **Keep single file (Option C)**

**Reasoning**:
1. 1907 lines is large but **not egregious** for a complex algorithm
2. File has clear structure:
   - Lines 1-500: Core layout (ComputeLayout, LayoutElement, dispatch)
   - Lines 500-1200: Single-line (LayoutColumnFlex, LayoutRowFlex)
   - Lines 1200-1850: Multi-line (CalculateFlexLines, ResolveFlexForLine, LayoutWrappedFlex)
   - Lines 1850+: Helpers (ClampSize, ResolveFlexBasis, etc.)

3. **Modern IDEs** handle large files well (collapse regions, quick navigation)
4. **Partial classes** are antipattern for logic cohesion (use for generated code only)

**Recommendation**: **No refactoring needed for Phase 3**. Consider if file reaches 2500+ lines.

---

### Q4: Performance — List allocations vs fixed arrays

**Current allocations** (per wrapped flex container):

| Location | Type | Allocation | Frequency |
|----------|------|------------|-----------|
| `CalculateFlexLines` line 1361 | `List<FlexLine>` | 1 per container | O(lines) growth |
| `ResolveFlexForLine` line 1427 | `List<LayoutNode>` | 1 per line | O(children) per line |
| Total | | 1 + N (N = line count) | Acceptable for wrapping |

**Yoga comparison**:
- Yoga C++ uses `std::vector<FlexLine>` (heap allocation, same as C# List)
- Yoga Java uses `ArrayList<FlexLine>` (heap allocation)

**Analysis**:

**List<FlexLine> allocation** (line 1361):
- **Frequency**: Once per wrapped flex container
- **Size**: Typically < 10 lines (1000-line limit via MaxFlexLines)
- **Growth**: Amortized O(1) per Add()
- **Alternative**: Fixed array via ArrayPool
  ```csharp
  var linesArray = ArrayPool<FlexLine>.Shared.Rent(_limits.MaxFlexLines);
  try { ... } finally { ArrayPool<FlexLine>.Shared.Return(linesArray); }
  ```
- **Trade-off**: ArrayPool adds complexity (try/finally, manual return) for **marginal gain**

**List<LayoutNode> allocation** (line 1427):
- **Frequency**: Once per flex line (N times per container)
- **Size**: Typically < 20 children per line
- **Alternative**: Slice `node.Children` directly with Span/Range
  ```csharp
  var lineChildren = node.Children[line.StartIndex..(line.StartIndex + line.Count)];
  ```
- **Blocker**: `IReadOnlyList<LayoutNode>` doesn't support slicing without LINQ `.Skip().Take()` or manual loop

**Verdict**: **ACCEPTABLE AS-IS**

**Reasoning**:
1. **Not a hot path**: Wrapping is uncommon in templates (most use single-line flex)
2. **Small allocations**: < 100 items total per container (lines + children)
3. **Gen0 collection**: These allocations are short-lived, collected quickly
4. **Clarity**: `List<T>` is clearer than ArrayPool boilerplate
5. **Premature optimization**: No profiling data suggests this is a bottleneck

**Recommendation**:
- **Phase 3**: Keep as-is ✅
- **Future**: If profiling shows wrapping is >5% of render time, revisit with:
  - ArrayPool for FlexLine
  - ReadOnlySpan slicing for lineChildren (requires API refactoring)

---

### Q5: Extensibility — Ready for Phase 4 (position: absolute/relative) and Phase 5 (auto margins)?

**Phase 4: Absolute/Relative Positioning**

**Required changes**:
1. Skip `position: absolute` children in line breaking (CalculateFlexLines)
2. Position absolute children AFTER flex layout (in LayoutWrappedFlex)
3. Apply `position: relative` offsets after normal positioning

**Impact on current code**:
- ✅ **Minimal**: CalculateFlexLines already filters Display.None — same pattern for absolute
- ✅ **Additive**: Absolute positioning is post-process (no algorithm changes)

**Verdict**: **READY** ✅

---

**Phase 5: Auto Margins**

**Required changes**:
1. Parse `margin: auto` (PaddingParser extension or new MarginValue type)
2. In ResolveFlexForLine: check for auto margins BEFORE justify-content
3. If auto margins present: distribute freeSpace to auto margins, skip justify-content

**Impact on current code**:
- ✅ **Localized**: Changes in ResolveFlexForLine (justify-content section) and LayoutColumnFlex/LayoutRowFlex
- ✅ **Non-breaking**: Auto margins are opt-in feature

**Verdict**: **READY** ✅

---

## 2. Code Quality Review

### 2.1 AOT Safety

✅ **PASS**

- No `System.Reflection` usage
- No `dynamic` keyword
- No `Type.GetType()` calls
- Switch expressions for dispatch (lines 530-559)

### 2.2 Performance — Hot Paths

✅ **PASS**

- **No LINQ** in layout code (verified via grep)
- `stackalloc` for <= 32 items (lines 1437-1439) ✅
- Foreach loops (no boxing)
- Manual margin accumulation (no LINQ `.Sum()`)

### 2.3 XML Documentation

✅ **PASS**

New methods documented:
- `FlexLine` record struct (lines 1341-1348)
- `CalculateFlexLines` (lines 1350-1353)
- `ResolveFlexForLine` (lines 1420-1423)
- `LayoutWrappedFlex` (lines 1617-1622)

### 2.4 Resource Limits

✅ **PASS** — New limit added

**MaxFlexLines**: 1000 (line 1390-1391, 1412-1413)
- Protects against DoS via excessive line breaking
- Throws `InvalidOperationException` when exceeded
- **Correctly placed**: Checked BOTH during line finalization AND last line

### 2.5 Null Checks

✅ **PASS**

- `!string.IsNullOrEmpty()` checks for RowGap/ColumnGap (lines 535, 543)
- Display.None filters (lines 1370, 1430)

### 2.6 Struct Usage

✅ **PASS**

- `FlexLine`: `readonly record struct` (line 1348) — correct for small immutable data
- 4 fields (StartIndex, Count, MainAxisSize, CrossAxisSize) — fits in 2 cache lines (16 bytes)

---

## 3. New Methods Deep Dive

### 3.1 FlexLine Struct

**Location**: Line 1348

```csharp
private readonly record struct FlexLine(int StartIndex, int Count, float MainAxisSize, float CrossAxisSize);
```

**Review**:
- ✅ `readonly` — immutable ✅
- ✅ `record struct` — value semantics, no heap allocation ✅
- ✅ XML docs — complete ✅
- ✅ Fields meaningful — StartIndex/Count for slicing, MainAxisSize/CrossAxisSize for layout ✅

**Verdict**: APPROVED

---

### 3.2 CalculateFlexLines

**Location**: Lines 1354-1418 (64 lines)

**Algorithm**: Greedy line-breaking

**Key features**:
1. Uses **flex basis clamped by min/max** for line-breaking (lines 1373-1375) — matches Yoga
2. Includes **main-axis margins** (lines 1378-1382)
3. **MaxFlexLines check** (lines 1390-1391, 1412-1413) — DoS protection ✅
4. **Display.None skip** (line 1370)

**Edge cases handled**:
- ✅ Empty children → return empty list
- ✅ Single item exceeding available space → still breaks to new line (correct per CSS)
- ✅ Last line not forgotten (lines 1410-1415)

**Verdict**: APPROVED

---

### 3.3 ResolveFlexForLine

**Location**: Lines 1424-1615 (197 lines)

**Sections**:
1. Line children extraction (lines 1427-1434)
2. Margin calculation (lines 1442-1450)
3. Basis resolution (lines 1452-1460)
4. Freeze zero-factor items (lines 1468-1477)
5. **Iterative freeze loop** (lines 1479-1545) — 80% duplicated from LayoutColumnFlex ⚠️
6. Apply sizes (lines 1548-1555)
7. **Justify-content** (lines 1558-1600) — same as single-line layout
8. Main-axis positioning (lines 1602-1612)

**Duplication assessment**:
- Lines 1482-1545: **DUPLICATED** from LayoutColumnFlex (lines ~795-860)
- Lines 1558-1600 (justify-content): **DUPLICATED** from LayoutColumnFlex (lines ~882-920)

**Verdict**: **APPROVED** with note that duplication is acceptable (see Q1)

---

### 3.4 LayoutWrappedFlex

**Location**: Lines 1617-1850 (234 lines)

**Sections**:
1. **Setup** (lines 1624-1630): isColumn, availableMainSize
2. **Line breaking** (line 1633): CalculateFlexLines
3. **Per-line resolution** (lines 1637-1641): ResolveFlexForLine loop
4. **Cross-axis sizing** (lines 1644-1660)
5. **Align-content** (lines 1663-1716):
   - Overflow fallback (lines 1665-1671)
   - Distribution switch (lines 1673-1716)
6. **Cross-axis positioning** (lines 1719-1791):
   - Per-child align-items/align-self
   - Stretch handling
7. **WrapReverse** (lines 1794-1850): Position inversion

**Key features**:
- ✅ **Overflow fallback for align-content** (lines 1665-1671) — SpaceBetween/Stretch/SpaceAround/SpaceEvenly → Start
- ✅ **Per-child cross-axis alignment** (lines 1732-1760) — GetEffectiveAlign()
- ✅ **Stretch on cross-axis** (lines 1763-1768)
- ✅ **WrapReverse inversion** (lines 1794-1850) — mirrors line positions

**Verdict**: APPROVED — Well-structured, clear sections

---

## 4. Integration with Existing Code

### 4.1 Dispatch Logic (Lines 529-559)

```csharp
if (flex.Wrap != FlexWrap.NoWrap)
{
    // RowGap/ColumnGap resolution
    LayoutWrappedFlex(node, flex, innerContext, padding, mainGap, crossGap);
}
else if (isColumn)
{
    LayoutColumnFlex(node, flex, innerContext, padding, gap);
}
else
{
    LayoutRowFlex(node, flex, innerContext, padding, gap);
}
```

**Review**:
- ✅ Clean separation: Wrap vs NoWrap
- ✅ RowGap/ColumnGap only resolved for wrapping (avoids overhead for single-line)
- ✅ Existing single-line methods unchanged (backward compatible)

**Verdict**: APPROVED

---

### 4.2 Gap Resolution (Lines 533-550)

**Logic**:
- `gap` shorthand applies to both axes
- `RowGap` / `ColumnGap` override individually
- For column: RowGap=main, ColumnGap=cross
- For row: ColumnGap=main, RowGap=cross

**Review**:
- ✅ Matches CSS spec
- ✅ Null checks for RowGap/ColumnGap (lines 535, 543)
- ✅ UnitParser.Parse().Resolve() pattern (consistent)

**Verdict**: APPROVED

---

## 5. Comparison to Phase 2

| Aspect | Phase 2 | Phase 3 | Change |
|--------|---------|---------|--------|
| File size | ~1250 lines | 1907 lines | +650 lines |
| Tests | 1131 | 1151 | +20 |
| Methods | 12 | 16 | +4 (FlexLine, CalculateFlexLines, ResolveFlexForLine, LayoutWrappedFlex) |
| Duplication | Minimal (shared helpers) | ~150 lines (iterative loop + justify) | Expected for wrapping |
| Allocations | 0 per flex (stackalloc only) | 2-10 per wrapped flex (List<FlexLine> + List<LayoutNode> per line) | Acceptable |
| Complexity | Single-line algorithm | Single-line + Multi-line | Managed well |

---

## 6. Recommendations

### 6.1 Immediate (Phase 3)

✅ **No changes required** — Code is production-ready as-is.

### 6.2 Future Refactoring Opportunities (Phase 4+)

**If file size exceeds 2500 lines**:
1. Consider partial class split:
   - `LayoutEngine.cs` (core + dispatch, ~800 lines)
   - `LayoutEngine.SingleLine.cs` (LayoutColumnFlex, LayoutRowFlex, ~600 lines)
   - `LayoutEngine.MultiLine.cs` (wrapping, ~600 lines)
   - `LayoutEngine.Helpers.cs` (shared helpers, ~500 lines)

**If wrapping becomes hot path** (> 5% render time):
2. Replace `List<FlexLine>` with ArrayPool<FlexLine>
3. Replace `List<LayoutNode> lineChildren` with ReadOnlySpan slicing (requires API change)

**If duplication becomes maintenance burden**:
3. Extract iterative freeze loop into shared method:
   ```csharp
   private static void ResolveFlexSizes(
       Span<float> bases, Span<float> sizes, Span<bool> frozen,
       IReadOnlyList<LayoutNode> children, bool isColumn, ...)
   ```
   - **Pros**: Single source of truth
   - **Cons**: 8+ parameters, less readable

---

## 7. Verdict

**Status**: ✅ **APPROVED**

### Strengths

1. **Correctness**: Wrapping algorithm matches Yoga/CSS spec
2. **Structure**: Clear separation (dispatch → line breaking → per-line resolution → distribution)
3. **Resource limits**: MaxFlexLines protection added
4. **Performance**: No LINQ, stackalloc where possible, minimal allocations
5. **Extensibility**: Ready for Phase 4 (absolute/relative) and Phase 5 (auto margins)
6. **Documentation**: All new methods have XML docs

### Acceptable Trade-offs

1. **Duplication**: ~150 lines duplicated (iterative loop + justify) — Acceptable for clarity
2. **Method size**: LayoutWrappedFlex 234 lines — Acceptable with clear sections
3. **File size**: 1907 lines — Large but manageable with IDE navigation
4. **Allocations**: List<FlexLine> + List<LayoutNode> per line — Acceptable for wrapping

### No Blocking Issues

All architectural concerns addressed:
- ✅ Q1: Code duplication acceptable
- ✅ Q2: Method sizes acceptable
- ✅ Q3: Single-file structure approved
- ✅ Q4: Performance acceptable
- ✅ Q5: Ready for Phase 4 & 5

---

## 8. Sign-off

**Reviewed by**: architect
**Date**: 2026-02-06
**Approved for**: Merge to feat/flexbox-expansion
**Next phase**: Phase 4 (Absolute/Relative Positioning)

All architectural requirements met. Code is production-ready for Phase 3 scope.

**Test coverage**: 1151 passing (20 new wrap + align-content tests)

**Recommendation**: **MERGE** — Phase 3 complete and ready for production use.
