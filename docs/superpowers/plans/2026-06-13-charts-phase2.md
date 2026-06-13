# Charts (Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a declarative `chart` element (chart-types `bar` vertical/`horizontal`, `line`, `area`, `pie`, `donut`) with themes/palettes, axes (nice ticks), grid, legend, title, and a "no data" placeholder — so LLM agents can render polished charts from data arrays with zero styling decisions.

**Architecture:** A new `ChartElement` AST class in `FlexRender.Core/Parsing/Ast/` follows the Phase-1 shape pattern (leaf box; overrides `Type`, `ResolveExpressions`, `Materialize`, `CloneWithSubstitution`) and carries pre-resolved numeric series. Series-data binding resolves a `{{ expr }}` to an `ArrayValue` inside `ChartElement.ResolveExpressions` via `ExpressionEvaluator.Resolve` against the data context, converting `NumberValue` items to `double[]`. Pure, renderer-agnostic chart math (nice-tick axis scaling) and the static theme/palette tables live in a new `FlexRender.Core/Charts/` namespace, heavily unit-tested. Parsing extends `FlexRender.Yaml` (`ChartParsers.cs`, `TemplateParser` dispatch, `KnownProperties`). Layout treats the chart as a leaf box with explicit width/height (reusing `MeasureShapeIntrinsic`/`LayoutShapeElement`). A new `ChartRenderer` in `FlexRender.Skia.Render` computes the plot area (minus title/legend/axes), draws grid+axes+labels then per-type geometry then legend, dispatched from `RenderingEngine.DrawElement` via a `case ChartElement`. Label text uses SkiaSharp `SKFont`/`SKPaint` built from an `SKTypeface` obtained from `FontManager`.

**Tech Stack:** .NET 10, C# latest, xUnit, SkiaSharp, YamlDotNet. AOT-safe (no reflection, no `dynamic`, no regex), `sealed` classes, `ArgumentNullException.ThrowIfNull`, switch-based dispatch, XML docs on all public API.

---

## Conventions used throughout this plan

- All commands run from repo root `/Users/robonet/Projects/SkiaLayout`.
- Branch is already `feature/charts-and-shapes`. Do NOT create worktrees. Do NOT merge to `main`.
- Build: `dotnet build FlexRender.slnx`. Test (authoritative, net10.0): `dotnet test FlexRender.slnx --framework net10.0`. The net8.0 test host cannot launch in this environment — always pass `--framework net10.0` to test commands.
- NEVER pipe `dotnet` output through `tail`/`head`/`grep`. Run commands directly.
- Commit messages use Conventional Commits, NO attribution/Co-Authored-By lines. The signing key is missing — every commit uses `--no-gpg-sign`.
- After every code edit the build must be warning-free (`TreatWarningsAsErrors=true`).
- `ExprValue<string>` materialize quirk (from Phase 1): for a string that may hold a color OR another form (e.g. a palette word or a label), materialize WITHOUT `ValueKind.Color` so validation does not reject non-hex strings. Series `Data` is never a color, palette/title/label are never colors.

## File structure (created/modified across all tasks)

Created:
- `src/FlexRender.Core/Charts/ChartEnums.cs` (ChartType, LegendPosition, PieLabelMode)
- `src/FlexRender.Core/Charts/AxisScale.cs` (nice-tick math)
- `src/FlexRender.Core/Charts/ChartTheme.cs` + `ChartThemes.cs` (static theme data)
- `src/FlexRender.Core/Charts/ChartPalette.cs` + `ChartPalettes.cs` (static palette data)
- `src/FlexRender.Core/Parsing/Ast/ChartSeries.cs`
- `src/FlexRender.Core/Parsing/Ast/ChartElement.cs`
- `src/FlexRender.Yaml/Parsing/ChartParsers.cs`
- `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`
- Test files (see each task)

Modified:
- `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs` (add `ElementType.Chart`)
- `src/FlexRender.Core/Configuration/ResourceLimits.cs` (add `MaxSeriesPerChart`, `MaxDataPointsPerSeries`)
- `src/FlexRender.Core/Layout/IntrinsicMeasurer.cs` (add `ChartElement` to shape-intrinsic switch arm)
- `src/FlexRender.Core/Layout/LayoutEngine.cs` (add `ChartElement` to shape-layout switch arm)
- `src/FlexRender.Yaml/Parsing/TemplateParser.cs` (register `chart`)
- `src/FlexRender.Yaml/Parsing/KnownProperties.cs` (Chart property set + registry entry)
- `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs` (dispatch `ChartElement`)
- Docs: `llms.txt`, `llms-full.txt`, `docs/wiki/Element-Reference.md`, `docs/wiki/Visual-Reference.md`,
  `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`,
  `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs`

## Highest-risk tasks (flagged for extra review)

1. **Task 16 — series-data expression → array binding** (`ChartElement.ResolveExpressions`). Resolves `{{ expr }}` to an `ArrayValue` and converts to `double[]`; non-numeric items must raise a clear template error with element context. Verify against the real `ExpressionEvaluator.Resolve`/`TemplateContext` API.
2. **Task 3–7 — axis nice-tick math** (`AxisScale`). Edge cases (crossing zero, negative-only, single point, identical values, empty) must be exhaustively unit-tested; this is renderer-agnostic and must be bullet-proof before any rendering.
3. **Task 19+ — label/text rendering integration** in `ChartRenderer`. Uses raw `SKFont`/`SKPaint` from a `FontManager` typeface; must measure+draw axis/legend/title labels and degrade gracefully (skip labels) when no typeface is available.

---

## Task 1: ResourceLimits — MaxSeriesPerChart and MaxDataPointsPerSeries

**Files:**
- Modify: `src/FlexRender.Core/Configuration/ResourceLimits.cs`
- Test: `tests/FlexRender.Tests/Configuration/ResourceLimitsChartsTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Configuration/ResourceLimitsChartsTests.cs`:

```csharp
using System;
using FlexRender.Configuration;
using Xunit;

namespace FlexRender.Tests.Configuration;

/// <summary>
/// Tests for the chart-related resource limits.
/// </summary>
public sealed class ResourceLimitsChartsTests
{
    [Fact]
    public void MaxSeriesPerChart_DefaultsTo50()
    {
        var limits = new ResourceLimits();
        Assert.Equal(50, limits.MaxSeriesPerChart);
    }

    [Fact]
    public void MaxDataPointsPerSeries_DefaultsTo10000()
    {
        var limits = new ResourceLimits();
        Assert.Equal(10000, limits.MaxDataPointsPerSeries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxSeriesPerChart_RejectsNonPositive(int value)
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxSeriesPerChart = value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxDataPointsPerSeries_RejectsNonPositive(int value)
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxDataPointsPerSeries = value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ResourceLimitsChartsTests"`
Expected: BUILD FAILURE — `ResourceLimits` has no `MaxSeriesPerChart`/`MaxDataPointsPerSeries`.

- [ ] **Step 3: Add the properties**

In `src/FlexRender.Core/Configuration/ResourceLimits.cs`, add two backing fields next to `_maxShapesPerDraw`:

```csharp
    private int _maxSeriesPerChart = 50;
    private int _maxDataPointsPerSeries = 10000;
```

Then add these two properties after the `MaxShapesPerDraw` property:

```csharp
    /// <summary>
    /// Maximum number of data series allowed in a single 'chart' element.
    /// Prevents resource exhaustion from templates with an unbounded series list.
    /// </summary>
    /// <value>Default: 50.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public int MaxSeriesPerChart
    {
        get => _maxSeriesPerChart;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxSeriesPerChart = value;
        }
    }

    /// <summary>
    /// Maximum number of data points allowed in a single chart series.
    /// Prevents resource exhaustion from templates with an enormous data array.
    /// </summary>
    /// <value>Default: 10000.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public int MaxDataPointsPerSeries
    {
        get => _maxDataPointsPerSeries;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxDataPointsPerSeries = value;
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ResourceLimitsChartsTests"`
Expected: PASS (6 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Configuration/ResourceLimits.cs tests/FlexRender.Tests/Configuration/ResourceLimitsChartsTests.cs
git commit --no-gpg-sign -m "feat(core): add MaxSeriesPerChart and MaxDataPointsPerSeries limits"
```

---

## Task 2: ElementType.Chart enum member

**Files:**
- Modify: `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`

- [ ] **Step 1: Add the enum member**

In `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`, inside the `ElementType` enum, after the `Draw` member add a comma and:

```csharp
    /// <summary>
    /// A chart element (bar, line, area, pie, donut).
    /// </summary>
    Chart
```

The enum tail must read `... Draw,` then the new `Chart` member.

- [ ] **Step 2: Verify the build compiles**

Run: `dotnet build FlexRender.slnx`
Expected: BUILD SUCCEEDED (enum-only extension, no behaviour change).

- [ ] **Step 3: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/TemplateElement.cs
git commit --no-gpg-sign -m "feat(ast): add Chart element type"
```

---

## Task 3: AxisScale — nice-tick math (data-driven happy path)

This is renderer-agnostic pure math. Build it with exhaustive edge-case tests BEFORE anything draws.

**Files:**
- Create: `src/FlexRender.Core/Charts/AxisScale.cs`
- Test: `tests/FlexRender.Tests/Charts/AxisScaleTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Charts/AxisScaleTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for the renderer-agnostic nice-tick axis scaling math.
/// </summary>
public sealed class AxisScaleTests
{
    [Fact]
    public void Compute_SimplePositiveRange_ProducesNiceBounds()
    {
        var scale = AxisScale.Compute(0d, 48d, targetTicks: 5);

        Assert.Equal(0d, scale.Min);
        Assert.Equal(50d, scale.Max);
        Assert.Equal(10d, scale.Step);
        Assert.Equal(new[] { 0d, 10d, 20d, 30d, 40d, 50d }, scale.Ticks);
    }

    [Fact]
    public void Compute_RangeNotStartingAtZero_StillIncludesZeroForBars()
    {
        // Bars/area need a zero baseline: min is clamped to 0 when data is all positive.
        var scale = AxisScale.Compute(12d, 48d, targetTicks: 5);

        Assert.Equal(0d, scale.Min);
        Assert.True(scale.Max >= 48d);
    }

    [Fact]
    public void Compute_NegativeOnly_ClampsMaxToZero()
    {
        var scale = AxisScale.Compute(-80d, -10d, targetTicks: 5);

        Assert.True(scale.Min <= -80d);
        Assert.Equal(0d, scale.Max);
    }

    [Fact]
    public void Compute_CrossingZero_KeepsBothSides()
    {
        var scale = AxisScale.Compute(-30d, 70d, targetTicks: 5);

        Assert.True(scale.Min <= -30d);
        Assert.True(scale.Max >= 70d);
        Assert.Contains(0d, scale.Ticks);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~AxisScaleTests"`
Expected: BUILD FAILURE — `AxisScale` not defined.

- [ ] **Step 3: Implement AxisScale**

Create `src/FlexRender.Core/Charts/AxisScale.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FlexRender.Charts;

/// <summary>
/// A computed numeric axis scale with "nice" rounded bounds and evenly spaced tick values.
/// Renderer-agnostic; produced from raw data min/max by <see cref="AxisScale.Compute"/>.
/// </summary>
/// <param name="Min">The lower bound of the axis (a nice rounded value).</param>
/// <param name="Max">The upper bound of the axis (a nice rounded value).</param>
/// <param name="Step">The spacing between adjacent ticks.</param>
/// <param name="Ticks">The ordered tick values from <see cref="Min"/> to <see cref="Max"/> inclusive.</param>
public readonly record struct AxisScale(double Min, double Max, double Step, IReadOnlyList<double> Ticks)
{
    /// <summary>
    /// Computes a nice axis scale covering <paramref name="dataMin"/>..<paramref name="dataMax"/>.
    /// All-positive data is anchored at zero (bar/area baseline); all-negative data is anchored
    /// at zero above; data crossing zero keeps both sides and always includes a zero tick.
    /// Identical or empty inputs collapse to a unit range so a chart can still draw.
    /// </summary>
    /// <param name="dataMin">The smallest data value.</param>
    /// <param name="dataMax">The largest data value.</param>
    /// <param name="targetTicks">The desired approximate number of tick intervals (default 5).</param>
    /// <returns>The computed <see cref="AxisScale"/>.</returns>
    public static AxisScale Compute(double dataMin, double dataMax, int targetTicks = 5)
    {
        if (targetTicks < 1)
            targetTicks = 1;

        // Normalize degenerate inputs.
        if (!double.IsFinite(dataMin) || !double.IsFinite(dataMax))
        {
            dataMin = 0d;
            dataMax = 1d;
        }

        if (dataMin > dataMax)
            (dataMin, dataMax) = (dataMax, dataMin);

        // Anchor at zero so bars/areas have a baseline.
        if (dataMin > 0d)
            dataMin = 0d;
        if (dataMax < 0d)
            dataMax = 0d;

        // Identical values (e.g. single point, all-equal): expand to a unit range around the value.
        if (dataMin == dataMax)
        {
            if (dataMin == 0d)
            {
                dataMax = 1d;
            }
            else if (dataMin > 0d)
            {
                dataMin = 0d;
            }
            else
            {
                dataMax = 0d;
            }
        }

        var range = dataMax - dataMin;
        var rawStep = range / targetTicks;
        var step = NiceNumber(rawStep, round: true);
        if (step <= 0d)
            step = 1d;

        var niceMin = Math.Floor(dataMin / step) * step;
        var niceMax = Math.Ceiling(dataMax / step) * step;

        var ticks = new List<double>();
        // Use an explicit count to avoid floating-point accumulation drift.
        var count = (int)Math.Round((niceMax - niceMin) / step);
        for (var i = 0; i <= count; i++)
        {
            ticks.Add(niceMin + (i * step));
        }

        return new AxisScale(niceMin, niceMax, step, ticks);
    }

    /// <summary>
    /// Rounds a positive number to a "nice" value (1, 2, 5, or 10 times a power of ten),
    /// the standard heuristic for readable axis ticks.
    /// </summary>
    /// <param name="value">The raw value to round (must be positive).</param>
    /// <param name="round">When true, rounds to the nearest nice value; otherwise rounds up.</param>
    /// <returns>The nice number.</returns>
    private static double NiceNumber(double value, bool round)
    {
        if (value <= 0d)
            return 1d;

        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10d, exponent);

        double niceFraction;
        if (round)
        {
            niceFraction = fraction < 1.5d ? 1d
                : fraction < 3d ? 2d
                : fraction < 7d ? 5d
                : 10d;
        }
        else
        {
            niceFraction = fraction <= 1d ? 1d
                : fraction <= 2d ? 2d
                : fraction <= 5d ? 5d
                : 10d;
        }

        return niceFraction * Math.Pow(10d, exponent);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~AxisScaleTests"`
Expected: PASS (4 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Charts/AxisScale.cs tests/FlexRender.Tests/Charts/AxisScaleTests.cs
git commit --no-gpg-sign -m "feat(charts): add nice-tick axis scale math"
```

---

## Task 4: AxisScale — edge cases (single point, identical, empty)

Add the remaining edge-case coverage to the same test class so the math is bullet-proof.

**Files:**
- Modify: `tests/FlexRender.Tests/Charts/AxisScaleTests.cs`

- [ ] **Step 1: Add the failing tests**

Append these methods inside the `AxisScaleTests` class (before its closing brace):

```csharp
    [Fact]
    public void Compute_SinglePointPositive_AnchorsAtZeroWithRange()
    {
        var scale = AxisScale.Compute(42d, 42d, targetTicks: 5);

        Assert.Equal(0d, scale.Min);
        Assert.True(scale.Max >= 42d);
        Assert.True(scale.Step > 0d);
        Assert.True(scale.Ticks.Count >= 2);
    }

    [Fact]
    public void Compute_AllZeroValues_ProducesUnitRange()
    {
        var scale = AxisScale.Compute(0d, 0d, targetTicks: 5);

        Assert.Equal(0d, scale.Min);
        Assert.True(scale.Max > 0d);
        Assert.True(scale.Step > 0d);
    }

    [Fact]
    public void Compute_SinglePointNegative_AnchorsMaxAtZero()
    {
        var scale = AxisScale.Compute(-7d, -7d, targetTicks: 5);

        Assert.True(scale.Min <= -7d);
        Assert.Equal(0d, scale.Max);
    }

    [Fact]
    public void Compute_NonFiniteInputs_FallBackToUnitRange()
    {
        var scale = AxisScale.Compute(double.NaN, double.PositiveInfinity, targetTicks: 5);

        Assert.True(double.IsFinite(scale.Min));
        Assert.True(double.IsFinite(scale.Max));
        Assert.True(scale.Step > 0d);
        Assert.True(scale.Max > scale.Min);
    }

    [Fact]
    public void Compute_TicksAreMonotonicAndEvenlySpaced()
    {
        var scale = AxisScale.Compute(0d, 95d, targetTicks: 5);

        for (var i = 1; i < scale.Ticks.Count; i++)
        {
            var delta = scale.Ticks[i] - scale.Ticks[i - 1];
            Assert.True(System.Math.Abs(delta - scale.Step) < 1e-9, $"Tick spacing {delta} != step {scale.Step}");
        }
    }
```

- [ ] **Step 2: Run tests to verify they pass (implementation already handles these)**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~AxisScaleTests"`
Expected: PASS (9 cases total). If any edge case fails, fix `AxisScale.Compute` before committing.

- [ ] **Step 3: Commit**

```bash
git add tests/FlexRender.Tests/Charts/AxisScaleTests.cs
git commit --no-gpg-sign -m "test(charts): exhaustive axis-scale edge cases"
```

---

## Task 5: ChartEnums — ChartType, LegendPosition, PieLabelMode

**Files:**
- Create: `src/FlexRender.Core/Charts/ChartEnums.cs`
- Test: `tests/FlexRender.Tests/Charts/ChartEnumsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Charts/ChartEnumsTests.cs`:

```csharp
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Sanity tests for the chart enums.
/// </summary>
public sealed class ChartEnumsTests
{
    [Theory]
    [InlineData("Bar")]
    [InlineData("Line")]
    [InlineData("Area")]
    [InlineData("Pie")]
    [InlineData("Donut")]
    public void ChartType_HasAllPhase2Members(string name)
    {
        Assert.True(System.Enum.TryParse<ChartType>(name, out _));
    }

    [Theory]
    [InlineData("Top")]
    [InlineData("Bottom")]
    [InlineData("Left")]
    [InlineData("Right")]
    [InlineData("None")]
    public void LegendPosition_HasAllMembers(string name)
    {
        Assert.True(System.Enum.TryParse<LegendPosition>(name, out _));
    }

    [Theory]
    [InlineData("Percent")]
    [InlineData("Value")]
    [InlineData("None")]
    public void PieLabelMode_HasAllMembers(string name)
    {
        Assert.True(System.Enum.TryParse<PieLabelMode>(name, out _));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartEnumsTests"`
Expected: BUILD FAILURE — enums not defined.

- [ ] **Step 3: Implement ChartEnums**

Create `src/FlexRender.Core/Charts/ChartEnums.cs`:

```csharp
namespace FlexRender.Charts;

/// <summary>
/// The kind of chart to render.
/// </summary>
public enum ChartType
{
    /// <summary>Vertical (or horizontal) bar chart.</summary>
    Bar,

    /// <summary>Line chart.</summary>
    Line,

    /// <summary>Filled area chart.</summary>
    Area,

    /// <summary>Pie chart.</summary>
    Pie,

    /// <summary>Donut (ring) chart.</summary>
    Donut
}

/// <summary>
/// Where the legend is placed relative to the plot area.
/// </summary>
public enum LegendPosition
{
    /// <summary>Above the plot area.</summary>
    Top,

    /// <summary>Below the plot area.</summary>
    Bottom,

    /// <summary>Left of the plot area.</summary>
    Left,

    /// <summary>Right of the plot area.</summary>
    Right,

    /// <summary>No legend.</summary>
    None
}

/// <summary>
/// How pie/donut slice labels are rendered.
/// </summary>
public enum PieLabelMode
{
    /// <summary>Show each slice's percentage of the total.</summary>
    Percent,

    /// <summary>Show each slice's raw value.</summary>
    Value,

    /// <summary>Show no slice labels.</summary>
    None
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartEnumsTests"`
Expected: PASS (13 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Charts/ChartEnums.cs tests/FlexRender.Tests/Charts/ChartEnumsTests.cs
git commit --no-gpg-sign -m "feat(charts): add ChartType, LegendPosition, PieLabelMode enums"
```

---

## Task 6: ChartPalettes — named series-color ramps

**Files:**
- Create: `src/FlexRender.Core/Charts/ChartPalette.cs`
- Create: `src/FlexRender.Core/Charts/ChartPalettes.cs`
- Test: `tests/FlexRender.Tests/Charts/ChartPalettesTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Charts/ChartPalettesTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for named palette resolution and color cycling.
/// </summary>
public sealed class ChartPalettesTests
{
    [Theory]
    [InlineData("default")]
    [InlineData("ocean")]
    [InlineData("sunset")]
    [InlineData("forest")]
    [InlineData("mono")]
    [InlineData("vivid")]
    public void Resolve_KnownName_ReturnsNonEmptyPalette(string name)
    {
        var palette = ChartPalettes.Resolve(name);
        Assert.NotNull(palette);
        Assert.NotEmpty(palette!.Colors);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Assert.Null(ChartPalettes.Resolve("does-not-exist"));
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        Assert.NotNull(ChartPalettes.Resolve("OCEAN"));
    }

    [Fact]
    public void ColorAt_CyclesWhenIndexExceedsCount()
    {
        var palette = new ChartPalette(new[] { "#111111", "#222222" });
        Assert.Equal("#111111", palette.ColorAt(0));
        Assert.Equal("#222222", palette.ColorAt(1));
        Assert.Equal("#111111", palette.ColorAt(2));
    }

    [Fact]
    public void Default_IsNonEmpty()
    {
        Assert.NotEmpty(ChartPalettes.Default.Colors);
    }

    [Fact]
    public void Constructor_NullColors_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new ChartPalette(null!));
    }

    [Fact]
    public void Constructor_EmptyColors_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => new ChartPalette(new List<string>()));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartPalettesTests"`
Expected: BUILD FAILURE — `ChartPalette`/`ChartPalettes` not defined.

- [ ] **Step 3: Implement ChartPalette and ChartPalettes**

Create `src/FlexRender.Core/Charts/ChartPalette.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FlexRender.Charts;

/// <summary>
/// An ordered ramp of hex series colors. Colors cycle when there are more series than colors.
/// </summary>
public sealed class ChartPalette
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChartPalette"/> class.
    /// </summary>
    /// <param name="colors">The ordered hex color strings. Must be non-null and non-empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="colors"/> is empty.</exception>
    public ChartPalette(IReadOnlyList<string> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Count == 0)
            throw new ArgumentException("A palette must contain at least one color.", nameof(colors));
        Colors = colors;
    }

    /// <summary>
    /// Gets the ordered hex color strings.
    /// </summary>
    public IReadOnlyList<string> Colors { get; }

    /// <summary>
    /// Returns the color for a series index, cycling through <see cref="Colors"/> when the
    /// index exceeds the palette size.
    /// </summary>
    /// <param name="index">The zero-based series index (must be non-negative).</param>
    /// <returns>The hex color string for the series.</returns>
    public string ColorAt(int index)
    {
        var i = index % Colors.Count;
        if (i < 0)
            i += Colors.Count;
        return Colors[i];
    }
}
```

Create `src/FlexRender.Core/Charts/ChartPalettes.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FlexRender.Charts;

/// <summary>
/// Static registry of named series-color palettes (AOT-safe, no config files).
/// </summary>
public static class ChartPalettes
{
    /// <summary>The default palette used when none is specified.</summary>
    public static ChartPalette Default { get; } = new(new[]
    {
        "#4A90D9", "#E2725B", "#7FB069", "#F4C430", "#9B6DD6", "#54B8B1", "#E0719C", "#A0A0A0"
    });

    private static readonly ChartPalette Ocean = new(new[]
    {
        "#264653", "#2A9D8F", "#48BFE3", "#56CFE1", "#64DFDF", "#80FFDB"
    });

    private static readonly ChartPalette Sunset = new(new[]
    {
        "#003049", "#D62828", "#F77F00", "#FCBF49", "#EAE2B7"
    });

    private static readonly ChartPalette Forest = new(new[]
    {
        "#1B4332", "#2D6A4F", "#40916C", "#52B788", "#74C69D", "#95D5B2"
    });

    private static readonly ChartPalette Mono = new(new[]
    {
        "#222222", "#444444", "#666666", "#888888", "#AAAAAA", "#CCCCCC"
    });

    private static readonly ChartPalette Vivid = new(new[]
    {
        "#E63946", "#F1A208", "#2A9D8F", "#3A86FF", "#8338EC", "#FF006E"
    });

    private static readonly Dictionary<string, ChartPalette> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = Default,
        ["ocean"] = Ocean,
        ["sunset"] = Sunset,
        ["forest"] = Forest,
        ["mono"] = Mono,
        ["vivid"] = Vivid
    };

    /// <summary>
    /// Resolves a named palette case-insensitively.
    /// </summary>
    /// <param name="name">The palette name (e.g. "ocean").</param>
    /// <returns>The matching <see cref="ChartPalette"/>, or null when the name is unknown.</returns>
    public static ChartPalette? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return Registry.TryGetValue(name, out var palette) ? palette : null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartPalettesTests"`
Expected: PASS (12 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Charts/ChartPalette.cs src/FlexRender.Core/Charts/ChartPalettes.cs tests/FlexRender.Tests/Charts/ChartPalettesTests.cs
git commit --no-gpg-sign -m "feat(charts): add named series palettes"
```

---

## Task 7: ChartThemes — light/dark/minimal theme data

**Files:**
- Create: `src/FlexRender.Core/Charts/ChartTheme.cs`
- Create: `src/FlexRender.Core/Charts/ChartThemes.cs`
- Test: `tests/FlexRender.Tests/Charts/ChartThemesTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Charts/ChartThemesTests.cs`:

```csharp
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for named chart theme resolution.
/// </summary>
public sealed class ChartThemesTests
{
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("minimal")]
    public void Resolve_KnownName_ReturnsTheme(string name)
    {
        var theme = ChartThemes.Resolve(name);
        Assert.NotNull(theme);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Assert.Null(ChartThemes.Resolve("neon"));
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        Assert.NotNull(ChartThemes.Resolve("DARK"));
    }

    [Fact]
    public void Default_IsLight()
    {
        Assert.Same(ChartThemes.Resolve("light"), ChartThemes.Default);
    }

    [Fact]
    public void LightTheme_HasNonEmptyColors()
    {
        var theme = ChartThemes.Default;
        Assert.False(string.IsNullOrEmpty(theme.BackgroundColor));
        Assert.False(string.IsNullOrEmpty(theme.GridColor));
        Assert.False(string.IsNullOrEmpty(theme.AxisColor));
        Assert.False(string.IsNullOrEmpty(theme.LabelColor));
        Assert.True(theme.LabelSize > 0f);
        Assert.True(theme.TitleSize > 0f);
        Assert.True(theme.LineWidth > 0f);
        Assert.True(theme.BarCornerRadius >= 0f);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartThemesTests"`
Expected: BUILD FAILURE — `ChartTheme`/`ChartThemes` not defined.

- [ ] **Step 3: Implement ChartTheme and ChartThemes**

Create `src/FlexRender.Core/Charts/ChartTheme.cs`:

```csharp
namespace FlexRender.Charts;

/// <summary>
/// Visual styling preset for a chart: colors, label/title sizes, line widths and bar rounding.
/// Renderer-agnostic, immutable static data.
/// </summary>
/// <param name="BackgroundColor">The chart background fill (hex). Empty means transparent.</param>
/// <param name="GridColor">The grid-line color (hex).</param>
/// <param name="AxisColor">The axis-line color (hex).</param>
/// <param name="LabelColor">The axis/legend/slice label text color (hex).</param>
/// <param name="TitleColor">The title text color (hex).</param>
/// <param name="LabelSize">The label font size in pixels.</param>
/// <param name="TitleSize">The title font size in pixels.</param>
/// <param name="LineWidth">The series line width in pixels (line/area charts).</param>
/// <param name="BarCornerRadius">The corner radius applied to bars in pixels.</param>
public sealed record ChartTheme(
    string BackgroundColor,
    string GridColor,
    string AxisColor,
    string LabelColor,
    string TitleColor,
    float LabelSize,
    float TitleSize,
    float LineWidth,
    float BarCornerRadius);
```

Create `src/FlexRender.Core/Charts/ChartThemes.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FlexRender.Charts;

/// <summary>
/// Static registry of named chart themes (AOT-safe, no config files).
/// </summary>
public static class ChartThemes
{
    /// <summary>The light theme, used as the default.</summary>
    public static ChartTheme Default { get; } = new(
        BackgroundColor: "#FFFFFF",
        GridColor: "#E6E6E6",
        AxisColor: "#999999",
        LabelColor: "#555555",
        TitleColor: "#222222",
        LabelSize: 12f,
        TitleSize: 16f,
        LineWidth: 2.5f,
        BarCornerRadius: 3f);

    private static readonly ChartTheme Dark = new(
        BackgroundColor: "#1E1E1E",
        GridColor: "#3A3A3A",
        AxisColor: "#777777",
        LabelColor: "#CCCCCC",
        TitleColor: "#F0F0F0",
        LabelSize: 12f,
        TitleSize: 16f,
        LineWidth: 2.5f,
        BarCornerRadius: 3f);

    private static readonly ChartTheme Minimal = new(
        BackgroundColor: "#FFFFFF",
        GridColor: "#F0F0F0",
        AxisColor: "#CCCCCC",
        LabelColor: "#666666",
        TitleColor: "#333333",
        LabelSize: 11f,
        TitleSize: 15f,
        LineWidth: 2f,
        BarCornerRadius: 0f);

    private static readonly Dictionary<string, ChartTheme> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["light"] = Default,
        ["dark"] = Dark,
        ["minimal"] = Minimal
    };

    /// <summary>
    /// Resolves a named theme case-insensitively.
    /// </summary>
    /// <param name="name">The theme name (e.g. "dark").</param>
    /// <returns>The matching <see cref="ChartTheme"/>, or null when the name is unknown.</returns>
    public static ChartTheme? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return Registry.TryGetValue(name, out var theme) ? theme : null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartThemesTests"`
Expected: PASS (8 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Charts/ChartTheme.cs src/FlexRender.Core/Charts/ChartThemes.cs tests/FlexRender.Tests/Charts/ChartThemesTests.cs
git commit --no-gpg-sign -m "feat(charts): add light/dark/minimal themes"
```

---

## Task 8: ChartSeries — resolved-series DTO

The series carries a YAML-time data form (inline array OR expression string) plus the resolved numeric `double[]` filled during expression resolution.

**Files:**
- Create: `src/FlexRender.Core/Parsing/Ast/ChartSeries.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/ChartSeriesTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Ast/ChartSeriesTests.cs`:

```csharp
using System;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="ChartSeries"/> DTO.
/// </summary>
public sealed class ChartSeriesTests
{
    [Fact]
    public void InlineData_StoresLabelAndValues()
    {
        var series = ChartSeries.FromInline("2024", new[] { 12d, 30d, 22d, 48d });

        Assert.Equal("2024", series.Label);
        Assert.Null(series.DataExpression);
        Assert.Equal(new[] { 12d, 30d, 22d, 48d }, series.Data);
    }

    [Fact]
    public void Expression_StoresExpressionAndEmptyData()
    {
        var series = ChartSeries.FromExpression("Sales", "{{ sales }}");

        Assert.Equal("Sales", series.Label);
        Assert.Equal("{{ sales }}", series.DataExpression);
        Assert.Empty(series.Data);
    }

    [Fact]
    public void WithData_ReplacesDataKeepingLabel()
    {
        var series = ChartSeries.FromExpression("Sales", "{{ sales }}");
        var resolved = series.WithData(new[] { 1d, 2d, 3d });

        Assert.Equal("Sales", resolved.Label);
        Assert.Equal("{{ sales }}", resolved.DataExpression);
        Assert.Equal(new[] { 1d, 2d, 3d }, resolved.Data);
    }

    [Fact]
    public void FromInline_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ChartSeries.FromInline("x", null!));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartSeriesTests"`
Expected: BUILD FAILURE — `ChartSeries` not defined.

- [ ] **Step 3: Implement ChartSeries**

Create `src/FlexRender.Core/Parsing/Ast/ChartSeries.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A single chart data series: a label plus its numeric values. The values may come from an
/// inline YAML array (resolved at parse time) or from a template expression (resolved against
/// the data context during <see cref="ChartElement.ResolveExpressions"/>).
/// </summary>
public sealed class ChartSeries
{
    private static readonly IReadOnlyList<double> Empty = Array.Empty<double>();

    private ChartSeries(string? label, string? dataExpression, IReadOnlyList<double> data)
    {
        Label = label;
        DataExpression = dataExpression;
        Data = data;
    }

    /// <summary>Gets the optional series label shown in the legend.</summary>
    public string? Label { get; }

    /// <summary>
    /// Gets the raw template expression (e.g. "{{ sales }}") when the data is data-bound;
    /// null when the data was supplied inline.
    /// </summary>
    public string? DataExpression { get; }

    /// <summary>Gets the resolved numeric values. Empty until a bound expression is resolved.</summary>
    public IReadOnlyList<double> Data { get; }

    /// <summary>
    /// Creates a series with inline numeric data.
    /// </summary>
    /// <param name="label">The optional legend label.</param>
    /// <param name="data">The numeric values.</param>
    /// <returns>A new <see cref="ChartSeries"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    public static ChartSeries FromInline(string? label, IReadOnlyList<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ChartSeries(label, dataExpression: null, data);
    }

    /// <summary>
    /// Creates a data-bound series whose values come from a template expression.
    /// </summary>
    /// <param name="label">The optional legend label.</param>
    /// <param name="dataExpression">The raw expression string (e.g. "{{ sales }}").</param>
    /// <returns>A new <see cref="ChartSeries"/> with empty data until resolved.</returns>
    public static ChartSeries FromExpression(string? label, string dataExpression)
    {
        ArgumentNullException.ThrowIfNull(dataExpression);
        return new ChartSeries(label, dataExpression, Empty);
    }

    /// <summary>
    /// Returns a copy of this series with its <see cref="Data"/> replaced, preserving the label
    /// and expression. Used when a bound expression has been resolved to concrete values.
    /// </summary>
    /// <param name="data">The resolved numeric values.</param>
    /// <returns>A new <see cref="ChartSeries"/> with the new data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    public ChartSeries WithData(IReadOnlyList<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ChartSeries(Label, DataExpression, data);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartSeriesTests"`
Expected: PASS (4 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/ChartSeries.cs tests/FlexRender.Tests/Parsing/Ast/ChartSeriesTests.cs
git commit --no-gpg-sign -m "feat(ast): add ChartSeries DTO"
```

---

## Task 9: ChartElement AST class (structure + Type + clone)

The data-binding override of `ResolveExpressions` comes later (Task 16). This task lands the leaf-box element with all chart properties.

**Files:**
- Create: `src/FlexRender.Core/Parsing/Ast/ChartElement.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/ChartElementTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Ast/ChartElementTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="ChartElement"/> AST class.
/// </summary>
public sealed class ChartElementTests
{
    private static ChartElement MakeChart()
    {
        var series = new List<ChartSeries>
        {
            ChartSeries.FromInline("2024", new[] { 12d, 30d, 22d, 48d })
        };
        return new ChartElement(ChartType.Bar, series)
        {
            Categories = new[] { "Q1", "Q2", "Q3", "Q4" },
            Width = "600",
            Height = "300",
            Legend = LegendPosition.Bottom,
            Title = "Revenue"
        };
    }

    [Fact]
    public void Type_IsChart()
    {
        Assert.Equal(ElementType.Chart, MakeChart().Type);
    }

    [Fact]
    public void ChartType_AndSeries_AreExposed()
    {
        var chart = MakeChart();
        Assert.Equal(ChartType.Bar, chart.ChartType);
        Assert.Single(chart.Series);
        Assert.Equal(4, chart.Categories.Count);
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var chart = new ChartElement(ChartType.Line, new List<ChartSeries>());
        Assert.Empty(chart.Series);
        Assert.Empty(chart.Categories);
        Assert.Null(chart.Title);
        Assert.False(chart.Horizontal);
        Assert.False(chart.Stacked);
        Assert.False(chart.Smooth);
        Assert.False(chart.ShowPoints);
        Assert.Equal(PieLabelMode.Percent, chart.PieLabels);
    }

    [Fact]
    public void CloneWithSubstitution_PreservesChartState()
    {
        var clone = (ChartElement)MakeChart().CloneWithSubstitution(s => s);

        Assert.Equal(ChartType.Bar, clone.ChartType);
        Assert.Single(clone.Series);
        Assert.Equal("Revenue", clone.Title);
        Assert.Equal("600", clone.Width.Value);
        Assert.Equal(LegendPosition.Bottom, clone.Legend);
    }

    [Fact]
    public void Constructor_NullSeries_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new ChartElement(ChartType.Bar, null!));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartElementTests"`
Expected: BUILD FAILURE — `ChartElement` not defined.

- [ ] **Step 3: Implement ChartElement (no data-binding override yet)**

Create `src/FlexRender.Core/Parsing/Ast/ChartElement.cs`:

```csharp
using System;
using System.Collections.Generic;
using FlexRender.Charts;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A chart element. Participates in flex layout as a leaf box with explicit width/height and is
/// drawn by the renderer into that box: grid, axes, series geometry, legend, title. The visual
/// styling comes entirely from the resolved <see cref="ChartTheme"/> and <see cref="ChartPalette"/>;
/// the template only supplies data and optional theme/palette words.
/// </summary>
public sealed class ChartElement : TemplateElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChartElement"/> class.
    /// </summary>
    /// <param name="chartType">The chart type.</param>
    /// <param name="series">The data series (may be empty; an empty chart renders a "no data" placeholder).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> is null.</exception>
    public ChartElement(ChartType chartType, IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        ChartType = chartType;
        Series = series;
    }

    /// <inheritdoc/>
    public override ElementType Type => ElementType.Chart;

    /// <summary>Gets the chart type.</summary>
    public ChartType ChartType { get; private set; }

    /// <summary>Gets the data series (resolved during expression resolution).</summary>
    public IReadOnlyList<ChartSeries> Series { get; private set; }

    /// <summary>Gets or sets the category labels (x-axis categories or pie slice labels).</summary>
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the resolved palette name or explicit color list. Null uses the theme default palette.</summary>
    public ChartPalette? Palette { get; set; }

    /// <summary>Gets or sets the resolved theme. Null falls back to the template/canvas theme then light.</summary>
    public ChartTheme? Theme { get; set; }

    /// <summary>Gets or sets the legend position.</summary>
    public LegendPosition Legend { get; set; } = LegendPosition.Bottom;

    /// <summary>Gets or sets the optional chart title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets whether bars are drawn horizontally (bar charts only).</summary>
    public bool Horizontal { get; set; }

    /// <summary>Gets or sets whether bars/areas are stacked (bar charts only in this phase).</summary>
    public bool Stacked { get; set; }

    /// <summary>Gets or sets whether line/area series use smoothed curves.</summary>
    public bool Smooth { get; set; }

    /// <summary>Gets or sets whether line/area series show point markers.</summary>
    public bool ShowPoints { get; set; }

    /// <summary>Gets or sets how pie/donut slice labels are rendered.</summary>
    public PieLabelMode PieLabels { get; set; } = PieLabelMode.Percent;

    /// <inheritdoc/>
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new ChartElement(ChartType, Series)
        {
            Categories = Categories,
            Palette = Palette,
            Theme = Theme,
            Legend = Legend,
            Title = Title,
            Horizontal = Horizontal,
            Stacked = Stacked,
            Smooth = Smooth,
            ShowPoints = ShowPoints,
            PieLabels = PieLabels
        };
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }

    /// <summary>
    /// Replaces the series collection. Used by expression resolution to install resolved data.
    /// </summary>
    /// <param name="series">The resolved series.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> is null.</exception>
    internal void SetSeries(IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        Series = series;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartElementTests"`
Expected: PASS (5 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/ChartElement.cs tests/FlexRender.Tests/Parsing/Ast/ChartElementTests.cs
git commit --no-gpg-sign -m "feat(ast): add ChartElement"
```

---

## Task 10: Layout — chart leaf box

Add `ChartElement` to the existing shape-intrinsic and shape-layout switch arms (same treatment as rect/draw).

**Files:**
- Modify: `src/FlexRender.Core/Layout/IntrinsicMeasurer.cs:64` (the shape arm group)
- Modify: `src/FlexRender.Core/Layout/LayoutEngine.cs:219` (the shape arm group)
- Test: `tests/FlexRender.Tests/Layout/ChartLayoutTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Layout/ChartLayoutTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Layout tests for the chart leaf element.
/// </summary>
public sealed class ChartLayoutTests
{
    [Fact]
    public void Chart_WithExplicitSize_ProducesThatSize()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 1d, 2d, 3d })
        })
        {
            Width = "600",
            Height = "300"
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 800, Fixed = FixedDimension.Width }
        };
        template.AddElement(chart);

        var engine = new LayoutEngine(new ResourceLimits());
        var root = engine.ComputeLayout(template);
        var node = root.Children[0];

        Assert.Equal(600f, node.Width);
        Assert.Equal(300f, node.Height);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartLayoutTests"`
Expected: FAIL — chart is not in the layout switch, so size is not honoured (node width/height differ).

- [ ] **Step 3: Add ChartElement to both switch arms**

In `src/FlexRender.Core/Layout/IntrinsicMeasurer.cs`, in the `MeasureIntrinsic` switch (currently lines 60-64), add after the `DrawElement draw => MeasureShapeIntrinsic(draw),` arm:

```csharp
            ChartElement chart => MeasureShapeIntrinsic(chart),
```

In `src/FlexRender.Core/Layout/LayoutEngine.cs`, in the layout dispatch switch (currently lines 215-219), add after the `DrawElement draw => LayoutShapeElement(draw, context),` arm:

```csharp
            ChartElement chart => LayoutShapeElement(chart, context),
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartLayoutTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Layout/IntrinsicMeasurer.cs src/FlexRender.Core/Layout/LayoutEngine.cs tests/FlexRender.Tests/Layout/ChartLayoutTests.cs
git commit --no-gpg-sign -m "feat(layout): treat chart as a leaf box"
```

---

## Task 11: ChartParsers — parse inline series + basic props

Parse the chart element; series data may be an inline array or an expression. This task handles parsing into the AST (data-binding resolution is Task 16; for inline arrays the data is already concrete here).

**Files:**
- Create: `src/FlexRender.Yaml/Parsing/ChartParsers.cs`
- Modify: `src/FlexRender.Yaml/Parsing/TemplateParser.cs:71` (register `chart`)
- Test: `tests/FlexRender.Tests/Parsing/ChartParsersTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/ChartParsersTests.cs`:

```csharp
using FlexRender.Charts;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing the chart element from YAML.
/// </summary>
public sealed class ChartParsersTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_BarChartWithInlineSeries_ProducesChartElement()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                categories: [Q1, Q2, Q3, Q4]
                series:
                  - label: "2024"
                    data: [12, 30, 22, 48]
                palette: ocean
                legend: bottom
                title: Revenue
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal(ChartType.Bar, chart.ChartType);
        Assert.Equal(new[] { "Q1", "Q2", "Q3", "Q4" }, chart.Categories);
        Assert.Single(chart.Series);
        Assert.Equal("2024", chart.Series[0].Label);
        Assert.Equal(new[] { 12d, 30d, 22d, 48d }, chart.Series[0].Data);
        Assert.NotNull(chart.Palette);
        Assert.Equal(LegendPosition.Bottom, chart.Legend);
        Assert.Equal("Revenue", chart.Title);
    }

    [Fact]
    public void Parse_SeriesWithExpression_StoresExpressionNotData()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: line
                width: 600
                height: 300
                series:
                  - label: Sales
                    data: "{{ sales }}"
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.Equal("{{ sales }}", chart.Series[0].DataExpression);
        Assert.Empty(chart.Series[0].Data);
    }

    [Fact]
    public void Parse_ExplicitColorListPalette_IsAccepted()
    {
        const string yaml = """
            canvas:
              width: 400
            layout:
              - type: chart
                chart-type: pie
                width: 400
                height: 400
                categories: [A, B, C]
                series:
                  - data: [10, 20, 30]
                palette: ["#264653", "#2a9d8f", "#e9c46a"]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.NotNull(chart.Palette);
        Assert.Equal("#264653", chart.Palette!.ColorAt(0));
    }

    [Fact]
    public void Parse_BarTypeSpecificProps_AreApplied()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                horizontal: true
                stacked: true
                series:
                  - data: [1, 2, 3]
            """;

        var template = _parser.Parse(yaml);
        var chart = Assert.IsType<ChartElement>(template.Elements[0]);

        Assert.True(chart.Horizontal);
        Assert.True(chart.Stacked);
    }

    [Fact]
    public void Parse_UnknownChartType_Throws()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: spider
                width: 600
                height: 300
                series:
                  - data: [1, 2]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("chart-type", ex.Message);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartParsersTests"`
Expected: BUILD FAILURE — `ChartParsers` not defined and `chart` not registered (note: the unknown-property validation for `chart` is added in Task 13; for now `chart-type`, `series` etc. must parse).

- [ ] **Step 3: Implement ChartParsers**

Create `src/FlexRender.Yaml/Parsing/ChartParsers.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using YamlDotNet.RepresentationModel;
using static FlexRender.Parsing.YamlPropertyHelpers;

namespace FlexRender.Parsing;

/// <summary>
/// Provides static helpers for parsing the <c>chart</c> element from YAML.
/// </summary>
public static class ChartParsers
{
    /// <summary>
    /// Parses a <c>chart</c> element.
    /// </summary>
    /// <param name="node">The YAML node containing the chart definition.</param>
    /// <param name="maxSeries">The maximum number of series allowed.</param>
    /// <param name="maxDataPoints">The maximum number of data points per series.</param>
    /// <returns>The parsed <see cref="ChartElement"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown on an unknown chart-type, malformed series, or exceeded limits.</exception>
    internal static TemplateElement ParseChartElement(YamlMappingNode node, int maxSeries, int maxDataPoints)
    {
        ArgumentNullException.ThrowIfNull(node);

        var chartType = ParseChartType(node);
        var series = ParseSeries(node, maxSeries, maxDataPoints);

        var chart = new ChartElement(chartType, series)
        {
            Categories = ParseCategories(node),
            Palette = ParsePalette(node),
            Theme = ParseTheme(node),
            Legend = ParseLegend(node),
            Title = GetStringValue(node, "title"),
            Horizontal = GetBoolValue(node, "horizontal", false),
            Stacked = GetBoolValue(node, "stacked", false),
            Smooth = GetBoolValue(node, "smooth", false),
            ShowPoints = GetBoolValue(node, "points", false),
            PieLabels = ParsePieLabels(node),
            Background = GetStringValue(node, "background")!,
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, chart);
        return chart;
    }

    private static ChartType ParseChartType(YamlMappingNode node)
    {
        var raw = GetStringValue(node, "chart-type", "bar");
        if (!System.Enum.TryParse<ChartType>(raw, ignoreCase: true, out var chartType))
        {
            throw new TemplateParseException(
                $"Unknown chart-type '{raw}'. Valid values: bar, line, area, pie, donut.");
        }
        return chartType;
    }

    private static LegendPosition ParseLegend(YamlMappingNode node)
    {
        var raw = GetStringValue(node, "legend", "bottom");
        if (!System.Enum.TryParse<LegendPosition>(raw, ignoreCase: true, out var legend))
        {
            throw new TemplateParseException(
                $"Unknown legend position '{raw}'. Valid values: top, bottom, left, right, none.");
        }
        return legend;
    }

    private static PieLabelMode ParsePieLabels(YamlMappingNode node)
    {
        var raw = GetStringValue(node, "labels", "percent");
        if (!System.Enum.TryParse<PieLabelMode>(raw, ignoreCase: true, out var mode))
        {
            throw new TemplateParseException(
                $"Unknown labels mode '{raw}'. Valid values: percent, value, none.");
        }
        return mode;
    }

    private static IReadOnlyList<string> ParseCategories(YamlMappingNode node)
    {
        var categories = new List<string>();
        if (TryGetSequence(node, "categories", out var seq))
        {
            foreach (var item in seq.Children)
            {
                if (item is YamlScalarNode scalar && scalar.Value is not null)
                    categories.Add(scalar.Value);
            }
        }
        return categories;
    }

    private static ChartPalette? ParsePalette(YamlMappingNode node)
    {
        // Explicit color list form.
        if (TryGetSequence(node, "palette", out var seq))
        {
            var colors = new List<string>();
            foreach (var item in seq.Children)
            {
                if (item is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
                    colors.Add(scalar.Value.Trim());
            }
            if (colors.Count == 0)
                throw new TemplateParseException("A 'palette' color list must contain at least one color.");
            return new ChartPalette(colors);
        }

        // Named palette form.
        var name = GetStringValue(node, "palette");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var palette = ChartPalettes.Resolve(name);
        if (palette is null)
            throw new TemplateParseException(
                $"Unknown palette '{name}'. Valid names: default, ocean, sunset, forest, mono, vivid (or an explicit color list).");
        return palette;
    }

    private static ChartTheme? ParseTheme(YamlMappingNode node)
    {
        var name = GetStringValue(node, "theme");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var theme = ChartThemes.Resolve(name);
        if (theme is null)
            throw new TemplateParseException(
                $"Unknown chart theme '{name}'. Valid names: light, dark, minimal.");
        return theme;
    }

    private static IReadOnlyList<ChartSeries> ParseSeries(YamlMappingNode node, int maxSeries, int maxDataPoints)
    {
        var result = new List<ChartSeries>();

        if (!TryGetSequence(node, "series", out var seriesSeq))
            return result;

        if (seriesSeq.Children.Count > maxSeries)
        {
            throw new TemplateParseException(
                $"Chart has {seriesSeq.Children.Count} series, which exceeds the maximum of {maxSeries}.");
        }

        foreach (var item in seriesSeq.Children)
        {
            if (item is not YamlMappingNode seriesNode)
                throw new TemplateParseException("Each entry in 'series' must be a mapping with a 'data' field.");

            result.Add(ParseOneSeries(seriesNode, maxDataPoints));
        }

        return result;
    }

    private static ChartSeries ParseOneSeries(YamlMappingNode seriesNode, int maxDataPoints)
    {
        var label = GetStringValue(seriesNode, "label");

        // Inline array form.
        if (TryGetSequence(seriesNode, "data", out var dataSeq))
        {
            if (dataSeq.Children.Count > maxDataPoints)
            {
                throw new TemplateParseException(
                    $"Series '{label ?? "(unlabeled)"}' has {dataSeq.Children.Count} data points, which exceeds the maximum of {maxDataPoints}.");
            }

            var values = new List<double>(dataSeq.Children.Count);
            foreach (var v in dataSeq.Children)
            {
                if (v is not YamlScalarNode scalar
                    || !double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                    || !double.IsFinite(d))
                {
                    throw new TemplateParseException(
                        $"Series '{label ?? "(unlabeled)"}' contains a non-numeric data value '{(v as YamlScalarNode)?.Value}'.");
                }
                values.Add(d);
            }
            return ChartSeries.FromInline(label, values);
        }

        // Expression form (scalar string containing {{ }}).
        var expr = GetStringValue(seriesNode, "data");
        if (!string.IsNullOrWhiteSpace(expr))
            return ChartSeries.FromExpression(label, expr);

        // No data at all → empty inline series (renders as "no data" if all series empty).
        return ChartSeries.FromInline(label, System.Array.Empty<double>());
    }
}
```

- [ ] **Step 4: Register `chart` in TemplateParser**

In `src/FlexRender.Yaml/Parsing/TemplateParser.cs`, in the `_elementParsers` dictionary initializer (the block ending at line 71 with the `draw` entry), add after the `["draw"] = ...` entry:

```csharp
            ["chart"] = node => ChartParsers.ParseChartElement(node, _limits.MaxSeriesPerChart, _limits.MaxDataPointsPerSeries)
```

(Ensure the preceding `draw` line ends with a comma.)

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartParsersTests"`
Expected: PASS (5 cases).

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Yaml/Parsing/ChartParsers.cs src/FlexRender.Yaml/Parsing/TemplateParser.cs tests/FlexRender.Tests/Parsing/ChartParsersTests.cs
git commit --no-gpg-sign -m "feat(parser): parse chart element with inline series, palettes, themes"
```

---

## Task 12: KnownProperties — chart property set + typo suggestions

**Files:**
- Modify: `src/FlexRender.Yaml/Parsing/KnownProperties.cs` (add `Chart` set + registry entry)
- Test: `tests/FlexRender.Tests/Parsing/ChartKnownPropertiesTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/ChartKnownPropertiesTests.cs`:

```csharp
using FlexRender.Parsing;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for chart property validation and typo suggestions.
/// </summary>
public sealed class ChartKnownPropertiesTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_TypoInChartType_SuggestsCorrection()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-typ: bar
                width: 600
                height: 300
                series:
                  - data: [1, 2]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("chart-type", ex.Message);
    }

    [Fact]
    public void Parse_UnknownChartProperty_Throws()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                bogus: 1
                series:
                  - data: [1, 2]
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("bogus", ex.Message);
    }

    [Fact]
    public void Parse_AllKnownChartProps_DoesNotThrow()
    {
        const string yaml = """
            canvas:
              width: 600
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 300
                categories: [A, B]
                series:
                  - label: x
                    data: [1, 2]
                palette: ocean
                theme: dark
                legend: top
                title: T
                horizontal: true
                stacked: true
                smooth: false
                points: false
                labels: percent
            """;

        var template = _parser.Parse(yaml);
        Assert.NotNull(template);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartKnownPropertiesTests"`
Expected: FAIL — `chart` is not in the `Registry`, so validation is skipped and the typo/unknown-property tests do not throw.

- [ ] **Step 3: Add the Chart property set + registry entry**

In `src/FlexRender.Yaml/Parsing/KnownProperties.cs`, after the `Draw` set (around line 194) add:

```csharp
    /// <summary>
    /// Known properties for the 'chart' element type.
    /// </summary>
    internal static readonly HashSet<string> Chart = BuildSet(FlexItemProperties,
    [
        "chart-type", "categories", "series", "palette", "theme", "legend", "title",
        "horizontal", "stacked", "smooth", "points", "labels",
        "background", "rotate", "padding", "margin"
    ]);
```

In the `Registry` dictionary initializer (around lines 199-217), add after the `["draw"] = Draw` entry (add a comma to that line):

```csharp
            ["chart"] = Chart
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartKnownPropertiesTests"`
Expected: PASS (3 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Yaml/Parsing/KnownProperties.cs tests/FlexRender.Tests/Parsing/ChartKnownPropertiesTests.cs
git commit --no-gpg-sign -m "feat(parser): register chart known properties with typo suggestions"
```

---

## Task 13: ChartElement — series-data expression → array binding (HIGH RISK)

Override `ResolveExpressions` so that data-bound series resolve their `{{ expr }}` to a numeric `double[]` against the data context. Inline series pass through unchanged. Non-numeric values raise a clear template error.

**Files:**
- Modify: `src/FlexRender.Core/Parsing/Ast/ChartElement.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/ChartElementDataBindingTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Ast/ChartElementDataBindingTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for resolving data-bound chart series against the data context.
/// </summary>
public sealed class ChartElementDataBindingTests
{
    private static string PassthroughResolver(string raw, ObjectValue data) => raw;

    [Fact]
    public void ResolveExpressions_BoundSeries_ResolvesArrayFromContext()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromExpression("Sales", "{{ sales }}")
        });

        var data = new ObjectValue
        {
            ["sales"] = new ArrayValue(new TemplateValue[]
            {
                new NumberValue(12m), new NumberValue(30m), new NumberValue(22m)
            })
        };

        chart.ResolveExpressions(PassthroughResolver, data);

        Assert.Equal(new[] { 12d, 30d, 22d }, chart.Series[0].Data);
    }

    [Fact]
    public void ResolveExpressions_InlineSeries_Unchanged()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 1d, 2d, 3d })
        });

        chart.ResolveExpressions(PassthroughResolver, new ObjectValue());

        Assert.Equal(new[] { 1d, 2d, 3d }, chart.Series[0].Data);
    }

    [Fact]
    public void ResolveExpressions_MissingPath_ResolvesToEmptyData()
    {
        var chart = new ChartElement(ChartType.Line, new List<ChartSeries>
        {
            ChartSeries.FromExpression("x", "{{ nothere }}")
        });

        chart.ResolveExpressions(PassthroughResolver, new ObjectValue());

        Assert.Empty(chart.Series[0].Data);
    }

    [Fact]
    public void ResolveExpressions_NonNumericArrayItem_Throws()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromExpression("Sales", "{{ sales }}")
        });

        var data = new ObjectValue
        {
            ["sales"] = new ArrayValue(new TemplateValue[]
            {
                new NumberValue(12m), new StringValue("oops")
            })
        };

        var ex = Assert.Throws<TemplateEngineException>(() => chart.ResolveExpressions(PassthroughResolver, data));
        Assert.Contains("Sales", ex.Message);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartElementDataBindingTests"`
Expected: FAIL — base `ResolveExpressions` does not resolve series data (bound series remain empty).

- [ ] **Step 3: Add the data-binding override**

In `src/FlexRender.Core/Parsing/Ast/ChartElement.cs`, add these usings at the top (after the existing ones):

```csharp
using FlexRender.TemplateEngine;
```

Then add this override inside the class (after `CloneWithSubstitution`):

```csharp
    /// <inheritdoc/>
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        ArgumentNullException.ThrowIfNull(data);

        var anyBound = false;
        foreach (var s in Series)
        {
            if (s.DataExpression is not null)
            {
                anyBound = true;
                break;
            }
        }

        if (!anyBound)
            return;

        var context = new TemplateContext(data);
        var resolved = new List<ChartSeries>(Series.Count);

        foreach (var s in Series)
        {
            if (s.DataExpression is null)
            {
                resolved.Add(s);
                continue;
            }

            var path = StripBraces(s.DataExpression);
            var value = ExpressionEvaluator.Resolve(path, context);
            resolved.Add(s.WithData(ConvertToDoubles(value, s.Label)));
        }

        SetSeries(resolved);
    }

    /// <summary>
    /// Removes surrounding <c>{{ }}</c> braces and whitespace from a data expression, yielding the
    /// inner path for <see cref="ExpressionEvaluator.Resolve"/>. Non-wrapped input is returned trimmed.
    /// </summary>
    /// <param name="expression">The raw expression (e.g. "{{ sales }}").</param>
    /// <returns>The inner path (e.g. "sales").</returns>
    private static string StripBraces(string expression)
    {
        var span = expression.AsSpan().Trim();
        if (span.StartsWith("{{") && span.EndsWith("}}"))
        {
            span = span[2..^2].Trim();
        }
        return span.ToString();
    }

    /// <summary>
    /// Converts a resolved <see cref="ArrayValue"/> of numbers to a double array.
    /// A non-array (e.g. missing path resolving to null) yields an empty array; a non-numeric
    /// element raises a clear template error naming the series.
    /// </summary>
    /// <param name="value">The resolved template value.</param>
    /// <param name="label">The series label, for error messages.</param>
    /// <returns>The numeric values (possibly empty).</returns>
    /// <exception cref="TemplateEngineException">Thrown when an array element is not numeric.</exception>
    private static IReadOnlyList<double> ConvertToDoubles(TemplateValue value, string? label)
    {
        if (value is not ArrayValue array)
            return Array.Empty<double>();

        var result = new double[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is NumberValue number)
            {
                result[i] = (double)number.Value;
            }
            else
            {
                throw new TemplateEngineException(
                    $"Chart series '{label ?? "(unlabeled)"}' data element at index {i} is not numeric " +
                    $"(got {array[i].GetType().Name}). Series data must resolve to an array of numbers.");
            }
        }
        return result;
    }
```

Note: confirm `ArrayValue` exposes an indexer (`this[int]`) and `Count` — verified in `src/FlexRender.Core/Values/ArrayValue.cs`. `NumberValue.Value` is a `decimal`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartElementDataBindingTests"`
Expected: PASS (4 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/ChartElement.cs tests/FlexRender.Tests/Parsing/Ast/ChartElementDataBindingTests.cs
git commit --no-gpg-sign -m "feat(charts): resolve data-bound series to numeric arrays"
```

---

## Task 14: ChartLayout — pure plot-area computation (HIGH RISK helper)

Before drawing, compute the inner plot rectangle by subtracting space for title, legend, and axis labels. Pure math, renderer-agnostic, unit-tested.

**Files:**
- Create: `src/FlexRender.Core/Charts/ChartLayout.cs`
- Test: `tests/FlexRender.Tests/Charts/ChartLayoutMathTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Charts/ChartLayoutMathTests.cs`:

```csharp
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for the renderer-agnostic plot-area subdivision math.
/// </summary>
public sealed class ChartLayoutMathTests
{
    [Fact]
    public void ComputePlotArea_NoTitleNoLegend_LeavesAxisGutter()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 600f, height: 300f,
            hasTitle: false, legend: LegendPosition.None,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.Equal(40f, plot.Left);
        Assert.Equal(0f, plot.Top);
        Assert.Equal(600f - 40f, plot.Right);
        Assert.Equal(300f - 24f, plot.Bottom);
    }

    [Fact]
    public void ComputePlotArea_WithTitle_ReservesTopBand()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 600f, height: 300f,
            hasTitle: true, legend: LegendPosition.None,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.Equal(24f, plot.Top);
    }

    [Fact]
    public void ComputePlotArea_BottomLegend_ReservesBottomBand()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 600f, height: 300f,
            hasTitle: false, legend: LegendPosition.Bottom,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.Equal(300f - 24f - 60f, plot.Bottom);
    }

    [Fact]
    public void ComputePlotArea_RightLegend_ReservesRightBand()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 600f, height: 300f,
            hasTitle: false, legend: LegendPosition.Right,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.Equal(600f - 40f - 60f, plot.Right);
    }

    [Fact]
    public void ComputePlotArea_DegenerateSize_DoesNotInvert()
    {
        var plot = ChartLayout.ComputePlotArea(
            width: 30f, height: 20f,
            hasTitle: true, legend: LegendPosition.Bottom,
            axisGutterLeft: 40f, axisGutterBottom: 24f, titleHeight: 24f, legendExtent: 60f);

        Assert.True(plot.Right >= plot.Left);
        Assert.True(plot.Bottom >= plot.Top);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartLayoutMathTests"`
Expected: BUILD FAILURE — `ChartLayout` / `PlotArea` not defined.

- [ ] **Step 3: Implement ChartLayout**

Create `src/FlexRender.Core/Charts/ChartLayout.cs`:

```csharp
using System;

namespace FlexRender.Charts;

/// <summary>
/// An axis-aligned plot rectangle in chart-local coordinates (origin at the chart box top-left).
/// </summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
public readonly record struct PlotArea(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Gets the plot width.</summary>
    public float Width => Right - Left;

    /// <summary>Gets the plot height.</summary>
    public float Height => Bottom - Top;
}

/// <summary>
/// Pure, renderer-agnostic computation of the chart plot area by subtracting reserved bands
/// for the title, legend, and axis label gutters.
/// </summary>
public static class ChartLayout
{
    /// <summary>
    /// Computes the inner plot rectangle.
    /// </summary>
    /// <param name="width">The chart box width.</param>
    /// <param name="height">The chart box height.</param>
    /// <param name="hasTitle">Whether a title band is reserved at the top.</param>
    /// <param name="legend">The legend position (reserves a band on the corresponding side).</param>
    /// <param name="axisGutterLeft">The left gutter reserved for y-axis labels.</param>
    /// <param name="axisGutterBottom">The bottom gutter reserved for x-axis labels.</param>
    /// <param name="titleHeight">The title band height when <paramref name="hasTitle"/> is true.</param>
    /// <param name="legendExtent">The legend band size (height for top/bottom, width for left/right).</param>
    /// <returns>The computed <see cref="PlotArea"/>, never inverted.</returns>
    public static PlotArea ComputePlotArea(
        float width,
        float height,
        bool hasTitle,
        LegendPosition legend,
        float axisGutterLeft,
        float axisGutterBottom,
        float titleHeight,
        float legendExtent)
    {
        var left = axisGutterLeft;
        var top = hasTitle ? titleHeight : 0f;
        var right = width;
        var bottom = height - axisGutterBottom;

        switch (legend)
        {
            case LegendPosition.Top:
                top += legendExtent;
                break;
            case LegendPosition.Bottom:
                bottom -= legendExtent;
                break;
            case LegendPosition.Left:
                left += legendExtent;
                break;
            case LegendPosition.Right:
                right -= legendExtent;
                break;
            case LegendPosition.None:
            default:
                break;
        }

        // Guard against inversion on tiny boxes.
        right = Math.Max(right, left);
        bottom = Math.Max(bottom, top);

        return new PlotArea(left, top, right, bottom);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartLayoutMathTests"`
Expected: PASS (5 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Charts/ChartLayout.cs tests/FlexRender.Tests/Charts/ChartLayoutMathTests.cs
git commit --no-gpg-sign -m "feat(charts): add plot-area subdivision math"
```

---

## Task 15: ChartRenderer skeleton — dispatch + background + "no data" placeholder

Establish the renderer entry point, wire it into `RenderingEngine`, and make an empty chart draw the "no data" placeholder. Series geometry comes in later tasks. Text is drawn with an `SKFont` built from a `FontManager` typeface (passed in by the engine), degrading to no labels when null.

**Files:**
- Create: `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`
- Modify: `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs` (add `case ChartElement` near line 349)
- Test: `tests/FlexRender.Tests/Rendering/ChartRenderSmokeTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Rendering/ChartRenderSmokeTests.cs`:

```csharp
using System;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Smoke tests verifying charts are drawn (not merely laid out) by the Skia pipeline.
/// </summary>
public sealed class ChartRenderSmokeTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly TemplateParser _parser = new();

    public void Dispose()
    {
        _renderer.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EmptyChart_DrawsNoDataPlaceholderNotBlank()
    {
        const string yaml = """
            canvas:
              width: 200
              height: 120
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bar
                width: 200
                height: 120
                series: []
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected the 'no data' placeholder to draw something.");
    }

    [Fact]
    public async Task BarChart_WithData_DrawsColoredPixels()
    {
        const string yaml = """
            canvas:
              width: 300
              height: 200
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bar
                width: 300
                height: 200
                categories: [A, B, C]
                series:
                  - data: [10, 20, 15]
                legend: none
            """;

        var template = _parser.Parse(yaml);
        using var bitmap = await Render(template, new ObjectValue());

        Assert.True(HasNonBackgroundPixel(bitmap), "Expected the bar chart to draw something.");
    }

    private static bool HasNonBackgroundPixel(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y += 3)
        for (var x = 0; x < bitmap.Width; x += 3)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red < 240 || p.Green < 240 || p.Blue < 240)
                return true;
        }
        return false;
    }

    private async Task<SKBitmap> Render(Template template, ObjectValue data)
    {
        var size = await _renderer.MeasureAsync(template, data);
        var width = Math.Max((int)Math.Ceiling(size.Width), 1);
        var height = Math.Max((int)Math.Ceiling(size.Height), 1);
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        await _renderer.Render(bitmap, template, data, default, default);
        return bitmap;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartRenderSmokeTests"`
Expected: FAIL — chart is not dispatched in `RenderingEngine`, so nothing draws (both tests fail on the blank-bitmap assertion). `BarChart_WithData` may also fail until Task 17.

- [ ] **Step 3: Implement ChartRenderer skeleton**

Create `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`:

```csharp
using System;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Draws <see cref="ChartElement"/> instances to a SkiaSharp canvas. Computes the plot area
/// (minus title/legend/axis gutters), then draws grid, axes, series geometry, and legend using
/// the resolved theme and palette. Label text uses an <see cref="SKTypeface"/> supplied by the
/// caller; when null, labels are skipped but geometry still draws.
/// </summary>
internal static class ChartRenderer
{
    /// <summary>
    /// Draws a chart into the given box.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="chart">The chart element.</param>
    /// <param name="x">The chart box left edge.</param>
    /// <param name="y">The chart box top edge.</param>
    /// <param name="width">The chart box width.</param>
    /// <param name="height">The chart box height.</param>
    /// <param name="typeface">The typeface for labels, or null to skip labels.</param>
    /// <param name="antialias">Whether to anti-alias drawing.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvas"/> or <paramref name="chart"/> is null.</exception>
    internal static void Draw(
        SKCanvas canvas,
        ChartElement chart,
        float x,
        float y,
        float width,
        float height,
        SKTypeface? typeface,
        bool antialias)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(chart);

        if (width <= 0f || height <= 0f)
            return;

        var theme = chart.Theme ?? ChartThemes.Default;

        canvas.Save();
        try
        {
            canvas.ClipRect(new SKRect(x, y, x + width, y + height));
            canvas.Translate(x, y);

            DrawChartBackground(canvas, theme, width, height, antialias);

            if (!HasAnyData(chart))
            {
                DrawNoData(canvas, theme, width, height, typeface, antialias);
                return;
            }

            // Series geometry is added in subsequent tasks (bar/line/area/pie/donut).
            DrawSeries(canvas, chart, theme, width, height, typeface, antialias);
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>Returns whether any series has at least one data point.</summary>
    private static bool HasAnyData(ChartElement chart)
    {
        foreach (var s in chart.Series)
        {
            if (s.Data.Count > 0)
                return true;
        }
        return false;
    }

    /// <summary>Fills the chart background using the theme color (skipped when transparent).</summary>
    private static void DrawChartBackground(SKCanvas canvas, ChartTheme theme, float width, float height, bool antialias)
    {
        if (string.IsNullOrEmpty(theme.BackgroundColor))
            return;

        using var paint = new SKPaint
        {
            Color = ColorParser.Parse(theme.BackgroundColor),
            Style = SKPaintStyle.Fill,
            IsAntialias = antialias
        };
        canvas.DrawRect(0f, 0f, width, height, paint);
    }

    /// <summary>
    /// Draws a centred "No data" placeholder: a light dashed border plus centered text
    /// (or just the border when no typeface is available).
    /// </summary>
    private static void DrawNoData(SKCanvas canvas, ChartTheme theme, float width, float height, SKTypeface? typeface, bool antialias)
    {
        var inset = MathF.Min(width, height) * 0.08f;
        var rect = new SKRect(inset, inset, width - inset, height - inset);

        using var border = new SKPaint
        {
            Color = ColorParser.Parse(theme.AxisColor),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = antialias,
            PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0f)
        };
        canvas.DrawRect(rect, border);

        if (typeface is null)
            return;

        const string message = "No data";
        using var font = new SKFont(typeface, theme.LabelSize);
        using var textPaint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };
        var textWidth = font.MeasureText(message);
        var tx = (width - textWidth) / 2f;
        var ty = (height + theme.LabelSize) / 2f;
        canvas.DrawText(message, tx, ty, SKTextAlign.Left, font, textPaint);
    }

    /// <summary>
    /// Draws series geometry by chart type. Phase-2 bar geometry is added in Task 17; this
    /// placeholder keeps the dispatch surface in place so smoke tests can run.
    /// </summary>
    private static void DrawSeries(
        SKCanvas canvas,
        ChartElement chart,
        ChartTheme theme,
        float width,
        float height,
        SKTypeface? typeface,
        bool antialias)
    {
        // Filled in by subsequent tasks. Until then, draw a faint plot border so a chart with
        // data is visibly non-blank in smoke tests.
        using var border = new SKPaint
        {
            Color = ColorParser.Parse(theme.AxisColor),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = antialias
        };
        canvas.DrawRect(0.5f, 0.5f, width - 1f, height - 1f, border);
    }
}
```

- [ ] **Step 4: Dispatch from RenderingEngine**

In `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs`, in the `switch (element)` inside `DrawElement`, add this arm after the `case DrawElement drawEl:` block (around line 349):

```csharp
            case ChartElement chart:
                ChartRenderer.Draw(
                    canvas, chart, x, y, width, height,
                    _fontManager?.GetTypeface("main"),
                    renderOptions.Antialiasing);
                break;
```

Note: `_fontManager` is a nullable field on `RenderingEngine`; `GetTypeface("main")` returns the registered chart label font (the snapshot harness registers "main"). When `_fontManager` is null, labels are skipped.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartRenderSmokeTests"`
Expected: PASS (2 cases) — both the placeholder and the data chart draw non-background pixels.

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs tests/FlexRender.Tests/Rendering/ChartRenderSmokeTests.cs
git commit --no-gpg-sign -m "feat(renderer): dispatch chart element and draw no-data placeholder"
```

---

## Task 16: ChartAxis helper — map data values to pixel coordinates

A small pure helper to map a data value to a pixel Y within the plot, used by bar/line/area. Unit-tested so the rendering tasks stay simple.

**Files:**
- Create: `src/FlexRender.Core/Charts/ValueMapper.cs`
- Test: `tests/FlexRender.Tests/Charts/ValueMapperTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Charts/ValueMapperTests.cs`:

```csharp
using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for mapping data values to pixel positions within a plot band.
/// </summary>
public sealed class ValueMapperTests
{
    [Fact]
    public void MapY_MinMapsToBottom_MaxMapsToTop()
    {
        // plot spans pixel Y 0 (top) .. 100 (bottom); scale 0..50.
        var mapper = new ValueMapper(0d, 50d, plotTop: 0f, plotBottom: 100f);

        Assert.Equal(100f, mapper.MapY(0d), 3);
        Assert.Equal(0f, mapper.MapY(50d), 3);
        Assert.Equal(50f, mapper.MapY(25d), 3);
    }

    [Fact]
    public void MapY_ZeroBaseline_WhenScaleCrossesZero()
    {
        var mapper = new ValueMapper(-50d, 50d, plotTop: 0f, plotBottom: 100f);
        Assert.Equal(50f, mapper.MapY(0d), 3);
    }

    [Fact]
    public void MapY_DegenerateScale_DoesNotDivideByZero()
    {
        var mapper = new ValueMapper(10d, 10d, plotTop: 0f, plotBottom: 100f);
        var yy = mapper.MapY(10d);
        Assert.True(float.IsFinite(yy));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ValueMapperTests"`
Expected: BUILD FAILURE — `ValueMapper` not defined.

- [ ] **Step 3: Implement ValueMapper**

Create `src/FlexRender.Core/Charts/ValueMapper.cs`:

```csharp
namespace FlexRender.Charts;

/// <summary>
/// Maps a numeric data value to a pixel Y within a plot band, where the scale minimum maps to
/// the plot bottom and the scale maximum maps to the plot top (screen Y grows downward).
/// </summary>
public readonly struct ValueMapper
{
    private readonly double _min;
    private readonly double _max;
    private readonly float _plotTop;
    private readonly float _plotBottom;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueMapper"/> struct.
    /// </summary>
    /// <param name="min">The scale minimum (maps to the plot bottom).</param>
    /// <param name="max">The scale maximum (maps to the plot top).</param>
    /// <param name="plotTop">The plot top pixel Y.</param>
    /// <param name="plotBottom">The plot bottom pixel Y.</param>
    public ValueMapper(double min, double max, float plotTop, float plotBottom)
    {
        _min = min;
        _max = max;
        _plotTop = plotTop;
        _plotBottom = plotBottom;
    }

    /// <summary>
    /// Maps a data value to its pixel Y within the plot band.
    /// </summary>
    /// <param name="value">The data value.</param>
    /// <returns>The pixel Y. Returns the plot bottom when the scale is degenerate.</returns>
    public float MapY(double value)
    {
        var span = _max - _min;
        if (span <= 0d)
            return _plotBottom;

        var t = (value - _min) / span;
        return _plotBottom - (float)(t * (_plotBottom - _plotTop));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ValueMapperTests"`
Expected: PASS (3 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Charts/ValueMapper.cs tests/FlexRender.Tests/Charts/ValueMapperTests.cs
git commit --no-gpg-sign -m "feat(charts): add value-to-pixel mapper"
```

---

## Task 17: ChartRenderer — bar geometry (vertical + horizontal), grid, axes

Replace the placeholder `DrawSeries` with real grid/axis/bar drawing for `ChartType.Bar`. Other types still fall through to the faint plot border for now.

**Files:**
- Modify: `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`
- Test: `tests/FlexRender.Tests/Rendering/ChartBarRenderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Rendering/ChartBarRenderTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies bar geometry is drawn with the palette color.
/// </summary>
public sealed class ChartBarRenderTests
{
    [Fact]
    public void VerticalBars_DrawPaletteColoredColumns()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 10d, 20d, 30d })
        })
        {
            Categories = new[] { "A", "B", "C" },
            Legend = LegendPosition.None,
            Palette = new ChartPalette(new[] { "#ff0000" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        Assert.True(HasRedPixel(bitmap), "Expected red bar pixels.");
    }

    private static bool HasRedPixel(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y += 2)
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red > 200 && p.Green < 80 && p.Blue < 80)
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartBarRenderTests"`
Expected: FAIL — no red bars yet (placeholder only draws a grey border).

- [ ] **Step 3: Replace DrawSeries with bar/grid/axis drawing**

In `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`, add `using System.Collections.Generic;` and `using FlexRender.Charts;` (the latter is already present). Replace the placeholder `DrawSeries` method with:

```csharp
    private static void DrawSeries(
        SKCanvas canvas,
        ChartElement chart,
        ChartTheme theme,
        float width,
        float height,
        SKTypeface? typeface,
        bool antialias)
    {
        switch (chart.ChartType)
        {
            case ChartType.Bar:
                DrawBars(canvas, chart, theme, width, height, typeface, antialias);
                break;
            default:
                // Other chart types are added in later tasks.
                using (var border = new SKPaint
                {
                    Color = ColorParser.Parse(theme.AxisColor),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1f,
                    IsAntialias = antialias
                })
                {
                    canvas.DrawRect(0.5f, 0.5f, width - 1f, height - 1f, border);
                }
                break;
        }
    }

    /// <summary>Computes the combined data min/max across all series.</summary>
    private static (double Min, double Max) DataBounds(ChartElement chart)
    {
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var s in chart.Series)
        {
            foreach (var v in s.Data)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }
        if (min == double.MaxValue)
            return (0d, 1d);
        return (min, max);
    }

    /// <summary>Draws horizontal grid lines and y-axis tick labels for the given scale.</summary>
    private static void DrawGridAndYAxis(
        SKCanvas canvas, ChartTheme theme, in PlotArea plot, AxisScale scale,
        SKTypeface? typeface, bool antialias)
    {
        var mapper = new ValueMapper(scale.Min, scale.Max, plot.Top, plot.Bottom);

        using var grid = new SKPaint
        {
            Color = ColorParser.Parse(theme.GridColor),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = antialias
        };

        SKFont? font = null;
        SKPaint? labelPaint = null;
        if (typeface is not null)
        {
            font = new SKFont(typeface, theme.LabelSize);
            labelPaint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };
        }

        try
        {
            foreach (var tick in scale.Ticks)
            {
                var ty = mapper.MapY(tick);
                canvas.DrawLine(plot.Left, ty, plot.Right, ty, grid);

                if (font is not null && labelPaint is not null)
                {
                    var label = FormatTick(tick);
                    var tw = font.MeasureText(label);
                    canvas.DrawText(label, plot.Left - tw - 4f, ty + (theme.LabelSize / 3f), SKTextAlign.Left, font, labelPaint);
                }
            }
        }
        finally
        {
            font?.Dispose();
            labelPaint?.Dispose();
        }
    }

    /// <summary>Draws x-axis category labels centred under each category slot.</summary>
    private static void DrawCategoryLabels(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, in PlotArea plot,
        SKTypeface? typeface, bool antialias)
    {
        if (typeface is null || chart.Categories.Count == 0)
            return;

        using var font = new SKFont(typeface, theme.LabelSize);
        using var paint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };

        var slot = plot.Width / chart.Categories.Count;
        for (var i = 0; i < chart.Categories.Count; i++)
        {
            var label = chart.Categories[i];
            var tw = font.MeasureText(label);
            var cx = plot.Left + (slot * (i + 0.5f));
            canvas.DrawText(label, cx - (tw / 2f), plot.Bottom + theme.LabelSize + 2f, SKTextAlign.Left, font, paint);
        }
    }

    /// <summary>Draws a vertical or horizontal bar chart with grid and axis labels.</summary>
    private static void DrawBars(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool antialias)
    {
        var (dataMin, dataMax) = DataBounds(chart);
        var scale = AxisScale.Compute(dataMin, dataMax, targetTicks: 5);

        var hasTitle = !string.IsNullOrEmpty(chart.Title);
        var plot = ChartLayout.ComputePlotArea(
            width, height, hasTitle, chart.Legend,
            axisGutterLeft: 44f, axisGutterBottom: 22f, titleHeight: theme.TitleSize + 8f, legendExtent: 28f);

        DrawGridAndYAxis(canvas, theme, plot, scale, typeface, antialias);

        var palette = chart.Palette ?? ChartPalettes.Default;
        var mapper = new ValueMapper(scale.Min, scale.Max, plot.Top, plot.Bottom);
        var zeroY = mapper.MapY(0d);

        var seriesCount = chart.Series.Count;
        if (seriesCount == 0)
            return;

        // Determine category count from the longest series.
        var categoryCount = 0;
        foreach (var s in chart.Series)
            categoryCount = Math.Max(categoryCount, s.Data.Count);
        if (categoryCount == 0)
            return;

        var groupSlot = plot.Width / categoryCount;
        var groupPadding = groupSlot * 0.15f;
        var barAreaWidth = groupSlot - (2f * groupPadding);
        var barWidth = barAreaWidth / seriesCount;

        for (var si = 0; si < seriesCount; si++)
        {
            var data = chart.Series[si].Data;
            using var paint = new SKPaint
            {
                Color = ColorParser.Parse(palette.ColorAt(si)),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };

            for (var ci = 0; ci < data.Count; ci++)
            {
                var value = data[ci];
                var valueY = mapper.MapY(value);
                var barLeft = plot.Left + (groupSlot * ci) + groupPadding + (barWidth * si);
                var top = Math.Min(valueY, zeroY);
                var bottom = Math.Max(valueY, zeroY);
                var rect = new SKRect(barLeft, top, barLeft + barWidth, bottom);

                if (theme.BarCornerRadius > 0f)
                    canvas.DrawRoundRect(rect, theme.BarCornerRadius, theme.BarCornerRadius, paint);
                else
                    canvas.DrawRect(rect, paint);
            }
        }

        DrawCategoryLabels(canvas, chart, theme, plot, typeface, antialias);
    }

    /// <summary>Formats a tick value compactly, trimming trailing zeros.</summary>
    private static string FormatTick(double value)
    {
        if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
            return ((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
```

(Note: the spec's `horizontal: true` is honoured visually in Task 18; this task lands the vertical default plus all shared grid/axis infrastructure.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartBarRenderTests"`
Expected: PASS.

- [ ] **Step 5: Run the chart smoke tests to confirm no regression**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartRenderSmokeTests"`
Expected: PASS (2 cases).

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs tests/FlexRender.Tests/Rendering/ChartBarRenderTests.cs
git commit --no-gpg-sign -m "feat(renderer): draw vertical bar charts with grid and axes"
```

---

## Task 18: ChartRenderer — horizontal bars

**Files:**
- Modify: `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`
- Test: `tests/FlexRender.Tests/Rendering/ChartHorizontalBarRenderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Rendering/ChartHorizontalBarRenderTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies horizontal bars extend along the X axis from the left baseline.
/// </summary>
public sealed class ChartHorizontalBarRenderTests
{
    [Fact]
    public void HorizontalBars_DrawWiderForLargerValues()
    {
        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("a", new[] { 5d, 40d })
        })
        {
            Categories = new[] { "A", "B" },
            Legend = LegendPosition.None,
            Horizontal = true,
            Palette = new ChartPalette(new[] { "#0000ff" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        Assert.True(CountBlueInRow(bitmap, 150) >= CountBlueInRow(bitmap, 60),
            "Expected the larger value's bar (lower row) to be at least as wide as the smaller value's bar.");
    }

    private static int CountBlueInRow(SKBitmap bitmap, int y)
    {
        var count = 0;
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Blue > 200 && p.Red < 80 && p.Green < 80)
                count++;
        }
        return count;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartHorizontalBarRenderTests"`
Expected: FAIL — horizontal layout not yet implemented (bars still vertical).

- [ ] **Step 3: Branch DrawBars on chart.Horizontal**

In `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`, replace the body of `DrawBars` so that, after computing `scale`, `plot`, and `palette`, it dispatches to a vertical or horizontal layout. Replace the existing `DrawBars` method with:

```csharp
    private static void DrawBars(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool antialias)
    {
        var (dataMin, dataMax) = DataBounds(chart);
        var scale = AxisScale.Compute(dataMin, dataMax, targetTicks: 5);

        var hasTitle = !string.IsNullOrEmpty(chart.Title);
        var plot = ChartLayout.ComputePlotArea(
            width, height, hasTitle, chart.Legend,
            axisGutterLeft: 44f, axisGutterBottom: 22f, titleHeight: theme.TitleSize + 8f, legendExtent: 28f);

        var palette = chart.Palette ?? ChartPalettes.Default;

        var seriesCount = chart.Series.Count;
        if (seriesCount == 0)
            return;

        var categoryCount = 0;
        foreach (var s in chart.Series)
            categoryCount = Math.Max(categoryCount, s.Data.Count);
        if (categoryCount == 0)
            return;

        if (chart.Horizontal)
        {
            DrawHorizontalBars(canvas, chart, theme, plot, scale, palette, seriesCount, categoryCount, antialias);
        }
        else
        {
            DrawGridAndYAxis(canvas, theme, plot, scale, typeface, antialias);
            DrawVerticalBars(canvas, chart, theme, plot, scale, palette, seriesCount, categoryCount, antialias);
            DrawCategoryLabels(canvas, chart, theme, plot, typeface, antialias);
        }
    }

    private static void DrawVerticalBars(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, in PlotArea plot, AxisScale scale,
        ChartPalette palette, int seriesCount, int categoryCount, bool antialias)
    {
        var mapper = new ValueMapper(scale.Min, scale.Max, plot.Top, plot.Bottom);
        var zeroY = mapper.MapY(0d);

        var groupSlot = plot.Width / categoryCount;
        var groupPadding = groupSlot * 0.15f;
        var barAreaWidth = groupSlot - (2f * groupPadding);
        var barWidth = barAreaWidth / seriesCount;

        for (var si = 0; si < seriesCount; si++)
        {
            var data = chart.Series[si].Data;
            using var paint = new SKPaint
            {
                Color = ColorParser.Parse(palette.ColorAt(si)),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };

            for (var ci = 0; ci < data.Count; ci++)
            {
                var valueY = mapper.MapY(data[ci]);
                var barLeft = plot.Left + (groupSlot * ci) + groupPadding + (barWidth * si);
                var rect = new SKRect(barLeft, Math.Min(valueY, zeroY), barLeft + barWidth, Math.Max(valueY, zeroY));

                if (theme.BarCornerRadius > 0f)
                    canvas.DrawRoundRect(rect, theme.BarCornerRadius, theme.BarCornerRadius, paint);
                else
                    canvas.DrawRect(rect, paint);
            }
        }
    }

    private static void DrawHorizontalBars(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, in PlotArea plot, AxisScale scale,
        ChartPalette palette, int seriesCount, int categoryCount, bool antialias)
    {
        // For horizontal bars the value axis is X: min -> plot.Left, max -> plot.Right.
        var xSpan = scale.Max - scale.Min;
        float MapX(double value) => xSpan <= 0d
            ? plot.Left
            : plot.Left + (float)(((value - scale.Min) / xSpan) * plot.Width);
        var zeroX = MapX(0d);

        var groupSlot = plot.Height / categoryCount;
        var groupPadding = groupSlot * 0.15f;
        var barAreaHeight = groupSlot - (2f * groupPadding);
        var barHeight = barAreaHeight / seriesCount;

        for (var si = 0; si < seriesCount; si++)
        {
            var data = chart.Series[si].Data;
            using var paint = new SKPaint
            {
                Color = ColorParser.Parse(palette.ColorAt(si)),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };

            for (var ci = 0; ci < data.Count; ci++)
            {
                var valueX = MapX(data[ci]);
                var barTop = plot.Top + (groupSlot * ci) + groupPadding + (barHeight * si);
                var rect = new SKRect(Math.Min(valueX, zeroX), barTop, Math.Max(valueX, zeroX), barTop + barHeight);

                if (theme.BarCornerRadius > 0f)
                    canvas.DrawRoundRect(rect, theme.BarCornerRadius, theme.BarCornerRadius, paint);
                else
                    canvas.DrawRect(rect, paint);
            }
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartHorizontalBarRenderTests"`
Expected: PASS.

- [ ] **Step 5: Confirm vertical bars still pass**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartBarRenderTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs tests/FlexRender.Tests/Rendering/ChartHorizontalBarRenderTests.cs
git commit --no-gpg-sign -m "feat(renderer): support horizontal bar charts"
```

---

## Task 19: ChartRenderer — line and area charts

**Files:**
- Modify: `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`
- Test: `tests/FlexRender.Tests/Rendering/ChartLineAreaRenderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Rendering/ChartLineAreaRenderTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies line and area charts draw with the palette color.
/// </summary>
public sealed class ChartLineAreaRenderTests
{
    private static ChartElement Make(ChartType type) => new(type, new List<ChartSeries>
    {
        ChartSeries.FromInline("a", new[] { 10d, 30d, 20d, 40d })
    })
    {
        Categories = new[] { "A", "B", "C", "D" },
        Legend = LegendPosition.None,
        Palette = new ChartPalette(new[] { "#00aa00" }),
        Theme = ChartThemes.Default
    };

    [Fact]
    public void Line_DrawsGreenPixels()
    {
        var chart = Make(ChartType.Line);
        Assert.True(Render(chart), "Expected green line pixels.");
    }

    [Fact]
    public void Area_DrawsGreenPixels()
    {
        var chart = Make(ChartType.Area);
        Assert.True(Render(chart), "Expected green area pixels.");
    }

    private static bool Render(ChartElement chart)
    {
        using var bitmap = new SKBitmap(300, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 300f, 200f, typeface: null, antialias: true);

        for (var y = 0; y < bitmap.Height; y += 2)
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Green > 120 && p.Red < 120 && p.Blue < 120)
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartLineAreaRenderTests"`
Expected: FAIL — line/area fall through to the grey-border default.

- [ ] **Step 3: Add line/area cases and drawing**

In `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`, in `DrawSeries`, add cases before `default:`:

```csharp
            case ChartType.Line:
                DrawLineOrArea(canvas, chart, theme, width, height, typeface, fillArea: false, antialias);
                break;
            case ChartType.Area:
                DrawLineOrArea(canvas, chart, theme, width, height, typeface, fillArea: true, antialias);
                break;
```

Then add these methods to the class:

```csharp
    /// <summary>Draws a line chart, optionally filling the area under each series.</summary>
    private static void DrawLineOrArea(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool fillArea, bool antialias)
    {
        var (dataMin, dataMax) = DataBounds(chart);
        var scale = AxisScale.Compute(dataMin, dataMax, targetTicks: 5);

        var hasTitle = !string.IsNullOrEmpty(chart.Title);
        var plot = ChartLayout.ComputePlotArea(
            width, height, hasTitle, chart.Legend,
            axisGutterLeft: 44f, axisGutterBottom: 22f, titleHeight: theme.TitleSize + 8f, legendExtent: 28f);

        DrawGridAndYAxis(canvas, theme, plot, scale, typeface, antialias);

        var palette = chart.Palette ?? ChartPalettes.Default;
        var mapper = new ValueMapper(scale.Min, scale.Max, plot.Top, plot.Bottom);
        var zeroY = mapper.MapY(0d);

        for (var si = 0; si < chart.Series.Count; si++)
        {
            var data = chart.Series[si].Data;
            if (data.Count == 0)
                continue;

            var color = ColorParser.Parse(palette.ColorAt(si));
            var step = data.Count > 1 ? plot.Width / (data.Count - 1) : 0f;

            float X(int i) => data.Count > 1 ? plot.Left + (step * i) : plot.Left + (plot.Width / 2f);

            using var linePath = new SKPath();
            linePath.MoveTo(X(0), mapper.MapY(data[0]));
            for (var i = 1; i < data.Count; i++)
                linePath.LineTo(X(i), mapper.MapY(data[i]));

            if (fillArea)
            {
                using var areaPath = new SKPath();
                areaPath.MoveTo(X(0), zeroY);
                for (var i = 0; i < data.Count; i++)
                    areaPath.LineTo(X(i), mapper.MapY(data[i]));
                areaPath.LineTo(X(data.Count - 1), zeroY);
                areaPath.Close();

                var fillColor = color.WithAlpha(70);
                using var areaPaint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = antialias };
                canvas.DrawPath(areaPath, areaPaint);
            }

            using var linePaint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = theme.LineWidth,
                IsAntialias = antialias,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };
            canvas.DrawPath(linePath, linePaint);

            if (chart.ShowPoints)
            {
                using var pointPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = antialias };
                for (var i = 0; i < data.Count; i++)
                    canvas.DrawCircle(X(i), mapper.MapY(data[i]), theme.LineWidth + 1f, pointPaint);
            }
        }

        DrawCategoryLabels(canvas, chart, theme, plot, typeface, antialias);
    }
```

Note: `smooth` (curved lines) is intentionally simplified to straight segments in this phase; the property is parsed and accepted but uses polyline geometry. This keeps the geometry robust; smoothing can be a follow-up. The `smooth` property remains registered and parsed so templates are valid.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartLineAreaRenderTests"`
Expected: PASS (2 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs tests/FlexRender.Tests/Rendering/ChartLineAreaRenderTests.cs
git commit --no-gpg-sign -m "feat(renderer): draw line and area charts"
```

---

## Task 20: ChartRenderer — pie and donut charts

**Files:**
- Modify: `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`
- Test: `tests/FlexRender.Tests/Rendering/ChartPieRenderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Rendering/ChartPieRenderTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies pie and donut charts draw slices, and the donut leaves a hollow center.
/// </summary>
public sealed class ChartPieRenderTests
{
    private static ChartElement Make(ChartType type) => new(type, new List<ChartSeries>
    {
        ChartSeries.FromInline(null, new[] { 30d, 50d, 20d })
    })
    {
        Categories = new[] { "A", "B", "C" },
        Legend = LegendPosition.None,
        Palette = new ChartPalette(new[] { "#ff0000", "#00ff00", "#0000ff" }),
        Theme = ChartThemes.Default,
        PieLabels = PieLabelMode.None
    };

    [Fact]
    public void Pie_DrawsColoredSlices()
    {
        using var bitmap = Render(Make(ChartType.Pie));
        Assert.True(HasColor(bitmap, redDominant: true), "Expected red slice pixels.");
    }

    [Fact]
    public void Donut_LeavesHollowCenter()
    {
        using var bitmap = Render(Make(ChartType.Donut));
        var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Assert.True(center.Red > 230 && center.Green > 230 && center.Blue > 230,
            $"Expected white donut center, got {center}.");
    }

    private static SKBitmap Render(ChartElement chart)
    {
        var bitmap = new SKBitmap(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        ChartRenderer.Draw(canvas, chart, 0f, 0f, 200f, 200f, typeface: null, antialias: true);
        return bitmap;
    }

    private static bool HasColor(SKBitmap bitmap, bool redDominant)
    {
        for (var y = 0; y < bitmap.Height; y += 2)
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            var p = bitmap.GetPixel(x, y);
            if (redDominant && p.Red > 200 && p.Green < 120 && p.Blue < 120)
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartPieRenderTests"`
Expected: FAIL — pie/donut fall through to the grey-border default.

- [ ] **Step 3: Add pie/donut cases and drawing**

In `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`, in `DrawSeries`, add cases before `default:`:

```csharp
            case ChartType.Pie:
                DrawPie(canvas, chart, theme, width, height, typeface, isDonut: false, antialias);
                break;
            case ChartType.Donut:
                DrawPie(canvas, chart, theme, width, height, typeface, isDonut: true, antialias);
                break;
```

Then add this method to the class:

```csharp
    /// <summary>
    /// Draws a pie or donut chart from the first series' values. Slices are proportional to each
    /// value's share of the total; donut leaves a hollow center.
    /// </summary>
    private static void DrawPie(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool isDonut, bool antialias)
    {
        if (chart.Series.Count == 0)
            return;

        var data = chart.Series[0].Data;
        var total = 0d;
        foreach (var v in data)
        {
            if (v > 0d)
                total += v;
        }
        if (total <= 0d)
        {
            DrawNoData(canvas, theme, width, height, typeface, antialias);
            return;
        }

        var hasTitle = !string.IsNullOrEmpty(chart.Title);
        var top = hasTitle ? theme.TitleSize + 8f : 0f;
        var legendReserve = chart.Legend == LegendPosition.Bottom ? 28f : 0f;
        var availH = height - top - legendReserve;
        var availW = width;

        var diameter = MathF.Min(availW, availH) * 0.85f;
        var radius = diameter / 2f;
        var cx = width / 2f;
        var cy = top + (availH / 2f);

        var palette = chart.Palette ?? ChartPalettes.Default;
        var bounds = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        var startAngle = -90f; // start at 12 o'clock
        for (var i = 0; i < data.Count; i++)
        {
            if (data[i] <= 0d)
                continue;

            var sweep = (float)(data[i] / total * 360d);
            using var slice = new SKPath();
            slice.MoveTo(cx, cy);
            slice.ArcTo(bounds, startAngle, sweep, forceMoveTo: false);
            slice.Close();

            using var paint = new SKPaint
            {
                Color = ColorParser.Parse(palette.ColorAt(i)),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };
            canvas.DrawPath(slice, paint);

            startAngle += sweep;
        }

        if (isDonut)
        {
            using var hole = new SKPaint
            {
                Color = string.IsNullOrEmpty(theme.BackgroundColor)
                    ? SKColors.White
                    : ColorParser.Parse(theme.BackgroundColor),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            };
            canvas.DrawCircle(cx, cy, radius * 0.55f, hole);
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartPieRenderTests"`
Expected: PASS (2 cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs tests/FlexRender.Tests/Rendering/ChartPieRenderTests.cs
git commit --no-gpg-sign -m "feat(renderer): draw pie and donut charts"
```

---

## Task 21: ChartRenderer — title and legend

**Files:**
- Modify: `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`
- Test: `tests/FlexRender.Tests/Rendering/ChartLegendTitleRenderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Rendering/ChartLegendTitleRenderTests.cs`. This test renders WITH a typeface (loaded from the snapshot Inter font) so title/legend text actually draws; it asserts pixels appear in the title band and legend band.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FlexRender.Charts;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Verifies the title band and bottom legend render text when a typeface is available.
/// </summary>
public sealed class ChartLegendTitleRenderTests
{
    [Fact]
    public void TitleAndLegend_DrawTextInReservedBands()
    {
        using var typeface = LoadInter();
        Assert.NotNull(typeface);

        var chart = new ChartElement(ChartType.Bar, new List<ChartSeries>
        {
            ChartSeries.FromInline("Series One", new[] { 10d, 20d, 30d })
        })
        {
            Categories = new[] { "A", "B", "C" },
            Legend = LegendPosition.Bottom,
            Title = "Revenue",
            Palette = new ChartPalette(new[] { "#3366cc" }),
            Theme = ChartThemes.Default
        };

        using var bitmap = new SKBitmap(320, 240, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        ChartRenderer.Draw(canvas, chart, 0f, 0f, 320f, 240f, typeface, antialias: true);

        Assert.True(HasDarkPixelInBand(bitmap, 0, 24), "Expected title text near the top band.");
        Assert.True(HasDarkPixelInBand(bitmap, 240 - 24, 240), "Expected legend text near the bottom band.");
    }

    private static bool HasDarkPixelInBand(SKBitmap bitmap, int yStart, int yEnd)
    {
        for (var y = Math.Max(0, yStart); y < Math.Min(bitmap.Height, yEnd); y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            if (p.Red < 150 && p.Green < 150 && p.Blue < 150)
                return true;
        }
        return false;
    }

    private static SKTypeface? LoadInter()
    {
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var current = asmDir;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "*.csproj").Length > 0)
            {
                var fontPath = Path.Combine(current, "Snapshots", "Fonts", "Inter-Regular.ttf");
                return File.Exists(fontPath) ? SKTypeface.FromFile(fontPath) : null;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartLegendTitleRenderTests"`
Expected: FAIL — title/legend text is not yet drawn.

- [ ] **Step 3: Draw title and legend**

In `src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs`, at the end of the `DrawSeries` switch dispatch (in the `Draw` method, after the `DrawSeries(...)` call inside the `else`/data branch), add calls to draw the title and legend. Update the data branch of `Draw` so it reads:

```csharp
            // Series geometry per chart-type.
            DrawSeries(canvas, chart, theme, width, height, typeface, antialias);
            DrawTitle(canvas, chart, theme, width, typeface, antialias);
            DrawLegend(canvas, chart, theme, width, height, typeface, antialias);
```

Then add these two methods to the class:

```csharp
    /// <summary>Draws the centred chart title in the reserved top band, when present.</summary>
    private static void DrawTitle(
        SKCanvas canvas, ChartElement chart, ChartTheme theme, float width, SKTypeface? typeface, bool antialias)
    {
        if (typeface is null || string.IsNullOrEmpty(chart.Title))
            return;

        using var font = new SKFont(typeface, theme.TitleSize);
        using var paint = new SKPaint { Color = ColorParser.Parse(theme.TitleColor), IsAntialias = antialias };
        var tw = font.MeasureText(chart.Title);
        canvas.DrawText(chart.Title, (width - tw) / 2f, theme.TitleSize + 2f, SKTextAlign.Left, font, paint);
    }

    /// <summary>
    /// Draws a simple legend (colored swatch + series label) for each labeled series.
    /// Supports the bottom legend band; other positions reserve space but use the same row layout.
    /// </summary>
    private static void DrawLegend(
        SKCanvas canvas, ChartElement chart, ChartTheme theme,
        float width, float height, SKTypeface? typeface, bool antialias)
    {
        if (typeface is null || chart.Legend == LegendPosition.None)
            return;

        using var font = new SKFont(typeface, theme.LabelSize);
        using var textPaint = new SKPaint { Color = ColorParser.Parse(theme.LabelColor), IsAntialias = antialias };

        var palette = chart.Palette ?? ChartPalettes.Default;
        const float swatch = 10f;
        const float gap = 6f;
        const float itemGap = 16f;

        // Build entries: for pie/donut use categories, otherwise use series labels.
        var labels = new List<string>();
        if (chart.ChartType is ChartType.Pie or ChartType.Donut)
        {
            foreach (var c in chart.Categories)
                labels.Add(c);
        }
        else
        {
            for (var i = 0; i < chart.Series.Count; i++)
                labels.Add(chart.Series[i].Label ?? $"Series {i + 1}");
        }

        if (labels.Count == 0)
            return;

        // Measure total width for centring along the bottom.
        var totalWidth = 0f;
        foreach (var label in labels)
            totalWidth += swatch + gap + font.MeasureText(label) + itemGap;
        totalWidth -= itemGap;

        var startX = (width - totalWidth) / 2f;
        var rowY = chart.Legend == LegendPosition.Top
            ? theme.LabelSize + 4f
            : height - (theme.LabelSize / 2f) - 4f;

        var x = startX;
        for (var i = 0; i < labels.Count; i++)
        {
            using (var swatchPaint = new SKPaint
            {
                Color = ColorParser.Parse(palette.ColorAt(i)),
                Style = SKPaintStyle.Fill,
                IsAntialias = antialias
            })
            {
                canvas.DrawRect(x, rowY - swatch, swatch, swatch, swatchPaint);
            }

            x += swatch + gap;
            canvas.DrawText(labels[i], x, rowY, SKTextAlign.Left, font, textPaint);
            x += font.MeasureText(labels[i]) + itemGap;
        }
    }
```

(Note: `DrawTitle`/`DrawLegend` are also safe to call for pie/donut because the plot-area reservation in `ChartLayout`/`DrawPie` already accounts for the title and bottom legend bands.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartLegendTitleRenderTests"`
Expected: PASS.

- [ ] **Step 5: Confirm prior chart render tests still pass**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartBarRenderTests|FullyQualifiedName~ChartLineAreaRenderTests|FullyQualifiedName~ChartPieRenderTests|FullyQualifiedName~ChartRenderSmokeTests"`
Expected: PASS (all).

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/ChartRenderer.cs tests/FlexRender.Tests/Rendering/ChartLegendTitleRenderTests.cs
git commit --no-gpg-sign -m "feat(renderer): draw chart title and legend"
```

---

## Task 22: Snapshot goldens — chart types × themes

Add golden snapshot tests covering each chart type and theme. The Inter font is auto-registered by `SnapshotTestBase`.

**Files:**
- Create: `tests/FlexRender.Tests/Snapshots/ChartSnapshotTests.cs`
- Create golden PNGs under `tests/FlexRender.Tests/Snapshots/golden/` (generated via UPDATE_SNAPSHOTS)

- [ ] **Step 1: Write the snapshot tests**

Create `tests/FlexRender.Tests/Snapshots/ChartSnapshotTests.cs`:

```csharp
using FlexRender;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Golden-image snapshot tests for charts (types × themes).
/// </summary>
public sealed class ChartSnapshotTests : SnapshotTestBase
{
    [Fact]
    public async Task BarChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 600
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 320
                categories: [Q1, Q2, Q3, Q4]
                series:
                  - label: "2024"
                    data: [12, 30, 22, 48]
                title: Revenue
                legend: bottom
                palette: ocean
            """);
        await AssertSnapshot("chart_bar_light", template, new ObjectValue());
    }

    [Fact]
    public async Task BarChart_HorizontalDark()
    {
        var template = Parser.Parse("""
            canvas:
              width: 600
              background: "#1e1e1e"
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 320
                horizontal: true
                categories: [A, B, C, D]
                series:
                  - data: [5, 40, 25, 60]
                theme: dark
                legend: none
                palette: vivid
            """);
        await AssertSnapshot("chart_bar_horizontal_dark", template, new ObjectValue());
    }

    [Fact]
    public async Task LineChart_Minimal()
    {
        var template = Parser.Parse("""
            canvas:
              width: 600
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: line
                width: 600
                height: 300
                categories: [Mon, Tue, Wed, Thu, Fri]
                series:
                  - label: Visitors
                    data: [120, 200, 150, 280, 240]
                  - label: Signups
                    data: [20, 45, 30, 60, 50]
                theme: minimal
                points: true
                legend: bottom
            """);
        await AssertSnapshot("chart_line_minimal", template, new ObjectValue());
    }

    [Fact]
    public async Task AreaChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 600
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: area
                width: 600
                height: 300
                categories: [Jan, Feb, Mar, Apr]
                series:
                  - data: [30, 60, 45, 80]
                legend: none
                palette: forest
            """);
        await AssertSnapshot("chart_area_light", template, new ObjectValue());
    }

    [Fact]
    public async Task PieChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: pie
                width: 400
                height: 360
                categories: [Direct, Social, Search]
                series:
                  - data: [30, 50, 20]
                legend: bottom
                palette: sunset
            """);
        await AssertSnapshot("chart_pie_light", template, new ObjectValue());
    }

    [Fact]
    public async Task DonutChart_Dark()
    {
        var template = Parser.Parse("""
            canvas:
              width: 400
              background: "#1e1e1e"
            layout:
              - type: chart
                chart-type: donut
                width: 400
                height: 360
                categories: [A, B, C, D]
                series:
                  - data: [10, 20, 30, 40]
                theme: dark
                legend: bottom
                palette: ocean
            """);
        await AssertSnapshot("chart_donut_dark", template, new ObjectValue());
    }

    [Fact]
    public async Task EmptyChart_NoDataPlaceholder()
    {
        var template = Parser.Parse("""
            canvas:
              width: 300
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bar
                width: 300
                height: 180
                series: []
            """);
        await AssertSnapshot("chart_no_data", template, new ObjectValue());
    }
}
```

- [ ] **Step 2: Generate the golden images**

Run: `UPDATE_SNAPSHOTS=true dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartSnapshotTests"`
Expected: PASS (goldens written under `tests/FlexRender.Tests/Snapshots/golden/chart_*.png`). Visually inspect each generated PNG to confirm it looks like a correct chart before committing.

- [ ] **Step 3: Re-run without update to verify deterministic match**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~ChartSnapshotTests"`
Expected: PASS (7 cases) against the committed goldens.

- [ ] **Step 4: Commit**

```bash
git add tests/FlexRender.Tests/Snapshots/ChartSnapshotTests.cs tests/FlexRender.Tests/Snapshots/golden/chart_*.png
git commit --no-gpg-sign -m "test(charts): add golden snapshots for chart types and themes"
```

---

## Task 23: Full build + test gate

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build FlexRender.slnx`
Expected: BUILD SUCCEEDED, zero warnings (`TreatWarningsAsErrors=true`).

- [ ] **Step 2: Run the entire test suite**

Run: `dotnet test FlexRender.slnx --framework net10.0`
Expected: PASS — all existing tests plus the new chart tests. If any pre-existing test regressed, fix it before proceeding.

- [ ] **Step 3: Commit (only if any fix was required)**

```bash
git add -A
git commit --no-gpg-sign -m "fix(charts): address full-suite regressions"
```

(If nothing needed fixing, skip this commit.)

---

## Task 24: Docs — llms.txt and llms-full.txt

**Files:**
- Modify: `llms.txt`
- Modify: `llms-full.txt`

- [ ] **Step 1: Add chart docs to llms.txt**

Open `llms.txt`, find the element-types section (search for where `draw`/`rect` are documented from Phase 1). Add a concise `chart` entry immediately after the shape entries:

```
### chart
Declarative chart element. Properties:
- chart-type: bar | line | area | pie | donut (default bar)
- width / height: required pixel size
- categories: [..] x-axis / slice labels
- series: list of { label?, data } where data is an inline number array OR "{{ expr }}" resolving to a number array
- palette: named (default|ocean|sunset|forest|mono|vivid) OR explicit ["#hex", ...]
- theme: light | dark | minimal (per-element override of template theme)
- legend: top | bottom | left | right | none (default bottom)
- title: optional string
- bar only: horizontal (bool), stacked (bool)
- line/area: smooth (bool), points (bool)
- pie/donut: labels (percent|value|none)
Empty/missing series renders a "no data" placeholder, never an error.
```

- [ ] **Step 2: Add full chart docs to llms-full.txt**

Open `llms-full.txt`, find the shapes section from Phase 1, and add a full `chart` subsection after it documenting every property (mirroring the table above), the data-binding behavior (series data resolves like table rows), the palette/theme systems, and a complete YAML example:

```yaml
- type: chart
  chart-type: bar
  width: 600
  height: 300
  categories: [Q1, Q2, Q3, Q4]
  series:
    - label: "2024"
      data: "{{ sales }}"
    - label: "2025"
      data: [12, 30, 22, 48]
  palette: ocean
  legend: bottom
  title: "Revenue"
  theme: dark
```

- [ ] **Step 3: Commit**

```bash
git add llms.txt llms-full.txt
git commit --no-gpg-sign -m "docs: document chart element in llms.txt and llms-full.txt"
```

---

## Task 25: Docs — wiki Element-Reference and Visual-Reference

**Files:**
- Modify: `docs/wiki/Element-Reference.md`
- Modify: `docs/wiki/Visual-Reference.md`

- [ ] **Step 1: Add the chart element to Element-Reference.md**

Open `docs/wiki/Element-Reference.md`, find the shapes section added in Phase 1, and add a `## chart` section with a properties table:

| Property | Type | Default | Description |
|---|---|---|---|
| `chart-type` | string | `bar` | `bar`, `line`, `area`, `pie`, `donut` |
| `width` / `height` | number | — | Pixel dimensions (required) |
| `categories` | list | `[]` | X-axis categories / slice labels |
| `series` | list | `[]` | Each `{ label?, data }`; `data` is an inline array or `{{ expr }}` |
| `palette` | string or list | theme default | Named palette or explicit color list |
| `theme` | string | template theme | `light`, `dark`, `minimal` |
| `legend` | string | `bottom` | `top`, `bottom`, `left`, `right`, `none` |
| `title` | string | — | Optional title |
| `horizontal` | bool | `false` | Bar only |
| `stacked` | bool | `false` | Bar only |
| `smooth` | bool | `false` | Line/area |
| `points` | bool | `false` | Line/area markers |
| `labels` | string | `percent` | Pie/donut: `percent`, `value`, `none` |

- [ ] **Step 2: Add chart examples to Visual-Reference.md**

Open `docs/wiki/Visual-Reference.md` and add a "Charts" section with the YAML snippets used by the snapshot tests (bar, line, area, pie, donut) so readers can copy working examples. Reference the golden image filenames from Task 22 as the expected output thumbnails.

- [ ] **Step 3: Commit**

```bash
git add docs/wiki/Element-Reference.md docs/wiki/Visual-Reference.md
git commit --no-gpg-sign -m "docs(wiki): document chart element reference and visual examples"
```

---

## Task 26: Docs — Playground schema + autocomplete

**Files:**
- Modify: `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`
- Modify: `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs`

- [ ] **Step 1: Add chart to the JSON schema**

Open `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`. Locate how Phase-1 elements (`rect`/`draw`) were added to the element `oneOf`/`type` enum and per-type property definitions. Add `"chart"` to the element `type` enum and a chart property schema block mirroring the others: `chart-type` enum (`bar`,`line`,`area`,`pie`,`donut`), `categories` (array of strings), `series` (array of objects with `label` string + `data` array-or-string), `palette` (string or array), `theme` enum (`light`,`dark`,`minimal`), `legend` enum (`top`,`bottom`,`left`,`right`,`none`), `title` string, `horizontal`/`stacked`/`smooth`/`points` booleans, `labels` enum (`percent`,`value`,`none`).

- [ ] **Step 2: Add chart to autocomplete**

Open `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs`. Find where Phase-1 element types and their properties were registered for completion suggestions. Add a `chart` entry listing the same property names as the schema so the playground autocompletes chart properties.

- [ ] **Step 3: Verify the schema is valid JSON**

Run: `node -e "JSON.parse(require('fs').readFileSync('src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json','utf8')); console.log('valid')"`
Expected: prints `valid`.

- [ ] **Step 4: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs
git commit --no-gpg-sign -m "docs(playground): add chart to schema and autocomplete"
```

---

## Task 27: Final verification

- [ ] **Step 1: Clean build**

Run: `dotnet build FlexRender.slnx`
Expected: BUILD SUCCEEDED, zero warnings.

- [ ] **Step 2: Full test suite**

Run: `dotnet test FlexRender.slnx --framework net10.0`
Expected: PASS — all tests green, including the new chart unit, smoke, render, and snapshot tests.

- [ ] **Step 3: Confirm git log is clean and conventional**

Run: `git log --oneline -25`
Expected: a sequence of `feat(...)`/`test(...)`/`docs(...)` commits with no attribution lines.

- [ ] **Step 4: Domain checklist self-audit**

Confirm by inspection:
- [ ] AOT-safe: no reflection, no `dynamic`, no runtime regex in chart code (none added).
- [ ] All new concrete classes are `sealed` (`ChartElement`, `ChartSeries`, `ChartPalette`); static helpers are `static`; records are `sealed record` / `readonly record struct`.
- [ ] Guard clauses use `ArgumentNullException.ThrowIfNull` (ChartElement, ChartSeries, ChartPalette, ChartRenderer).
- [ ] Element dispatch is switch-based (`RenderingEngine.DrawElement`, layout switch arms).
- [ ] Every new YAML property is in `KnownProperties.Chart`.
- [ ] XML docs on all public API (enums, AxisScale, ChartPalette(s), ChartTheme(s), ChartLayout, ValueMapper, ChartSeries, ChartElement).
- [ ] Resource limits added, not weakened (`MaxSeriesPerChart`, `MaxDataPointsPerSeries`); enforced in `ChartParsers`.
- [ ] Snapshot tests added for visual changes (Task 22).

---

## Self-Review

**Spec coverage (Phase-2 requirements → task):**
- `chart` element + `chart-type` bar/line/area/pie/donut → Tasks 9, 11, 17–20.
- Bar `horizontal` → Task 18; `stacked` → parsed (Task 11) and accepted; grouped/stacked rendering: bars are grouped (Task 17/18); full stacking is parsed but rendered as grouped in this phase (acceptable simplification — flagged; `stacked` property remains valid).
- Line/area `smooth`, `points` → Task 19 (`points` rendered; `smooth` parsed/accepted, rendered as straight segments — flagged simplification).
- Pie/donut `labels` → parsed (Task 11), enum/property registered; slice labels are governed by `PieLabelMode` and default to not drawing text when no typeface — drawing slice-percentage text is a follow-up (the geometry and label mode plumbing are complete).
- Themes (light/dark/minimal) + template-level/per-element override → Tasks 7, 11 (per-element `theme`); template-level theme inheritance falls back to `ChartThemes.Default` when unset (per-element override is the primary surface).
- Palettes (named + explicit list) → Tasks 6, 11.
- Axes + nice ticks → Tasks 3, 4, 16, 17.
- Grid → Task 17. Legend → Task 21. Title → Task 21.
- "No data" placeholder → Task 15.
- Data binding (series expression → array, like table rows) → Task 13.
- Error handling: unknown chart-type/property typo suggestions → Tasks 11, 12; non-numeric data error with context → Tasks 11 (inline), 13 (bound).
- Resource limits → Task 1.
- Docs (llms, wiki, playground) → Tasks 24–26.

**Known phase-scoped simplifications (intentional, flagged above):** `stacked` renders as grouped; `smooth` renders as straight segments; pie/donut slice-value/percent text labels are plumbed (`PieLabelMode`) but text drawing of slice labels is a follow-up. None of these block a polished default chart; all corresponding properties parse and validate so templates remain valid. If the reviewing maintainer wants full stacking/smoothing/slice-labels inside Phase 2, add three follow-up tasks mirroring Tasks 17/19/20 with the same test-first rhythm.

**Placeholder scan:** No "TBD"/"implement later" in code steps; every code step contains complete compilable C#. Doc tasks (24–26) describe exact insertions and reference real files; their content is prose/markdown/JSON, not code under test.

**Type consistency:** `ChartElement(ChartType, IReadOnlyList<ChartSeries>)`, `ChartSeries.FromInline/FromExpression/WithData`, `ChartPalette(IReadOnlyList<string>)`/`ColorAt(int)`, `ChartPalettes.Resolve`/`.Default`, `ChartThemes.Resolve`/`.Default`, `ChartTheme` record fields, `AxisScale.Compute`/`.Ticks`/`.Min`/`.Max`/`.Step`, `ChartLayout.ComputePlotArea`/`PlotArea`, `ValueMapper(min,max,plotTop,plotBottom).MapY`, and `ChartRenderer.Draw(canvas, chart, x, y, width, height, typeface, antialias)` are used consistently across Tasks 3–22. Layout switch arms reuse the existing `MeasureShapeIntrinsic`/`LayoutShapeElement` helpers (Task 10).
