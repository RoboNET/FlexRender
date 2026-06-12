# Shapes (Phase 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add declarative shape primitives — `rect`, `circle`, `ellipse` (flex box shapes with fill/gradient/stroke/opacity), and a `draw` element holding absolute-coordinate shapes (`line`, `polyline`, `rect`, `circle`, `path`) — so LLM agents can produce custom graphics without hand-written SVG.

**Architecture:** New AST element classes in `FlexRender.Core/Parsing/Ast/` (`RectElement`, `CircleElement`, `EllipseElement`, `DrawElement`) follow the existing `SeparatorElement` pattern (override `Type`, `ResolveExpressions`, `Materialize`, `CloneWithSubstitution`). A pure, renderer-agnostic, hand-written `PathDataParser` (no regex) lives in Core. Parsing extends `FlexRender.Yaml` (`ElementParsers`, `TemplateParser` dispatch, `KnownProperties`). Layout treats shapes as leaf boxes with explicit dimensions (like `SeparatorElement`). Rendering extends `FlexRender.Skia.Render` via the existing `switch (element)` dispatch in `RenderingEngine.DrawElement`. The object-form gradient is converted to FlexRender's existing CSS-gradient string at parse time, reusing `GradientParser`.

**Tech Stack:** .NET 10, C# latest, xUnit, SkiaSharp, YamlDotNet. AOT-safe (no reflection, no `dynamic`, no regex for path parsing), `sealed` classes, `ArgumentNullException.ThrowIfNull`, switch-based dispatch, XML docs on all public API.

---

## Conventions used throughout this plan

- All commands are run from the repo root `/Users/robonet/Projects/SkiaLayout`.
- Branch is already `feature/charts-and-shapes`. Do NOT create worktrees. Do NOT merge to `main`.
- Build: `dotnet build FlexRender.slnx`. Test: `dotnet test FlexRender.slnx`.
- NEVER pipe `dotnet` output through `tail`/`head`/`grep`. Run commands directly.
- Commit messages use Conventional Commits, no attribution/Co-Authored-By lines.
- After every code edit, the build must be warning-free (`TreatWarningsAsErrors=true`).

## File structure (created/modified across all tasks)

Created:
- `src/FlexRender.Core/Parsing/Ast/RectElement.cs`
- `src/FlexRender.Core/Parsing/Ast/CircleElement.cs`
- `src/FlexRender.Core/Parsing/Ast/EllipseElement.cs`
- `src/FlexRender.Core/Parsing/Ast/DrawShapes.cs` (shape DTOs for `draw`)
- `src/FlexRender.Core/Parsing/Ast/DrawElement.cs`
- `src/FlexRender.Core/Parsing/PathDataParser.cs` (pure tokenizer)
- `src/FlexRender.Yaml/Parsing/ShapeParsers.cs` (parse box shapes + draw shapes + gradient object)
- `src/FlexRender.Skia.Render/Rendering/ShapeRenderer.cs` (Skia drawing for shapes + draw)
- Test files (see each task)

Modified:
- `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs` (add `ElementType` enum members)
- `src/FlexRender.Core/Configuration/ResourceLimits.cs` (add `MaxShapesPerDraw`)
- `src/FlexRender.Core/Layout/LayoutEngine.cs` (layout the new leaf elements)
- `src/FlexRender.Core/Layout/IntrinsicMeasurer.cs` (intrinsic size for new leaf elements)
- `src/FlexRender.Yaml/Parsing/TemplateParser.cs` (register `rect`/`circle`/`ellipse`/`draw`)
- `src/FlexRender.Yaml/Parsing/KnownProperties.cs` (new property sets + registry entries)
- `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs` (dispatch new elements)
- Docs: `llms.txt`, `llms-full.txt`, `docs/wiki/Element-Reference.md`, `docs/wiki/Visual-Reference.md`,
  `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`,
  `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs`

---

## Task 1: ResourceLimits.MaxShapesPerDraw

**Files:**
- Modify: `src/FlexRender.Core/Configuration/ResourceLimits.cs`
- Test: `tests/FlexRender.Tests/Configuration/ResourceLimitsShapesTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Configuration/ResourceLimitsShapesTests.cs`:

```csharp
using System;
using FlexRender.Configuration;
using Xunit;

namespace FlexRender.Tests.Configuration;

/// <summary>
/// Tests for the <see cref="ResourceLimits.MaxShapesPerDraw"/> limit.
/// </summary>
public sealed class ResourceLimitsShapesTests
{
    [Fact]
    public void MaxShapesPerDraw_DefaultsTo1000()
    {
        var limits = new ResourceLimits();
        Assert.Equal(1000, limits.MaxShapesPerDraw);
    }

    [Fact]
    public void MaxShapesPerDraw_AcceptsPositiveValue()
    {
        var limits = new ResourceLimits { MaxShapesPerDraw = 50 };
        Assert.Equal(50, limits.MaxShapesPerDraw);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxShapesPerDraw_RejectsNonPositive(int value)
    {
        var limits = new ResourceLimits();
        Assert.Throws<ArgumentOutOfRangeException>(() => limits.MaxShapesPerDraw = value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ResourceLimitsShapesTests"`
Expected: BUILD FAILURE — `ResourceLimits` does not contain a definition for `MaxShapesPerDraw`.

- [ ] **Step 3: Add the property**

In `src/FlexRender.Core/Configuration/ResourceLimits.cs`, add a backing field next to the others (after `private int _maxFlexLines = 1000;`):

```csharp
    private int _maxShapesPerDraw = 1000;
```

Then add this property after the `MaxImageSize` property (before the closing brace of the class):

```csharp
    /// <summary>
    /// Maximum number of shapes allowed in a single 'draw' element.
    /// Prevents resource exhaustion from templates with a huge shape list.
    /// </summary>
    /// <value>Default: 1000.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is zero or negative.</exception>
    public int MaxShapesPerDraw
    {
        get => _maxShapesPerDraw;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxShapesPerDraw = value;
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ResourceLimitsShapesTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Configuration/ResourceLimits.cs tests/FlexRender.Tests/Configuration/ResourceLimitsShapesTests.cs
git commit -m "feat(core): add MaxShapesPerDraw resource limit"
```

---

## Task 2: ElementType enum members

**Files:**
- Modify: `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`

- [ ] **Step 1: Add the new enum members**

In `src/FlexRender.Core/Parsing/Ast/TemplateElement.cs`, inside the `ElementType` enum, add these members after the `Content` member (insert a comma after `Content` first):

```csharp
    /// <summary>
    /// A rectangle shape element (flex box drawn as a filled/stroked rect).
    /// </summary>
    Rect,

    /// <summary>
    /// A circle shape element (flex box drawn as a filled/stroked circle).
    /// </summary>
    Circle,

    /// <summary>
    /// An ellipse shape element (flex box drawn as a filled/stroked ellipse).
    /// </summary>
    Ellipse,

    /// <summary>
    /// A free-form drawing element holding absolute-coordinate shapes.
    /// </summary>
    Draw
```

The enum tail must read:

```csharp
    /// <summary>
    /// A content element that expands formatted text into a subtree.
    /// </summary>
    Content,

    /// <summary>
    /// A rectangle shape element (flex box drawn as a filled/stroked rect).
    /// </summary>
    Rect,
    // ... (Circle, Ellipse, Draw as above)
```

- [ ] **Step 2: Verify the build compiles**

Run: `dotnet build FlexRender.slnx`
Expected: BUILD SUCCEEDED (no behavior change yet; this only extends the enum).

- [ ] **Step 3: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/TemplateElement.cs
git commit -m "feat(ast): add Rect, Circle, Ellipse, Draw element types"
```

---

## Task 3: PathDataParser (hand-written tokenizer, no regex)

This is the highest-risk component. Build it pure (Core, renderer-agnostic) with thorough edge-case tests before wiring anything into Skia.

**Files:**
- Create: `src/FlexRender.Core/Parsing/PathDataParser.cs`
- Test: `tests/FlexRender.Tests/Parsing/PathDataParserTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/FlexRender.Tests/Parsing/PathDataParserTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Parsing;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Edge-case tests for the hand-written absolute-only SVG-style path tokenizer.
/// </summary>
public sealed class PathDataParserTests
{
    [Fact]
    public void Parse_MoveAndLine_ProducesTwoCommands()
    {
        var commands = PathDataParser.Parse("M 0 0 L 100 50");

        Assert.Equal(2, commands.Count);
        Assert.Equal(PathCommandKind.MoveTo, commands[0].Kind);
        Assert.Equal(0f, commands[0].Points[0].X);
        Assert.Equal(0f, commands[0].Points[0].Y);
        Assert.Equal(PathCommandKind.LineTo, commands[1].Kind);
        Assert.Equal(100f, commands[1].Points[0].X);
        Assert.Equal(50f, commands[1].Points[0].Y);
    }

    [Fact]
    public void Parse_QuadraticAndCubicAndClose_ProducesAllCommands()
    {
        var commands = PathDataParser.Parse("M 0 0 Q 150 0 200 50 C 10 20 30 40 50 60 Z");

        Assert.Equal(4, commands.Count);
        Assert.Equal(PathCommandKind.MoveTo, commands[0].Kind);
        Assert.Equal(PathCommandKind.QuadTo, commands[1].Kind);
        Assert.Equal(2, commands[1].Points.Count);
        Assert.Equal(PathCommandKind.CubicTo, commands[2].Kind);
        Assert.Equal(3, commands[2].Points.Count);
        Assert.Equal(PathCommandKind.Close, commands[3].Kind);
        Assert.Empty(commands[3].Points);
    }

    [Fact]
    public void Parse_CommaSeparatedCoordinates_ParsesCorrectly()
    {
        var commands = PathDataParser.Parse("M0,0 L100,50");

        Assert.Equal(2, commands.Count);
        Assert.Equal(100f, commands[1].Points[0].X);
        Assert.Equal(50f, commands[1].Points[0].Y);
    }

    [Fact]
    public void Parse_NegativeAndDecimalCoordinates_ParsesCorrectly()
    {
        var commands = PathDataParser.Parse("M -1.5 -2.25 L 3.0 -4");

        Assert.Equal(-1.5f, commands[0].Points[0].X);
        Assert.Equal(-2.25f, commands[0].Points[0].Y);
        Assert.Equal(3.0f, commands[1].Points[0].X);
        Assert.Equal(-4f, commands[1].Points[0].Y);
    }

    [Fact]
    public void Parse_ImplicitRepeatedLineTo_AfterSingleCommandLetter()
    {
        // SVG semantics: "L 10 10 20 20" means two LineTo commands.
        var commands = PathDataParser.Parse("M 0 0 L 10 10 20 20");

        Assert.Equal(3, commands.Count);
        Assert.Equal(PathCommandKind.LineTo, commands[1].Kind);
        Assert.Equal(10f, commands[1].Points[0].X);
        Assert.Equal(PathCommandKind.LineTo, commands[2].Kind);
        Assert.Equal(20f, commands[2].Points[0].X);
        Assert.Equal(20f, commands[2].Points[0].Y);
    }

    [Fact]
    public void Parse_LowercaseCommands_TreatedAsAbsolute()
    {
        // Lowercase (relative) letters are accepted but treated as absolute,
        // matching the spec's "absolute only" constraint without erroring on case.
        var commands = PathDataParser.Parse("m 0 0 l 100 50");

        Assert.Equal(PathCommandKind.MoveTo, commands[0].Kind);
        Assert.Equal(PathCommandKind.LineTo, commands[1].Kind);
        Assert.Equal(100f, commands[1].Points[0].X);
    }

    [Fact]
    public void Parse_ExtraWhitespace_Ignored()
    {
        var commands = PathDataParser.Parse("  M   0    0\tL\n100  50  ");
        Assert.Equal(2, commands.Count);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        Assert.Empty(PathDataParser.Parse(""));
        Assert.Empty(PathDataParser.Parse("   "));
    }

    [Fact]
    public void Parse_UnknownCommand_ThrowsWithCommandAndPosition()
    {
        var ex = Assert.Throws<PathParseException>(() => PathDataParser.Parse("M 0 0 X 1 1"));
        Assert.Contains("'X'", ex.Message);
        Assert.Contains("position", ex.Message);
    }

    [Fact]
    public void Parse_MissingCoordinate_ThrowsWithCommand()
    {
        var ex = Assert.Throws<PathParseException>(() => PathDataParser.Parse("M 0 0 L 10"));
        Assert.Contains("'L'", ex.Message);
    }

    [Fact]
    public void Parse_DataBeforeFirstCommand_Throws()
    {
        var ex = Assert.Throws<PathParseException>(() => PathDataParser.Parse("10 20 L 30 40"));
        Assert.Contains("position", ex.Message);
    }

    [Fact]
    public void Parse_NullArgument_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => PathDataParser.Parse(null!));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~PathDataParserTests"`
Expected: BUILD FAILURE — `PathDataParser`, `PathCommandKind`, `PathParseException` not defined.

- [ ] **Step 3: Implement PathDataParser**

Create `src/FlexRender.Core/Parsing/PathDataParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlexRender.Parsing;

/// <summary>
/// The kind of a parsed path command.
/// </summary>
public enum PathCommandKind
{
    /// <summary>Move the pen to a point (M).</summary>
    MoveTo,

    /// <summary>Draw a straight line to a point (L).</summary>
    LineTo,

    /// <summary>Draw a quadratic Bézier curve (Q): one control point, one end point.</summary>
    QuadTo,

    /// <summary>Draw a cubic Bézier curve (C): two control points, one end point.</summary>
    CubicTo,

    /// <summary>Close the current sub-path (Z).</summary>
    Close
}

/// <summary>
/// A 2D point in absolute path coordinates.
/// </summary>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
public readonly record struct PathPoint(float X, float Y);

/// <summary>
/// A single parsed path command with its associated points.
/// </summary>
/// <param name="Kind">The command kind.</param>
/// <param name="Points">The command's points (empty for <see cref="PathCommandKind.Close"/>).</param>
public sealed record PathCommand(PathCommandKind Kind, IReadOnlyList<PathPoint> Points);

/// <summary>
/// Thrown when path data ('d' attribute) cannot be parsed.
/// </summary>
public sealed class PathParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PathParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PathParseException(string message) : base(message)
    {
    }
}

/// <summary>
/// Hand-written tokenizer for SVG-style path data restricted to absolute commands
/// M, L, Q, C, Z. AOT-safe — no regex, no backtracking.
/// </summary>
/// <remarks>
/// Lowercase command letters are accepted but treated as absolute (the spec restricts
/// drawing to absolute coordinates). Numbers may be separated by whitespace and/or commas.
/// Implicit repeated commands are supported per SVG semantics (e.g. "L 1 1 2 2" is two
/// line-to commands).
/// </remarks>
public static class PathDataParser
{
    /// <summary>
    /// Parses path data into an ordered list of absolute commands.
    /// </summary>
    /// <param name="data">The path data string (e.g. "M 0 0 L 100 50 Z").</param>
    /// <returns>The parsed commands in order. Empty when <paramref name="data"/> is blank.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="PathParseException">Thrown on malformed input, naming the command and position.</exception>
    public static IReadOnlyList<PathCommand> Parse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var commands = new List<PathCommand>();
        var i = 0;
        var length = data.Length;
        char currentCommand = '\0';

        while (i < length)
        {
            SkipSeparators(data, ref i);
            if (i >= length)
                break;

            var c = data[i];
            var upper = char.ToUpperInvariant(c);

            if (upper is 'M' or 'L' or 'Q' or 'C' or 'Z')
            {
                currentCommand = upper;
                i++;

                if (currentCommand == 'Z')
                {
                    commands.Add(new PathCommand(PathCommandKind.Close, Array.Empty<PathPoint>()));
                    currentCommand = '\0';
                }
                continue;
            }

            // Not a command letter: must be a coordinate continuing the current command.
            if (currentCommand == '\0')
            {
                throw new PathParseException(
                    $"Unexpected character '{c}' at position {i}: path data must begin with a command letter (M, L, Q, C, Z).");
            }

            if (!IsCoordinateStart(c))
            {
                throw new PathParseException(
                    $"Unexpected character '{c}' at position {i} while reading command '{currentCommand}'.");
            }

            var (kind, pointCount) = currentCommand switch
            {
                'M' => (PathCommandKind.MoveTo, 1),
                'L' => (PathCommandKind.LineTo, 1),
                'Q' => (PathCommandKind.QuadTo, 2),
                'C' => (PathCommandKind.CubicTo, 3),
                _ => throw new PathParseException(
                    $"Internal error: unexpected command '{currentCommand}' at position {i}.")
            };

            var points = new PathPoint[pointCount];
            for (var p = 0; p < pointCount; p++)
            {
                var x = ReadNumber(data, ref i, currentCommand);
                var y = ReadNumber(data, ref i, currentCommand);
                points[p] = new PathPoint(x, y);
            }

            commands.Add(new PathCommand(kind, points));

            // After an initial MoveTo, repeated coordinates imply LineTo (SVG semantics).
            if (currentCommand == 'M')
            {
                currentCommand = 'L';
            }
        }

        return commands;
    }

    private static bool IsCoordinateStart(char c)
        => c is '-' or '+' or '.' || (c >= '0' && c <= '9');

    private static void SkipSeparators(string data, ref int i)
    {
        while (i < data.Length)
        {
            var c = data[i];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == ',')
            {
                i++;
                continue;
            }
            break;
        }
    }

    private static float ReadNumber(string data, ref int i, char command)
    {
        SkipSeparators(data, ref i);

        var start = i;
        var length = data.Length;

        if (i < length && (data[i] == '-' || data[i] == '+'))
            i++;

        var hasDigits = false;
        while (i < length && data[i] >= '0' && data[i] <= '9')
        {
            i++;
            hasDigits = true;
        }

        if (i < length && data[i] == '.')
        {
            i++;
            while (i < length && data[i] >= '0' && data[i] <= '9')
            {
                i++;
                hasDigits = true;
            }
        }

        // Exponent (e.g. 1e3, 2.5E-2)
        if (i < length && (data[i] == 'e' || data[i] == 'E'))
        {
            i++;
            if (i < length && (data[i] == '-' || data[i] == '+'))
                i++;
            while (i < length && data[i] >= '0' && data[i] <= '9')
                i++;
        }

        if (!hasDigits)
        {
            throw new PathParseException(
                $"Expected a number at position {start} while reading command '{command}'.");
        }

        var span = data.AsSpan(start, i - start);
        if (!float.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new PathParseException(
                $"Invalid number '{span.ToString()}' at position {start} while reading command '{command}'.");
        }

        return value;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~PathDataParserTests"`
Expected: PASS (12 test cases).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/PathDataParser.cs tests/FlexRender.Tests/Parsing/PathDataParserTests.cs
git commit -m "feat(parser): add hand-written absolute-only path data tokenizer"
```

---

## Task 4: RectElement AST class

**Files:**
- Create: `src/FlexRender.Core/Parsing/Ast/RectElement.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/RectElementTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Ast/RectElementTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="RectElement"/> AST class.
/// </summary>
public sealed class RectElementTests
{
    [Fact]
    public void Type_IsRect()
    {
        var rect = new RectElement();
        Assert.Equal(ElementType.Rect, rect.Type);
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var rect = new RectElement();
        Assert.Null(rect.Fill.Value);
        Assert.Null(rect.Stroke.Value);
        Assert.Equal(0f, rect.StrokeWidth.Value);
        Assert.Null(rect.Radius.Value);
    }

    [Fact]
    public void CloneWithSubstitution_CopiesShapeProperties()
    {
        var rect = new RectElement
        {
            Fill = "#4A90D9",
            Stroke = "#333333",
            StrokeWidth = 2f,
            Radius = "4",
            Width = "100",
            Height = "50"
        };

        var clone = (RectElement)rect.CloneWithSubstitution(s => s);

        Assert.Equal("#4A90D9", clone.Fill.Value);
        Assert.Equal("#333333", clone.Stroke.Value);
        Assert.Equal(2f, clone.StrokeWidth.Value);
        Assert.Equal("4", clone.Radius.Value);
        Assert.Equal("100", clone.Width.Value);
        Assert.Equal("50", clone.Height.Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~RectElementTests"`
Expected: BUILD FAILURE — `RectElement` not defined.

- [ ] **Step 3: Implement RectElement**

Create `src/FlexRender.Core/Parsing/Ast/RectElement.cs`:

```csharp
using System;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A rectangle shape element. Participates in flex layout as a box with explicit
/// width/height and is drawn as a filled and/or stroked rectangle with optional rounded corners.
/// </summary>
public sealed class RectElement : TemplateElement
{
    /// <inheritdoc/>
    public override ElementType Type => ElementType.Rect;

    /// <summary>
    /// Fill: a solid color (e.g. "#4A90D9") or a gradient string produced from the YAML
    /// gradient object form (a CSS-style "linear-gradient(...)" / "radial-gradient(...)").
    /// Null means no fill.
    /// </summary>
    public ExprValue<string> Fill { get; set; }

    /// <summary>Stroke color in hex format. Null means no stroke.</summary>
    public ExprValue<string> Stroke { get; set; }

    /// <summary>Stroke width in pixels. Zero means no stroke.</summary>
    public ExprValue<float> StrokeWidth { get; set; }

    /// <summary>Corner radius (px, em). Null means square corners.</summary>
    public ExprValue<string> Radius { get; set; }

    /// <inheritdoc />
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new RectElement
        {
            Fill = substitutor(Fill.Value)!,
            Stroke = Stroke,
            StrokeWidth = StrokeWidth,
            Radius = Radius
        };
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Fill = Fill.Resolve(resolver, data);
        Stroke = Stroke.Resolve(resolver, data);
        StrokeWidth = StrokeWidth.Resolve(resolver, data);
        Radius = Radius.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Fill = Fill.Materialize(nameof(Fill));
        Stroke = Stroke.Materialize(nameof(Stroke), ValueKind.Color);
        StrokeWidth = StrokeWidth.Materialize(nameof(StrokeWidth));
        Radius = Radius.Materialize(nameof(Radius), ValueKind.Size);
    }
}
```

Note: `Fill` is materialized with `ValueKind.Any` (not `Color`) because it may hold a gradient string.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~RectElementTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/RectElement.cs tests/FlexRender.Tests/Parsing/Ast/RectElementTests.cs
git commit -m "feat(ast): add RectElement shape"
```

---

## Task 5: CircleElement AST class

**Files:**
- Create: `src/FlexRender.Core/Parsing/Ast/CircleElement.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/CircleElementTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Ast/CircleElementTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="CircleElement"/> AST class.
/// </summary>
public sealed class CircleElementTests
{
    [Fact]
    public void Type_IsCircle()
    {
        var circle = new CircleElement();
        Assert.Equal(ElementType.Circle, circle.Type);
    }

    [Fact]
    public void CloneWithSubstitution_CopiesShapeProperties()
    {
        var circle = new CircleElement
        {
            Fill = "#e74c3c",
            Stroke = "#000000",
            StrokeWidth = 1.5f,
            Width = "40",
            Height = "40"
        };

        var clone = (CircleElement)circle.CloneWithSubstitution(s => s);

        Assert.Equal("#e74c3c", clone.Fill.Value);
        Assert.Equal("#000000", clone.Stroke.Value);
        Assert.Equal(1.5f, clone.StrokeWidth.Value);
        Assert.Equal("40", clone.Width.Value);
        Assert.Equal("40", clone.Height.Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~CircleElementTests"`
Expected: BUILD FAILURE — `CircleElement` not defined.

- [ ] **Step 3: Implement CircleElement**

Create `src/FlexRender.Core/Parsing/Ast/CircleElement.cs`:

```csharp
using System;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A circle shape element. Participates in flex layout as a square box (the YAML 'size'
/// shorthand sets both Width and Height) and is drawn as a filled and/or stroked circle
/// inscribed in the box. When Width and Height differ, the smaller dimension is used as the diameter.
/// </summary>
public sealed class CircleElement : TemplateElement
{
    /// <inheritdoc/>
    public override ElementType Type => ElementType.Circle;

    /// <summary>
    /// Fill: a solid color (e.g. "#e74c3c") or a gradient string produced from the YAML
    /// gradient object form. Null means no fill.
    /// </summary>
    public ExprValue<string> Fill { get; set; }

    /// <summary>Stroke color in hex format. Null means no stroke.</summary>
    public ExprValue<string> Stroke { get; set; }

    /// <summary>Stroke width in pixels. Zero means no stroke.</summary>
    public ExprValue<float> StrokeWidth { get; set; }

    /// <inheritdoc />
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new CircleElement
        {
            Fill = substitutor(Fill.Value)!,
            Stroke = Stroke,
            StrokeWidth = StrokeWidth
        };
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Fill = Fill.Resolve(resolver, data);
        Stroke = Stroke.Resolve(resolver, data);
        StrokeWidth = StrokeWidth.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Fill = Fill.Materialize(nameof(Fill));
        Stroke = Stroke.Materialize(nameof(Stroke), ValueKind.Color);
        StrokeWidth = StrokeWidth.Materialize(nameof(StrokeWidth));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~CircleElementTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/CircleElement.cs tests/FlexRender.Tests/Parsing/Ast/CircleElementTests.cs
git commit -m "feat(ast): add CircleElement shape"
```

---

## Task 6: EllipseElement AST class

**Files:**
- Create: `src/FlexRender.Core/Parsing/Ast/EllipseElement.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/EllipseElementTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Ast/EllipseElementTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="EllipseElement"/> AST class.
/// </summary>
public sealed class EllipseElementTests
{
    [Fact]
    public void Type_IsEllipse()
    {
        var ellipse = new EllipseElement();
        Assert.Equal(ElementType.Ellipse, ellipse.Type);
    }

    [Fact]
    public void CloneWithSubstitution_CopiesShapeProperties()
    {
        var ellipse = new EllipseElement
        {
            Fill = "#2ecc71",
            Stroke = "#111111",
            StrokeWidth = 3f,
            Width = "120",
            Height = "60"
        };

        var clone = (EllipseElement)ellipse.CloneWithSubstitution(s => s);

        Assert.Equal("#2ecc71", clone.Fill.Value);
        Assert.Equal("#111111", clone.Stroke.Value);
        Assert.Equal(3f, clone.StrokeWidth.Value);
        Assert.Equal("120", clone.Width.Value);
        Assert.Equal("60", clone.Height.Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~EllipseElementTests"`
Expected: BUILD FAILURE — `EllipseElement` not defined.

- [ ] **Step 3: Implement EllipseElement**

Create `src/FlexRender.Core/Parsing/Ast/EllipseElement.cs`:

```csharp
using System;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// An ellipse shape element. Participates in flex layout as a box with explicit
/// width/height and is drawn as a filled and/or stroked ellipse inscribed in the box.
/// </summary>
public sealed class EllipseElement : TemplateElement
{
    /// <inheritdoc/>
    public override ElementType Type => ElementType.Ellipse;

    /// <summary>
    /// Fill: a solid color or a gradient string produced from the YAML gradient object form.
    /// Null means no fill.
    /// </summary>
    public ExprValue<string> Fill { get; set; }

    /// <summary>Stroke color in hex format. Null means no stroke.</summary>
    public ExprValue<string> Stroke { get; set; }

    /// <summary>Stroke width in pixels. Zero means no stroke.</summary>
    public ExprValue<float> StrokeWidth { get; set; }

    /// <inheritdoc />
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new EllipseElement
        {
            Fill = substitutor(Fill.Value)!,
            Stroke = Stroke,
            StrokeWidth = StrokeWidth
        };
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Fill = Fill.Resolve(resolver, data);
        Stroke = Stroke.Resolve(resolver, data);
        StrokeWidth = StrokeWidth.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Fill = Fill.Materialize(nameof(Fill));
        Stroke = Stroke.Materialize(nameof(Stroke), ValueKind.Color);
        StrokeWidth = StrokeWidth.Materialize(nameof(StrokeWidth));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~EllipseElementTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/EllipseElement.cs tests/FlexRender.Tests/Parsing/Ast/EllipseElementTests.cs
git commit -m "feat(ast): add EllipseElement shape"
```

---

## Task 7: DrawShapes DTOs

The `draw` element holds a list of absolute-coordinate shapes. These are renderer-agnostic
immutable DTOs in Core. The pre-parsed path commands are stored on the `DrawPath` DTO so the
renderer never re-parses.

**Files:**
- Create: `src/FlexRender.Core/Parsing/Ast/DrawShapes.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/DrawShapesTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Ast/DrawShapesTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the immutable draw-shape DTOs.
/// </summary>
public sealed class DrawShapesTests
{
    [Fact]
    public void DrawLine_StoresCoordinatesAndStroke()
    {
        var line = new DrawLine(0f, 100f, 400f, 50f, "#333333", 2f);
        Assert.Equal(0f, line.X1);
        Assert.Equal(100f, line.Y1);
        Assert.Equal(400f, line.X2);
        Assert.Equal(50f, line.Y2);
        Assert.Equal("#333333", line.Stroke);
        Assert.Equal(2f, line.StrokeWidth);
    }

    [Fact]
    public void DrawPolyline_StoresPointsAndStroke()
    {
        var points = new List<PathPoint> { new(0f, 10f), new(50f, 40f) };
        var polyline = new DrawPolyline(points, "#4A90D9", 1f, fill: null);
        Assert.Equal(2, polyline.Points.Count);
        Assert.Equal("#4A90D9", polyline.Stroke);
    }

    [Fact]
    public void DrawRect_StoresGeometryFillStrokeRadius()
    {
        var rect = new DrawRect(10f, 10f, 80f, 40f, "#eeeeee", stroke: null, strokeWidth: 0f, radius: 4f);
        Assert.Equal(10f, rect.X);
        Assert.Equal(80f, rect.Width);
        Assert.Equal("#eeeeee", rect.Fill);
        Assert.Equal(4f, rect.Radius);
    }

    [Fact]
    public void DrawCircle_StoresCenterRadiusFill()
    {
        var circle = new DrawCircle(200f, 75f, 30f, "#e74c3c", stroke: null, strokeWidth: 0f);
        Assert.Equal(200f, circle.Cx);
        Assert.Equal(75f, circle.Cy);
        Assert.Equal(30f, circle.R);
        Assert.Equal("#e74c3c", circle.Fill);
    }

    [Fact]
    public void DrawPath_StoresCommandsFillStroke()
    {
        var commands = PathDataParser.Parse("M 0 0 L 100 50 Z");
        var path = new DrawPath(commands, "#2ecc71", stroke: null, strokeWidth: 0f);
        Assert.Equal(3, path.Commands.Count);
        Assert.Equal("#2ecc71", path.Fill);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~DrawShapesTests"`
Expected: BUILD FAILURE — `DrawLine`, `DrawPolyline`, `DrawRect`, `DrawCircle`, `DrawPath`, `DrawShape` not defined.

- [ ] **Step 3: Implement DrawShapes**

Create `src/FlexRender.Core/Parsing/Ast/DrawShapes.cs`:

```csharp
using System.Collections.Generic;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Base type for a single shape inside a <see cref="DrawElement"/>.
/// All coordinates are absolute, relative to the draw element's top-left corner.
/// </summary>
public abstract record DrawShape;

/// <summary>
/// A straight line segment.
/// </summary>
/// <param name="X1">Start X.</param>
/// <param name="Y1">Start Y.</param>
/// <param name="X2">End X.</param>
/// <param name="Y2">End Y.</param>
/// <param name="Stroke">Stroke color (hex). Null means default black.</param>
/// <param name="StrokeWidth">Stroke width in pixels.</param>
public sealed record DrawLine(
    float X1, float Y1, float X2, float Y2, string? Stroke, float StrokeWidth) : DrawShape;

/// <summary>
/// A connected sequence of line segments through the given points.
/// </summary>
/// <param name="Points">The vertices in order.</param>
/// <param name="Stroke">Stroke color (hex). Null means default black.</param>
/// <param name="StrokeWidth">Stroke width in pixels.</param>
/// <param name="Fill">Optional fill color (hex) for the enclosed area. Null means no fill.</param>
public sealed record DrawPolyline(
    IReadOnlyList<PathPoint> Points, string? Stroke, float StrokeWidth, string? Fill) : DrawShape;

/// <summary>
/// A rectangle, optionally rounded.
/// </summary>
/// <param name="X">Top-left X.</param>
/// <param name="Y">Top-left Y.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Fill">Optional fill color (hex). Null means no fill.</param>
/// <param name="Stroke">Optional stroke color (hex). Null means no stroke.</param>
/// <param name="StrokeWidth">Stroke width in pixels.</param>
/// <param name="Radius">Corner radius in pixels.</param>
public sealed record DrawRect(
    float X, float Y, float Width, float Height,
    string? Fill, string? Stroke, float StrokeWidth, float Radius) : DrawShape;

/// <summary>
/// A circle centred at (Cx, Cy) with radius R.
/// </summary>
/// <param name="Cx">Centre X.</param>
/// <param name="Cy">Centre Y.</param>
/// <param name="R">Radius in pixels.</param>
/// <param name="Fill">Optional fill color (hex). Null means no fill.</param>
/// <param name="Stroke">Optional stroke color (hex). Null means no stroke.</param>
/// <param name="StrokeWidth">Stroke width in pixels.</param>
public sealed record DrawCircle(
    float Cx, float Cy, float R,
    string? Fill, string? Stroke, float StrokeWidth) : DrawShape;

/// <summary>
/// A free-form path made of pre-parsed absolute commands.
/// </summary>
/// <param name="Commands">The parsed path commands (M/L/Q/C/Z).</param>
/// <param name="Fill">Optional fill color (hex). Null means no fill.</param>
/// <param name="Stroke">Optional stroke color (hex). Null means no stroke.</param>
/// <param name="StrokeWidth">Stroke width in pixels.</param>
public sealed record DrawPath(
    IReadOnlyList<PathCommand> Commands,
    string? Fill, string? Stroke, float StrokeWidth) : DrawShape;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~DrawShapesTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/DrawShapes.cs tests/FlexRender.Tests/Parsing/Ast/DrawShapesTests.cs
git commit -m "feat(ast): add draw shape DTOs"
```

---

## Task 8: DrawElement AST class

**Files:**
- Create: `src/FlexRender.Core/Parsing/Ast/DrawElement.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/DrawElementTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Ast/DrawElementTests.cs`:

```csharp
using System.Collections.Generic;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for the <see cref="DrawElement"/> AST class.
/// </summary>
public sealed class DrawElementTests
{
    [Fact]
    public void Type_IsDraw()
    {
        var draw = new DrawElement(new List<DrawShape>());
        Assert.Equal(ElementType.Draw, draw.Type);
    }

    [Fact]
    public void Shapes_AreExposedInOrder()
    {
        var shapes = new List<DrawShape>
        {
            new DrawLine(0f, 0f, 10f, 10f, "#000000", 1f),
            new DrawCircle(5f, 5f, 3f, "#ff0000", null, 0f)
        };
        var draw = new DrawElement(shapes) { Width = "400", Height = "200" };

        Assert.Equal(2, draw.Shapes.Count);
        Assert.IsType<DrawLine>(draw.Shapes[0]);
        Assert.IsType<DrawCircle>(draw.Shapes[1]);
        Assert.Equal("400", draw.Width.Value);
    }

    [Fact]
    public void CloneWithSubstitution_PreservesShapes()
    {
        var shapes = new List<DrawShape> { new DrawLine(0f, 0f, 10f, 10f, "#000000", 1f) };
        var draw = new DrawElement(shapes) { Width = "400", Height = "200" };

        var clone = (DrawElement)draw.CloneWithSubstitution(s => s);

        Assert.Single(clone.Shapes);
        Assert.Equal("400", clone.Width.Value);
        Assert.Equal("200", clone.Height.Value);
    }

    [Fact]
    public void Constructor_NullShapes_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new DrawElement(null!));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~DrawElementTests"`
Expected: BUILD FAILURE — `DrawElement` not defined.

- [ ] **Step 3: Implement DrawElement**

Create `src/FlexRender.Core/Parsing/Ast/DrawElement.cs`:

```csharp
using System;
using System.Collections.Generic;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// A free-form drawing element. Participates in flex layout as a box with explicit
/// width/height; inside, an ordered list of absolute-coordinate shapes is painted
/// in list order (painter's algorithm).
/// </summary>
/// <remarks>
/// Shapes use absolute coordinates relative to the element's top-left corner.
/// The shape list is fixed at parse time and is not expression-resolvable.
/// </remarks>
public sealed class DrawElement : TemplateElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawElement"/> class.
    /// </summary>
    /// <param name="shapes">The ordered shapes to paint.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shapes"/> is null.</exception>
    public DrawElement(IReadOnlyList<DrawShape> shapes)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        Shapes = shapes;
    }

    /// <inheritdoc/>
    public override ElementType Type => ElementType.Draw;

    /// <summary>
    /// The ordered list of shapes painted inside this element.
    /// </summary>
    public IReadOnlyList<DrawShape> Shapes { get; }

    /// <inheritdoc />
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new DrawElement(Shapes);
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }
}
```

Note: `DrawElement` does not override `ResolveExpressions`/`Materialize` because it has no
`ExprValue` properties of its own beyond the base; the base implementations suffice.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~DrawElementTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/DrawElement.cs tests/FlexRender.Tests/Parsing/Ast/DrawElementTests.cs
git commit -m "feat(ast): add DrawElement"
```

---

## Task 9: Layout for shape leaf elements (intrinsic + layout)

Shapes are leaf boxes like `SeparatorElement`. They need intrinsic measurement and layout entries.

**Files:**
- Modify: `src/FlexRender.Core/Layout/IntrinsicMeasurer.cs`
- Modify: `src/FlexRender.Core/Layout/LayoutEngine.cs`
- Test: `tests/FlexRender.Tests/Layout/ShapeLayoutTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Layout/ShapeLayoutTests.cs`:

```csharp
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Layout tests for the shape leaf elements (rect, circle, ellipse, draw).
/// </summary>
public sealed class ShapeLayoutTests
{
    private static LayoutEngine CreateEngine() => new(new ResourceLimits());

    [Fact]
    public void Rect_WithExplicitSize_ProducesThatSize()
    {
        var rect = new RectElement { Width = "100", Height = "50", Fill = "#ff0000" };
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300, Fixed = FixedDimension.Width }
        };
        template.AddElement(rect);

        var engine = CreateEngine();
        var root = engine.ComputeLayout(template);
        var node = root.Children[0];

        Assert.Equal(100f, node.Width);
        Assert.Equal(50f, node.Height);
    }

    [Fact]
    public void Draw_WithExplicitSize_ProducesThatSize()
    {
        var draw = new DrawElement(new[] { (DrawShape)new DrawLine(0f, 0f, 10f, 10f, "#000", 1f) })
        {
            Width = "400",
            Height = "200"
        };
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400, Fixed = FixedDimension.Width }
        };
        template.AddElement(draw);

        var engine = CreateEngine();
        var root = engine.ComputeLayout(template);
        var node = root.Children[0];

        Assert.Equal(400f, node.Width);
        Assert.Equal(200f, node.Height);
    }
}
```

Note: Verify the exact entrypoint name (`ComputeLayout(Template)` returning a root `LayoutNode`)
against `LayoutEngine`; if the public method differs, mirror the call used by existing layout
tests in `tests/FlexRender.Tests/Layout/`. (The two-pass engine wires intrinsics internally for
the `Template` overload.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeLayoutTests"`
Expected: FAIL — shapes fall through to the default leaf branch and produce `ContainerWidth`/default height instead of explicit dimensions.

- [ ] **Step 3a: Add intrinsic measurement**

In `src/FlexRender.Core/Layout/IntrinsicMeasurer.cs`, extend the dispatch switch in
`MeasureIntrinsic` (currently mapping `SeparatorElement` etc.). Add these cases before the
`FlexElement flex =>` case:

```csharp
            RectElement rect => MeasureBoxShapeIntrinsic(rect, rect.Width.Value, rect.Height.Value),
            CircleElement circle => MeasureBoxShapeIntrinsic(circle, circle.Width.Value, circle.Height.Value),
            EllipseElement ellipse => MeasureBoxShapeIntrinsic(ellipse, ellipse.Width.Value, ellipse.Height.Value),
            DrawElement draw => MeasureBoxShapeIntrinsic(draw, draw.Width.Value, draw.Height.Value),
```

Then add this helper method (place it after `MeasureSeparatorIntrinsic`):

```csharp
    /// <summary>
    /// Measures intrinsic size for a leaf shape element (rect, circle, ellipse, draw)
    /// using its explicit width/height. Defaults to zero when a dimension is unspecified.
    /// </summary>
    private static IntrinsicSize MeasureBoxShapeIntrinsic(TemplateElement element, string? width, string? height)
    {
        var w = ParseAbsolutePixelValue(width, 0f);
        var h = ParseAbsolutePixelValue(height, 0f);
        var intrinsic = new IntrinsicSize(w, w, h, h);
        return ApplyPaddingBorderAndMargin(intrinsic, element);
    }
```

- [ ] **Step 3b: Add layout entries**

In `src/FlexRender.Core/Layout/LayoutEngine.cs`, in `LayoutElement`'s `element switch`, add these
cases before the `SeparatorElement separator =>` line:

```csharp
            RectElement rect => LayoutBoxShapeElement(rect, rect.Width.Value, rect.Height.Value, context),
            CircleElement circle => LayoutBoxShapeElement(circle, circle.Width.Value, circle.Height.Value, context),
            EllipseElement ellipse => LayoutBoxShapeElement(ellipse, ellipse.Width.Value, ellipse.Height.Value, context),
            DrawElement draw => LayoutBoxShapeElement(draw, draw.Width.Value, draw.Height.Value, context),
```

Then add this method right after `LayoutSeparatorElement`:

```csharp
    /// <summary>
    /// Lays out a leaf shape element (rect, circle, ellipse, draw) using its explicit
    /// width/height plus padding and border. Width defaults to the container width and
    /// height to zero when unspecified.
    /// </summary>
    private static LayoutNode LayoutBoxShapeElement(TemplateElement element, string? width, string? height, LayoutContext context)
    {
        var padding = PaddingParser.Parse(element.Padding.Value, context.ContainerWidth, context.FontSize).ClampNegatives();
        var border = BorderParser.Resolve(element, context.ContainerWidth, context.FontSize);

        var contentWidth = context.ResolveWidth(width) ?? context.ContainerWidth;
        var contentHeight = context.ResolveHeight(height) ?? 0f;

        var totalWidth = contentWidth + padding.Horizontal + border.Horizontal;
        var totalHeight = contentHeight + padding.Vertical + border.Vertical;

        return new LayoutNode(element, 0, 0, totalWidth, totalHeight) { ComputedFontSize = context.FontSize };
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeLayoutTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Core/Layout/IntrinsicMeasurer.cs src/FlexRender.Core/Layout/LayoutEngine.cs tests/FlexRender.Tests/Layout/ShapeLayoutTests.cs
git commit -m "feat(layout): measure and lay out shape leaf elements"
```

---

## Task 10: Gradient object form -> CSS gradient string converter

The YAML `fill` object form is converted to the existing CSS gradient string so the renderer can
reuse `GradientParser`. This converter lives in `FlexRender.Yaml` as a static helper.

**Files:**
- Create: `src/FlexRender.Yaml/Parsing/ShapeParsers.cs` (start with the gradient converter only)
- Test: `tests/FlexRender.Tests/Parsing/GradientObjectParseTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/GradientObjectParseTests.cs`:

```csharp
using System.IO;
using FlexRender.Parsing;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for converting the YAML gradient object form to a CSS gradient string.
/// </summary>
public sealed class GradientObjectParseTests
{
    private static YamlMappingNode ParseMapping(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    [Fact]
    public void LinearGradient_WithAngleAndColors_ProducesCssString()
    {
        var node = ParseMapping("""
            gradient: linear
            colors: ["#ff0000", "#0000ff"]
            angle: 45
            """);

        var css = ShapeParsers.ConvertGradientObjectToCss(node);

        Assert.Equal("linear-gradient(45deg, #ff0000, #0000ff)", css);
    }

    [Fact]
    public void LinearGradient_WithoutAngle_DefaultsToZeroDeg()
    {
        var node = ParseMapping("""
            gradient: linear
            colors: ["#fff", "#000"]
            """);

        var css = ShapeParsers.ConvertGradientObjectToCss(node);

        Assert.Equal("linear-gradient(0deg, #fff, #000)", css);
    }

    [Fact]
    public void RadialGradient_IgnoresAngle()
    {
        var node = ParseMapping("""
            gradient: radial
            colors: ["#fff", "#000"]
            angle: 90
            """);

        var css = ShapeParsers.ConvertGradientObjectToCss(node);

        Assert.Equal("radial-gradient(#fff, #000)", css);
    }

    [Fact]
    public void Gradient_WithFewerThanTwoColors_Throws()
    {
        var node = ParseMapping("""
            gradient: linear
            colors: ["#fff"]
            """);

        Assert.Throws<TemplateParseException>(() => ShapeParsers.ConvertGradientObjectToCss(node));
    }

    [Fact]
    public void Gradient_WithUnknownType_Throws()
    {
        var node = ParseMapping("""
            gradient: conic
            colors: ["#fff", "#000"]
            """);

        Assert.Throws<TemplateParseException>(() => ShapeParsers.ConvertGradientObjectToCss(node));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~GradientObjectParseTests"`
Expected: BUILD FAILURE — `ShapeParsers.ConvertGradientObjectToCss` not defined.

- [ ] **Step 3: Implement ShapeParsers with the gradient converter**

Create `src/FlexRender.Yaml/Parsing/ShapeParsers.cs`:

```csharp
using System.Globalization;
using System.Text;
using FlexRender.Parsing.Ast;
using YamlDotNet.RepresentationModel;
using static FlexRender.Parsing.YamlPropertyHelpers;

namespace FlexRender.Parsing;

/// <summary>
/// Parsers for shape elements (rect, circle, ellipse, draw), their fill gradients,
/// and the absolute-coordinate draw shapes.
/// </summary>
internal static class ShapeParsers
{
    /// <summary>
    /// Converts a YAML gradient object mapping into FlexRender's CSS-style gradient string
    /// so the existing gradient renderer can consume it.
    /// </summary>
    /// <param name="node">The gradient mapping (keys: gradient, colors, angle).</param>
    /// <returns>A "linear-gradient(...)" or "radial-gradient(...)" string.</returns>
    /// <exception cref="TemplateParseException">Thrown on unknown type or fewer than two colors.</exception>
    internal static string ConvertGradientObjectToCss(YamlMappingNode node)
    {
        var type = (GetStringValue(node, "gradient") ?? "linear").Trim().ToLowerInvariant();

        if (!TryGetSequence(node, "colors", out var colorsNode))
        {
            throw new TemplateParseException("Gradient fill requires a 'colors' list.");
        }

        var colors = new List<string>(colorsNode.Children.Count);
        foreach (var child in colorsNode.Children)
        {
            if (child is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            {
                colors.Add(scalar.Value.Trim());
            }
        }

        if (colors.Count < 2)
        {
            throw new TemplateParseException("Gradient fill requires at least two colors.");
        }

        var joinedColors = string.Join(", ", colors);

        switch (type)
        {
            case "linear":
                var angle = GetFloatValue(node, "angle", 0f);
                var angleStr = angle.ToString(CultureInfo.InvariantCulture);
                return $"linear-gradient({angleStr}deg, {joinedColors})";

            case "radial":
                return $"radial-gradient({joinedColors})";

            default:
                throw new TemplateParseException(
                    $"Unknown gradient type '{type}'. Expected 'linear' or 'radial'.");
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~GradientObjectParseTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Yaml/Parsing/ShapeParsers.cs tests/FlexRender.Tests/Parsing/GradientObjectParseTests.cs
git commit -m "feat(parser): convert gradient object form to css gradient string"
```

---

## Task 11: Parse box shapes (rect/circle/ellipse) + register in parser/KnownProperties

**Files:**
- Modify: `src/FlexRender.Yaml/Parsing/ShapeParsers.cs`
- Modify: `src/FlexRender.Yaml/Parsing/TemplateParser.cs`
- Modify: `src/FlexRender.Yaml/Parsing/KnownProperties.cs`
- Test: `tests/FlexRender.Tests/Parsing/ShapeParserTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/ShapeParserTests.cs`:

```csharp
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing rect/circle/ellipse shape elements from YAML.
/// </summary>
public sealed class ShapeParserTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_Rect_SolidFillStrokeRadius()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: rect
                width: 100
                height: 50
                fill: "#4A90D9"
                stroke: "#333333"
                stroke-width: 2
                radius: 4
            """;

        var template = _parser.Parse(yaml);
        var rect = Assert.IsType<RectElement>(template.Elements[0]);

        Assert.Equal("#4A90D9", rect.Fill.Value);
        Assert.Equal("#333333", rect.Stroke.Value);
        Assert.Equal(2f, rect.StrokeWidth.Value);
        Assert.Equal("4", rect.Radius.Value);
        Assert.Equal("100", rect.Width.Value);
        Assert.Equal("50", rect.Height.Value);
    }

    [Fact]
    public void Parse_Rect_GradientObjectFill_ConvertsToCss()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: rect
                width: 100
                height: 100
                fill:
                  gradient: linear
                  colors: ["#f00", "#00f"]
                  angle: 45
            """;

        var template = _parser.Parse(yaml);
        var rect = Assert.IsType<RectElement>(template.Elements[0]);

        Assert.Equal("linear-gradient(45deg, #f00, #00f)", rect.Fill.Value);
    }

    [Fact]
    public void Parse_Circle_SizeShorthand_SetsWidthAndHeight()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: circle
                size: 40
                fill: "#e74c3c"
            """;

        var template = _parser.Parse(yaml);
        var circle = Assert.IsType<CircleElement>(template.Elements[0]);

        Assert.Equal("40", circle.Width.Value);
        Assert.Equal("40", circle.Height.Value);
        Assert.Equal("#e74c3c", circle.Fill.Value);
    }

    [Fact]
    public void Parse_Ellipse_WidthHeightFill()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: ellipse
                width: 120
                height: 60
                fill: "#2ecc71"
            """;

        var template = _parser.Parse(yaml);
        var ellipse = Assert.IsType<EllipseElement>(template.Elements[0]);

        Assert.Equal("120", ellipse.Width.Value);
        Assert.Equal("60", ellipse.Height.Value);
        Assert.Equal("#2ecc71", ellipse.Fill.Value);
    }

    [Fact]
    public void Parse_Rect_UnknownProperty_SuggestsCorrection()
    {
        var yaml = """
            canvas:
              width: 300
            layout:
              - type: rect
                fil: "#fff"
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("'fil'", ex.Message);
        Assert.Contains("fill", ex.Message);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeParserTests"`
Expected: FAIL — `Unknown element type: 'rect'` (not yet registered).

- [ ] **Step 3a: Add box-shape parsers to ShapeParsers**

Append these methods inside the `ShapeParsers` class in `src/FlexRender.Yaml/Parsing/ShapeParsers.cs`
(after `ConvertGradientObjectToCss`):

```csharp
    /// <summary>
    /// Reads the 'fill' property which may be a solid color string, a gradient string,
    /// or a gradient object mapping. Returns an <see cref="ExprValue{T}"/> string (CSS for gradients).
    /// </summary>
    private static ExprValue<string> ParseFill(YamlMappingNode node)
    {
        var fillKey = new YamlScalarNode("fill");
        if (!node.Children.TryGetValue(fillKey, out var fillNode))
        {
            return default;
        }

        switch (fillNode)
        {
            case YamlScalarNode scalar when scalar.Value is not null:
                return ContainsExpression(scalar.Value)
                    ? ExprValue<string>.Expression(scalar.Value)
                    : scalar.Value;

            case YamlMappingNode mapping:
                return ConvertGradientObjectToCss(mapping);

            default:
                return default;
        }
    }

    /// <summary>
    /// Parses a 'rect' shape element.
    /// </summary>
    /// <param name="node">The YAML mapping for the element.</param>
    /// <returns>The parsed <see cref="RectElement"/>.</returns>
    internal static TemplateElement ParseRectElement(YamlMappingNode node)
    {
        var rect = new RectElement
        {
            Fill = ParseFill(node),
            Stroke = GetExprStringValueOptional(node, "stroke"),
            StrokeWidth = GetExprFloatValue(node, "stroke-width", 0f),
            Radius = GetExprStringValueOptional(node, "radius"),
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background")!,
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, rect);
        return rect;
    }

    /// <summary>
    /// Parses a 'circle' shape element. The 'size' shorthand sets both width and height.
    /// </summary>
    /// <param name="node">The YAML mapping for the element.</param>
    /// <returns>The parsed <see cref="CircleElement"/>.</returns>
    internal static TemplateElement ParseCircleElement(YamlMappingNode node)
    {
        var circle = new CircleElement
        {
            Fill = ParseFill(node),
            Stroke = GetExprStringValueOptional(node, "stroke"),
            StrokeWidth = GetExprFloatValue(node, "stroke-width", 0f),
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background")!,
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, circle);

        // 'size' shorthand: applied after ApplyFlexItemProperties so it overrides width/height.
        var size = GetStringValue(node, "size");
        if (size is not null)
        {
            circle.Width = size;
            circle.Height = size;
        }

        return circle;
    }

    /// <summary>
    /// Parses an 'ellipse' shape element.
    /// </summary>
    /// <param name="node">The YAML mapping for the element.</param>
    /// <returns>The parsed <see cref="EllipseElement"/>.</returns>
    internal static TemplateElement ParseEllipseElement(YamlMappingNode node)
    {
        var ellipse = new EllipseElement
        {
            Fill = ParseFill(node),
            Stroke = GetExprStringValueOptional(node, "stroke"),
            StrokeWidth = GetExprFloatValue(node, "stroke-width", 0f),
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background")!,
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, ellipse);
        return ellipse;
    }
```

Note: `ApplyFlexItemProperties` is `internal static` on `ElementParsers` — confirm it is callable
from `ShapeParsers` (same assembly, same namespace `FlexRender.Parsing`). It is.

- [ ] **Step 3b: Register the parsers in TemplateParser**

In `src/FlexRender.Yaml/Parsing/TemplateParser.cs`, add these entries to the `_elementParsers`
dictionary initializer (after `["content"] = ElementParsers.ParseContentElement`):

```csharp
            ["rect"] = ShapeParsers.ParseRectElement,
            ["circle"] = ShapeParsers.ParseCircleElement,
            ["ellipse"] = ShapeParsers.ParseEllipseElement,
```

(The `draw` entry is added in Task 12.)

- [ ] **Step 3c: Register known properties**

In `src/FlexRender.Yaml/Parsing/KnownProperties.cs`, add three property sets after the `Content`
set:

```csharp
    /// <summary>
    /// Known properties for the 'rect' element type.
    /// </summary>
    internal static readonly HashSet<string> Rect = BuildSet(FlexItemProperties,
    [
        "fill", "stroke", "stroke-width", "radius",
        "background", "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Known properties for the 'circle' element type.
    /// </summary>
    internal static readonly HashSet<string> Circle = BuildSet(FlexItemProperties,
    [
        "fill", "stroke", "stroke-width", "size",
        "background", "rotate", "padding", "margin"
    ]);

    /// <summary>
    /// Known properties for the 'ellipse' element type.
    /// </summary>
    internal static readonly HashSet<string> Ellipse = BuildSet(FlexItemProperties,
    [
        "fill", "stroke", "stroke-width",
        "background", "rotate", "padding", "margin"
    ]);
```

Then add registry entries to the `Registry` dictionary (after `["content"] = Content`):

```csharp
            ["rect"] = Rect,
            ["circle"] = Circle,
            ["ellipse"] = Ellipse,
```

(The `draw` set + registry entry is added in Task 12.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeParserTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Yaml/Parsing/ShapeParsers.cs src/FlexRender.Yaml/Parsing/TemplateParser.cs src/FlexRender.Yaml/Parsing/KnownProperties.cs tests/FlexRender.Tests/Parsing/ShapeParserTests.cs
git commit -m "feat(parser): parse rect, circle, ellipse shapes with gradient fill"
```

---

## Task 12: Parse the draw element + shapes + MaxShapesPerDraw enforcement

**Files:**
- Modify: `src/FlexRender.Yaml/Parsing/ShapeParsers.cs`
- Modify: `src/FlexRender.Yaml/Parsing/TemplateParser.cs`
- Modify: `src/FlexRender.Yaml/Parsing/KnownProperties.cs`
- Test: `tests/FlexRender.Tests/Parsing/DrawParserTests.cs`

The `draw` parser needs the resource limit. `TemplateParser` already holds a `ResourceLimits _limits`
field. Register `draw` via an instance lambda that passes `_limits.MaxShapesPerDraw`.

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/DrawParserTests.cs`:

```csharp
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>
/// Tests for parsing the 'draw' element and its absolute-coordinate shapes.
/// </summary>
public sealed class DrawParserTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_Draw_AllShapeKinds()
    {
        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                height: 200
                shapes:
                  - line: {x1: 0, y1: 100, x2: 400, y2: 50, stroke: "#333", stroke-width: 2}
                  - polyline: {points: [[0, 10], [50, 40], [100, 20]], stroke: "#4A90D9"}
                  - rect: {x: 10, y: 10, width: 80, height: 40, fill: "#eee", radius: 4}
                  - circle: {cx: 200, cy: 75, r: 30, fill: "#e74c3c"}
                  - path: {d: "M 0 0 L 100 50 Q 150 0 200 50 Z", fill: "#2ecc71"}
            """;

        var template = _parser.Parse(yaml);
        var draw = Assert.IsType<DrawElement>(template.Elements[0]);

        Assert.Equal(5, draw.Shapes.Count);

        var line = Assert.IsType<DrawLine>(draw.Shapes[0]);
        Assert.Equal(0f, line.X1);
        Assert.Equal(400f, line.X2);
        Assert.Equal("#333", line.Stroke);
        Assert.Equal(2f, line.StrokeWidth);

        var polyline = Assert.IsType<DrawPolyline>(draw.Shapes[1]);
        Assert.Equal(3, polyline.Points.Count);
        Assert.Equal(50f, polyline.Points[1].X);
        Assert.Equal(40f, polyline.Points[1].Y);

        var rect = Assert.IsType<DrawRect>(draw.Shapes[2]);
        Assert.Equal(80f, rect.Width);
        Assert.Equal("#eee", rect.Fill);
        Assert.Equal(4f, rect.Radius);

        var circle = Assert.IsType<DrawCircle>(draw.Shapes[3]);
        Assert.Equal(200f, circle.Cx);
        Assert.Equal(30f, circle.R);

        var path = Assert.IsType<DrawPath>(draw.Shapes[4]);
        Assert.Equal("#2ecc71", path.Fill);
        Assert.True(path.Commands.Count >= 4);
    }

    [Fact]
    public void Parse_Draw_NoShapes_ProducesEmptyList()
    {
        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                height: 200
            """;

        var template = _parser.Parse(yaml);
        var draw = Assert.IsType<DrawElement>(template.Elements[0]);
        Assert.Empty(draw.Shapes);
    }

    [Fact]
    public void Parse_Draw_MalformedPath_ThrowsWithCommand()
    {
        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                height: 200
                shapes:
                  - path: {d: "M 0 0 X 1 1"}
            """;

        var ex = Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
        Assert.Contains("'X'", ex.Message);
    }

    [Fact]
    public void Parse_Draw_ExceedsShapeLimit_Throws()
    {
        var limits = new ResourceLimits { MaxShapesPerDraw = 2 };
        var parser = new TemplateParser(limits);

        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                height: 200
                shapes:
                  - line: {x1: 0, y1: 0, x2: 1, y2: 1}
                  - line: {x1: 0, y1: 0, x2: 1, y2: 1}
                  - line: {x1: 0, y1: 0, x2: 1, y2: 1}
            """;

        var ex = Assert.Throws<TemplateParseException>(() => parser.Parse(yaml));
        Assert.Contains("shapes", ex.Message);
    }

    [Fact]
    public void Parse_Draw_UnknownProperty_Throws()
    {
        var yaml = """
            canvas:
              width: 400
            layout:
              - type: draw
                width: 400
                shaps: []
            """;

        Assert.Throws<TemplateParseException>(() => _parser.Parse(yaml));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~DrawParserTests"`
Expected: FAIL — `Unknown element type: 'draw'`.

- [ ] **Step 3a: Add the draw parser to ShapeParsers**

Append these methods inside `ShapeParsers` in `src/FlexRender.Yaml/Parsing/ShapeParsers.cs`:

```csharp
    /// <summary>
    /// Parses a 'draw' element and its ordered shape list.
    /// </summary>
    /// <param name="node">The YAML mapping for the element.</param>
    /// <param name="maxShapes">Maximum shapes allowed (resource limit).</param>
    /// <returns>The parsed <see cref="DrawElement"/>.</returns>
    /// <exception cref="TemplateParseException">Thrown on malformed shapes or shape-count overflow.</exception>
    internal static TemplateElement ParseDrawElement(YamlMappingNode node, int maxShapes)
    {
        var shapes = new List<DrawShape>();

        if (TryGetSequence(node, "shapes", out var shapesNode))
        {
            if (shapesNode.Children.Count > maxShapes)
            {
                throw new TemplateParseException(
                    $"Draw element has {shapesNode.Children.Count} shapes, exceeding the maximum of {maxShapes}.");
            }

            foreach (var item in shapesNode.Children)
            {
                if (item is YamlMappingNode shapeMapping)
                {
                    shapes.Add(ParseDrawShape(shapeMapping));
                }
            }
        }

        var draw = new DrawElement(shapes)
        {
            Rotate = GetExprStringValue(node, "rotate", "none"),
            Background = GetStringValue(node, "background")!,
            Padding = GetExprStringValue(node, "padding", "0"),
            Margin = GetExprStringValue(node, "margin", "0")
        };

        ElementParsers.ApplyFlexItemProperties(node, draw);
        return draw;
    }

    /// <summary>
    /// Parses a single shape mapping (one of line/polyline/rect/circle/path).
    /// </summary>
    private static DrawShape ParseDrawShape(YamlMappingNode shapeMapping)
    {
        if (TryGetMapping(shapeMapping, "line", out var lineNode))
            return ParseDrawLine(lineNode);
        if (TryGetMapping(shapeMapping, "polyline", out var polylineNode))
            return ParseDrawPolyline(polylineNode);
        if (TryGetMapping(shapeMapping, "rect", out var rectNode))
            return ParseDrawRect(rectNode);
        if (TryGetMapping(shapeMapping, "circle", out var circleNode))
            return ParseDrawCircle(circleNode);
        if (TryGetMapping(shapeMapping, "path", out var pathNode))
            return ParseDrawPath(pathNode);

        throw new TemplateParseException(
            "Each draw shape must be one of: line, polyline, rect, circle, path.");
    }

    private static DrawShape ParseDrawLine(YamlMappingNode node) => new DrawLine(
        GetFloatValue(node, "x1", 0f),
        GetFloatValue(node, "y1", 0f),
        GetFloatValue(node, "x2", 0f),
        GetFloatValue(node, "y2", 0f),
        GetStringValue(node, "stroke"),
        GetFloatValue(node, "stroke-width", 1f));

    private static DrawShape ParseDrawPolyline(YamlMappingNode node)
    {
        var points = ParsePoints(node);
        return new DrawPolyline(
            points,
            GetStringValue(node, "stroke"),
            GetFloatValue(node, "stroke-width", 1f),
            GetStringValue(node, "fill"));
    }

    private static DrawShape ParseDrawRect(YamlMappingNode node) => new DrawRect(
        GetFloatValue(node, "x", 0f),
        GetFloatValue(node, "y", 0f),
        GetFloatValue(node, "width", 0f),
        GetFloatValue(node, "height", 0f),
        GetStringValue(node, "fill"),
        GetStringValue(node, "stroke"),
        GetFloatValue(node, "stroke-width", 0f),
        GetFloatValue(node, "radius", 0f));

    private static DrawShape ParseDrawCircle(YamlMappingNode node) => new DrawCircle(
        GetFloatValue(node, "cx", 0f),
        GetFloatValue(node, "cy", 0f),
        GetFloatValue(node, "r", 0f),
        GetStringValue(node, "fill"),
        GetStringValue(node, "stroke"),
        GetFloatValue(node, "stroke-width", 0f));

    private static DrawShape ParseDrawPath(YamlMappingNode node)
    {
        var d = GetStringValue(node, "d") ?? string.Empty;
        IReadOnlyList<PathCommand> commands;
        try
        {
            commands = PathDataParser.Parse(d);
        }
        catch (PathParseException ex)
        {
            throw new TemplateParseException($"Invalid path data: {ex.Message}", ex);
        }

        return new DrawPath(
            commands,
            GetStringValue(node, "fill"),
            GetStringValue(node, "stroke"),
            GetFloatValue(node, "stroke-width", 0f));
    }

    /// <summary>
    /// Parses a 'points' sequence of [x, y] pairs.
    /// </summary>
    private static List<PathPoint> ParsePoints(YamlMappingNode node)
    {
        var points = new List<PathPoint>();
        if (!TryGetSequence(node, "points", out var pointsNode))
            return points;

        foreach (var item in pointsNode.Children)
        {
            if (item is YamlSequenceNode pair && pair.Children.Count >= 2 &&
                pair.Children[0] is YamlScalarNode xs && pair.Children[1] is YamlScalarNode ys &&
                float.TryParse(xs.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(ys.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                points.Add(new PathPoint(x, y));
            }
        }
        return points;
    }
```

Note: `GetFloatValue` and `GetStringValue(node, key)` (the nullable overload) come from
`YamlPropertyHelpers` (already `using static`). `PathPoint`, `PathCommand`, `PathParseException`,
`PathDataParser` are in `FlexRender.Parsing` (same namespace). The `using` directives at the top
of the file already include `System.Globalization` and `FlexRender.Parsing.Ast`.

- [ ] **Step 3b: Register the draw parser**

In `src/FlexRender.Yaml/Parsing/TemplateParser.cs`, add to the `_elementParsers` initializer
(after the `ellipse` entry from Task 11):

```csharp
            ["draw"] = node => ShapeParsers.ParseDrawElement(node, _limits.MaxShapesPerDraw),
```

- [ ] **Step 3c: Register draw known properties**

In `src/FlexRender.Yaml/Parsing/KnownProperties.cs`, add the set (after `Ellipse`):

```csharp
    /// <summary>
    /// Known properties for the 'draw' element type.
    /// </summary>
    internal static readonly HashSet<string> Draw = BuildSet(FlexItemProperties,
    [
        "shapes",
        "background", "rotate", "padding", "margin"
    ]);
```

And the registry entry (after `["ellipse"] = Ellipse`):

```csharp
            ["draw"] = Draw,
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~DrawParserTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Yaml/Parsing/ShapeParsers.cs src/FlexRender.Yaml/Parsing/TemplateParser.cs src/FlexRender.Yaml/Parsing/KnownProperties.cs tests/FlexRender.Tests/Parsing/DrawParserTests.cs
git commit -m "feat(parser): parse draw element with absolute shapes and shape limit"
```

---

## Task 13: Skia rendering — box shapes (rect/circle/ellipse)

**Files:**
- Create: `src/FlexRender.Skia.Render/Rendering/ShapeRenderer.cs`
- Modify: `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs`
- Test: snapshot in Task 15 (rendering wired here; visual verification follows)

This task wires drawing into the existing dispatch. We verify with a non-snapshot unit test that
the shape elements no longer fall through to the default branch (i.e., a render of a rect does not
throw and produces non-background pixels). Snapshot golden images come in Task 15.

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Rendering/ShapeRenderSmokeTests.cs`:

```csharp
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Smoke tests confirming shape elements render visible (non-background) pixels.
/// </summary>
public sealed class ShapeRenderSmokeTests
{
    [Fact]
    public async Task Rect_RendersFilledPixels()
    {
        var yaml = """
            canvas:
              width: 120
              height: 80
              background: "#ffffff"
            layout:
              - type: rect
                width: 100
                height: 50
                fill: "#ff0000"
                margin: "10"
            """;

        var parser = new TemplateParser();
        var template = parser.Parse(yaml);

        using var renderer = new SkiaRenderer(new ResourceLimits(), deterministicRendering: true);
        var size = await renderer.MeasureAsync(template, new ObjectValue());
        using var bitmap = new SKBitmap((int)System.Math.Ceiling(size.Width), (int)System.Math.Ceiling(size.Height), SKColorType.Rgba8888, SKAlphaType.Premul);
        await renderer.Render(bitmap, template, new ObjectValue(), default, default);

        // Center of the rect should be red, not white.
        var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Assert.True(center.Red > 200 && center.Green < 80 && center.Blue < 80,
            $"Expected red center pixel, got {center}.");
    }
}
```

Note: Verify the exact `SkiaRenderer` constructor used by tests. `SnapshotTestBase` constructs it
as `new SkiaRenderer(new ResourceLimits(), new QrProvider(), new BarcodeProvider(), imageLoader: null, deterministicRendering: true)`. If a parameterless-limits-only `SkiaRenderer(ResourceLimits, deterministicRendering:)` overload does not exist, use the same constructor form as `SnapshotTestBase` (pass `new QrProvider(), new BarcodeProvider(), imageLoader: null`).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeRenderSmokeTests"`
Expected: FAIL — rect falls through the renderer's default switch arm and draws nothing; center pixel stays white.

- [ ] **Step 3a: Implement ShapeRenderer (box shapes)**

Create `src/FlexRender.Skia.Render/Rendering/ShapeRenderer.cs`:

```csharp
using FlexRender.Parsing.Ast;
using SkiaSharp;

namespace FlexRender.Rendering;

/// <summary>
/// Draws shape elements (rect, circle, ellipse) and the draw element's absolute shapes
/// onto an <see cref="SKCanvas"/>.
/// </summary>
internal static class ShapeRenderer
{
    /// <summary>
    /// Draws a rectangle shape (fill and/or stroke, optional rounded corners).
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="rect">The rectangle element.</param>
    /// <param name="x">Box X.</param>
    /// <param name="y">Box Y.</param>
    /// <param name="width">Box width.</param>
    /// <param name="height">Box height.</param>
    /// <param name="fontSize">Effective font size for em-relative radius.</param>
    /// <param name="antialias">Whether to antialias.</param>
    internal static void DrawRect(SKCanvas canvas, RectElement rect, float x, float y, float width, float height, float fontSize, bool antialias)
    {
        var radius = ResolveRadius(rect.Radius.Value, fontSize);

        if (!string.IsNullOrEmpty(rect.Fill.Value))
        {
            using var fillPaint = CreateFillPaint(rect.Fill.Value, x, y, width, height, antialias);
            if (fillPaint is not null)
            {
                DrawRoundOrSharpRect(canvas, x, y, width, height, radius, fillPaint);
            }
        }

        if (HasStroke(rect.Stroke.Value, rect.StrokeWidth.Value))
        {
            using var strokePaint = CreateStrokePaint(rect.Stroke.Value!, rect.StrokeWidth.Value, antialias);
            DrawRoundOrSharpRect(canvas, x, y, width, height, radius, strokePaint);
        }
    }

    /// <summary>
    /// Draws a circle shape inscribed in the box (diameter = min(width, height)).
    /// </summary>
    internal static void DrawCircle(SKCanvas canvas, CircleElement circle, float x, float y, float width, float height, bool antialias)
    {
        var diameter = System.Math.Min(width, height);
        var r = diameter / 2f;
        var cx = x + width / 2f;
        var cy = y + height / 2f;

        if (!string.IsNullOrEmpty(circle.Fill.Value))
        {
            using var fillPaint = CreateFillPaint(circle.Fill.Value, x, y, width, height, antialias);
            if (fillPaint is not null)
                canvas.DrawCircle(cx, cy, r, fillPaint);
        }

        if (HasStroke(circle.Stroke.Value, circle.StrokeWidth.Value))
        {
            using var strokePaint = CreateStrokePaint(circle.Stroke.Value!, circle.StrokeWidth.Value, antialias);
            canvas.DrawCircle(cx, cy, r, strokePaint);
        }
    }

    /// <summary>
    /// Draws an ellipse shape inscribed in the box.
    /// </summary>
    internal static void DrawEllipse(SKCanvas canvas, EllipseElement ellipse, float x, float y, float width, float height, bool antialias)
    {
        var cx = x + width / 2f;
        var cy = y + height / 2f;
        var rx = width / 2f;
        var ry = height / 2f;

        if (!string.IsNullOrEmpty(ellipse.Fill.Value))
        {
            using var fillPaint = CreateFillPaint(ellipse.Fill.Value, x, y, width, height, antialias);
            if (fillPaint is not null)
                canvas.DrawOval(cx, cy, rx, ry, fillPaint);
        }

        if (HasStroke(ellipse.Stroke.Value, ellipse.StrokeWidth.Value))
        {
            using var strokePaint = CreateStrokePaint(ellipse.Stroke.Value!, ellipse.StrokeWidth.Value, antialias);
            canvas.DrawOval(cx, cy, rx, ry, strokePaint);
        }
    }

    private static void DrawRoundOrSharpRect(SKCanvas canvas, float x, float y, float width, float height, float radius, SKPaint paint)
    {
        if (radius > 0f)
            canvas.DrawRoundRect(x, y, width, height, radius, radius, paint);
        else
            canvas.DrawRect(x, y, width, height, paint);
    }

    private static bool HasStroke(string? stroke, float strokeWidth)
        => !string.IsNullOrEmpty(stroke) && strokeWidth > 0f;

    private static float ResolveRadius(string? radius, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(radius))
            return 0f;

        var unit = Layout.Units.UnitParser.Parse(radius);
        return unit.Resolve(0f, fontSize) ?? 0f;
    }

    /// <summary>
    /// Creates a fill paint that handles both solid colors and gradient strings.
    /// Returns null when the fill value is empty.
    /// </summary>
    internal static SKPaint? CreateFillPaint(string? fill, float x, float y, float width, float height, bool antialias)
    {
        if (string.IsNullOrEmpty(fill))
            return null;

        if (GradientParser.IsGradient(fill) && GradientParser.TryParse(fill, out var gradient) && gradient is not null)
        {
            var shader = GradientParser.CreateShader(gradient, x, y, width, height);
            if (shader is not null)
            {
                return new SKPaint { Shader = shader, Style = SKPaintStyle.Fill, IsAntialias = antialias };
            }
        }

        return new SKPaint { Color = ColorParser.Parse(fill), Style = SKPaintStyle.Fill, IsAntialias = antialias };
    }

    /// <summary>
    /// Creates a stroke paint with the given color and width.
    /// </summary>
    internal static SKPaint CreateStrokePaint(string stroke, float strokeWidth, bool antialias)
        => new()
        {
            Color = ColorParser.Parse(stroke),
            StrokeWidth = strokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = antialias
        };
}
```

Note: `SKPaint.Shader` must be disposed with the paint. Disposing the `SKPaint` (via `using`) does
not dispose the shader, so explicitly dispose the shader after drawing. To keep the API simple, the
caller uses `using var fillPaint = ...`; the shader leak is avoided by wrapping shader disposal.
Revise `CreateFillPaint`'s gradient branch to attach the shader and document that the caller must
dispose. Implement instead with a paint that owns disposal by using `SKPaint` + manual shader dispose
at call sites is error-prone — so the simplest correct approach: in `CreateFillPaint`, after creating
the shader, set it on the paint and store the shader in the paint only; then at each call site, after
`canvas.DrawX(...)`, call `fillPaint.Shader?.Dispose()` is NOT available. Therefore: keep the shader
local. To do this cleanly, change `CreateFillPaint` gradient branch to:

```csharp
        if (GradientParser.IsGradient(fill) && GradientParser.TryParse(fill, out var gradient) && gradient is not null)
        {
            var shader = GradientParser.CreateShader(gradient, x, y, width, height);
            if (shader is not null)
            {
                var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = antialias };
                paint.Shader = shader; // paint takes a reference; dispose shader after the paint
                return paint;
            }
        }
```

And at each box-shape fill call site, after drawing, dispose the shader explicitly:

```csharp
            using var fillPaint = CreateFillPaint(rect.Fill.Value, x, y, width, height, antialias);
            if (fillPaint is not null)
            {
                DrawRoundOrSharpRect(canvas, x, y, width, height, radius, fillPaint);
                fillPaint.Shader?.Dispose();
            }
```

Apply the same `fillPaint.Shader?.Dispose();` after `canvas.DrawCircle(...)` and
`canvas.DrawOval(...)` in `DrawCircle`/`DrawEllipse`. (For solid-color paints `Shader` is null, so
the call is a safe no-op.)

- [ ] **Step 3b: Dispatch the box shapes in RenderingEngine**

In `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs`, in `DrawElement`'s `switch (element)`,
add cases before the `case SeparatorElement separator:` arm:

```csharp
            case RectElement rect:
                ShapeRenderer.DrawRect(canvas, rect, x, y, width, height, effectiveFontSize, renderOptions.Antialiasing);
                break;

            case CircleElement circle:
                ShapeRenderer.DrawCircle(canvas, circle, x, y, width, height, renderOptions.Antialiasing);
                break;

            case EllipseElement ellipse:
                ShapeRenderer.DrawEllipse(canvas, ellipse, x, y, width, height, renderOptions.Antialiasing);
                break;
```

(The `DrawElement` case is added in Task 14.) Confirm `effectiveFontSize` and
`renderOptions.Antialiasing` are in scope in `DrawElement` — they are (see the existing
`SeparatorElement` arm and `effectiveFontSize` local at the top of `DrawElement`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeRenderSmokeTests"`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/ShapeRenderer.cs src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs tests/FlexRender.Tests/Rendering/ShapeRenderSmokeTests.cs
git commit -m "feat(renderer): draw rect, circle, ellipse shapes via skia"
```

---

## Task 14: Skia rendering — draw element shapes

**Files:**
- Modify: `src/FlexRender.Skia.Render/Rendering/ShapeRenderer.cs`
- Modify: `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs`
- Test: `tests/FlexRender.Tests/Rendering/DrawRenderSmokeTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Rendering/DrawRenderSmokeTests.cs`:

```csharp
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Rendering;
using FlexRender.TemplateEngine;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Smoke tests confirming the draw element paints its shapes.
/// </summary>
public sealed class DrawRenderSmokeTests
{
    [Fact]
    public async Task Draw_FilledCircle_RendersColoredPixels()
    {
        var yaml = """
            canvas:
              width: 200
              height: 150
              background: "#ffffff"
            layout:
              - type: draw
                width: 200
                height: 150
                shapes:
                  - circle: {cx: 100, cy: 75, r: 40, fill: "#0000ff"}
            """;

        var parser = new TemplateParser();
        var template = parser.Parse(yaml);

        using var renderer = new SkiaRenderer(new ResourceLimits(), deterministicRendering: true);
        var size = await renderer.MeasureAsync(template, new ObjectValue());
        using var bitmap = new SKBitmap((int)System.Math.Ceiling(size.Width), (int)System.Math.Ceiling(size.Height), SKColorType.Rgba8888, SKAlphaType.Premul);
        await renderer.Render(bitmap, template, new ObjectValue(), default, default);

        var center = bitmap.GetPixel(100, 75);
        Assert.True(center.Blue > 200 && center.Red < 80 && center.Green < 80,
            $"Expected blue center pixel, got {center}.");
    }
}
```

Note: same `SkiaRenderer` constructor caveat as Task 13.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~DrawRenderSmokeTests"`
Expected: FAIL — `DrawElement` falls through; center stays white.

- [ ] **Step 3a: Add DrawShapes rendering to ShapeRenderer**

Append to `src/FlexRender.Skia.Render/Rendering/ShapeRenderer.cs` (inside the `ShapeRenderer` class).
Add `using FlexRender.Parsing;` at the top of the file (for `PathCommand`/`PathCommandKind`):

```csharp
    /// <summary>
    /// Draws all shapes of a draw element, clipped to the element box and offset to (x, y).
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="draw">The draw element.</param>
    /// <param name="x">Box X (origin for absolute shape coordinates).</param>
    /// <param name="y">Box Y.</param>
    /// <param name="width">Box width.</param>
    /// <param name="height">Box height.</param>
    /// <param name="antialias">Whether to antialias.</param>
    internal static void DrawShapes(SKCanvas canvas, DrawElement draw, float x, float y, float width, float height, bool antialias)
    {
        canvas.Save();
        canvas.ClipRect(new SKRect(x, y, x + width, y + height));
        canvas.Translate(x, y);

        foreach (var shape in draw.Shapes)
        {
            switch (shape)
            {
                case DrawLine line:
                    DrawShapeLine(canvas, line, antialias);
                    break;
                case DrawPolyline polyline:
                    DrawShapePolyline(canvas, polyline, antialias);
                    break;
                case DrawRect rect:
                    DrawShapeRect(canvas, rect, antialias);
                    break;
                case DrawCircle circle:
                    DrawShapeCircle(canvas, circle, antialias);
                    break;
                case DrawPath path:
                    DrawShapePath(canvas, path, antialias);
                    break;
            }
        }

        canvas.Restore();
    }

    private static void DrawShapeLine(SKCanvas canvas, DrawLine line, bool antialias)
    {
        using var paint = CreateStrokePaint(line.Stroke ?? "#000000", line.StrokeWidth, antialias);
        canvas.DrawLine(line.X1, line.Y1, line.X2, line.Y2, paint);
    }

    private static void DrawShapePolyline(SKCanvas canvas, DrawPolyline polyline, bool antialias)
    {
        if (polyline.Points.Count < 2)
            return;

        using var path = new SKPath();
        path.MoveTo(polyline.Points[0].X, polyline.Points[0].Y);
        for (var i = 1; i < polyline.Points.Count; i++)
            path.LineTo(polyline.Points[i].X, polyline.Points[i].Y);

        if (!string.IsNullOrEmpty(polyline.Fill))
        {
            using var fillPaint = new SKPaint { Color = ColorParser.Parse(polyline.Fill), Style = SKPaintStyle.Fill, IsAntialias = antialias };
            canvas.DrawPath(path, fillPaint);
        }

        using var strokePaint = CreateStrokePaint(polyline.Stroke ?? "#000000", polyline.StrokeWidth, antialias);
        canvas.DrawPath(path, strokePaint);
    }

    private static void DrawShapeRect(SKCanvas canvas, DrawRect rect, bool antialias)
    {
        if (!string.IsNullOrEmpty(rect.Fill))
        {
            using var fillPaint = new SKPaint { Color = ColorParser.Parse(rect.Fill), Style = SKPaintStyle.Fill, IsAntialias = antialias };
            DrawRoundOrSharpRect(canvas, rect.X, rect.Y, rect.Width, rect.Height, rect.Radius, fillPaint);
        }

        if (HasStroke(rect.Stroke, rect.StrokeWidth))
        {
            using var strokePaint = CreateStrokePaint(rect.Stroke!, rect.StrokeWidth, antialias);
            DrawRoundOrSharpRect(canvas, rect.X, rect.Y, rect.Width, rect.Height, rect.Radius, strokePaint);
        }
    }

    private static void DrawShapeCircle(SKCanvas canvas, DrawCircle circle, bool antialias)
    {
        if (!string.IsNullOrEmpty(circle.Fill))
        {
            using var fillPaint = new SKPaint { Color = ColorParser.Parse(circle.Fill), Style = SKPaintStyle.Fill, IsAntialias = antialias };
            canvas.DrawCircle(circle.Cx, circle.Cy, circle.R, fillPaint);
        }

        if (HasStroke(circle.Stroke, circle.StrokeWidth))
        {
            using var strokePaint = CreateStrokePaint(circle.Stroke!, circle.StrokeWidth, antialias);
            canvas.DrawCircle(circle.Cx, circle.Cy, circle.R, strokePaint);
        }
    }

    private static void DrawShapePath(SKCanvas canvas, DrawPath drawPath, bool antialias)
    {
        using var path = new SKPath();
        foreach (var command in drawPath.Commands)
        {
            switch (command.Kind)
            {
                case PathCommandKind.MoveTo:
                    path.MoveTo(command.Points[0].X, command.Points[0].Y);
                    break;
                case PathCommandKind.LineTo:
                    path.LineTo(command.Points[0].X, command.Points[0].Y);
                    break;
                case PathCommandKind.QuadTo:
                    path.QuadTo(command.Points[0].X, command.Points[0].Y, command.Points[1].X, command.Points[1].Y);
                    break;
                case PathCommandKind.CubicTo:
                    path.CubicTo(
                        command.Points[0].X, command.Points[0].Y,
                        command.Points[1].X, command.Points[1].Y,
                        command.Points[2].X, command.Points[2].Y);
                    break;
                case PathCommandKind.Close:
                    path.Close();
                    break;
            }
        }

        if (!string.IsNullOrEmpty(drawPath.Fill))
        {
            using var fillPaint = new SKPaint { Color = ColorParser.Parse(drawPath.Fill), Style = SKPaintStyle.Fill, IsAntialias = antialias };
            canvas.DrawPath(path, fillPaint);
        }

        if (HasStroke(drawPath.Stroke, drawPath.StrokeWidth))
        {
            using var strokePaint = CreateStrokePaint(drawPath.Stroke!, drawPath.StrokeWidth, antialias);
            canvas.DrawPath(path, strokePaint);
        }
    }
```

Note: `HasStroke` and `CreateStrokePaint` already exist from Task 13. The `using FlexRender.Parsing.Ast;`
is already at the top of the file from Task 13. Add `using FlexRender.Parsing;` for the path command types.

- [ ] **Step 3b: Dispatch DrawElement in RenderingEngine**

In `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs`, in `DrawElement`'s `switch (element)`,
add this case after the `EllipseElement` arm from Task 13:

```csharp
            case DrawElement drawEl:
                ShapeRenderer.DrawShapes(canvas, drawEl, x, y, width, height, renderOptions.Antialiasing);
                break;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~DrawRenderSmokeTests"`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/ShapeRenderer.cs src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs tests/FlexRender.Tests/Rendering/DrawRenderSmokeTests.cs
git commit -m "feat(renderer): draw line, polyline, rect, circle, path inside draw element"
```

---

## Task 15: Snapshot tests (golden images)

**Files:**
- Create: `tests/FlexRender.Tests/Snapshots/ShapeSnapshotTests.cs`
- Generated: `tests/FlexRender.Tests/Snapshots/golden/shapes_box_basic.png`,
  `tests/FlexRender.Tests/Snapshots/golden/shapes_gradient.png`,
  `tests/FlexRender.Tests/Snapshots/golden/draw_overlap.png`

- [ ] **Step 1: Write the snapshot tests**

Create `tests/FlexRender.Tests/Snapshots/ShapeSnapshotTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Visual snapshot tests for shape primitives and the draw element.
/// Run with <c>UPDATE_SNAPSHOTS=true</c> to regenerate golden images.
/// </summary>
public sealed class ShapeSnapshotTests : SnapshotTestBase
{
    private static Template CreateTemplate(int width, int height)
        => new()
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Both,
                Width = width,
                Height = height,
                Background = "#ffffff"
            }
        };

    [Fact]
    public async Task Shapes_BoxBasic_RectCircleEllipse()
    {
        var template = CreateTemplate(260, 90);

        var yaml = """
            canvas:
              width: 260
              height: 90
              fixed: both
              background: "#ffffff"
            layout:
              - type: flex
                direction: row
                gap: "10"
                padding: "10"
                align: center
                children:
                  - type: rect
                    width: 70
                    height: 50
                    fill: "#4A90D9"
                    stroke: "#1f3a5f"
                    stroke-width: 2
                    radius: 6
                  - type: circle
                    size: 50
                    fill: "#e74c3c"
                  - type: ellipse
                    width: 80
                    height: 50
                    fill: "#2ecc71"
                    stroke: "#145a32"
                    stroke-width: 2
            """;

        var parsed = Parser.Parse(yaml);
        await AssertSnapshot("shapes_box_basic", parsed, new ObjectValue());
    }

    [Fact]
    public async Task Shapes_Gradient_LinearAndRadial()
    {
        var yaml = """
            canvas:
              width: 220
              height: 110
              fixed: both
              background: "#ffffff"
            layout:
              - type: flex
                direction: row
                gap: "10"
                padding: "10"
                children:
                  - type: rect
                    width: 90
                    height: 90
                    fill:
                      gradient: linear
                      colors: ["#ff0000", "#0000ff"]
                      angle: 45
                  - type: circle
                    size: 90
                    fill:
                      gradient: radial
                      colors: ["#ffffff", "#222222"]
            """;

        var parsed = Parser.Parse(yaml);
        await AssertSnapshot("shapes_gradient", parsed, new ObjectValue());
    }

    [Fact]
    public async Task Draw_Overlap_PaintersOrder()
    {
        var yaml = """
            canvas:
              width: 200
              height: 160
              fixed: both
              background: "#ffffff"
            layout:
              - type: draw
                width: 200
                height: 160
                shapes:
                  - rect: {x: 20, y: 20, width: 120, height: 80, fill: "#cccccc", radius: 8}
                  - line: {x1: 0, y1: 80, x2: 200, y2: 40, stroke: "#333333", stroke-width: 3}
                  - polyline: {points: [[10, 140], [60, 110], [110, 130], [160, 100]], stroke: "#4A90D9", stroke-width: 2}
                  - circle: {cx: 130, cy: 70, r: 35, fill: "#e74c3c"}
                  - path: {d: "M 20 150 L 80 110 Q 120 90 160 120 Z", fill: "#2ecc71"}
            """;

        var parsed = Parser.Parse(yaml);
        await AssertSnapshot("draw_overlap", parsed, new ObjectValue());
    }
}
```

Note: `CreateTemplate` is unused if all three tests parse YAML directly; remove it to avoid an
unused-private-method warning (`TreatWarningsAsErrors=true`). Delete the `CreateTemplate` method
from the file before building.

- [ ] **Step 2: Run tests to verify they fail (no golden yet)**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeSnapshotTests"`
Expected: FAIL — "Golden image not found" for all three tests (actual images written to `Snapshots/output/`).

- [ ] **Step 3: Generate golden images**

Run: `UPDATE_SNAPSHOTS=true dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeSnapshotTests"`
Expected: PASS (update mode writes goldens, asserts nothing). Three PNGs appear in
`tests/FlexRender.Tests/Snapshots/golden/`.

- [ ] **Step 4: Re-run without update to verify goldens match**

Run: `dotnet test FlexRender.slnx --filter "FullyQualifiedName~ShapeSnapshotTests"`
Expected: PASS (3 tests).

Manual check: open the three PNGs in `tests/FlexRender.Tests/Snapshots/golden/` and confirm the
shapes look correct (rounded blue rect with border, red circle, green ellipse; gradients smooth;
draw shapes overlap in list order).

- [ ] **Step 5: Commit**

```bash
git add tests/FlexRender.Tests/Snapshots/ShapeSnapshotTests.cs tests/FlexRender.Tests/Snapshots/golden/shapes_box_basic.png tests/FlexRender.Tests/Snapshots/golden/shapes_gradient.png tests/FlexRender.Tests/Snapshots/golden/draw_overlap.png
git commit -m "test(renderer): add shape and draw snapshot golden images"
```

---

## Task 16: Full build + full test suite gate

**Files:** none (verification only).

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build FlexRender.slnx`
Expected: BUILD SUCCEEDED, zero warnings (`TreatWarningsAsErrors=true`).

- [ ] **Step 2: Run the entire test suite**

Run: `dotnet test FlexRender.slnx`
Expected: PASS — all pre-existing tests plus the new ones (1264+ → 1264+ ~ +40). No regressions.

- [ ] **Step 3: If any pre-existing snapshot/test fails**

Investigate; new element enum members or dispatch arms must not change existing rendering.
Do not regenerate unrelated goldens. Fix the cause, re-run.

- [ ] **Step 4: Commit (only if a fix was needed)**

```bash
git add -A
git commit -m "fix: resolve regressions surfaced by full suite after shapes"
```

If no fix was needed, skip this commit.

---

## Task 17: Docs — llms.txt and llms-full.txt

**Files:**
- Modify: `llms.txt`
- Modify: `llms-full.txt`

These are documentation; no test. Mirror the style of the existing `separator`/`svg` entries.

- [ ] **Step 1: Inspect existing element documentation**

Run: `dotnet --version` (no-op placeholder). Then open `llms.txt` and locate the element-type list
and the `separator` description. Open `llms-full.txt` and locate the per-element property reference
(search for "separator").

- [ ] **Step 2: Add shape entries to llms.txt**

In `llms.txt`, in the element-type overview list, add `rect`, `circle`, `ellipse`, `draw` alongside
the existing element types. Add a concise block (matching the file's existing format) such as:

```
- rect/circle/ellipse: shape boxes. Props: fill (color or gradient object), stroke, stroke-width, opacity, radius (rect only), and for circle the `size` shorthand. Participate in flex layout.
- draw: free-form drawing box. Holds `shapes:` (absolute coords): line {x1,y1,x2,y2,stroke,stroke-width}, polyline {points:[[x,y],...],stroke,fill}, rect {x,y,width,height,fill,stroke,radius}, circle {cx,cy,r,fill,stroke}, path {d:"M L Q C Z absolute",fill,stroke}. Max shapes = ResourceLimits.MaxShapesPerDraw (1000). Gradient fill object: {gradient: linear|radial, colors: [..], angle: deg}.
```

Place these entries in the same section and ordering style as the existing elements.

- [ ] **Step 3: Add shape reference to llms-full.txt**

In `llms-full.txt`, add a full per-element subsection for `rect`, `circle`, `ellipse`, and `draw`,
matching the structure used for `separator`/`table`/`svg`. Include:
- Property tables (fill including gradient object form; stroke; stroke-width; opacity; radius for rect; size for circle).
- The `draw` shape grammar for line/polyline/rect/circle/path, noting absolute coordinates and the supported path commands M/L/Q/C/Z (absolute only).
- The `MaxShapesPerDraw` limit (default 1000) in the resource-limits table.
- A YAML example for each element type (use the examples from this plan's Task 11/12 tests).

- [ ] **Step 4: Verify docs build (smoke)**

Run: `dotnet build FlexRender.slnx`
Expected: BUILD SUCCEEDED (docs are text; this just confirms nothing referencing them broke).

- [ ] **Step 5: Commit**

```bash
git add llms.txt llms-full.txt
git commit -m "docs: document rect, circle, ellipse, draw shapes in llms files"
```

---

## Task 18: Docs — wiki Element-Reference and Visual-Reference

**Files:**
- Modify: `docs/wiki/Element-Reference.md`
- Modify: `docs/wiki/Visual-Reference.md`

- [ ] **Step 1: Add element entries to Element-Reference.md**

Open `docs/wiki/Element-Reference.md`. Find the table of contents / element list and the existing
`separator` section. Add four new sections — `rect`, `circle`, `ellipse`, `draw` — each with:
- Description.
- A property table (columns: Property, Type, Default, Description).
- A minimal YAML example.

Property rows for box shapes:

| Property | Type | Default | Description |
|---|---|---|---|
| `fill` | string or object | none | Solid color or gradient object `{gradient, colors, angle}` |
| `stroke` | string | none | Stroke color (hex) |
| `stroke-width` | number | 0 | Stroke width in px |
| `opacity` | number | 1.0 | 0..1, inherited base property |
| `radius` | unit | none | Corner radius (rect only) |
| `size` | unit | none | Sets width and height (circle only) |

For `draw`, document `shapes:` and the five shape kinds with their property keys, noting absolute
coordinates relative to the element box and the supported path commands (M, L, Q, C, Z — absolute only).

- [ ] **Step 2: Add visuals to Visual-Reference.md**

Open `docs/wiki/Visual-Reference.md`. Following the existing pattern (a short caption + a YAML snippet
+ a reference to a rendered image), add entries for shapes and draw. Use the YAML from Task 15's
snapshot tests. If the file references images by path, point to the golden PNGs generated in Task 15
(`tests/FlexRender.Tests/Snapshots/golden/shapes_box_basic.png`, `shapes_gradient.png`,
`draw_overlap.png`) using the repo's documented image-URL convention from AGENTS.md
(`media.githubusercontent.com/.../<path>` for committed binaries). Match whatever convention the
existing entries already use.

- [ ] **Step 3: Commit**

```bash
git add docs/wiki/Element-Reference.md docs/wiki/Visual-Reference.md
git commit -m "docs(wiki): add rect, circle, ellipse, draw to element and visual references"
```

---

## Task 19: Docs — Playground JSON schema + autocomplete

**Files:**
- Modify: `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`
- Modify: `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs` (only if needed)

The schema uses `definitions/element` with an `allOf` list of `if/then` `$ref` branches and an
`enum` of element-type names. New element schemas plug into both.

- [ ] **Step 1: Add the type enum and dispatch branches**

In `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`:

1. In the element `type` enum (the line listing `["text", "flex", ..., "content"]`), add
   `"rect"`, `"circle"`, `"ellipse"`, `"draw"`.

2. In the `allOf` list of `if/then` branches (after the `content` branch), add four branches:

```json
        {
          "if": {
            "properties": { "type": { "const": "rect" } },
            "required": ["type"]
          },
          "then": { "$ref": "#/definitions/rectElement" }
        },
        {
          "if": {
            "properties": { "type": { "const": "circle" } },
            "required": ["type"]
          },
          "then": { "$ref": "#/definitions/circleElement" }
        },
        {
          "if": {
            "properties": { "type": { "const": "ellipse" } },
            "required": ["type"]
          },
          "then": { "$ref": "#/definitions/ellipseElement" }
        },
        {
          "if": {
            "properties": { "type": { "const": "draw" } },
            "required": ["type"]
          },
          "then": { "$ref": "#/definitions/drawElement" }
        }
```

(Match the exact `if/then` shape of the existing branches — confirm whether existing branches use
`"required": ["type"]`; mirror them precisely.)

- [ ] **Step 2: Add the element definitions**

In the same file, under `definitions`, add four definitions following the structure of
`separatorElement` (each uses `allOf` referencing `flexItemProperties` and lists its own properties).
Example for `rectElement`:

```json
    "rectElement": {
      "allOf": [
        { "$ref": "#/definitions/flexItemProperties" }
      ],
      "properties": {
        "type": { "const": "rect" },
        "fill": {
          "description": "Solid color (hex) or gradient object.",
          "oneOf": [
            { "type": "string" },
            { "$ref": "#/definitions/gradientFill" }
          ]
        },
        "stroke": { "type": "string", "description": "Stroke color (hex)." },
        "stroke-width": { "type": "number", "description": "Stroke width in px." },
        "radius": { "type": ["number", "string"], "description": "Corner radius (px/em)." }
      }
    },
    "circleElement": {
      "allOf": [
        { "$ref": "#/definitions/flexItemProperties" }
      ],
      "properties": {
        "type": { "const": "circle" },
        "fill": {
          "oneOf": [
            { "type": "string" },
            { "$ref": "#/definitions/gradientFill" }
          ]
        },
        "stroke": { "type": "string" },
        "stroke-width": { "type": "number" },
        "size": { "type": ["number", "string"], "description": "Sets width and height (diameter)." }
      }
    },
    "ellipseElement": {
      "allOf": [
        { "$ref": "#/definitions/flexItemProperties" }
      ],
      "properties": {
        "type": { "const": "ellipse" },
        "fill": {
          "oneOf": [
            { "type": "string" },
            { "$ref": "#/definitions/gradientFill" }
          ]
        },
        "stroke": { "type": "string" },
        "stroke-width": { "type": "number" }
      }
    },
    "drawElement": {
      "allOf": [
        { "$ref": "#/definitions/flexItemProperties" }
      ],
      "properties": {
        "type": { "const": "draw" },
        "shapes": {
          "type": "array",
          "description": "Ordered absolute-coordinate shapes.",
          "items": { "$ref": "#/definitions/drawShape" }
        }
      }
    },
    "gradientFill": {
      "type": "object",
      "properties": {
        "gradient": { "type": "string", "enum": ["linear", "radial"] },
        "colors": { "type": "array", "items": { "type": "string" }, "minItems": 2 },
        "angle": { "type": "number", "description": "Linear gradient angle in degrees." }
      },
      "required": ["gradient", "colors"]
    },
    "drawShape": {
      "type": "object",
      "properties": {
        "line": {
          "type": "object",
          "properties": {
            "x1": { "type": "number" }, "y1": { "type": "number" },
            "x2": { "type": "number" }, "y2": { "type": "number" },
            "stroke": { "type": "string" }, "stroke-width": { "type": "number" }
          }
        },
        "polyline": {
          "type": "object",
          "properties": {
            "points": { "type": "array", "items": { "type": "array", "items": { "type": "number" }, "minItems": 2, "maxItems": 2 } },
            "stroke": { "type": "string" }, "stroke-width": { "type": "number" }, "fill": { "type": "string" }
          }
        },
        "rect": {
          "type": "object",
          "properties": {
            "x": { "type": "number" }, "y": { "type": "number" },
            "width": { "type": "number" }, "height": { "type": "number" },
            "fill": { "type": "string" }, "stroke": { "type": "string" },
            "stroke-width": { "type": "number" }, "radius": { "type": "number" }
          }
        },
        "circle": {
          "type": "object",
          "properties": {
            "cx": { "type": "number" }, "cy": { "type": "number" }, "r": { "type": "number" },
            "fill": { "type": "string" }, "stroke": { "type": "string" }, "stroke-width": { "type": "number" }
          }
        },
        "path": {
          "type": "object",
          "properties": {
            "d": { "type": "string", "description": "Absolute path data: M L Q C Z." },
            "fill": { "type": "string" }, "stroke": { "type": "string" }, "stroke-width": { "type": "number" }
          }
        }
      }
    }
```

Confirm comma placement so the JSON stays valid. If existing element definitions set
`additionalProperties: false`, mirror that on the new definitions for consistency. (The new shape
elements allow flex-item properties via the `allOf` ref, so do NOT set `additionalProperties: false`
on the box-shape elements unless the existing `separatorElement` does and still validates with the
ref — mirror `separatorElement` exactly.)

- [ ] **Step 3: Validate the JSON parses**

Run: `node -e "JSON.parse(require('fs').readFileSync('src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json','utf8')); console.log('valid')"`
Expected: prints `valid`. (If `node` is unavailable in the sandbox, instead run
`python3 -c "import json;json.load(open('src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json'));print('valid')"`.)

- [ ] **Step 4: Autocomplete (only if needed)**

`yaml-autocomplete.mjs` derives element types and properties from the schema's
`layout.items.$ref` and the element definitions. Open it and confirm it reads element types from the
schema dynamically (the `elementTypes` derivation near the top). If it does, the new schema entries
are picked up automatically and no change is needed — verify by reading the relevant lines. If it
hard-codes any element list or snippet map (e.g. the `element:` snippet object near the bottom),
add `rect`, `circle`, `ellipse`, `draw` there with minimal snippets, e.g.:

```js
        rect: { type: '0', width: '1', height: '2', fill: '3' },
        circle: { type: '0', size: '1', fill: '2' },
        ellipse: { type: '0', width: '1', height: '2', fill: '3' },
        draw: { type: '0', width: '1', height: '2', shapes: '3' },
```

Only edit if a hard-coded list exists; otherwise leave the file unchanged and note "no change needed".

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs
git commit -m "docs(playground): add shape and draw schemas to template json"
```

(If `yaml-autocomplete.mjs` was not modified, omit it from the `git add`.)

---

## Task 20: Docs — template SKILL.md (marketplace repo, conditional)

**Files:**
- Modify (external repo, if present locally): `flexrender/skills/template/SKILL.md`

Per AGENTS.md, the `template` skill lives in the separate `RoboNET/FlexRender-Marketplace` repo and
is NOT part of this checkout (confirmed: no `flexrender/skills/` directory exists here).

- [ ] **Step 1: Check for a local skill file**

Run: `test -f flexrender/skills/template/SKILL.md && echo present || echo absent`
Expected: `absent` (the skill is in the marketplace repo).

- [ ] **Step 2: Record the follow-up if absent**

If absent, no file change is made in this repo. Instead, ensure the PR description (Task 21) lists a
required follow-up: "Update `flexrender/skills/template/SKILL.md` in `RoboNET/FlexRender-Marketplace`
to document the new `rect`/`circle`/`ellipse`/`draw` elements, gradient fill object form, and the
`MaxShapesPerDraw` limit (Element Types + Common Properties sections)."

If present (unexpected), add the new element types, gradient object form, and `draw` shape grammar to
the Element Types and Common Properties sections, then commit:

```bash
git add flexrender/skills/template/SKILL.md
git commit -m "docs(skill): document shapes and draw in template skill"
```

- [ ] **Step 3: No-op confirmation**

If absent, there is nothing to commit for this task. Proceed to Task 21.

---

## Task 21: Final verification and PR

**Files:** none (verification + PR).

- [ ] **Step 1: Clean build**

Run: `dotnet build FlexRender.slnx`
Expected: BUILD SUCCEEDED, zero warnings.

- [ ] **Step 2: Full test suite**

Run: `dotnet test FlexRender.slnx`
Expected: PASS, no regressions.

- [ ] **Step 3: Push the branch**

```bash
git push -u origin feature/charts-and-shapes
```

- [ ] **Step 4: Open the PR**

```bash
gh pr create --base main --head feature/charts-and-shapes \
  --title "feat: shape primitives (rect, circle, ellipse, draw) — Phase 1" \
  --body "Implements Phase 1 (Shapes) of the charts-and-shapes design.

## Added
- Box shapes \`rect\`, \`circle\`, \`ellipse\` (fill/gradient/stroke/opacity; rect radius; circle \`size\` shorthand).
- Gradient fill object form (linear/radial; colors; angle) converted to FlexRender's CSS gradient string.
- \`draw\` element with absolute-coordinate shapes: line, polyline, rect, circle, path (M/L/Q/C/Z, absolute only, hand-written tokenizer, no regex).
- \`ResourceLimits.MaxShapesPerDraw\` (default 1000).
- Parser + KnownProperties (typo suggestions), Skia rendering, layout, unit + snapshot tests.
- Docs: llms.txt, llms-full.txt, wiki Element/Visual references, Playground JSON schema.

## Follow-up (separate repo)
- Update \`flexrender/skills/template/SKILL.md\` in RoboNET/FlexRender-Marketplace for the new elements, gradient object form, and MaxShapesPerDraw.

## Out of scope
Charts (later phases), SVG backend for shapes.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

- [ ] **Step 5: Confirm CI**

Run: `gh pr checks` (after CI starts) — confirm checks pass or report failures.

---

## Self-Review

**1. Spec coverage (Phase 1 scope):**
- `rect`/`circle`/`ellipse` box shapes (fill/gradient/stroke/opacity, rect radius, circle `size`): Tasks 4, 5, 6 (AST), 11 (parse), 13 (render), 15 (snapshot). Opacity is the inherited base property (Tasks reuse `TemplateElement.Opacity`); `stroke-width` parsed in Task 11. ✓
- Gradient fill object form (linear/radial, colors, angle): Task 10 (converter) + Task 11 (wired into fill parsing) + Task 15 (gradient snapshot). ✓
- `draw` element with line/polyline/rect/circle/path, absolute coords, hand-written path tokenizer (no regex): Tasks 3 (tokenizer), 7 (DTOs), 8 (element), 12 (parse), 14 (render), 15 (snapshot). ✓
- `MaxShapesPerDraw` (default 1000): Task 1 (limit) + Task 12 (enforcement test + parser). ✓
- Parser support + KnownProperties typo suggestions: Tasks 11, 12 (registry + `ShapeParserTests.Parse_Rect_UnknownProperty_SuggestsCorrection`). ✓
- Skia rendering: Tasks 13, 14. ✓
- Unit tests (path edge cases, parsing, validation, limits): Tasks 3, 10, 11, 12. Snapshot tests: Task 15. ✓
- Docs (llms.txt, llms-full.txt, Element-Reference, Visual-Reference, SKILL.md, Playground schema + autocomplete): Tasks 17, 18, 19, 20. ✓
- Error handling: malformed `path.d` names offending command (Task 3 tests + Task 12 wrap to `TemplateParseException`); shape-count overflow (Task 12). ✓
- Charts / SVG backend explicitly out of scope. ✓

**2. Placeholder scan:** No "TBD"/"implement later"/"add validation"-style placeholders. Every code
step contains complete, compilable code. The few "verify exact signature" notes (LayoutEngine
entrypoint in Task 9; `SkiaRenderer` constructor in Tasks 13–14; schema branch shape in Task 19)
are explicit verification instructions with a concrete fallback (mirror `SnapshotTestBase` / mirror
`separatorElement`), not missing content.

**3. Type consistency across tasks:**
- `ElementType.Rect/Circle/Ellipse/Draw` defined in Task 2, used in Tasks 4–8.
- `PathCommandKind`, `PathPoint`, `PathCommand`, `PathParseException`, `PathDataParser.Parse` defined
  in Task 3, used in Tasks 7 (DTOs), 12 (parse), 14 (render). Names match exactly.
- `DrawShape`/`DrawLine`/`DrawPolyline`/`DrawRect`/`DrawCircle`/`DrawPath` field names defined in
  Task 7 are consumed identically in Task 12 (construction) and Task 14 (rendering): `DrawLine(X1,Y1,X2,Y2,Stroke,StrokeWidth)`,
  `DrawPolyline(Points,Stroke,StrokeWidth,Fill)`, `DrawRect(X,Y,Width,Height,Fill,Stroke,StrokeWidth,Radius)`,
  `DrawCircle(Cx,Cy,R,Fill,Stroke,StrokeWidth)`, `DrawPath(Commands,Fill,Stroke,StrokeWidth)`. ✓
- `RectElement.Fill/Stroke/StrokeWidth/Radius`, `CircleElement.Fill/Stroke/StrokeWidth`,
  `EllipseElement.Fill/Stroke/StrokeWidth` defined in Tasks 4–6 and used in Tasks 11, 13. ✓
- `ShapeParsers.ConvertGradientObjectToCss` (Task 10) used by `ParseFill` (Task 11). ✓
- `ShapeRenderer.DrawRect/DrawCircle/DrawEllipse/DrawShapes` (Tasks 13–14) dispatched from
  `RenderingEngine.DrawElement` with matching parameter lists. ✓
- `LayoutBoxShapeElement` / `MeasureBoxShapeIntrinsic` (Task 9) names match between definition and
  switch usage. ✓

**Domain checklist holds:** AOT-safe (no reflection/dynamic/Type.GetType; path parsing is a
hand-written tokenizer, no regex); all new concrete classes are `sealed`; DTOs are `sealed record`;
`PathPoint` is a `readonly record struct`; `ArgumentNullException.ThrowIfNull` guards on
`PathDataParser.Parse`, `DrawElement` ctor, and all `CloneWithSubstitution` overrides; new element
types use the existing switch-based dispatch in layout, intrinsic measurement, and rendering; all new
YAML properties registered in `KnownProperties.cs`; XML docs on all public API; `MaxShapesPerDraw`
added (never weakening existing limits).
