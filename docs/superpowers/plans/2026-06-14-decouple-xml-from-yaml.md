# Decouple FlexRender.Xml from FlexRender.Yaml — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `FlexRender.Xml` depend only on `FlexRender.Core` (drop `FlexRender.Yaml` + `YamlDotNet`) by introducing a format-neutral node model and moving the shared template-parsing engine into `FlexRender.Core`.

**Architecture:** A new pure node model (`TemplateMapping`/`TemplateSequence`/`TemplateScalar`) lives in `FlexRender.Core`. The parsing engine (`ElementParsers`, `ChartParsers`, `ShapeParsers`, `KnownProperties`, `NodePropertyHelpers`, and the document-root parse logic) moves into Core, rewritten against the neutral nodes. `FlexRender.Yaml` keeps the public `TemplateParser` facade: it loads YAML via YamlDotNet, converts the YamlDotNet DOM → neutral nodes (`YamlNodeConverter`), then calls the Core engine. `FlexRender.Xml` converts `XDocument` → neutral nodes (`XmlNodeConverter`) and calls the SAME Core engine. YamlDotNet stays only in `FlexRender.Yaml`.

**Tech Stack:** .NET 10, C# latest, xUnit, YamlDotNet (Yaml only), `System.Xml.Linq` (Xml only). AOT-safe, no reflection, `sealed`, file-scoped namespaces, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-Recommended`.

---

## Key facts discovered during exploration (read before starting)

- All engine files in `src/FlexRender.Yaml/Parsing/` currently use namespace **`FlexRender.Parsing`** (NOT a Yaml-specific namespace). `TemplateParseException` and `PathDataParser` **already live in Core** under `FlexRender.Parsing`. Therefore moving the engine files to Core keeps the **same `FlexRender.Parsing` namespace** — no consumer `using` changes, no namespace breakage.
- `ChartParsers` and `ShapeParsers` are `public static` classes. `ChartElement.MaxDataPointsPerSeries` is `internal` in Core and already accessed by `ChartParsers`; Core already has `InternalsVisibleTo("FlexRender.Yaml")`. After the move this access is intra-assembly (Core) — fine.
- The `Charts` types (`ChartType`, `ChartSeries`, `ChartPoint`, `ChartPalette`, `ChartPalettes`, `ChartTheme`, `ChartThemes`, `LegendPosition`, `PieLabelMode`) are in `FlexRender.Charts`, already in Core.
- `ExprValue<T>` and all AST types are in `FlexRender.Parsing.Ast` in Core.
- **Three test files** touch internals that change type:
  - `tests/FlexRender.Tests/Parsing/GradientObjectParseTests.cs` — builds `YamlMappingNode`, calls `ShapeParsers.ConvertGradientObjectToCss(YamlMappingNode)`.
  - `tests/FlexRender.Tests/Parsing/Xml/InternalEntryPointTests.cs` — builds `YamlMappingNode`, calls `TemplateParser.ParseDocumentRootForTests(YamlMappingNode)`.
  - All other parsing tests (incl. `ChartKnownPropertiesTests`, `XmlValidationTests`) call only public `Parse(string)` and stay unchanged.
- `src/FlexRender.Yaml/AssemblyInfo.cs` has `InternalsVisibleTo("FlexRender.Xml")` and `InternalsVisibleTo("FlexRender.Tests")`. After the move, `FlexRender.Xml` no longer needs Yaml internals; `FlexRender.Tests` still needs Yaml internals only for the YAML `TemplateParser` facade's `ParseDocumentRootForTests` — which we relocate to Core. Core needs `InternalsVisibleTo("FlexRender.Tests")` (already present).
- Per-file YAML-DOM token counts (size of mechanical swap): TemplateParser 31, ElementParsers 41, ChartParsers 40, ShapeParsers 27, KnownProperties 3, YamlPropertyHelpers 41.
- Build command: `dotnet build FlexRender.slnx`. Test command: `dotnet test FlexRender.slnx --framework net10.0 --filter "..."`. NEVER pipe dotnet through tail/head/grep. Use `--no-gpg-sign` on commits. Conventional commits, NO attribution lines.

---

## Neutral Node Model (locked type definitions)

Namespace: `FlexRender.Parsing.Nodes`. Location: `src/FlexRender.Core/Parsing/Nodes/`. Zero external dependencies. All `sealed`.

```csharp
// TemplateNode.cs
namespace FlexRender.Parsing.Nodes;

/// <summary>
/// Base type for the format-neutral template node model produced by format parsers
/// (YAML, XML) and consumed by the shared parsing engine. Pure, AOT-safe, no external deps.
/// </summary>
public abstract class TemplateNode
{
}
```

```csharp
// TemplateScalar.cs
namespace FlexRender.Parsing.Nodes;

/// <summary>A leaf node holding a single (possibly null) string value.</summary>
public sealed class TemplateScalar : TemplateNode
{
    /// <summary>Initializes a new scalar with the given value.</summary>
    /// <param name="value">The scalar string value (may be null).</param>
    public TemplateScalar(string? value) => Value = value;

    /// <summary>Gets the scalar string value (may be null).</summary>
    public string? Value { get; }
}
```

```csharp
// TemplateSequence.cs
using System.Collections.Generic;

namespace FlexRender.Parsing.Nodes;

/// <summary>An ordered list of child nodes (the neutral analogue of a YAML sequence).</summary>
public sealed class TemplateSequence : TemplateNode
{
    private readonly List<TemplateNode> _items;

    /// <summary>Initializes an empty sequence.</summary>
    public TemplateSequence() => _items = [];

    /// <summary>Initializes a sequence with a starting capacity.</summary>
    /// <param name="capacity">The initial capacity.</param>
    public TemplateSequence(int capacity) => _items = new List<TemplateNode>(capacity);

    /// <summary>Gets the ordered child items.</summary>
    public IReadOnlyList<TemplateNode> Items => _items;

    /// <summary>Appends a child node to the sequence.</summary>
    /// <param name="node">The node to append.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public void Add(TemplateNode node)
    {
        System.ArgumentNullException.ThrowIfNull(node);
        _items.Add(node);
    }
}
```

```csharp
// TemplateMapping.cs
using System.Collections.Generic;

namespace FlexRender.Parsing.Nodes;

/// <summary>
/// An ordered, string-keyed mapping of child nodes (the neutral analogue of a YAML mapping).
/// Preserves insertion order and supports key lookup and key enumeration for validation.
/// </summary>
public sealed class TemplateMapping : TemplateNode
{
    // Insertion-ordered keys + value lookup. Keys are compared ordinally (YAML/XML keys are case-sensitive).
    private readonly List<string> _keys = [];
    private readonly Dictionary<string, TemplateNode> _values = new(System.StringComparer.Ordinal);

    /// <summary>Gets the keys in insertion order.</summary>
    public IReadOnlyList<string> Keys => _keys;

    /// <summary>
    /// Adds or replaces a child node by key. If the key already exists its value is overwritten
    /// but its position in the key order is preserved (last-wins on value, matching YAML semantics).
    /// </summary>
    /// <param name="key">The child key.</param>
    /// <param name="node">The child node.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="node"/> is null.</exception>
    public void Add(string key, TemplateNode node)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(node);
        if (!_values.ContainsKey(key))
            _keys.Add(key);
        _values[key] = node;
    }

    /// <summary>Tries to get any child node by key.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="node">The child node when found.</param>
    /// <returns>True when the key exists; otherwise false.</returns>
    public bool TryGet(string key, out TemplateNode node) => _values.TryGetValue(key, out node!);

    /// <summary>Tries to get a child mapping by key.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="mapping">The child mapping when the key exists and is a mapping.</param>
    /// <returns>True when the key exists and is a <see cref="TemplateMapping"/>; otherwise false.</returns>
    public bool TryGetMapping(string key, out TemplateMapping mapping)
    {
        if (_values.TryGetValue(key, out var n) && n is TemplateMapping m)
        {
            mapping = m;
            return true;
        }
        mapping = null!;
        return false;
    }

    /// <summary>Tries to get a child sequence by key.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="sequence">The child sequence when the key exists and is a sequence.</param>
    /// <returns>True when the key exists and is a <see cref="TemplateSequence"/>; otherwise false.</returns>
    public bool TryGetSequence(string key, out TemplateSequence sequence)
    {
        if (_values.TryGetValue(key, out var n) && n is TemplateSequence s)
        {
            sequence = s;
            return true;
        }
        sequence = null!;
        return false;
    }

    /// <summary>Gets the scalar string value for a key, or null when absent or not a scalar.</summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The scalar value, or null.</returns>
    public string? GetScalar(string key)
        => _values.TryGetValue(key, out var n) && n is TemplateScalar s ? s.Value : null;
}
```

These four files are the entire neutral model. Everything the engine needs maps onto them (see Type Substitution Table).

---

## Type Substitution Table (apply mechanically when moving engine files)

| YamlDotNet construct | Neutral equivalent |
|---|---|
| `YamlMappingNode` (type) | `TemplateMapping` |
| `YamlSequenceNode` (type) | `TemplateSequence` |
| `YamlScalarNode` (type) | `TemplateScalar` |
| `YamlNode` (base, e.g. `ParseTupleScalar(YamlNode ...)`) | `TemplateNode` |
| `using YamlDotNet.RepresentationModel;` | `using FlexRender.Parsing.Nodes;` |
| `using YamlDotNet.Core;` (TemplateParser only) | (removed from Core engine; stays only in Yaml facade) |
| `parent.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlMappingNode m` | `parent.TryGetMapping(key, out var m)` |
| `parent.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlSequenceNode s` | `parent.TryGetSequence(key, out var s)` |
| `node.Children.TryGetValue(new YamlScalarNode(key), out var v) && v is YamlScalarNode sc ? sc.Value : null` | `node.GetScalar(key)` |
| `node.Children.TryGetValue(new YamlScalarNode(key), out var v)` (raw node, e.g. `fonts`, `elseIf`) | `node.TryGet(key, out var v)` |
| `seq.Children` (iterate) | `seq.Items` |
| `seq.Children.Count` | `seq.Items.Count` |
| `seq.Children[0]` / `tuple.Children[i]` | `seq.Items[0]` / `tuple.Items[i]` |
| `mapping.Children` (iterate `(keyNode, valueNode)`) | iterate `mapping.Keys`, then `mapping.TryGet(key, out var valueNode)` (see ConvertMappingToDictionary rewrite) |
| `node.Children.Keys` (validation key iteration) | `node.Keys` (already `string`, no `YamlScalarNode` cast needed) |
| `item is YamlScalarNode scalar ? scalar.Value` | `item is TemplateScalar scalar ? scalar.Value` |
| `item is YamlMappingNode m` | `item is TemplateMapping m` |
| `item is YamlSequenceNode s` | `item is TemplateSequence s` |
| `(x as YamlScalarNode)?.Value` (error messages) | `(x as TemplateScalar)?.Value` |
| `entry.Key is not YamlScalarNode k \|\| ...` (fonts mapping) | iterate `node.Keys` and `node.TryGet(key, out var value)` — key is already a `string` |

Note: `TemplateMapping.GetScalar(key)` replaces the helper `GetStringValue(node, key)` *primitive read*, but the engine keeps the higher-level `GetStringValue(...)` helpers in `NodePropertyHelpers` (they call `GetScalar` internally). Keep the public helper method names identical so call sites stay unchanged.

---

## File Structure (what gets created / moved / modified)

**Created in Core:**
- `src/FlexRender.Core/Parsing/Nodes/TemplateNode.cs`
- `src/FlexRender.Core/Parsing/Nodes/TemplateScalar.cs`
- `src/FlexRender.Core/Parsing/Nodes/TemplateSequence.cs`
- `src/FlexRender.Core/Parsing/Nodes/TemplateMapping.cs`
- `src/FlexRender.Core/Parsing/Engine/NodePropertyHelpers.cs` (moved+renamed from `YamlPropertyHelpers.cs`)
- `src/FlexRender.Core/Parsing/Engine/KnownProperties.cs` (moved)
- `src/FlexRender.Core/Parsing/Engine/ElementParsers.cs` (moved)
- `src/FlexRender.Core/Parsing/Engine/ShapeParsers.cs` (moved)
- `src/FlexRender.Core/Parsing/Engine/ChartParsers.cs` (moved)
- `src/FlexRender.Core/Parsing/Engine/TemplateEngine.cs` (new: holds the document-root parse + element dispatch extracted from `TemplateParser`)

**Created in Yaml:**
- `src/FlexRender.Yaml/Parsing/YamlNodeConverter.cs` (YamlDotNet DOM → neutral)

**Created in Xml:**
- `src/FlexRender.Xml/XmlNodeConverter.cs` (XDocument → neutral; rewrite of `XmlToYamlNodeConverter.cs`)

**Modified:**
- `src/FlexRender.Yaml/Parsing/TemplateParser.cs` (becomes a thin facade over `YamlNodeConverter` + `TemplateEngine`)
- `src/FlexRender.Xml/XmlTemplateParser.cs` (uses `XmlNodeConverter` + `TemplateEngine`)
- `src/FlexRender.Xml/XmlFlexRenderExtensions.cs` (drop `using FlexRender.Parsing;` if unused — verify)
- `src/FlexRender.Xml/FlexRender.Xml.csproj` (reference Core, not Yaml)
- `src/FlexRender.Yaml/AssemblyInfo.cs` (drop `InternalsVisibleTo("FlexRender.Xml")`)
- `tests/FlexRender.Tests/Parsing/GradientObjectParseTests.cs` (build neutral nodes)
- `tests/FlexRender.Tests/Parsing/Xml/InternalEntryPointTests.cs` (build neutral nodes, call `TemplateEngine`)
- `AGENTS.md` + `llms.txt` (doc updates)

**Deleted:**
- `src/FlexRender.Yaml/Parsing/YamlPropertyHelpers.cs`, `KnownProperties.cs`, `ElementParsers.cs`, `ShapeParsers.cs`, `ChartParsers.cs` (moved to Core)
- `src/FlexRender.Xml/XmlToYamlNodeConverter.cs` (replaced by `XmlNodeConverter.cs`)

---

## Phase 1 — Neutral node model in Core (additive, isolated)

### Task 1: Create the neutral node model

**Files:**
- Create: `src/FlexRender.Core/Parsing/Nodes/TemplateNode.cs`
- Create: `src/FlexRender.Core/Parsing/Nodes/TemplateScalar.cs`
- Create: `src/FlexRender.Core/Parsing/Nodes/TemplateSequence.cs`
- Create: `src/FlexRender.Core/Parsing/Nodes/TemplateMapping.cs`
- Test: `tests/FlexRender.Tests/Parsing/Nodes/TemplateMappingTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/Nodes/TemplateMappingTests.cs`:

```csharp
using FlexRender.Parsing.Nodes;
using Xunit;

namespace FlexRender.Tests.Parsing.Nodes;

/// <summary>Tests for the format-neutral node model.</summary>
public sealed class TemplateMappingTests
{
    [Fact]
    public void GetScalar_ReturnsValue_WhenKeyIsScalar()
    {
        var m = new TemplateMapping();
        m.Add("color", new TemplateScalar("#fff"));

        Assert.Equal("#fff", m.GetScalar("color"));
    }

    [Fact]
    public void GetScalar_ReturnsNull_WhenKeyMissingOrNotScalar()
    {
        var m = new TemplateMapping();
        m.Add("child", new TemplateSequence());

        Assert.Null(m.GetScalar("missing"));
        Assert.Null(m.GetScalar("child"));
    }

    [Fact]
    public void TryGetMapping_And_TryGetSequence_DiscriminateByType()
    {
        var m = new TemplateMapping();
        m.Add("map", new TemplateMapping());
        m.Add("seq", new TemplateSequence());
        m.Add("scalar", new TemplateScalar("x"));

        Assert.True(m.TryGetMapping("map", out _));
        Assert.False(m.TryGetMapping("seq", out _));
        Assert.True(m.TryGetSequence("seq", out _));
        Assert.False(m.TryGetSequence("scalar", out _));
    }

    [Fact]
    public void Keys_PreserveInsertionOrder_AndAddOverwritesValueKeepingPosition()
    {
        var m = new TemplateMapping();
        m.Add("a", new TemplateScalar("1"));
        m.Add("b", new TemplateScalar("2"));
        m.Add("a", new TemplateScalar("3"));

        Assert.Equal(new[] { "a", "b" }, m.Keys);
        Assert.Equal("3", m.GetScalar("a"));
    }

    [Fact]
    public void Sequence_PreservesOrder()
    {
        var s = new TemplateSequence();
        s.Add(new TemplateScalar("1"));
        s.Add(new TemplateScalar("2"));

        Assert.Equal(2, s.Items.Count);
        Assert.Equal("1", ((TemplateScalar)s.Items[0]).Value);
        Assert.Equal("2", ((TemplateScalar)s.Items[1]).Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~TemplateMappingTests"`
Expected: FAIL — compile error, `TemplateMapping`/`TemplateScalar`/`TemplateSequence` do not exist.

- [ ] **Step 3: Create the four node files**

Create the four files exactly as defined in the "Neutral Node Model (locked type definitions)" section above (`TemplateNode.cs`, `TemplateScalar.cs`, `TemplateSequence.cs`, `TemplateMapping.cs`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~TemplateMappingTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Verify full build still green**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Core/Parsing/Nodes tests/FlexRender.Tests/Parsing/Nodes
git commit --no-gpg-sign -m "feat(parser): add format-neutral template node model to Core"
```

---

## Phase 2 — Move the property helpers into Core (foundation for the engine)

`NodePropertyHelpers` is the leaf dependency every other engine file uses via `using static`. Move it first so subsequent engine files can compile against it. To keep the build green during the move, we COPY it into Core (neutral) while the Yaml originals still exist; we delete the Yaml originals only when the whole engine has moved (Phase 4). To avoid duplicate-symbol clashes (same namespace `FlexRender.Parsing`, same type name), this phase moves the helper as a single atomic file replacement is NOT possible while Yaml callers still reference the YAML-typed version.

**Sequencing decision (important):** The engine files form one tightly-coupled unit (they share `using static ...YamlPropertyHelpers` and call each other). A partial move causes duplicate type names in the same namespace. Therefore Phase 2–3 move the WHOLE engine in one coherent commit (Task 3), guarded by first creating the converter (Phase test data) — there is no clean smaller intermediate that compiles. The plan below makes Task 3 a single large mechanical move; Tasks 2 and 4 wrap it so each *commit* leaves the build green.

### Task 2: Add YamlNodeConverter in Yaml (additive, build stays green)

**Files:**
- Create: `src/FlexRender.Yaml/Parsing/YamlNodeConverter.cs`
- Test: `tests/FlexRender.Tests/Parsing/YamlNodeConverterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/FlexRender.Tests/Parsing/YamlNodeConverterTests.cs`:

```csharp
using System.IO;
using FlexRender.Parsing;
using FlexRender.Parsing.Nodes;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>Tests that the YamlDotNet DOM is faithfully converted to the neutral node model.</summary>
public sealed class YamlNodeConverterTests
{
    private static YamlMappingNode Load(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    [Fact]
    public void Convert_MappingScalarSequence_ProducesNeutralTree()
    {
        var yaml = Load("""
            canvas:
              width: 300
            layout:
              - type: text
                content: Hi
            """);

        var root = YamlNodeConverter.Convert(yaml);

        Assert.True(root.TryGetMapping("canvas", out var canvas));
        Assert.Equal("300", canvas.GetScalar("width"));

        Assert.True(root.TryGetSequence("layout", out var layout));
        var item = Assert.IsType<TemplateMapping>(Assert.Single(layout.Items));
        Assert.Equal("text", item.GetScalar("type"));
        Assert.Equal("Hi", item.GetScalar("content"));
    }

    [Fact]
    public void Convert_NestedSequenceOfSequences_Preserved()
    {
        var yaml = Load("""
            series:
              - data:
                  - [1, 2]
                  - [3, 4]
            """);

        var root = YamlNodeConverter.Convert(yaml);
        Assert.True(root.TryGetSequence("series", out var series));
        var s0 = Assert.IsType<TemplateMapping>(series.Items[0]);
        Assert.True(s0.TryGetSequence("data", out var data));
        var tuple0 = Assert.IsType<TemplateSequence>(data.Items[0]);
        Assert.Equal("1", ((TemplateScalar)tuple0.Items[0]).Value);
        Assert.Equal("2", ((TemplateScalar)tuple0.Items[1]).Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~YamlNodeConverterTests"`
Expected: FAIL — `YamlNodeConverter` does not exist.

- [ ] **Step 3: Implement YamlNodeConverter**

Create `src/FlexRender.Yaml/Parsing/YamlNodeConverter.cs`:

```csharp
using FlexRender.Parsing.Nodes;
using YamlDotNet.RepresentationModel;

namespace FlexRender.Parsing;

/// <summary>
/// Converts a YamlDotNet representation-model tree into the format-neutral
/// <see cref="TemplateNode"/> model consumed by the shared Core parsing engine.
/// This is the only place YamlDotNet types cross into the neutral world.
/// </summary>
internal static class YamlNodeConverter
{
    /// <summary>Converts a YamlDotNet document root mapping to a neutral <see cref="TemplateMapping"/>.</summary>
    /// <param name="root">The YamlDotNet mapping node (document root).</param>
    /// <returns>The equivalent neutral mapping.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    internal static TemplateMapping Convert(YamlMappingNode root)
    {
        System.ArgumentNullException.ThrowIfNull(root);
        return ConvertMapping(root);
    }

    private static TemplateNode ConvertNode(YamlNode node) => node switch
    {
        YamlMappingNode m => ConvertMapping(m),
        YamlSequenceNode s => ConvertSequence(s),
        YamlScalarNode sc => new TemplateScalar(sc.Value),
        // Aliases/anchors collapse to their resolved node in the representation model;
        // any unexpected node kind becomes an empty scalar to preserve total conversion.
        _ => new TemplateScalar(null)
    };

    private static TemplateMapping ConvertMapping(YamlMappingNode mapping)
    {
        var result = new TemplateMapping();
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            if (keyNode is YamlScalarNode { Value: { } key })
            {
                result.Add(key, ConvertNode(valueNode));
            }
        }
        return result;
    }

    private static TemplateSequence ConvertSequence(YamlSequenceNode sequence)
    {
        var result = new TemplateSequence(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            result.Add(ConvertNode(child));
        }
        return result;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~YamlNodeConverterTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Verify full build still green**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 warnings, 0 errors. (Converter is unused so far — `CA1812`/unused is suppressed because it is `internal static` and referenced by tests; if a warning appears, the next task wires it in.)

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Yaml/Parsing/YamlNodeConverter.cs tests/FlexRender.Tests/Parsing/YamlNodeConverterTests.cs
git commit --no-gpg-sign -m "feat(parser): add YamlDotNet-to-neutral node converter"
```

---

## Phase 3 — Move the engine into Core (THE BIG STEP — HIGHEST RISK)

This is one atomic commit because the engine files share a namespace and call each other; a partial move produces duplicate type names. The safety net is the full test suite run at the end.

### Task 3: Move and retype the parsing engine into Core

**Files:**
- Create: `src/FlexRender.Core/Parsing/Engine/NodePropertyHelpers.cs` (from `YamlPropertyHelpers.cs`)
- Create: `src/FlexRender.Core/Parsing/Engine/KnownProperties.cs`
- Create: `src/FlexRender.Core/Parsing/Engine/ElementParsers.cs`
- Create: `src/FlexRender.Core/Parsing/Engine/ShapeParsers.cs`
- Create: `src/FlexRender.Core/Parsing/Engine/ChartParsers.cs`
- Create: `src/FlexRender.Core/Parsing/Engine/TemplateEngine.cs`
- Delete: `src/FlexRender.Yaml/Parsing/YamlPropertyHelpers.cs`, `KnownProperties.cs`, `ElementParsers.cs`, `ShapeParsers.cs`, `ChartParsers.cs`
- Modify: `src/FlexRender.Yaml/Parsing/TemplateParser.cs` (becomes facade)

- [ ] **Step 1: Move + rename `YamlPropertyHelpers.cs` → Core `NodePropertyHelpers.cs`**

Move the file to `src/FlexRender.Core/Parsing/Engine/NodePropertyHelpers.cs`. Apply:
- Rename the class `YamlPropertyHelpers` → `NodePropertyHelpers`.
- Replace header `using YamlDotNet.RepresentationModel;` with `using FlexRender.Parsing.Nodes;`.
- Keep namespace `FlexRender.Parsing`.
- Apply the Type Substitution Table to every method signature and body. Specifically:
  - `TryGetMapping(YamlMappingNode parent, string key, out YamlMappingNode result)` body becomes a thin delegate to `parent.TryGetMapping(key, out result)`:
    ```csharp
    internal static bool TryGetMapping(TemplateMapping parent, string key, out TemplateMapping result)
        => parent.TryGetMapping(key, out result);
    ```
  - `TryGetSequence(...)` likewise:
    ```csharp
    internal static bool TryGetSequence(TemplateMapping parent, string key, out TemplateSequence result)
        => parent.TryGetSequence(key, out result);
    ```
  - `GetStringValue(YamlMappingNode node, string key)` becomes:
    ```csharp
    internal static string? GetStringValue(TemplateMapping node, string key) => node.GetScalar(key);
    ```
  - Every other helper keeps its exact signature and logic, swapping `YamlMappingNode` → `TemplateMapping`. They already call `GetStringValue(node, key)` internally, so only the parameter type changes.
  - `ConvertMappingToDictionary(YamlMappingNode mapping, int depth)` — rewrite the iteration to use neutral nodes:
    ```csharp
    internal static IReadOnlyDictionary<string, object> ConvertMappingToDictionary(TemplateMapping mapping, int depth = 0)
    {
        if (depth > 10)
            throw new InvalidOperationException("Options nesting depth exceeded (max 10).");

        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in mapping.Keys)
        {
            mapping.TryGet(key, out var valueNode);
            dict[key] = valueNode switch
            {
                TemplateScalar scalar => scalar.Value ?? string.Empty,
                TemplateMapping nested => ConvertMappingToDictionary(nested, depth + 1),
                TemplateSequence seq => ConvertSequenceToList(seq, depth + 1),
                _ => string.Empty
            };
        }
        return dict;
    }
    ```
  - `ConvertSequenceToList(YamlSequenceNode sequence, int depth)` → `TemplateSequence`, iterate `sequence.Items`, type checks `TemplateScalar`/`TemplateMapping`/`TemplateSequence`.
- Keep `GetIntValue`, `GetFloatValue`, `GetBoolValue`, `GetNullableIntValue`, `GetNullableBoolValue`, `GetNullableFloatValue`, `GetDoubleValue`, `ContainsExpression`, all `GetExpr*` methods — signatures change only `YamlMappingNode` → `TemplateMapping`; bodies unchanged (they delegate to `GetStringValue`).

- [ ] **Step 2: Move `KnownProperties.cs` → Core `Parsing/Engine/KnownProperties.cs`**

- Replace `using YamlDotNet.RepresentationModel;` with `using FlexRender.Parsing.Nodes;`.
- Keep namespace `FlexRender.Parsing`.
- Rewrite `Validate(YamlMappingNode node, string elementType)` key iteration (this is the typo-validation path — verify carefully):
  ```csharp
  internal static void Validate(TemplateMapping node, string elementType)
  {
      if (!Registry.TryGetValue(elementType, out var knownProperties))
          return;

      List<string>? unknown = null;

      foreach (var keyName in node.Keys)
      {
          if (string.Equals(keyName, TypeKey, StringComparison.Ordinal))
              continue;

          if (!knownProperties.Contains(keyName))
          {
              unknown ??= [];
              unknown.Add(keyName);
          }
      }

      if (unknown is { Count: > 0 })
      {
          var unknownList = string.Join(", ", unknown.Select(u => $"'{u}'"));
          var suggestion = BuildSuggestion(unknown, knownProperties);
          var message = $"Unknown properties on '{elementType}' element: [{unknownList}].";
          if (suggestion.Length > 0)
              message += $" Did you mean: {suggestion}?";
          throw new TemplateParseException(message);
      }
  }
  ```
  Note: neutral `Keys` are already non-null `string`, so the old `YamlScalarNode`/null guards are dropped. `BuildSuggestion`, `LevenshteinDistance`, `BuildSet`, and all `HashSet<string>` registry fields are unchanged.

- [ ] **Step 3: Move `ElementParsers.cs` → Core `Parsing/Engine/ElementParsers.cs`**

- Replace `using YamlDotNet.RepresentationModel;` with `using FlexRender.Parsing.Nodes;`. Keep `using static FlexRender.Parsing.NodePropertyHelpers;` (renamed from `YamlPropertyHelpers`). Keep namespace `FlexRender.Parsing`.
- Apply substitutions:
  - Field/ctor delegate `Func<YamlMappingNode, TemplateElement>` → `Func<TemplateMapping, TemplateElement>`.
  - Every method parameter `YamlMappingNode node` → `TemplateMapping node`; `YamlSequenceNode` → `TemplateSequence`.
  - `ParseFlexElement` children loop:
    ```csharp
    if (TryGetSequence(node, "children", out var childrenNode))
    {
        foreach (var child in childrenNode.Items)
        {
            if (child is TemplateMapping childMapping)
                flex.AddChild(_parseElement(childMapping));
        }
    }
    ```
  - `ParseIfElement` elseIf branch:
    ```csharp
    if (node.TryGet("elseIf", out var elseIfNode) && elseIfNode is TemplateMapping elseIfMapping)
        elseIf = (IfElement)ParseIfElement(elseIfMapping);
    ```
  - `ParseStringArray(YamlSequenceNode)` → `TemplateSequence`, iterate `.Items`, `child is TemplateScalar scalar`.
  - `ParseTableColumns` / `ParseTableRows`: `YamlSequenceNode` → `TemplateSequence`, `.Children` → `.Items`, `child is not YamlMappingNode` → `child is not TemplateMapping`.
  - `ParseChildren(YamlMappingNode node, string key)`:
    ```csharp
    private IReadOnlyList<TemplateElement> ParseChildren(TemplateMapping node, string key)
    {
        if (!node.TryGet(key, out var childrenNode) || childrenNode is not TemplateSequence sequence)
            return Array.Empty<TemplateElement>();

        var elements = new List<TemplateElement>(sequence.Items.Count);
        foreach (var child in sequence.Items)
        {
            if (child is TemplateMapping elementNode)
                elements.Add(_parseElement(elementNode));
        }
        return elements;
    }
    ```
  - `ParseConditionOperator`, `ParseTextElement`, `ParseQrElement`, etc.: only the `node` parameter type changes; all helper calls (`GetStringValue`, `GetExpr*`, `TryGetSequence`, `TryGetMapping`, `ConvertMappingToDictionary`) are unchanged because the helpers were moved with matching signatures.
  - `ParseContentElement`: `YamlPropertyHelpers.ConvertMappingToDictionary(optionsNode)` → `NodePropertyHelpers.ConvertMappingToDictionary(optionsNode)`.

- [ ] **Step 4: Move `ShapeParsers.cs` → Core `Parsing/Engine/ShapeParsers.cs`**

- Replace YAML using with `using FlexRender.Parsing.Nodes;`. Keep `using static ...NodePropertyHelpers;` and namespace `FlexRender.Parsing`. Keep `public static class ShapeParsers`.
- `ConvertGradientObjectToCss(YamlMappingNode node)` → `(TemplateMapping node)`. The `colors` loop: `colorsSeq.Children` → `colorsSeq.Items`, `item is YamlScalarNode scalar` → `item is TemplateScalar scalar`.
- `ParseFill(YamlMappingNode)` → `(TemplateMapping)`.
- `ParseRectElement`/`ParseCircleElement`/`ParseEllipseElement`/`ParseDrawElement`/`ParseDrawShape`: `YamlMappingNode` → `TemplateMapping`, `.Children` → `.Items` where iterating `shapesSeq`.
- `ParsePoints(YamlMappingNode)`: tuple checks `item is not YamlSequenceNode pair` → `item is not TemplateSequence pair`, `pair.Children` → `pair.Items`, `... is not YamlScalarNode xScalar` → `... is not TemplateScalar xScalar`.
- `GetFiniteFloatValue(YamlMappingNode ...)` → `(TemplateMapping ...)`.

- [ ] **Step 5: Move `ChartParsers.cs` → Core `Parsing/Engine/ChartParsers.cs`**

- Replace YAML using with `using FlexRender.Parsing.Nodes;`. Keep `using FlexRender.Charts;`, `using static ...NodePropertyHelpers;`, namespace `FlexRender.Parsing`, `public static class ChartParsers`.
- All `YamlMappingNode` → `TemplateMapping`, `YamlSequenceNode` → `TemplateSequence`, `YamlNode` → `TemplateNode`.
- `.Children` → `.Items` everywhere (`seriesSeq.Children`, `dataSeq.Children`, `tuple.Children`, `seq.Children`).
- `item is YamlScalarNode scalar` → `item is TemplateScalar scalar`; `dataSeq.Children[0] is YamlSequenceNode` → `dataSeq.Items[0] is TemplateSequence`; `item is not YamlMappingNode seriesNode` → `item is not TemplateMapping seriesNode`; `item is not YamlSequenceNode tuple` → `item is not TemplateSequence tuple`.
- `ParseTupleScalar(YamlNode node, string? label)` → `(TemplateNode node, string? label)`, `node is YamlScalarNode scalar` → `node is TemplateScalar scalar`, error message `(node as YamlScalarNode)?.Value` → `(node as TemplateScalar)?.Value`.
- `ChartElement.MaxDataPointsPerSeries` access is now intra-assembly (Core) — no change needed.

- [ ] **Step 6: Create `TemplateEngine.cs` in Core (the relocated document-root parse + dispatch)**

Create `src/FlexRender.Core/Parsing/Engine/TemplateEngine.cs`. This holds everything from the old `TemplateParser` EXCEPT the YAML string/stream/file loading (which stays in the Yaml facade). Keep namespace `FlexRender.Parsing`.

```csharp
using FlexRender.Charts;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Parsing.Nodes;
using static FlexRender.Parsing.NodePropertyHelpers;

namespace FlexRender.Parsing;

/// <summary>
/// Format-neutral template parsing engine. Consumes a neutral <see cref="TemplateMapping"/>
/// document root (produced by the YAML or XML node converters) and builds a <see cref="Template"/> AST.
/// Shared by <c>FlexRender.Yaml</c> and <c>FlexRender.Xml</c>; depends only on Core types.
/// </summary>
public sealed class TemplateEngine
{
    private readonly Dictionary<string, Func<TemplateMapping, TemplateElement>> _elementParsers;
    private readonly ResourceLimits _limits;
    private readonly ElementParsers _parsers;

    /// <summary>Gets the supported element type names.</summary>
    public IReadOnlyCollection<string> SupportedElementTypes => _elementParsers.Keys;

    /// <summary>Initializes a new engine with the given resource limits.</summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public TemplateEngine(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _parsers = new ElementParsers(ParseElement);
        _elementParsers = new Dictionary<string, Func<TemplateMapping, TemplateElement>>(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = ElementParsers.ParseTextElement,
            ["flex"] = _parsers.ParseFlexElement,
            ["qr"] = ElementParsers.ParseQrElement,
            ["barcode"] = ElementParsers.ParseBarcodeElement,
            ["image"] = ElementParsers.ParseImageElement,
            ["separator"] = ElementParsers.ParseSeparatorElement,
            ["each"] = _parsers.ParseEachElement,
            ["if"] = _parsers.ParseIfElement,
            ["table"] = ElementParsers.ParseTableElement,
            ["svg"] = ElementParsers.ParseSvgElement,
            ["content"] = ElementParsers.ParseContentElement,
            ["rect"] = ShapeParsers.ParseRectElement,
            ["circle"] = ShapeParsers.ParseCircleElement,
            ["ellipse"] = ShapeParsers.ParseEllipseElement,
            ["draw"] = node => ShapeParsers.ParseDrawElement(node, _limits.MaxShapesPerDraw),
            ["chart"] = node => ChartParsers.ParseChartElement(node, _limits.MaxSeriesPerChart, _limits.MaxDataPointsPerSeries)
        };
    }

    /// <summary>Builds a <see cref="Template"/> from a neutral document-root mapping.</summary>
    /// <param name="root">The document root mapping (from a format converter).</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when required sections are missing or invalid.</exception>
    public Template ParseDocumentRoot(TemplateMapping root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var template = new Template();

        if (TryGetMapping(root, "template", out var templateNode))
        {
            template.Name = GetStringValue(templateNode, "name");
            template.Version = GetIntValue(templateNode, "version", 1);
            template.Culture = GetStringValue(templateNode, "culture");
        }

        if (root.TryGet("fonts", out var fontsNode))
        {
            template.Fonts = fontsNode switch
            {
                TemplateMapping fontsMapping => ParseFonts(fontsMapping),
                TemplateSequence fontsSequence => ParseFontsList(fontsSequence),
                _ => throw new TemplateParseException(
                    "Invalid 'fonts' section. Expected a mapping (name: path) or a list of font entries.")
            };
        }

        if (!TryGetMapping(root, "canvas", out var canvasNode))
            throw new TemplateParseException("Missing required 'canvas' section");

        template.Canvas = ParseCanvas(canvasNode);

        if (TryGetSequence(root, "layout", out var layoutNode))
            template.Elements = ParseElements(layoutNode);

        return template;
    }

    // --- The following are moved VERBATIM from the old TemplateParser, retyped to neutral nodes: ---
    // ParseCanvas(TemplateMapping) — body unchanged (uses GetStringValue/GetIntValue helpers).
    // ParseFonts(TemplateMapping) — rewrite the entry loop (see Step 6a below).
    // ParseFontsList(TemplateSequence) — rewrite item loop (see Step 6b below).
    // ParseElements(TemplateSequence) — iterate .Items, child is TemplateMapping.
    // ParseElement(TemplateMapping) — unchanged except parameter type; calls KnownProperties.Validate(node, type).
}
```

- [ ] **Step 6a: Port `ParseCanvas`, `ParseElements`, `ParseElement` into `TemplateEngine`**

Copy them from the old `TemplateParser.cs`, changing only parameter types:
- `ParseCanvas(YamlMappingNode node)` → `private static CanvasSettings ParseCanvas(TemplateMapping node)` — body identical.
- `ParseElements(YamlSequenceNode node)` →
  ```csharp
  private List<TemplateElement> ParseElements(TemplateSequence node)
  {
      var elements = new List<TemplateElement>(node.Items.Count);
      foreach (var child in node.Items)
          if (child is TemplateMapping elementNode)
              elements.Add(ParseElement(elementNode));
      return elements;
  }
  ```
- `ParseElement(YamlMappingNode node)` → `private TemplateElement ParseElement(TemplateMapping node)` — body identical (calls `KnownProperties.Validate(node, type)` and the parser dict).

- [ ] **Step 6b: Port `ParseFonts` and `ParseFontsList` (entry/key iteration rewrite)**

`ParseFonts` — the old code iterates `node.Children` reading `entry.Key`/`entry.Value`. Rewrite:
```csharp
private static Dictionary<string, FontDefinition> ParseFonts(TemplateMapping node)
{
    var fonts = new Dictionary<string, FontDefinition>(StringComparer.OrdinalIgnoreCase);

    foreach (var fontName in node.Keys)
    {
        if (string.IsNullOrEmpty(fontName))
            continue;

        node.TryGet(fontName, out var value);
        FontDefinition fontDef;
        switch (value)
        {
            case TemplateScalar scalarValue:
                fontDef = new FontDefinition(scalarValue.Value ?? string.Empty);
                break;
            case TemplateMapping mappingValue:
                var path = GetStringValue(mappingValue, "path") ?? string.Empty;
                var fallback = GetStringValue(mappingValue, "fallback");
                fontDef = new FontDefinition(path, fallback);
                break;
            default:
                throw new TemplateParseException(
                    $"Invalid font definition for '{fontName}'. Expected a string path or object with 'path' and optional 'fallback' properties.");
        }
        fonts[fontName] = fontDef;
    }
    return fonts;
}
```

`ParseFontsList(YamlSequenceNode node)` → `(TemplateSequence node)`: iterate `node.Items`; `case YamlScalarNode scalar` → `case TemplateScalar scalar`; `case YamlMappingNode mapping` → `case TemplateMapping mapping`. All the name/default/index logic is unchanged.

- [ ] **Step 7: Rewrite the Yaml `TemplateParser.cs` facade**

Replace `src/FlexRender.Yaml/Parsing/TemplateParser.cs` with the facade below. It keeps the same public API (`Parse(string)`, `Parse(Stream)`, `ParseFile`, `ParseFileAsync`, `MaxFileSize`, `SupportedElementTypes`, both ctors) and the test shim `ParseDocumentRootForTests` is REMOVED here (it moves to a Core-typed test helper — see Task 6). YamlDotNet stays here only for loading.

```csharp
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace FlexRender.Parsing;

/// <summary>
/// Parses YAML templates into the <see cref="Template"/> AST. Loads YAML via YamlDotNet,
/// converts it to the format-neutral node model, then delegates to the shared
/// <see cref="TemplateEngine"/> for all element parsing and validation.
/// </summary>
public sealed class TemplateParser : ITemplateParser
{
    /// <summary>
    /// Maximum allowed file size in bytes (1 MB). Preserved for backward compatibility;
    /// the runtime limit comes from <see cref="ResourceLimits.MaxTemplateFileSize"/>.
    /// </summary>
    public const long MaxFileSize = 1024 * 1024;

    private readonly ResourceLimits _limits;
    private readonly TemplateEngine _engine;

    /// <summary>Gets the supported element type names.</summary>
    public IReadOnlyCollection<string> SupportedElementTypes => _engine.SupportedElementTypes;

    /// <summary>Initializes a new parser with default resource limits.</summary>
    public TemplateParser() : this(new ResourceLimits())
    {
    }

    /// <summary>Initializes a new parser with custom resource limits.</summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public TemplateParser(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _engine = new TemplateEngine(limits);
    }

    /// <summary>Parses a YAML string into a <see cref="Template"/> AST.</summary>
    /// <param name="content">The YAML string.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="TemplateParseException">Thrown when parsing fails.</exception>
    public Template Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new TemplateParseException("Template YAML is empty or whitespace");

        YamlMappingNode root;
        try
        {
            var yamlStream = new YamlStream();
            using var reader = new StringReader(content);
            yamlStream.Load(reader);

            if (yamlStream.Documents.Count == 0)
                throw new TemplateParseException("Template YAML is empty");

            root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
        }
        catch (YamlException ex)
        {
            throw new TemplateParseException($"Invalid YAML: {ex.Message}", ex);
        }

        return _engine.ParseDocumentRoot(YamlNodeConverter.Convert(root));
    }

    /// <summary>Parses a YAML template from a stream.</summary>
    /// <param name="stream">The stream containing YAML content.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when parsing fails.</exception>
    public Template Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>Asynchronously parses a YAML file into a <see cref="Template"/> AST.</summary>
    /// <param name="path">The path to the YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when parsing fails or the file exceeds the maximum size.</exception>
    public Task<Template> ParseFileAsync(string path, CancellationToken cancellationToken = default)
        => ParseFile(path, cancellationToken);

    /// <summary>Asynchronously parses a YAML file into a <see cref="Template"/> AST.</summary>
    /// <param name="path">The path to the YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="TemplateParseException">Thrown when parsing fails or the file exceeds the maximum size.</exception>
    public async Task<Template> ParseFile(string path, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Exists && fileInfo.Length > _limits.MaxTemplateFileSize)
        {
            throw new TemplateParseException(
                $"Template file size ({fileInfo.Length} bytes) exceeds maximum allowed size ({_limits.MaxTemplateFileSize} bytes)");
        }

        var yaml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Parse(yaml);
    }
}
```

- [ ] **Step 8: Delete the moved Yaml engine files**

Delete: `src/FlexRender.Yaml/Parsing/YamlPropertyHelpers.cs`, `KnownProperties.cs`, `ElementParsers.cs`, `ShapeParsers.cs`, `ChartParsers.cs`.

```bash
git rm src/FlexRender.Yaml/Parsing/YamlPropertyHelpers.cs src/FlexRender.Yaml/Parsing/KnownProperties.cs src/FlexRender.Yaml/Parsing/ElementParsers.cs src/FlexRender.Yaml/Parsing/ShapeParsers.cs src/FlexRender.Yaml/Parsing/ChartParsers.cs
```

- [ ] **Step 9: Build (the two internal-using tests will not yet compile — that is expected and fixed in Task 6)**

The two test files (`GradientObjectParseTests`, `InternalEntryPointTests`) still reference YAML-typed internals and will break the **test** build. The **source** build must be green. Build only source projects:

Run: `dotnet build src/FlexRender.Yaml/FlexRender.Yaml.csproj` then `dotnet build src/FlexRender.Core/FlexRender.Core.csproj`
Expected: both succeed, 0 warnings, 0 errors. Watch specifically for CA1859 (use concrete type) / unused-field / unused-using warnings in the moved files — fix any by removing the dead `using` or adjusting return types.

(If `dotnet build FlexRender.slnx` is run here it will fail on the two stale test files — that is acceptable mid-task; do not commit yet.)

- [ ] **Step 10: Fix the two internal-touching test files to use neutral nodes**

Rewrite `tests/FlexRender.Tests/Parsing/GradientObjectParseTests.cs` to build neutral nodes (the converter under test moved to Core, `ShapeParsers.ConvertGradientObjectToCss` now takes `TemplateMapping`). Replace the `ParseMapping` helper and its usages:

```csharp
using FlexRender.Parsing;
using FlexRender.Parsing.Nodes;
using Xunit;

namespace FlexRender.Tests.Parsing;

/// <summary>Tests for converting the gradient object form to a CSS gradient string.</summary>
public sealed class GradientObjectParseTests
{
    private static TemplateScalar S(string v) => new(v);

    private static TemplateMapping Linear(params string[] colors)
    {
        var m = new TemplateMapping();
        m.Add("gradient", S("linear"));
        var seq = new TemplateSequence();
        foreach (var c in colors) seq.Add(S(c));
        m.Add("colors", seq);
        return m;
    }

    [Fact]
    public void LinearGradient_WithAngleAndColors_ProducesCssString()
    {
        var node = Linear("#ff0000", "#0000ff");
        node.Add("angle", S("45"));

        var css = ShapeParsers.ConvertGradientObjectToCss(node);

        Assert.Equal("linear-gradient(45deg, #ff0000, #0000ff)", css);
    }

    [Fact]
    public void LinearGradient_WithoutAngle_DefaultsToZeroDeg()
    {
        var css = ShapeParsers.ConvertGradientObjectToCss(Linear("#fff", "#000"));
        Assert.Equal("linear-gradient(0deg, #fff, #000)", css);
    }

    [Fact]
    public void RadialGradient_IgnoresAngle()
    {
        var node = new TemplateMapping();
        node.Add("gradient", S("radial"));
        var seq = new TemplateSequence();
        seq.Add(S("#fff"));
        seq.Add(S("#000"));
        node.Add("colors", seq);
        node.Add("angle", S("90"));

        var css = ShapeParsers.ConvertGradientObjectToCss(node);
        Assert.Equal("radial-gradient(#fff, #000)", css);
    }

    [Fact]
    public void Gradient_WithFewerThanTwoColors_Throws()
    {
        Assert.Throws<TemplateParseException>(() => ShapeParsers.ConvertGradientObjectToCss(Linear("#fff")));
    }

    [Fact]
    public void Gradient_WithUnknownType_Throws()
    {
        var node = new TemplateMapping();
        node.Add("gradient", S("conic"));
        var seq = new TemplateSequence();
        seq.Add(S("#fff"));
        seq.Add(S("#000"));
        node.Add("colors", seq);

        Assert.Throws<TemplateParseException>(() => ShapeParsers.ConvertGradientObjectToCss(node));
    }
}
```

Rewrite `tests/FlexRender.Tests/Parsing/Xml/InternalEntryPointTests.cs` to build a neutral root and call `TemplateEngine.ParseDocumentRoot` (the public engine entry point — no shim needed):

```csharp
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Parsing.Nodes;
using Xunit;

namespace FlexRender.Tests.Parsing.Xml;

/// <summary>
/// Tests the shared <see cref="TemplateEngine.ParseDocumentRoot"/> entry point used by
/// both format parsers to reuse element parsing against the neutral node model.
/// </summary>
public class InternalEntryPointTests
{
    [Fact]
    public void ParseDocumentRoot_BuiltNode_ProducesEquivalentAst()
    {
        // Build: { canvas: { width: 300 }, layout: [ { type: text, content: Hi } ] }
        var canvas = new TemplateMapping();
        canvas.Add("width", new TemplateScalar("300"));

        var textNode = new TemplateMapping();
        textNode.Add("type", new TemplateScalar("text"));
        textNode.Add("content", new TemplateScalar("Hi"));

        var layout = new TemplateSequence();
        layout.Add(textNode);

        var root = new TemplateMapping();
        root.Add("canvas", canvas);
        root.Add("layout", layout);

        var template = new TemplateEngine(new ResourceLimits()).ParseDocumentRoot(root);

        var text = Assert.IsType<TextElement>(Assert.Single(template.Elements));
        Assert.Equal("Hi", text.Content);
        Assert.Equal(300, template.Canvas.Width);
    }
}
```

- [ ] **Step 11: Run the full parsing + chart + shape + XML test suites**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 warnings, 0 errors.

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~Parsing"`
Expected: PASS (all parsing/chart/shape/xml/converter/node tests green).

- [ ] **Step 12: Run the ENTIRE suite (safety net)**

Run: `dotnet test FlexRender.slnx --framework net10.0`
Expected: PASS — full suite (~3304 + the new node/converter tests) green.

- [ ] **Step 13: Commit**

```bash
git add -A
git commit --no-gpg-sign -m "refactor(parser): move shared parsing engine into Core on neutral node model"
```

---

## Phase 4 — Repoint FlexRender.Xml at Core only

### Task 4: Rewrite the XML converter to emit neutral nodes and drop the Yaml dependency

**Files:**
- Create: `src/FlexRender.Xml/XmlNodeConverter.cs`
- Delete: `src/FlexRender.Xml/XmlToYamlNodeConverter.cs`
- Modify: `src/FlexRender.Xml/XmlTemplateParser.cs`
- Modify: `src/FlexRender.Xml/FlexRender.Xml.csproj`
- Modify: `src/FlexRender.Xml/XmlFlexRenderExtensions.cs` (verify usings)

- [ ] **Step 1: Create `XmlNodeConverter.cs` (neutral output)**

Create `src/FlexRender.Xml/XmlNodeConverter.cs` — a mechanical rewrite of `XmlToYamlNodeConverter.cs` where every `YamlMappingNode`→`TemplateMapping`, `YamlSequenceNode`→`TemplateSequence`, `YamlScalarNode`→`TemplateScalar`, `YamlNode`→`TemplateNode`, and `.Children.Count`→`.Items.Count`/`.Keys.Count`. Full file:

```csharp
using System.Xml.Linq;
using FlexRender.Parsing;
using FlexRender.Parsing.Nodes;

namespace FlexRender.Xml;

/// <summary>
/// Converts a FlexRender XML template tree into the format-neutral
/// <see cref="TemplateMapping"/> document root that the shared
/// <see cref="TemplateEngine"/> consumes, so all element/chart/shape parsing and
/// validation is reused without duplication and without any YAML dependency.
/// </summary>
internal static class XmlNodeConverter
{
    private const string RootName = "flexrender";

    private static readonly HashSet<string> WrapperNames = new(StringComparer.Ordinal)
    {
        "then", "else", "else-if", "columns", "rows",
        "categories", "x-labels", "y-labels", "palette", "shapes"
    };

    private static readonly HashSet<string> ListAttributes = new(StringComparer.Ordinal)
    {
        "points", "categories", "palette", "x-labels", "y-labels"
    };

    /// <summary>Parses the XML string and builds the neutral document-root mapping.</summary>
    /// <param name="xml">The raw XML template.</param>
    /// <returns>The equivalent neutral document root mapping.</returns>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or the root element is wrong.</exception>
    internal static TemplateMapping Convert(string xml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new TemplateParseException($"Invalid XML: {ex.Message}", ex);
        }

        var rootEl = doc.Root
            ?? throw new TemplateParseException("XML template has no root element.");

        if (!string.Equals(rootEl.Name.LocalName, RootName, StringComparison.Ordinal))
        {
            throw new TemplateParseException(
                $"XML template root must be <{RootName}>, but was <{rootEl.Name.LocalName}>.");
        }

        var root = new TemplateMapping();

        var metadata = new TemplateMapping();
        AddAttrIfPresent(rootEl, "name", metadata);
        AddAttrIfPresent(rootEl, "version", metadata);
        AddAttrIfPresent(rootEl, "culture", metadata);
        if (metadata.Keys.Count > 0)
            root.Add("template", metadata);

        var layout = new TemplateSequence();

        foreach (var child in rootEl.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "canvas":
                    root.Add("canvas", AttributesToMapping(child));
                    break;
                case "fonts":
                    root.Add("fonts", ConvertFonts(child));
                    break;
                default:
                    layout.Add(ConvertElement(child));
                    break;
            }
        }

        root.Add("layout", layout);
        return root;
    }

    private static TemplateMapping AttributesToMapping(XElement el)
    {
        var node = new TemplateMapping();
        foreach (var attr in el.Attributes())
            node.Add(attr.Name.LocalName, new TemplateScalar(attr.Value));
        return node;
    }

    private static TemplateMapping ConvertElement(XElement el)
    {
        var type = el.Name.LocalName;
        var node = new TemplateMapping();
        node.Add("type", new TemplateScalar(type));

        foreach (var attr in el.Attributes())
        {
            var name = attr.Name.LocalName;
            node.Add(name, ListAttributes.Contains(name)
                ? ExpandListAttribute(attr.Value)
                : new TemplateScalar(attr.Value));
        }

        if (el.Attribute("content") is null && !el.HasElements)
        {
            var inner = el.Value;
            if (!string.IsNullOrWhiteSpace(inner))
                node.Add("content", new TemplateScalar(inner.Trim()));
        }

        var naturalList = new TemplateSequence();
        var seriesList = new TemplateSequence();
        foreach (var child in el.Elements())
        {
            var childName = child.Name.LocalName;
            if (string.Equals(childName, "series", StringComparison.Ordinal))
                seriesList.Add(ConvertSeries(child));
            else if (WrapperNames.Contains(childName))
                AddWrapper(node, child);
            else
                naturalList.Add(ConvertElement(child));
        }

        if (seriesList.Items.Count > 0)
            node.Add("series", seriesList);

        if (naturalList.Items.Count > 0)
            node.Add("children", naturalList);

        return node;
    }

    private static void AddWrapper(TemplateMapping node, XElement wrapper)
    {
        var name = wrapper.Name.LocalName;
        switch (name)
        {
            case "then":
            case "else":
                node.Add(name, ConvertElementSequence(wrapper));
                break;
            case "else-if":
                var children = wrapper.Elements().ToList();
                if (children.Count != 1
                    || !string.Equals(children[0].Name.LocalName, "if", StringComparison.Ordinal))
                {
                    throw new TemplateParseException(
                        "An <else-if> must contain exactly one <if> child element.");
                }
                node.Add("elseIf", ConvertElement(children[0]));
                break;
            case "columns":
                node.Add("columns", ConvertAttributeItemSequence(wrapper));
                break;
            case "rows":
                node.Add("rows", ConvertAttributeItemSequence(wrapper));
                break;
            case "categories":
            case "x-labels":
            case "y-labels":
                node.Add(name, ConvertScalarItemSequence(wrapper));
                break;
            case "palette":
                node.Add("palette", ConvertScalarItemSequence(wrapper));
                break;
            case "shapes":
                node.Add("shapes", ConvertShapeSequence(wrapper));
                break;
        }
    }

    private static TemplateMapping ConvertSeries(XElement series)
    {
        var node = new TemplateMapping();
        foreach (var attr in series.Attributes())
        {
            var name = attr.Name.LocalName;
            switch (name)
            {
                case "data":
                case "points":
                    node.Add("data", ExpandListAttribute(attr.Value));
                    break;
                default:
                    node.Add(name, new TemplateScalar(attr.Value));
                    break;
            }
        }
        return node;
    }

    private static TemplateSequence ConvertElementSequence(XElement wrapper)
    {
        var seq = new TemplateSequence();
        foreach (var child in wrapper.Elements())
            seq.Add(ConvertElement(child));
        return seq;
    }

    private static TemplateSequence ConvertAttributeItemSequence(XElement wrapper)
    {
        var seq = new TemplateSequence();
        foreach (var child in wrapper.Elements())
            seq.Add(AttributesToMapping(child));
        return seq;
    }

    private static TemplateSequence ConvertScalarItemSequence(XElement wrapper)
    {
        var seq = new TemplateSequence();
        foreach (var child in wrapper.Elements())
            seq.Add(new TemplateScalar(child.Value.Trim()));
        return seq;
    }

    private static TemplateSequence ConvertShapeSequence(XElement wrapper)
    {
        var seq = new TemplateSequence();
        foreach (var shape in wrapper.Elements())
        {
            var shapeMapping = new TemplateMapping();
            foreach (var attr in shape.Attributes())
            {
                var name = attr.Name.LocalName;
                shapeMapping.Add(name, ListAttributes.Contains(name)
                    ? ExpandListAttribute(attr.Value)
                    : new TemplateScalar(attr.Value));
            }

            var wrapped = new TemplateMapping();
            wrapped.Add(shape.Name.LocalName, shapeMapping);
            seq.Add(wrapped);
        }
        return seq;
    }

    private static TemplateSequence ConvertFonts(XElement fonts)
    {
        var seq = new TemplateSequence();
        foreach (var font in fonts.Elements())
            seq.Add(AttributesToMapping(font));
        return seq;
    }

    private static TemplateNode ExpandListAttribute(string value)
    {
        if (value.Contains("{{", StringComparison.Ordinal))
            return new TemplateScalar(value);

        if (value.Contains(';', StringComparison.Ordinal))
        {
            var outer = new TemplateSequence();
            foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var inner = new TemplateSequence();
                foreach (var comp in part.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    inner.Add(new TemplateScalar(comp));
                outer.Add(inner);
            }
            return outer;
        }

        var seq = new TemplateSequence();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            seq.Add(new TemplateScalar(item));
        return seq;
    }

    private static void AddAttrIfPresent(XElement el, string name, TemplateMapping node)
    {
        var attr = el.Attribute(name);
        if (attr is not null && !string.IsNullOrEmpty(attr.Value))
            node.Add(name, new TemplateScalar(attr.Value));
    }
}
```

Note: the old `NaturalListKey(type)` helper always returned `"children"`, so it is inlined as the literal `"children"` above (DRY/YAGNI).

- [ ] **Step 2: Update `XmlTemplateParser.cs` to use the engine**

Rewrite `src/FlexRender.Xml/XmlTemplateParser.cs`:

```csharp
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;

namespace FlexRender.Xml;

/// <summary>
/// Parses FlexRender XML templates into the same <see cref="Template"/> AST as the YAML parser.
/// XML is converted to the format-neutral node model and handed to the shared
/// <see cref="TemplateEngine"/>, so all element parsing, validation, and resource limits are reused.
/// Depends only on <c>FlexRender.Core</c> (no YAML, no YamlDotNet).
/// </summary>
public sealed class XmlTemplateParser : ITemplateParser
{
    private readonly TemplateEngine _engine;

    /// <summary>Initializes a new instance with default resource limits.</summary>
    public XmlTemplateParser() : this(new ResourceLimits())
    {
    }

    /// <summary>Initializes a new instance with custom resource limits.</summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public XmlTemplateParser(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _engine = new TemplateEngine(limits);
    }

    /// <summary>Parses an XML template string into a <see cref="Template"/> AST.</summary>
    /// <param name="content">The XML template content.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or invalid.</exception>
    public Template Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(content))
            throw new TemplateParseException("Template XML is empty or whitespace");

        return _engine.ParseDocumentRoot(XmlNodeConverter.Convert(content));
    }

    /// <summary>Parses an XML template from a stream.</summary>
    /// <param name="stream">The stream containing XML content.</param>
    /// <returns>The parsed template.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or invalid.</exception>
    public Template Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
```

- [ ] **Step 3: Delete the old XML→YAML converter**

```bash
git rm src/FlexRender.Xml/XmlToYamlNodeConverter.cs
```

- [ ] **Step 4: Repoint the csproj at Core**

Replace `src/FlexRender.Xml/FlexRender.Xml.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackageId>FlexRender.Xml</PackageId>
    <Description>XML template parsing for FlexRender (alternative to YAML).</Description>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\FlexRender.Core\FlexRender.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Verify `XmlFlexRenderExtensions.cs` usings**

The file has `using FlexRender.Parsing;` — it is used by `TemplateParseException` references in XML doc `<exception>` tags only (no code). `TemplateParseException` is in `FlexRender.Parsing` (Core), still reachable. Leave `using FlexRender.Parsing;` as-is; if the build reports it unused (CS8019 treated as error), remove it. No `using FlexRender.Yaml` exists in this file. No change expected.

- [ ] **Step 6: Build**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 7: Run the full XML test suite**

Run: `dotnet test FlexRender.slnx --framework net10.0 --filter "FullyQualifiedName~Parsing.Xml"`
Expected: PASS (all `XmlTemplateParserBasicTests`, `XmlChartTests`, `XmlControlFlowTests`, `XmlFlexNestingTests`, `XmlRenderExtensionTests`, `XmlResourceLimitTests`, `XmlShapeTests`, `XmlTableTests`, `XmlValidationTests`, `XmlYamlEquivalenceTests`, `InternalEntryPointTests`).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit --no-gpg-sign -m "refactor(xml): emit neutral nodes and depend only on Core"
```

---

## Phase 5 — Clean up InternalsVisibleTo

### Task 5: Remove the now-unneeded Yaml→Xml InternalsVisibleTo

**Files:**
- Modify: `src/FlexRender.Yaml/AssemblyInfo.cs`

- [ ] **Step 1: Edit AssemblyInfo**

`FlexRender.Xml` no longer references any `FlexRender.Yaml` internals (it does not reference the Yaml assembly at all). Remove that line. `FlexRender.Tests` still uses the YAML `TemplateParser` public surface plus the `YamlNodeConverter` internal (the new converter test), so keep the Tests line. New contents of `src/FlexRender.Yaml/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FlexRender.Tests")]
```

- [ ] **Step 2: Confirm Core IVT covers the Tests engine access**

`tests/FlexRender.Tests/Parsing/Xml/InternalEntryPointTests.cs` uses the PUBLIC `TemplateEngine`; `GradientObjectParseTests` uses PUBLIC `ShapeParsers.ConvertGradientObjectToCss` and the PUBLIC neutral nodes. No new Core IVT needed. `src/FlexRender.Core/FlexRender.Core.csproj` already has `InternalsVisibleTo("FlexRender.Tests")` (used elsewhere) — leave unchanged.

- [ ] **Step 3: Build**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/FlexRender.Yaml/AssemblyInfo.cs
git commit --no-gpg-sign -m "chore(yaml): drop unused InternalsVisibleTo to FlexRender.Xml"
```

---

## Phase 6 — Documentation

### Task 6: Update AGENTS.md and llms.txt for the new dependency shape

**Files:**
- Modify: `AGENTS.md`
- Modify: `llms.txt` (only if it documents the package dependency graph or parser location — verify first)

- [ ] **Step 1: Update the AGENTS.md Project Structure + NuGet diagram**

In `AGENTS.md`:
- In the `src/FlexRender.Core/` block, add a line under `Parsing/Ast/`:
  `  Parsing/Nodes/                # Format-neutral TemplateNode model (TemplateMapping, TemplateSequence, TemplateScalar)`
  `  Parsing/Engine/               # Shared parsing engine (TemplateEngine, ElementParsers, ChartParsers, ShapeParsers, KnownProperties, NodePropertyHelpers)`
- Change the `src/FlexRender.Yaml/` description from `# YAML template parser (-> Core + YamlDotNet)` / `Parsing/ # TemplateParser` to:
  `src/FlexRender.Yaml/            # YAML facade: YamlDotNet -> neutral nodes -> Core engine (-> Core + YamlDotNet)`
  `  Parsing/                      # TemplateParser (facade), YamlNodeConverter`
- Change the `src/FlexRender.Xml/` description to note it depends only on Core:
  `src/FlexRender.Xml/             # XML facade: XDocument -> neutral nodes -> Core engine (-> Core only). RenderXml extension`
  `  Parsing/                      # XmlTemplateParser, XmlNodeConverter`
- In the "NuGet Package Structure" ASCII diagram, the line where `FlexRender.Yaml` and others hang off Core is fine; add a short note beneath it:
  `Note: FlexRender.Xml depends only on FlexRender.Core (the shared parsing engine lives in Core). FlexRender.Yaml = Core + YamlDotNet.`
- In "Common Tasks → Add new element type", update the references so they point at Core: step 2 "Add parser function in `ElementParsers.cs` / register in `TemplateEngine._elementParsers`"; step 3 "Register all properties in `KnownProperties.cs` (now in Core)".

- [ ] **Step 2: Update llms.txt if needed**

Run: `grep -n "FlexRender.Yaml\|FlexRender.Xml\|TemplateParser\|YamlDotNet" llms.txt`
If any line states the package dependency graph or that XML depends on YAML, update it to reflect: XML depends only on Core; the parsing engine lives in Core. If `llms.txt` does not mention the dependency graph, make no change. Do NOT touch `llms-full.txt` unless it explicitly states XML→YAML dependency.

- [ ] **Step 3: Commit**

```bash
git add AGENTS.md llms.txt
git commit --no-gpg-sign -m "docs: reflect Xml-depends-only-on-Core and engine moved to Core"
```

---

## Phase 7 — Final verification

### Task 7: Full build, full suite, and decoupling assertions

- [ ] **Step 1: Full clean build**

Run: `dotnet build FlexRender.slnx`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 2: Full test suite on net10.0**

Run: `dotnet test FlexRender.slnx --framework net10.0`
Expected: PASS — all tests green (original 3304 + new node/converter tests).

- [ ] **Step 3: Assert FlexRender.Xml.csproj has NO Yaml/YamlDotNet reference**

Run: `grep -n "FlexRender.Yaml\|YamlDotNet" src/FlexRender.Xml/FlexRender.Xml.csproj; echo "exit=$?"`
Expected: no matches (grep exit=1). The csproj references only `FlexRender.Core`.

- [ ] **Step 4: Assert no YamlDotNet usings remain in FlexRender.Xml sources**

Run: `grep -rn "YamlDotNet\|FlexRender.Yaml" src/FlexRender.Xml --include=*.cs; echo "exit=$?"`
Expected: no matches (grep exit=1).

- [ ] **Step 5: Assert the old XML→YAML converter and Yaml engine files are gone**

Run: `ls src/FlexRender.Xml/XmlToYamlNodeConverter.cs src/FlexRender.Yaml/Parsing/ElementParsers.cs 2>&1`
Expected: "No such file or directory" for both.

- [ ] **Step 6: Assert the engine now lives in Core**

Run: `ls src/FlexRender.Core/Parsing/Engine/ src/FlexRender.Core/Parsing/Nodes/`
Expected: `Engine/` lists `ChartParsers.cs ElementParsers.cs KnownProperties.cs NodePropertyHelpers.cs ShapeParsers.cs TemplateEngine.cs`; `Nodes/` lists the four node files.

- [ ] **Step 7: Placeholder scan**

Run: `grep -rn "TODO\|TBD\|FIXME\|NotImplementedException\|throw new NotImplemented" src/FlexRender.Core/Parsing/Engine src/FlexRender.Core/Parsing/Nodes src/FlexRender.Yaml/Parsing src/FlexRender.Xml; echo "exit=$?"`
Expected: no matches (grep exit=1).

This task has no commit (verification only). If any check fails, return to the relevant task.

---

## Self-Review

**1. Spec coverage:**
- Neutral node model in Core with TryGet + key enumeration + sequence iteration + scalar read → Task 1 (TemplateMapping exposes `Keys`, `TryGet`, `TryGetMapping`, `TryGetSequence`, `GetScalar`; TemplateSequence exposes `Items`).
- Engine moves to Core, YamlDotNet→neutral, Core gains no external dep → Task 3 (all engine files moved, retyped; Core csproj unchanged, still 0 external deps).
- Yaml keeps `TemplateParser : ITemplateParser` facade, YamlDotNet only here, public API unchanged → Task 3 Step 7 (facade preserves `Parse(string)`, `Parse(Stream)`, `ParseFile`, `ParseFileAsync`, `MaxFileSize`, `SupportedElementTypes`, both ctors).
- Xml: `XmlNodeConverter` emits neutral nodes, drops Yaml+YamlDotNet, keeps `RenderXml` → Task 4 (+ `XmlFlexRenderExtensions.RenderXml` untouched).
- Net result Xml→Core only; neither parser depends on the other → Task 4 csproj + Task 7 Steps 3–4 assertions.
- KnownProperties key-iteration for typo validation against neutral mappings → Task 3 Step 2 (`node.Keys`).
- InternalsVisibleTo fixed → Task 5.
- Docs updated → Task 6.
- Final verification incl. grep assertions → Task 7.

**2. Placeholder scan:** No "TBD/implement later"; every code step has complete compilable code or an exact mechanical substitution rule keyed to the verbatim original (the engine bodies are unchanged except parameter types, fully specified by the Type Substitution Table). Task 7 Step 7 enforces no placeholders shipped.

**3. Type consistency:** `TemplateEngine.ParseDocumentRoot(TemplateMapping)` is the single entry point used by both `TemplateParser` (Task 3 Step 7) and `XmlTemplateParser` (Task 4 Step 2) and the test (Task 3 Step 10). Helper names (`GetStringValue`, `TryGetMapping`, `TryGetSequence`, `GetExpr*`, `ConvertMappingToDictionary`) are preserved exactly so call sites in `ElementParsers`/`ChartParsers`/`ShapeParsers` need no body edits beyond parameter types. `ShapeParsers.ConvertGradientObjectToCss(TemplateMapping)` matches its new test caller. `YamlNodeConverter.Convert(YamlMappingNode)→TemplateMapping` and `XmlNodeConverter.Convert(string)→TemplateMapping` both feed `ParseDocumentRoot`.

**Highest-risk tasks (flagged):**
- **Task 3 (engine move + type-swap)** — largest mechanical change, single commit; mitigated by full-suite run at Steps 11–12.
- **Task 3 Step 2 (KnownProperties typo validation)** — neutral `Keys` are non-null strings; the old `YamlScalarNode`/null guards are dropped — verify `XmlValidationTests` and `ChartKnownPropertiesTests` still produce the same "Did you mean" messages.
- **Task 3 Step 7 (TemplateParser facade parity)** — must keep identical public behavior for 600+ YAML tests; the facade preserves every member and error message; the only new internal hop is `YamlNodeConverter.Convert`.
